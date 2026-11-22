using CrmCDL.Application;
using CrmCDL.Domain;
using CrmCDL.Infrastructure;
using CrmCDL.Web.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CrmCDL.Web.Controllers;

/// <summary>Produtos, automações por etapa, formulários do site e metas.</summary>
public class ComercialController(AppDbContext db, ConfiguracaoService config) : BaseController
{
    // ---------------- produtos ----------------

    [Authorize(Policy = Politicas.Admin), HttpGet("/configuracoes/produtos")]
    public async Task<IActionResult> Produtos()
        => View(await db.Produtos.OrderByDescending(p => p.Ativo).ThenBy(p => p.Categoria).ThenBy(p => p.Nome).ToListAsync());

    [Authorize(Policy = Politicas.Admin), HttpPost("/configuracoes/produtos")]
    public async Task<IActionResult> SalvarProduto(int? id, string nome, string? categoria, decimal preco, string? descricao, bool ativo = true)
    {
        if (string.IsNullOrWhiteSpace(nome)) return VoltarCom("Informe o nome do produto.");
        if (preco < 0) return VoltarCom("O preço não pode ser negativo.");
        var p = id is int i ? await db.Produtos.FindAsync(i) : null;
        if (p is null) db.Produtos.Add(p = new Produto());
        p.Nome = nome.Trim();
        p.Categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria.Trim();
        p.Preco = preco;
        p.Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        p.Ativo = ativo;
        await db.SaveChangesAsync();
        return VoltarCom("Produto salvo.", "/configuracoes/produtos");
    }

    // ---------------- automações ----------------

    [Authorize(Policy = Politicas.Admin), HttpGet("/configuracoes/automacoes")]
    public async Task<IActionResult> Automacoes(int? funil)
    {
        var funis = await db.Funis.Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync();
        if (funis.Count == 0) return VoltarCom("Crie um funil primeiro.", "/configuracoes/funis");
        var atual = funis.FirstOrDefault(f => f.Id == funil) ?? funis[0];
        ViewBag.Funis = funis;
        ViewBag.Funil = atual;
        return View(await db.Etapas.Include(e => e.Automacoes).Where(e => e.FunilId == atual.Id && e.Tipo != TipoEtapa.Perdido)
            .OrderBy(e => e.Ordem).ToListAsync());
    }

    [Authorize(Policy = Politicas.Admin), HttpPost("/configuracoes/automacoes")]
    public async Task<IActionResult> SalvarAutomacao(int? id, int etapaId, TipoTarefa tipo, string texto, int prazoDias, int horaPrazo = 9, bool ativo = true, bool excluir = false)
    {
        var etapa = await db.Etapas.FindAsync(etapaId);
        if (etapa is null) return NotFound();
        var a = id is int i ? await db.Automacoes.FirstOrDefaultAsync(x => x.Id == i && x.EtapaId == etapaId) : null;
        if (excluir)
        {
            if (a is not null) db.Automacoes.Remove(a);
            await db.SaveChangesAsync();
            return VoltarCom("Automação removida.", $"/configuracoes/automacoes?funil={etapa.FunilId}");
        }
        if (string.IsNullOrWhiteSpace(texto)) return VoltarCom("Descreva a tarefa que será criada.");
        if (a is null) db.Automacoes.Add(a = new AutomacaoEtapa { EtapaId = etapaId });
        a.TipoTarefa = tipo;
        a.Texto = texto.Trim();
        a.PrazoDias = Math.Clamp(prazoDias, 0, 365);
        a.HoraPrazo = Math.Clamp(horaPrazo, 0, 23);
        a.Ativo = ativo;
        await db.SaveChangesAsync();
        return VoltarCom("Automação salva.", $"/configuracoes/automacoes?funil={etapa.FunilId}");
    }

    // ---------------- formulários do site ----------------

    [Authorize(Policy = Politicas.Admin), HttpGet("/configuracoes/formularios")]
    public async Task<IActionResult> Formularios()
    {
        ViewBag.Funis = await db.Funis.Include(f => f.Etapas).Where(f => f.Ativo).OrderBy(f => f.Ordem).ToListAsync();
        ViewBag.Usuarios = await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync();
        ViewBag.Base = $"{Request.Scheme}://{Request.Host}";
        return View(await db.Formularios.Include(f => f.Funil).Include(f => f.Etapa).Include(f => f.ResponsavelFixo).OrderBy(f => f.Nome).ToListAsync());
    }

    [Authorize(Policy = Politicas.Admin), HttpPost("/configuracoes/formularios")]
    public async Task<IActionResult> SalvarFormulario(int? id, string nome, int? etapaId, Distribuicao distribuicao,
        int? responsavelFixoId, List<int>? participantes, bool ativo = true, bool novoToken = false)
    {
        if (string.IsNullOrWhiteSpace(nome)) return VoltarCom("Informe o nome do formulário.");
        var etapa = etapaId is int e ? await db.Etapas.FirstOrDefaultAsync(x => x.Id == e) : null;
        if (etapa is null) return VoltarCom("Escolha o funil e a etapa de entrada.");
        var funilId = etapa.FunilId;
        if (distribuicao == Distribuicao.Rodizio && (participantes is null || participantes.Count == 0))
            return VoltarCom("Escolha quem participa do rodízio.");
        var f = id is int i ? await db.Formularios.FindAsync(i) : null;
        if (f is null) db.Formularios.Add(f = new FormularioCaptura { Token = CapturaService.NovoToken() });
        if (novoToken) f.Token = CapturaService.NovoToken();
        f.Nome = nome.Trim(); f.FunilId = funilId; f.EtapaId = etapaId; f.Distribuicao = distribuicao;
        f.ResponsavelFixoId = responsavelFixoId; f.ParticipantesIds = string.Join(",", participantes ?? new());
        f.Ativo = ativo;
        await db.SaveChangesAsync();
        return VoltarCom(novoToken ? "Novo endereço gerado. Atualize o formulário do site." : "Formulário salvo.", "/configuracoes/formularios");
    }

    // ---------------- metas ----------------

    [Authorize(Policy = Politicas.Gestor), HttpGet("/metas")]
    public async Task<IActionResult> Metas(int? ano, int? mes)
    {
        var hoje = Hora.HojeLocal;
        var a = ano ?? hoje.Year;
        var m = Math.Clamp(mes ?? hoje.Month, 1, 12);
        ViewBag.Ano = a; ViewBag.Mes = m;
        ViewBag.Metas = await db.Metas.Where(x => x.Ano == a && x.Mes == m).ToDictionaryAsync(x => x.UsuarioId);
        return View(await db.Usuarios.Where(u => u.Ativo).OrderBy(u => u.Nome).ToListAsync());
    }

    [Authorize(Policy = Politicas.Gestor), HttpPost("/metas")]
    public async Task<IActionResult> SalvarMetas(int ano, int mes, Dictionary<int, decimal> valor, Dictionary<int, int> qtd)
    {
        if (mes is < 1 or > 12 || ano is < 2000 or > 2100) return VoltarCom("Mês inválido.");
        var existentes = await db.Metas.Where(x => x.Ano == ano && x.Mes == mes).ToListAsync();
        foreach (var uid in valor.Keys.Union(qtd.Keys))
        {
            var v = Math.Max(0, valor.GetValueOrDefault(uid));
            var q = Math.Max(0, qtd.GetValueOrDefault(uid));
            var meta = existentes.FirstOrDefault(x => x.UsuarioId == uid);
            if (v == 0 && q == 0) { if (meta is not null) db.Metas.Remove(meta); continue; }
            if (meta is null) db.Metas.Add(meta = new Meta { UsuarioId = uid, Ano = ano, Mes = mes });
            meta.Valor = v; meta.QtdVendas = q;
        }
        await db.SaveChangesAsync();
        return VoltarCom("Metas salvas.", $"/metas?ano={ano}&mes={mes}");
    }

    // ---------------- modelo de funil ----------------

    /// <summary>Cria um funil comercial pronto, com etapas, probabilidades e automações de exemplo.</summary>
    [Authorize(Policy = Politicas.Admin), HttpPost("/configuracoes/funis/modelo")]
    public async Task<IActionResult> FunilModelo()
    {
        var f = await config.CriarFunilAsync("Comercial CDL");
        var normais = f.Etapas.Where(e => e.Tipo == TipoEtapa.Normal).ToList();
        db.Etapas.RemoveRange(normais);
        var etapas = new (string Nome, string Cor, int Prob)[]
        {
            ("Primeiro contato", "#99ccff", 10), ("Diagnóstico", "#c7d8ff", 25), ("Proposta enviada", "#ffff99", 50),
            ("Negociação", "#ffcc66", 75), ("Aguardando pagamento", "#87f2c0", 90)
        };
        var novas = etapas.Select((e, i) => new Etapa { FunilId = f.Id, Nome = e.Nome, Cor = e.Cor, Probabilidade = e.Prob, Ordem = i + 1 }).ToList();
        db.Etapas.AddRange(novas);
        foreach (var e in f.Etapas.Where(e => e.Tipo == TipoEtapa.Ganho)) e.Probabilidade = 100;
        await db.SaveChangesAsync();
        db.Automacoes.AddRange(
            new AutomacaoEtapa { EtapaId = novas[0].Id, TipoTarefa = TipoTarefa.Ligacao, Texto = "Fazer o primeiro contato", PrazoDias = 0, HoraPrazo = 17 },
            new AutomacaoEtapa { EtapaId = novas[2].Id, TipoTarefa = TipoTarefa.Retorno, Texto = "Retornar sobre a proposta", PrazoDias = 2 },
            new AutomacaoEtapa { EtapaId = novas[4].Id, TipoTarefa = TipoTarefa.Retorno, Texto = "Confirmar pagamento", PrazoDias = 3 });
        if (!await db.MotivosPerda.AnyAsync())
            db.MotivosPerda.AddRange(new[] { "Preço", "Sem retorno do cliente", "Fechou com concorrente", "Sem interesse no momento", "Fora do perfil" }
                .Select((n, i) => new MotivoPerda { Nome = n, Ordem = i + 1 }));
        await db.SaveChangesAsync();
        return VoltarCom("Funil \"Comercial CDL\" criado com etapas, probabilidades e automações.", $"/configuracoes/funis/{f.Id}");
    }
}

/// <summary>Endereço público que recebe os formulários do site e cria leads no CRM.</summary>
[AllowAnonymous, IgnoreAntiforgeryToken]
public class CapturaController(CapturaService captura, ILogger<CapturaController> log) : Controller
{
    [HttpPost("/api/captura/{token}"), EnableRateLimiting("captura")]
    public async Task<IActionResult> Receber(string token)
    {
        Response.Headers.AccessControlAllowOrigin = "*";
        var dados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Request.HasFormContentType)
            foreach (var kv in Request.Form) dados[kv.Key] = kv.Value.ToString();
        else if (Request.ContentType?.Contains("json") == true)
        {
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(Request.Body);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject())
                    dados[p.Name] = p.Value.ValueKind == System.Text.Json.JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.ToString();
        }

        // Campo escondido "_gotcha": gente não preenche, robô de spam preenche.
        if (dados.TryGetValue("_gotcha", out var armadilha) && armadilha != "") return Ok(new { ok = true });
        dados.TryGetValue("_redirect", out var volta);

        try
        {
            var lead = await captura.ReceberAsync(token, dados);
            log.LogInformation("Lead {Id} recebido pelo formulário", lead.Id);
        }
        catch (RegraNegocioException e)
        {
            return volta is { Length: > 0 } && Uri.TryCreate(volta, UriKind.Absolute, out _) ? Redirect(volta) : BadRequest(new { ok = false, erro = e.Message });
        }
        return volta is { Length: > 0 } && Uri.TryCreate(volta, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"
            ? Redirect(volta) : Ok(new { ok = true });
    }

    [HttpOptions("/api/captura/{token}")]
    public IActionResult Preflight()
    {
        Response.Headers.AccessControlAllowOrigin = "*";
        Response.Headers.AccessControlAllowMethods = "POST, OPTIONS";
        Response.Headers.AccessControlAllowHeaders = "Content-Type";
        return NoContent();
    }
}
