using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Infra;

public class AssistenteOptions
{
    /// <summary>Chave da API da Anthropic (variável de ambiente Assistente__ApiKey).</summary>
    public string ApiKey { get; set; } = "";
    public string Modelo { get; set; } = "claude-opus-5-5";
}

/// <summary>Uma fala da conversa como aparece na tela (só texto; as consultas internas não ficam no histórico).</summary>
public record FalaAssistente(string Papel, string Texto);

/// <summary>
/// Assistente de IA do CRM (Claude). Responde perguntas da equipe consultando o banco por ferramentas
/// somente leitura, sempre com as permissões de quem pergunta (vendedor só enxerga os próprios leads).
/// </summary>
public class Assistente(Microsoft.Extensions.Options.IOptions<AssistenteOptions> opcoes, AppDbContext db, LeadService leads, ILogger<Assistente> log)
{
    const int MaximoRodadas = 8;
    const int MaximoHistorico = 20;
    const int LimiteLista = 30;
    readonly AssistenteOptions _o = opcoes.Value;

    public bool Configurado => !string.IsNullOrWhiteSpace(_o.ApiKey);

    static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    string Sistema(UsuarioAtual u) => $"""
        Você é o assistente do CRM da CDL Uberlândia (Câmara de Dirigentes Lojistas), usado pela equipe comercial.
        Quem está falando com você: {u.Nome} ({Texto.NomePerfil(u.Perfil)}). Hoje é {Hora.HojeLocal:dddd, dd/MM/yyyy} (horário de Brasília).

        Responda sempre em português do Brasil, de forma direta e prática. Use as ferramentas para consultar dados reais do CRM
        antes de responder qualquer pergunta sobre leads, tarefas, contatos, funis ou resultados; nunca invente números, nomes ou datas.
        As ferramentas só leem dados e já respeitam o que esta pessoa pode ver. Você não consegue alterar nada no CRM: se pedirem
        para mudar algo, explique onde fazer isso na tela.
        Ao citar um lead, inclua o link no formato [Nome do lead](/leads/ID). Para contatos use /contatos/ID.
        Valores em reais no formato R$ 1.234,56. Quando sugerir mensagens de WhatsApp ou e-mail, escreva prontas para copiar,
        em tom cordial e profissional, em nome da CDL.
        """;

    // ---------------- ferramentas ----------------

    static BetaTool Ferramenta(string nome, string descricao, object propriedades, params string[] obrigatorios) => new()
    {
        Name = nome,
        Description = descricao,
        InputSchema = new()
        {
            Properties = JsonSerializer.SerializeToElement(propriedades).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone()),
            Required = obrigatorios,
        },
    };

    static readonly List<BetaToolUnion> Ferramentas =
    [
        Ferramenta("listar_funis", "Lista os funis de vendas ativos com as etapas (id, nome, chance de fechar).", new { }),
        Ferramenta("buscar_leads",
            "Busca leads. Todos os filtros são opcionais. Devolve no máximo 30, os mais recentes primeiro, com o total encontrado.",
            new
            {
                texto = new { type = "string", description = "Parte do nome do lead, contato, empresa, telefone ou e-mail" },
                funil_id = new { type = "integer" },
                etapa_id = new { type = "integer" },
                situacao = new { type = "string", @enum = new[] { "abertos", "ganhos", "perdidos", "todos" } },
                parados = new { type = "boolean", description = "Só leads em aberto sem movimentação há 7 dias ou mais" },
                responsavel = new { type = "string", description = "Nome (ou parte) do responsável" },
                tag = new { type = "string" },
            }),
        Ferramenta("detalhes_lead", "Tudo sobre um lead: etapa, valor, contatos, empresa, produtos, campos, tarefas em aberto e as últimas atividades e notas.",
            new { lead_id = new { type = "integer" } }, "lead_id"),
        Ferramenta("tarefas", "Lista tarefas não concluídas de quem pergunta (gestores veem as da equipe).",
            new
            {
                filtro = new { type = "string", @enum = new[] { "atrasadas", "hoje", "proximos_7_dias", "todas_abertas" } },
                responsavel = new { type = "string", description = "Só para gestores: nome (ou parte) de quem é a tarefa" },
            }, "filtro"),
        Ferramenta("resumo_funil", "Números de um funil: leads e valor em aberto por etapa, ganhos e perdidos no período, parados e motivos de perda.",
            new
            {
                funil_id = new { type = "integer", description = "Se não informar, usa o funil principal" },
                dias = new { type = "integer", description = "Período em dias para ganhos e perdidos (padrão 30)" },
            }),
        Ferramenta("buscar_contatos", "Busca contatos por nome, telefone, e-mail ou empresa. Devolve no máximo 30.",
            new { texto = new { type = "string" } }, "texto"),
        Ferramenta("produtos", "Catálogo de produtos e serviços da CDL com preços.", new { }),
    ];

    /// <summary>Executa uma ferramenta do assistente (público para os testes).</summary>
    public async Task<object> ExecutarAsync(string nome, IReadOnlyDictionary<string, JsonElement> e, UsuarioAtual u)
    {
        string? Str(string k) => e.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        int? Int(string k) => e.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
        bool Bool(string k) => e.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.True;
        var agora = DateTime.UtcNow;

        switch (nome)
        {
            case "listar_funis":
                return await db.Funis.Where(f => f.Ativo).OrderBy(f => f.Ordem)
                    .Select(f => new { f.Id, f.Nome, f.Principal, etapas = f.Etapas.OrderBy(x => x.Ordem).Select(x => new { x.Id, x.Nome, tipo = x.Tipo.ToString(), chance = x.Probabilidade }) })
                    .ToListAsync();

            case "buscar_leads":
            {
                int? resp = null;
                if (Str("responsavel") is { Length: > 0 } nomeResp)
                    resp = await db.Usuarios.Where(x => x.Nome.Contains(nomeResp)).Select(x => (int?)x.Id).FirstOrDefaultAsync() ?? -1;
                var filtro = new FiltroLeads(Int("funil_id") ?? 0, Str("texto"), resp, IncluirFechadosAntigos: true,
                    EtapaId: Int("etapa_id"), SoParados: Bool("parados"), Tag: Str("tag"), Situacao: Str("situacao") ?? "abertos");
                var q = leads.Filtrar(filtro, u, agora);
                var total = await q.CountAsync();
                var itens = await q.OrderByDescending(l => l.AtualizadoEm).Take(LimiteLista).Select(l => new
                {
                    l.Id, l.Titulo, l.Valor, funil = l.Funil!.Nome, etapa = l.Etapa!.Nome,
                    responsavel = l.Responsavel != null ? l.Responsavel.Nome : null,
                    contato = l.Contatos.Where(c => c.Principal).Select(c => c.Contato!.Nome).FirstOrDefault(),
                    empresa = l.Empresa != null ? l.Empresa.Nome : null,
                    previsao = l.DataPrevista, l.AtualizadoEm, fechado_em = l.FechadoEm,
                }).ToListAsync();
                return new
                {
                    total, mostrando = itens.Count,
                    leads = itens.Select(l => new
                    {
                        l.Id, l.Titulo, l.Valor, l.funil, l.etapa, l.responsavel, l.contato, l.empresa,
                        previsao = l.previsao?.ToString("dd/MM/yyyy"),
                        ultima_movimentacao = Hora.Local(l.AtualizadoEm).ToString("dd/MM/yyyy"),
                        dias_sem_movimento = l.fechado_em is null ? (int)(agora - l.AtualizadoEm).TotalDays : (int?)null,
                    })
                };
            }

            case "detalhes_lead":
            {
                var id = Int("lead_id") ?? 0;
                var l = await leads.Visiveis(u).Where(x => x.Id == id)
                    .Include(x => x.Funil).Include(x => x.Etapa).Include(x => x.Responsavel).Include(x => x.Empresa).Include(x => x.MotivoPerda)
                    .Include(x => x.Contatos).ThenInclude(c => c.Contato).Include(x => x.Tags).ThenInclude(t => t.Tag).Include(x => x.Itens)
                    .AsSplitQuery().FirstOrDefaultAsync();
                if (l is null) return new { erro = "Lead não encontrado ou sem permissão." };
                var campos = await db.Campos.Include(c => c.Opcoes).Where(c => c.Entidade == EntidadeCampo.Lead).ToListAsync();
                var valores = await db.ValoresCampo.Where(v => v.Entidade == EntidadeCampo.Lead && v.RegistroId == id).ToListAsync();
                var atividades = await db.Atividades.Include(a => a.Usuario).Where(a => a.LeadId == id).OrderByDescending(a => a.CriadoEm).Take(25).ToListAsync();
                var tarefas = await db.Tarefas.Include(t => t.Responsavel).Where(t => t.LeadId == id && !t.Concluida).OrderBy(t => t.Prazo).ToListAsync();
                return new
                {
                    l.Id, l.Titulo, l.Valor, funil = l.Funil!.Nome, etapa = l.Etapa!.Nome, situacao = l.FechadoEm is null ? "em aberto" : l.Etapa.Tipo.ToString(),
                    motivo_perda = l.MotivoPerda?.Nome, responsavel = l.Responsavel?.Nome, origem = l.Origem,
                    previsao = l.DataPrevista?.ToString("dd/MM/yyyy"), criado = Hora.Local(l.CriadoEm).ToString("dd/MM/yyyy"),
                    ultima_movimentacao = Hora.Local(l.AtualizadoEm).ToString("dd/MM/yyyy HH:mm"),
                    empresa = l.Empresa is null ? null : new { l.Empresa.Nome, l.Empresa.Cnpj },
                    contatos = l.Contatos.Select(c => new { c.Contato!.Id, c.Contato.Nome, c.Contato.Cargo, c.Contato.Celular, c.Contato.Email }),
                    tags = l.Tags.Select(t => t.Tag!.Nome),
                    produtos = l.Itens.Select(i => new { i.Descricao, i.Quantidade, i.PrecoUnitario, desconto_pct = i.Desconto, i.Total }),
                    campos = valores.Select(v => campos.FirstOrDefault(c => c.Id == v.CampoId) is { } c ? new { campo = c.Nome, valor = Consultas.TextoDoCampo(c, v.Valor) } : null).Where(x => x is not null),
                    tarefas_abertas = tarefas.Select(t => new { tipo = Texto.NomeTarefa(t.Tipo), t.Texto, prazo = Hora.Local(t.Prazo).ToString("dd/MM HH:mm"), atrasada = t.Prazo < agora, responsavel = t.Responsavel?.Nome }),
                    ultimas_atividades = atividades.Select(a => new { quando = Hora.Local(a.CriadoEm).ToString("dd/MM/yyyy HH:mm"), quem = a.Usuario?.Nome ?? "Sistema", tipo = a.Tipo.ToString(), a.Texto }),
                };
            }

            case "tarefas":
            {
                var q = db.Tarefas.Include(t => t.Lead).Include(t => t.Responsavel).Where(t => !t.Concluida);
                if (!u.VeTudo) q = q.Where(t => t.ResponsavelId == u.Id);
                else if (Str("responsavel") is { Length: > 0 } r) q = q.Where(t => t.Responsavel != null && t.Responsavel.Nome.Contains(r));
                var inicioHoje = Hora.ParaUtc(Hora.HojeLocal);
                var fimHoje = Hora.ParaUtc(Hora.HojeLocal.AddDays(1));
                q = Str("filtro") switch
                {
                    "atrasadas" => q.Where(t => t.Prazo < agora),
                    "hoje" => q.Where(t => t.Prazo >= inicioHoje && t.Prazo < fimHoje),
                    "proximos_7_dias" => q.Where(t => t.Prazo < agora.AddDays(7)),
                    _ => q
                };
                var lista = await q.OrderBy(t => t.Prazo).Take(50).ToListAsync();
                return lista.Select(t => new
                {
                    tipo = Texto.NomeTarefa(t.Tipo), t.Texto, prazo = Hora.Local(t.Prazo).ToString("dd/MM/yyyy HH:mm"), atrasada = t.Prazo < agora,
                    responsavel = t.Responsavel?.Nome, lead = t.Lead is null ? null : new { t.Lead.Id, t.Lead.Titulo }
                });
            }

            case "resumo_funil":
            {
                var funis = await db.Funis.Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync();
                var funil = funis.FirstOrDefault(f => f.Id == Int("funil_id")) ?? funis.FirstOrDefault(f => f.Principal) ?? funis.FirstOrDefault();
                if (funil is null) return new { erro = "Nenhum funil cadastrado." };
                var dias = Math.Clamp(Int("dias") ?? 30, 1, 365);
                var desde = agora.AddDays(-dias);
                var visiveis = leads.Visiveis(u).Where(l => l.FunilId == funil.Id);
                var porEtapa = (await visiveis.Where(l => l.FechadoEm == null).Select(l => new { l.Etapa!.Nome, l.Etapa.Ordem, l.Valor }).ToListAsync())
                    .GroupBy(l => (l.Nome, l.Ordem)).OrderBy(g => g.Key.Ordem)
                    .Select(g => new { etapa = g.Key.Nome, leads = g.Count(), valor = g.Sum(l => l.Valor) }).ToList();
                var fechados = visiveis.Where(l => l.FechadoEm >= desde);
                var limiteParado = agora.AddDays(-Consultas.DiasParado);
                return new
                {
                    funil = funil.Nome, periodo_dias = dias, em_aberto_por_etapa = porEtapa,
                    ganhos = await fechados.CountAsync(l => l.Etapa!.Tipo == TipoEtapa.Ganho),
                    valor_ganho = (await fechados.Where(l => l.Etapa!.Tipo == TipoEtapa.Ganho).Select(l => l.Valor).ToListAsync()).Sum(),
                    perdidos = await fechados.CountAsync(l => l.Etapa!.Tipo == TipoEtapa.Perdido),
                    motivos_perda = await fechados.Where(l => l.Etapa!.Tipo == TipoEtapa.Perdido)
                        .GroupBy(l => l.MotivoPerda != null ? l.MotivoPerda.Nome : "Sem motivo").Select(g => new { motivo = g.Key, qtd = g.Count() }).ToListAsync(),
                    parados = await visiveis.CountAsync(l => l.FechadoEm == null && l.AtualizadoEm < limiteParado),
                    novos_no_periodo = await visiveis.CountAsync(l => l.CriadoEm >= desde),
                };
            }

            case "buscar_contatos":
                return await Consultas.FiltrarContatos(db.Contatos, Str("texto")).OrderBy(c => c.Nome).Take(LimiteLista)
                    .Select(c => new { c.Id, c.Nome, c.Cargo, c.Celular, c.Telefone, c.Email, empresa = c.Empresa != null ? c.Empresa.Nome : null })
                    .ToListAsync();

            case "produtos":
                return await db.Produtos.Where(p => p.Ativo).OrderBy(p => p.Categoria).ThenBy(p => p.Nome)
                    .Select(p => new { p.Nome, p.Categoria, p.Preco, p.Descricao }).ToListAsync();
        }
        return new { erro = $"Ferramenta desconhecida: {nome}" };
    }

    // ---------------- conversa ----------------

    /// <summary>Envia a pergunta com o histórico (só texto) e devolve a resposta final do assistente.</summary>
    public async Task<string> ResponderAsync(IReadOnlyList<FalaAssistente> historico, string pergunta, UsuarioAtual u, CancellationToken ct)
    {
        if (!Configurado) return "O assistente ainda não foi configurado. Peça ao administrador para cadastrar a chave da API (Assistente__ApiKey).";
        var client = new AnthropicClient { ApiKey = _o.ApiKey };

        List<BetaMessageParam> mensagens = [];
        // A conversa precisa começar com uma fala do usuário e alternar usuário/assistente.
        var falas = historico.TakeLast(MaximoHistorico).Where(f => !string.IsNullOrWhiteSpace(f.Texto)).SkipWhile(f => f.Papel != "user").ToList();
        string? ultimo = null;
        foreach (var f in falas)
        {
            var papel = f.Papel == "user" ? "user" : "assistant";
            if (papel == ultimo) continue;
            mensagens.Add(papel == "user" ? new BetaUserMessageParam { Content = f.Texto } : new BetaAssistantMessageParam { Content = f.Texto });
            ultimo = papel;
        }
        if (ultimo == "user") mensagens.RemoveAt(mensagens.Count - 1);
        mensagens.Add(new BetaUserMessageParam { Content = pergunta });

        for (var rodada = 0; rodada < MaximoRodadas; rodada++)
        {
            BetaMessage resp;
            try
            {
                resp = await client.Beta.Messages.Create(new MessageCreateParams
                {
                    Model = _o.Modelo,
                    MaxTokens = 16000,
                    Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                    Fallbacks = new Default(),
                    OutputConfig = new BetaOutputConfig { Effort = Effort.Medium },
                    System = Sistema(u),
                    Tools = Ferramentas,
                    Messages = mensagens,
                }, ct);
            }
            catch (AnthropicRateLimitException)
            {
                return "O assistente está muito requisitado agora. Tente de novo em alguns segundos.";
            }
            catch (AnthropicApiException ex)
            {
                log.LogError(ex, "Erro na API do assistente");
                return "Não consegui falar com o serviço de IA agora. Tente de novo em instantes.";
            }

            if (resp.StopReason == "refusal") return "Não posso ajudar com esse pedido.";

            List<BetaContentBlockParam> eco = [];
            List<BetaContentBlockParam> resultados = [];
            var texto = new System.Text.StringBuilder();
            foreach (var b in resp.Content)
            {
                if (b.TryPickText(out var t)) { eco.Add(new BetaTextBlockParam { Text = t.Text }); texto.Append(t.Text); }
                else if (b.TryPickThinking(out var th)) eco.Add(new BetaThinkingBlockParam { Thinking = th.Thinking, Signature = th.Signature });
                else if (b.TryPickRedactedThinking(out var rt)) eco.Add(new BetaRedactedThinkingBlockParam { Data = rt.Data });
                else if (b.TryPickToolUse(out var tu))
                {
                    eco.Add(new BetaToolUseBlockParam { ID = tu.ID, Name = tu.Name, Input = tu.Input });
                    string conteudo;
                    var erro = false;
                    try { conteudo = JsonSerializer.Serialize(await ExecutarAsync(tu.Name, tu.Input, u), Json); }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "Falha na ferramenta {Nome} do assistente", tu.Name);
                        conteudo = "Erro ao consultar o CRM.";
                        erro = true;
                    }
                    resultados.Add(new BetaToolResultBlockParam { ToolUseID = tu.ID, Content = conteudo, IsError = erro });
                }
            }

            if (resp.StopReason != "tool_use" || resultados.Count == 0)
            {
                var final = texto.ToString().Trim();
                if (resp.StopReason == "max_tokens") final += "\n\n(Resposta cortada por ser longa demais. Peça um resumo.)";
                return final.Length > 0 ? final : "Não encontrei uma resposta. Tente reformular a pergunta.";
            }
            mensagens.Add(new BetaAssistantMessageParam { Content = eco });
            mensagens.Add(new BetaUserMessageParam { Content = resultados });
        }
        return "A pergunta precisou de consultas demais. Tente algo mais específico.";
    }
}
