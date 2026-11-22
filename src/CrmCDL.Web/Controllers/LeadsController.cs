using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using CrmCDL.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Controllers;

public class LeadsController(AppDbContext db, LeadService leads, ConfiguracaoService config) : BaseController
{
    [HttpGet("/")]
    public IActionResult Inicio() => Redirect("/leads");

    /// <summary>Quadro do funil (colunas por etapa).</summary>
    [HttpGet("/leads")]
    public async Task<IActionResult> Quadro(int? funil, string? busca, int? responsavel, bool fechados = false, bool parados = false)
    {
        var funis = await db.Funis.Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync();
        if (funis.Count == 0)
            return Eu.Admin ? Redirect("/configuracoes/funis")
                            : View("Mensagem", "Nenhum funil cadastrado ainda. Peça a um administrador para criar o primeiro funil.");
        var atual = EscolherFunil(funis, funil);
        var etapas = await db.Etapas.Where(e => e.FunilId == atual.Id).OrderBy(e => e.Ordem).ToListAsync();

        // Colunas de ganho/perdido mostram só os últimos 30 dias, para o quadro não ficar pesado.
        var agora = DateTime.UtcNow;
        var q = leads.Filtrar(new FiltroLeads(atual.Id, busca, responsavel, fechados, SoParados: parados), Eu, agora);
        ViewBag.Parados = parados;
        var cartoes = await q.OrderBy(l => l.Posicao).ThenByDescending(l => l.AtualizadoEm)
            .Select(l => new CartaoLead(
                l.Id, l.Titulo, l.Valor, l.EtapaId,
                l.Contatos.Where(c => c.Principal).Select(c => c.Contato!.Nome).FirstOrDefault(),
                l.Empresa != null ? l.Empresa.Nome : null,
                l.Responsavel != null ? l.Responsavel.Nome : null,
                l.Tags.Select(t => t.Tag!.Nome).ToList(),
                l.Tarefas.Where(t => !t.Concluida).OrderBy(t => t.Prazo).Select(t => (DateTime?)t.Prazo).FirstOrDefault(),
                l.AtualizadoEm, l.Posicao))
            .Take(2000).ToListAsync();

        var vm = new QuadroVm(atual, funis, etapas, cartoes, busca, responsavel, fechados,
            Eu.VeTudo ? await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync() : new(),
            await db.MotivosPerda.OrderBy(m => m.Ordem).ToListAsync(), agora);
        return View(vm);
    }

    static Funil EscolherFunil(List<Funil> funis, int? funil)
        => funis.FirstOrDefault(f => f.Id == funil) ?? funis.FirstOrDefault(f => f.Principal) ?? funis[0];

    const int MaximoExportacao = 50_000;

    /// <summary>Planilha Excel com os leads do quadro, usando a mesma busca e os mesmos filtros da tela.</summary>
    [HttpGet("/leads/exportar")]
    public async Task<IActionResult> Exportar(int? funil, string? busca, int? responsavel, bool fechados = false)
    {
        var funis = await db.Funis.Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync();
        if (funis.Count == 0) return VoltarCom("Nenhum funil cadastrado.");
        var atual = EscolherFunil(funis, funil);

        var q = leads.Filtrar(new FiltroLeads(atual.Id, busca, responsavel, fechados), Eu, DateTime.UtcNow);
        var linhas = await q.OrderBy(l => l.Etapa!.Ordem).ThenBy(l => l.Posicao)
            .Select(l => new
            {
                l.Id, l.Titulo, Etapa = l.Etapa!.Nome, l.Valor,
                Responsavel = l.Responsavel != null ? l.Responsavel.Nome : null,
                Contato = l.Contatos.Where(c => c.Principal).Select(c => c.Contato).FirstOrDefault(),
                Empresa = l.Empresa != null ? l.Empresa.Nome : null,
                Tags = l.Tags.Select(t => t.Tag!.Nome).ToList(),
                ProximaTarefa = l.Tarefas.Where(t => !t.Concluida).OrderBy(t => t.Prazo).Select(t => (DateTime?)t.Prazo).FirstOrDefault(),
                l.CriadoEm, l.AtualizadoEm, l.FechadoEm,
                Motivo = l.MotivoPerda != null ? l.MotivoPerda.Nome : null
            })
            .AsSplitQuery()
            .Take(MaximoExportacao).ToListAsync();

        var campos = await db.Campos.Include(c => c.Opcoes).Where(c => c.Entidade == EntidadeCampo.Lead && c.Ativo)
            .OrderBy(c => c.Ordem).ThenBy(c => c.Id).ToListAsync();
        var valores = (await db.ValoresCampo
                .Where(v => v.Entidade == EntidadeCampo.Lead && q.Select(l => l.Id).Contains(v.RegistroId))
                .Select(v => new { v.RegistroId, v.CampoId, v.Valor }).ToListAsync())
            .ToDictionary(v => (v.RegistroId, v.CampoId), v => v.Valor);

        var colunas = new List<ColunaPlanilha>
        {
            new("Id", FormatoColuna.Inteiro), new("Lead"), new("Etapa"), new("Valor", FormatoColuna.Dinheiro), new("Responsável"),
            new("Contato principal"), new("Celular"), new("E-mail"), new("Empresa"), new("Tags"),
            new("Próxima tarefa", FormatoColuna.DataHora), new("Criado em", FormatoColuna.DataHora),
            new("Atualizado em", FormatoColuna.DataHora), new("Fechado em", FormatoColuna.DataHora), new("Motivo da perda")
        };
        colunas.AddRange(campos.Select(c => new ColunaPlanilha(c.Nome)));

        static DateTime? Local(DateTime? utc) => utc is DateTime d ? Hora.Local(d) : null;
        var dados = linhas.Select(l => new object?[]
        {
            l.Id, l.Titulo, l.Etapa, l.Valor, l.Responsavel,
            l.Contato?.Nome, l.Contato?.Celular ?? l.Contato?.Telefone, l.Contato?.Email, l.Empresa, string.Join(", ", l.Tags),
            Local(l.ProximaTarefa), Local(l.CriadoEm), Local(l.AtualizadoEm), Local(l.FechadoEm), l.Motivo
        }.Concat(campos.Select(c => (object?)Consultas.TextoDoCampo(c, valores.GetValueOrDefault((l.Id, c.Id))))).ToArray());

        var arquivo = Planilha.Gerar("Leads", colunas, dados);
        return File(arquivo, Planilha.TipoConteudo, Planilha.NomeArquivo("leads-" + Slug(atual.Nome)));
    }

    /// <summary>Nome de arquivo sem acentos nem espaços.</summary>
    static string Slug(string texto)
    {
        var semAcento = new string(texto.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        var s = System.Text.RegularExpressions.Regex.Replace(semAcento.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return s.Length == 0 ? "funil" : s.Length > 40 ? s[..40].Trim('-') : s;
    }

    [HttpPost("/leads/novo")]
    public async Task<IActionResult> Criar(NovoLead novo, bool abrir = false)
    {
        var lead = await leads.CriarAsync(novo, Eu);
        if (abrir) return Redirect($"/leads/{lead.Id}");
        TempData["msg"] = $"Lead \"{lead.Titulo}\" criado.";
        return Redirect($"/leads?funil={lead.FunilId}");
    }

    public record MoverReq(int EtapaId, double? Posicao, int? MotivoPerdaId);

    /// <summary>Chamado pelo quadro ao soltar um cartão em outra posição/etapa.</summary>
    [HttpPost("/leads/{id:int}/mover")]
    public async Task<IActionResult> Mover(int id, [FromBody] MoverReq req)
    {
        await leads.MoverAsync(id, req.EtapaId, req.Posicao, req.MotivoPerdaId, Eu);
        return Ok(new { ok = true });
    }

    // ---------------- lista (tabela) ----------------

    const int PorPaginaLista = 50;

    /// <summary>Todos os leads em tabela, com filtros, ordenação e ações em massa.</summary>
    [HttpGet("/leads/lista")]
    public async Task<IActionResult> Lista(int funil = 0, int? etapa = null, int? responsavel = null, string? situacao = "abertos",
        string? busca = null, string? tag = null, bool parados = false, string ordem = "atualizado", int pagina = 1)
    {
        var agora = DateTime.UtcNow;
        var filtro = new FiltroLeads(funil, busca, responsavel, IncluirFechadosAntigos: true, EtapaId: etapa, SoParados: parados, Tag: tag, Situacao: situacao);
        var q = leads.Filtrar(filtro, Eu, agora);
        q = ordem switch
        {
            "valor" => q.OrderByDescending(l => l.Valor).ThenByDescending(l => l.Id),
            "criado" => q.OrderByDescending(l => l.CriadoEm),
            "previsao" => q.OrderBy(l => l.DataPrevista == null).ThenBy(l => l.DataPrevista),
            "nome" => q.OrderBy(l => l.Titulo),
            "parado" => q.OrderBy(l => l.AtualizadoEm),
            _ => q.OrderByDescending(l => l.AtualizadoEm)
        };
        var total = await q.CountAsync();
        var soma = await q.SumAsync(l => (decimal?)l.Valor) ?? 0;
        pagina = Math.Max(1, pagina);
        var linhas = await q.Skip((pagina - 1) * PorPaginaLista).Take(PorPaginaLista)
            .Select(l => new LinhaLead(l.Id, l.Titulo, l.Valor, l.Funil!.Nome, l.Etapa!.Nome, l.Etapa.Cor, l.Etapa.Tipo,
                l.Responsavel != null ? l.Responsavel.Nome : null,
                l.Contatos.Where(c => c.Principal).Select(c => c.Contato!.Nome).FirstOrDefault(),
                l.Empresa != null ? l.Empresa.Nome : null, l.DataPrevista, l.AtualizadoEm, l.CriadoEm, l.FechadoEm,
                l.Tarefas.Where(t => !t.Concluida).OrderBy(t => t.Prazo).Select(t => (DateTime?)t.Prazo).FirstOrDefault()))
            .ToListAsync();

        var funis = await db.Funis.Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync();
        var vm = new ListaLeadsVm
        {
            Linhas = linhas, Total = total, Soma = soma, Pagina = pagina, PorPagina = PorPaginaLista, Agora = agora,
            Funis = funis, FunilId = funil, EtapaId = etapa, ResponsavelId = responsavel, Situacao = situacao, Busca = busca,
            Tag = tag, Parados = parados, Ordem = ordem,
            Etapas = funil > 0 ? await db.Etapas.Where(e => e.FunilId == funil).OrderBy(e => e.Ordem).ToListAsync()
                               : await db.Etapas.Include(e => e.Funil).Where(e => e.Funil!.Ativo).OrderBy(e => e.Funil!.Ordem).ThenBy(e => e.Ordem).ToListAsync(),
            Usuarios = await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync(),
            Tags = await db.Tags.OrderBy(t => t.Nome).Select(t => t.Nome).ToListAsync(),
            PodeGerir = Eu.VeTudo
        };
        return View(vm);
    }

    [HttpPost("/leads/em-massa")]
    public async Task<IActionResult> EmMassa(List<int> ids, AcaoEmMassa acao, int? valorId, string? texto)
    {
        var n = await leads.EmMassaAsync(ids ?? new(), acao, valorId, texto, Eu);
        return VoltarCom(acao == AcaoEmMassa.Excluir ? $"{n} lead(s) excluído(s)." : $"{n} lead(s) atualizado(s).");
    }

    [HttpGet("/leads/{id:int}")]
    public async Task<IActionResult> Detalhe(int id)
    {
        var lead = await leads.Visiveis(Eu)
            .Include(l => l.Funil).Include(l => l.Etapa).Include(l => l.Responsavel).Include(l => l.Empresa).Include(l => l.MotivoPerda)
            .Include(l => l.Contatos).ThenInclude(c => c.Contato)
            .Include(l => l.Tags).ThenInclude(t => t.Tag)
            .Include(l => l.Itens)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id);
        if (lead is null) return NotFound();

        var vm = new LeadVm
        {
            Lead = lead,
            Etapas = await db.Etapas.Where(e => e.FunilId == lead.FunilId).OrderBy(e => e.Ordem).ToListAsync(),
            Usuarios = await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync(),
            Campos = await db.Campos.Include(c => c.Opcoes).Where(c => c.Entidade == EntidadeCampo.Lead && c.Ativo).OrderBy(c => c.Ordem).ThenBy(c => c.Id).ToListAsync(),
            Valores = await config.ValoresAsync(EntidadeCampo.Lead, lead.Id),
            Atividades = await db.Atividades.Include(a => a.Usuario).Where(a => a.LeadId == id).OrderByDescending(a => a.CriadoEm).Take(300).ToListAsync(),
            TarefasAbertas = await db.Tarefas.Include(t => t.Responsavel).Where(t => t.LeadId == id && !t.Concluida).OrderBy(t => t.Prazo).ToListAsync(),
            MotivosPerda = await db.MotivosPerda.OrderBy(m => m.Ordem).ToListAsync(),
            TodasTags = await db.Tags.OrderBy(t => t.Nome).Select(t => t.Nome).ToListAsync(),
            PodeGerir = Eu.VeTudo,
            Produtos = await db.Produtos.Where(p => p.Ativo).OrderBy(p => p.Categoria).ThenBy(p => p.Nome).ToListAsync(),
            Funis = await db.Funis.Include(f => f.Etapas).Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync()
        };
        return View(vm);
    }

    [HttpPost("/leads/{id:int}")]
    public async Task<IActionResult> Salvar(int id, string titulo, decimal valor, int? responsavelId, string? tags, DateTime? dataPrevista)
    {
        await leads.AtualizarAsync(id, titulo, valor, responsavelId, Eu, dataPrevista);
        await leads.DefinirTagsAsync(id, (tags ?? "").Split(','), Eu);
        await config.SalvarValoresAsync(EntidadeCampo.Lead, id, Texto.CamposDoForm(Request.Form));
        return VoltarCom("Alterações salvas.", $"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/etapa")]
    public async Task<IActionResult> TrocarEtapa(int id, int etapaId, int? motivoPerdaId)
    {
        await leads.MoverAsync(id, etapaId, null, motivoPerdaId, Eu);
        return Redirect($"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/nota")]
    public async Task<IActionResult> Nota(int id, string texto)
    {
        await leads.AdicionarNotaAsync(id, texto, Eu);
        return Redirect($"/leads/{id}#feed");
    }

    [HttpPost("/leads/{id:int}/tarefa")]
    public async Task<IActionResult> Tarefa(int id, TipoTarefa tipo, string texto, DateTime prazo, int? responsavelId)
    {
        await leads.CriarTarefaAsync(id, tipo, texto, Hora.ParaUtc(prazo), responsavelId, Eu, prazo);
        return Redirect($"/leads/{id}#feed");
    }

    [HttpPost("/leads/{id:int}/contatos")]
    public async Task<IActionResult> VincularContato(int id, int? contatoId, string? nome, string? celular, string? email)
    {
        if (contatoId is null)
        {
            if (string.IsNullOrWhiteSpace(nome)) return VoltarCom("Informe o nome do contato.");
            var lead = await leads.ObterAsync(id, Eu);
            var c = new Contato { Nome = nome.Trim(), Celular = celular?.Trim(), Email = email?.Trim(), EmpresaId = lead.EmpresaId, ResponsavelId = lead.ResponsavelId };
            db.Contatos.Add(c);
            await db.SaveChangesAsync();
            contatoId = c.Id;
        }
        await leads.VincularContatoAsync(id, contatoId.Value, Eu);
        return Redirect($"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/contatos/{contatoId:int}/remover")]
    public async Task<IActionResult> RemoverContato(int id, int contatoId)
    {
        await leads.DesvincularContatoAsync(id, contatoId, Eu);
        return Redirect($"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/empresa")]
    public async Task<IActionResult> DefinirEmpresa(int id, int? empresaId, string? nome)
    {
        var lead = await leads.ObterAsync(id, Eu);
        if (empresaId is null && !string.IsNullOrWhiteSpace(nome))
        {
            var e = new Empresa { Nome = nome.Trim(), ResponsavelId = lead.ResponsavelId };
            db.Empresas.Add(e);
            await db.SaveChangesAsync();
            empresaId = e.Id;
        }
        lead.EmpresaId = empresaId;
        await db.SaveChangesAsync();
        return Redirect($"/leads/{id}");
    }

    [HttpPost("/leads/{id:int}/excluir")]
    public async Task<IActionResult> Excluir(int id)
    {
        var funil = (await leads.ObterAsync(id, Eu)).FunilId;
        await leads.ExcluirAsync(id, Eu);
        TempData["msg"] = "Lead excluído.";
        return Redirect($"/leads?funil={funil}");
    }

    [HttpPost("/leads/{id:int}/itens")]
    public async Task<IActionResult> AdicionarItem(int id, int? produtoId, string? descricao, decimal quantidade = 1, decimal? preco = null, decimal desconto = 0)
    {
        await leads.AdicionarItemAsync(id, produtoId, descricao, quantidade, preco, desconto, Eu);
        return Redirect($"/leads/{id}#produtos");
    }

    [HttpPost("/leads/{id:int}/itens/{itemId:int}/remover")]
    public async Task<IActionResult> RemoverItem(int id, int itemId)
    {
        await leads.RemoverItemAsync(id, itemId, Eu);
        return Redirect($"/leads/{id}#produtos");
    }

    [HttpPost("/leads/{id:int}/funil")]
    public async Task<IActionResult> MoverFunil(int id, int etapaId)
    {
        var funilId = await db.Etapas.Where(e => e.Id == etapaId).Select(e => (int?)e.FunilId).FirstOrDefaultAsync()
                      ?? throw new RegraNegocioException("Escolha a etapa de destino.");
        await leads.MoverFunilAsync(id, funilId, etapaId, Eu);
        return VoltarCom("Lead movido de funil.", $"/leads/{id}");
    }

    /// <summary>Contatos que parecem ser a mesma pessoa (aviso ao cadastrar).</summary>
    [HttpGet("/duplicados")]
    public async Task<IActionResult> Duplicados(string? fone, string? email, int? ignorar)
        => Json(await Consultas.Duplicados(db.Contatos, fone, email, ignorar).OrderBy(c => c.Nome).Take(5)
            .Select(c => new { id = c.Id, nome = c.Nome, extra = c.Celular ?? c.Telefone ?? c.Email }).ToListAsync());

    /// <summary>Busca global (Ctrl+K): leads visíveis, contatos e empresas.</summary>
    [HttpGet("/busca-global")]
    public async Task<IActionResult> BuscaGlobal(string? q)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return Json(Array.Empty<object>());
        var chave = Consultas.ChaveFone(q);
        var ls = await leads.Visiveis(Eu).Where(l => l.Titulo.Contains(q) || (l.Empresa != null && l.Empresa.Nome.Contains(q)) || l.Contatos.Any(c => c.Contato!.Nome.Contains(q)))
            .OrderByDescending(l => l.AtualizadoEm).Take(8)
            .Select(l => new { tipo = "Lead", nome = l.Titulo, extra = l.Funil!.Nome + " · " + l.Etapa!.Nome, url = "/leads/" + l.Id }).ToListAsync();
        var cs = await db.Contatos.Where(c => c.Nome.Contains(q) || (c.Email != null && c.Email.Contains(q)) || (chave != null && c.FoneDigitos != null && c.FoneDigitos.Contains(chave)))
            .OrderBy(c => c.Nome).Take(6).Select(c => new { tipo = "Contato", nome = c.Nome, extra = c.Celular ?? c.Email ?? "", url = "/contatos/" + c.Id }).ToListAsync();
        var es = await db.Empresas.Where(e => e.Nome.Contains(q) || (e.Cnpj != null && e.Cnpj.Contains(q)))
            .OrderBy(e => e.Nome).Take(4).Select(e => new { tipo = "Empresa", nome = e.Nome, extra = e.Cnpj ?? "", url = "/empresas/" + e.Id }).ToListAsync();
        return Json(ls.Concat(cs).Concat(es));
    }

    /// <summary>Busca rápida usada nos campos de autocompletar (contatos e empresas).</summary>
    [HttpGet("/buscar/{tipo}")]
    public async Task<IActionResult> Buscar(string tipo, string q)
    {
        q = (q ?? "").Trim();
        if (q.Length < 2) return Json(Array.Empty<object>());
        if (tipo == "contatos")
            return Json(await db.Contatos.Where(c => c.Nome.Contains(q) || (c.Celular != null && c.Celular.Contains(q)) || (c.Email != null && c.Email.Contains(q)))
                .OrderBy(c => c.Nome).Take(10).Select(c => new { id = c.Id, nome = c.Nome, extra = c.Celular ?? c.Email }).ToListAsync());
        return Json(await db.Empresas.Where(e => e.Nome.Contains(q) || (e.Cnpj != null && e.Cnpj.Contains(q)))
            .OrderBy(e => e.Nome).Take(10).Select(e => new { id = e.Id, nome = e.Nome, extra = e.Cnpj }).ToListAsync());
    }
}
