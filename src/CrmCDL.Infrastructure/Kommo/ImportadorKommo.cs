using System.Text.Json;
using System.Text.RegularExpressions;
using CrmCDL.Application;
using CrmCDL.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmCDL.Infrastructure.Kommo;

public class KommoOptions
{
    /// <summary>Ex.: https://suaconta.kommo.com</summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>Token de longa duração da integração privada (somente leitura é o que usamos).</summary>
    public string Token { get; set; } = "";
    /// <summary>Funis cujo nome começa com algum destes textos não são importados.</summary>
    public List<string> IgnorarFunisQueComecamCom { get; set; } = new() { "Teste" };
}

/// <summary>
/// Lê a estrutura da Kommo pela API v4 e grava no banco do CRM. Pode ser executado de novo:
/// o que já foi importado é atualizado pelo id da Kommo, sem duplicar.
/// </summary>
public class ImportadorKommo(HttpClient http, IOptions<KommoOptions> opcoes, AppDbContext db) : IImportadorKommo
{
    // Na Kommo, as etapas "Venda ganha" e "Venda perdida" têm sempre estes ids.
    const long KommoGanho = 142, KommoPerdido = 143;

    // Campos de contato e empresa que viram colunas próprias no CRM (não campos personalizados).
    static readonly HashSet<string> CodigosFixos = new(StringComparer.OrdinalIgnoreCase) { "PHONE", "EMAIL", "POSITION", "WEB", "ADDRESS" };
    static readonly HashSet<string> NomesFixos = new(StringComparer.OrdinalIgnoreCase) { "CPF", "CNPJ", "Termos do usuário", "Assinatura de marketing" };

    readonly KommoOptions _o = opcoes.Value;

    public bool Configurado => !string.IsNullOrWhiteSpace(_o.BaseUrl) && !string.IsNullOrWhiteSpace(_o.Token);

    public async Task<ResultadoImportacao> ImportarEstruturaAsync(CancellationToken ct = default)
    {
        if (!Configurado) throw new RegraNegocioException("Integração com a Kommo não configurada (Kommo:BaseUrl e Kommo:Token).");
        var avisos = new List<string>();
        int funis = 0, etapas = 0, campos = 0, opcoesCount = 0, motivos = 0, tags = 0;

        // ---------- funis e etapas ----------
        var pipelines = await ListarAsync("leads/pipelines", "pipelines", ct);
        var ordemFunil = 0;
        foreach (var p in pipelines.OrderBy(p => p.GetProperty("sort").GetInt32()))
        {
            var nome = p.GetProperty("name").GetString()!.Trim();
            if (_o.IgnorarFunisQueComecamCom.Any(x => nome.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            {
                avisos.Add($"Funil \"{nome}\" ignorado (teste).");
                continue;
            }
            var kid = p.GetProperty("id").GetInt64();
            var funil = await db.Funis.Include(f => f.Etapas).FirstOrDefaultAsync(f => f.KommoId == kid, ct);
            if (funil is null) { funil = new Funil { KommoId = kid }; db.Funis.Add(funil); }
            funil.Nome = nome;
            funil.Ordem = ++ordemFunil;
            funil.Principal = p.GetProperty("is_main").GetBoolean();
            funis++;

            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ordem = 1;
            foreach (var s in p.GetProperty("_embedded").GetProperty("statuses").EnumerateArray().OrderBy(s => s.GetProperty("sort").GetInt32()))
            {
                var sid = s.GetProperty("id").GetInt64();
                var tipo = sid == KommoGanho ? TipoEtapa.Ganho
                         : sid == KommoPerdido ? TipoEtapa.Perdido
                         : s.GetProperty("type").GetInt32() == 1 ? TipoEtapa.Entrada : TipoEtapa.Normal;
                var nomeEtapa = CorrigirCapsLock(s.GetProperty("name").GetString()!.Trim());
                if (tipo == TipoEtapa.Entrada) nomeEtapa = "Leads de entrada";
                if (!vistos.Add(nomeEtapa))
                {
                    avisos.Add($"Funil \"{nome}\": etapa repetida \"{nomeEtapa}\" foi unificada.");
                    continue;
                }
                var etapa = funil.Etapas.FirstOrDefault(e => e.KommoId == sid)
                            ?? (tipo != TipoEtapa.Normal ? funil.Etapas.FirstOrDefault(e => e.Tipo == tipo) : null);
                if (etapa is null) { etapa = new Etapa(); funil.Etapas.Add(etapa); }
                etapa.KommoId = sid;
                etapa.Nome = nomeEtapa;
                etapa.Tipo = tipo;
                etapa.Cor = ConfiguracaoService.CorValida(s.GetProperty("color").GetString());
                etapa.Ordem = tipo switch { TipoEtapa.Entrada => 0, TipoEtapa.Ganho => 98, TipoEtapa.Perdido => 99, _ => ordem++ };
                etapas++;
            }
        }
        await db.SaveChangesAsync(ct);
        if (!await db.Funis.AnyAsync(f => f.Principal, ct))
        {
            var primeiro = await db.Funis.OrderBy(f => f.Ordem).FirstOrDefaultAsync(ct);
            if (primeiro is not null) { primeiro.Principal = true; await db.SaveChangesAsync(ct); }
        }

        // ---------- campos personalizados ----------
        foreach (var (rota, entidade) in new[] { ("leads", EntidadeCampo.Lead), ("contacts", EntidadeCampo.Contato), ("companies", EntidadeCampo.Empresa) })
        {
            var lista = await ListarAsync($"{rota}/custom_fields", "custom_fields", ct);
            foreach (var f in lista)
            {
                var nome = f.GetProperty("name").GetString()!.Trim();
                var codigo = f.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (entidade != EntidadeCampo.Lead && ((codigo is not null && CodigosFixos.Contains(codigo)) || NomesFixos.Contains(nome))) continue;

                var kid = f.GetProperty("id").GetInt64();
                var campo = await db.Campos.Include(x => x.Opcoes).FirstOrDefaultAsync(x => x.KommoId == kid, ct);
                if (campo is null) { campo = new CampoPersonalizado { KommoId = kid, Entidade = entidade }; db.Campos.Add(campo); }
                campo.Nome = nome;
                campo.Tipo = MapearTipo(f.GetProperty("type").GetString());
                campo.Ordem = f.TryGetProperty("sort", out var so) && so.ValueKind == JsonValueKind.Number ? so.GetInt32() : 0;
                campos++;

                if (f.TryGetProperty("enums", out var enums) && enums.ValueKind == JsonValueKind.Array && campo.Tipo is TipoCampo.Lista or TipoCampo.MultiplaEscolha)
                {
                    foreach (var en in enums.EnumerateArray())
                    {
                        var eid = en.GetProperty("id").GetInt64();
                        var op = campo.Opcoes.FirstOrDefault(o => o.KommoId == eid);
                        if (op is null) { op = new CampoOpcao { KommoId = eid }; campo.Opcoes.Add(op); }
                        op.Valor = en.GetProperty("value").GetString()!.Trim();
                        op.Ordem = en.TryGetProperty("sort", out var es) && es.ValueKind == JsonValueKind.Number ? es.GetInt32() : 0;
                        opcoesCount++;
                    }
                }
            }
        }
        await db.SaveChangesAsync(ct);

        // ---------- motivos de perda ----------
        foreach (var m in await ListarAsync("leads/loss_reasons", "loss_reasons", ct))
        {
            var kid = m.GetProperty("id").GetInt64();
            var motivo = await db.MotivosPerda.FirstOrDefaultAsync(x => x.KommoId == kid, ct);
            if (motivo is null) { motivo = new MotivoPerda { KommoId = kid }; db.MotivosPerda.Add(motivo); }
            motivo.Nome = m.GetProperty("name").GetString()!.Trim();
            motivo.Ordem = m.TryGetProperty("sort", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0;
            motivos++;
        }
        await db.SaveChangesAsync(ct);

        // ---------- tags ----------
        foreach (var t in await ListarAsync("leads/tags", "tags", ct))
        {
            var kid = t.GetProperty("id").GetInt64();
            var nome = t.GetProperty("name").GetString()!.Trim();
            var tag = await db.Tags.FirstOrDefaultAsync(x => x.KommoId == kid || x.Nome == nome, ct);
            if (tag is null) { tag = new Tag(); db.Tags.Add(tag); }
            tag.KommoId = kid;
            tag.Nome = nome;
            if (t.TryGetProperty("color", out var cor) && cor.ValueKind == JsonValueKind.String) tag.Cor = ConfiguracaoService.CorValida(cor.GetString());
            tags++;
        }
        await db.SaveChangesAsync(ct);

        return new ResultadoImportacao(funis, etapas, campos, opcoesCount, motivos, tags, avisos);
    }

    /// <summary>Busca todas as páginas de uma lista da API (a Kommo devolve 204 quando não há dados).</summary>
    async Task<List<JsonElement>> ListarAsync(string rota, string chave, CancellationToken ct)
    {
        var itens = new List<JsonElement>();
        for (var pagina = 1; pagina <= 50; pagina++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_o.BaseUrl.TrimEnd('/')}/api/v4/{rota}?limit=250&page={pagina}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _o.Token);
            using var resp = await http.SendAsync(req, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent) break;
            if (!resp.IsSuccessStatusCode)
                throw new RegraNegocioException($"A Kommo respondeu {(int)resp.StatusCode} ao ler \"{rota}\". Confira o token.");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("_embedded", out var emb) || !emb.TryGetProperty(chave, out var arr)) break;
            itens.AddRange(arr.EnumerateArray().Select(x => x.Clone()));
            var temProxima = doc.RootElement.TryGetProperty("_links", out var links) && links.TryGetProperty("next", out _);
            if (!temProxima) break;
        }
        return itens;
    }

    static TipoCampo MapearTipo(string? t) => t switch
    {
        "textarea" => TipoCampo.TextoLongo,
        "numeric" or "price" => TipoCampo.Numero,
        "date" or "birthday" => TipoCampo.Data,
        "date_time" => TipoCampo.DataHora,
        "select" or "radiobutton" => TipoCampo.Lista,
        "multiselect" => TipoCampo.MultiplaEscolha,
        "url" => TipoCampo.Url,
        "checkbox" => TipoCampo.Checkbox,
        "tracking_data" => TipoCampo.Rastreamento,
        _ => TipoCampo.Texto
    };

    /// <summary>"aGUARDANDO fECHAMENTO" (digitado com Caps Lock) vira "Aguardando Fechamento".</summary>
    public static string CorrigirCapsLock(string texto)
        => Regex.Replace(texto, @"\b\p{Ll}\p{Lu}+\b", m =>
            new string(m.Value.Select(ch => char.IsUpper(ch) ? char.ToLowerInvariant(ch) : char.ToUpperInvariant(ch)).ToArray()));
}
