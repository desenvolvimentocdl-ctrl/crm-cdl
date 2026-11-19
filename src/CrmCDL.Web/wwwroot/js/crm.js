// CRM CDL — interações (sem bibliotecas). Animações só com transform/opacity.
(() => {
  "use strict";
  const $ = (s, el = document) => el.querySelector(s);
  const $$ = (s, el = document) => [...el.querySelectorAll(s)];
  const csrf = $('meta[name="csrf"]')?.content || "";
  const brl = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL", maximumFractionDigits: 0 });

  function aviso(texto, erro = false) {
    const t = document.createElement("div");
    t.className = "toast " + (erro ? "erro" : "ok");
    t.setAttribute("role", erro ? "alert" : "status");
    t.textContent = texto;
    document.body.appendChild(t);
    setTimeout(() => t.remove(), 6000);
  }

  async function postJson(url, dados) {
    const r = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json", "Accept": "application/json", "X-CSRF": csrf },
      body: JSON.stringify(dados)
    });
    if (!r.ok) {
      let msg = "Não foi possível salvar. Tente de novo.";
      try { msg = (await r.json()).erro || msg; } catch { }
      throw new Error(msg);
    }
    return r.json();
  }

  // ---------- sino de notificações: atualiza o número a cada minuto sem recarregar ----------
  const badge = $("[data-notif]");
  if (badge) {
    const tituloOriginal = document.title;
    let ultima = badge.hidden ? 0 : parseInt(badge.textContent, 10) || 0;
    const mostrar = (n) => {
      badge.hidden = n === 0;
      badge.textContent = n > 99 ? "99+" : String(n);
      document.title = n > 0 ? `(${n}) ${tituloOriginal}` : tituloOriginal;
    };
    mostrar(ultima);
    let timer = null;
    const verificar = async () => {
      if (document.hidden) return;
      try {
        const r = await fetch("/notificacoes/contagem", { headers: { Accept: "application/json" }, cache: "no-store" });
        if (r.redirected || !r.ok) { clearInterval(timer); return; } // sessão expirou
        const n = (await r.json()).naoLidas || 0;
        if (n > ultima) aviso(n - ultima === 1 ? "Você tem uma nova notificação." : `Você tem ${n - ultima} novas notificações.`);
        ultima = n;
        mostrar(n);
      } catch { /* sem rede: tenta de novo no próximo minuto */ }
    };
    timer = setInterval(verificar, 60000);
    document.addEventListener("visibilitychange", () => { if (!document.hidden) verificar(); });
  }

  // ---------- modais ----------
  document.addEventListener("click", (e) => {
    const abre = e.target.closest("[data-abre]");
    if (abre) { const d = $(abre.dataset.abre); d?.showModal(); d?.querySelector("input:not([type=hidden]),select")?.focus(); }
    if (e.target.closest("[data-fecha]")) e.target.closest("dialog")?.close();
    if (e.target.tagName === "DIALOG") e.target.close(); // clique no fundo escuro
  });

  /** Pergunta o motivo de perda. Resolve com o id (ou "") e com null se cancelar. */
  function perguntarMotivo() {
    const d = $("#motivo-perda");
    if (!d) return Promise.resolve("");
    return new Promise((resolve) => {
      d.returnValue = "";
      d.showModal();
      d.addEventListener("close", () => resolve(d.returnValue === "ok" ? $("#motivo-select").value : null), { once: true });
    });
  }

  // ---------- linhas de tabela clicáveis ----------
  document.addEventListener("click", (e) => {
    const tr = e.target.closest("tr[data-href]");
    if (tr && !e.target.closest("a,button,input,select")) location.href = tr.dataset.href;
  });

  // ---------- filtro das opções de múltipla escolha ----------
  document.addEventListener("input", (e) => {
    if (!e.target.matches("[data-filtra-multi]")) return;
    const q = e.target.value.toLowerCase();
    $$("label", e.target.nextElementSibling).forEach(l => l.style.display = l.textContent.toLowerCase().includes(q) ? "" : "none");
  });

  // ---------- abas Nota / Tarefa ----------
  $$("[data-aba]").forEach(b => b.addEventListener("click", () => {
    $$("[data-aba]").forEach(x => x.classList.toggle("on", x === b));
    $$("[data-painel]").forEach(p => p.classList.toggle("on", p.dataset.painel === b.dataset.aba));
    $(`[data-painel="${b.dataset.aba}"] textarea, [data-painel="${b.dataset.aba}"] input[name=texto]`)?.focus();
  }));

  // ---------- troca de etapa na página do lead (pede motivo ao perder) ----------
  $$('form[data-perda="1"]').forEach(f => f.addEventListener("submit", async (e) => {
    if (f.dataset.ok) return;
    e.preventDefault();
    const motivo = await perguntarMotivo();
    if (motivo === null) return;
    f.querySelector("[name=motivoPerdaId]").value = motivo;
    f.dataset.ok = "1";
    f.submit();
  }));

  // ---------- autocompletar contatos/empresas ----------
  $$("form[data-autocompleta]").forEach(form => {
    const tipo = form.dataset.autocompleta;
    const campo = form.querySelector("[name=nome]");
    const id = form.querySelector("[name$=Id]");
    const lista = document.createElement("div");
    lista.className = "card";
    Object.assign(lista.style, { position: "absolute", zIndex: 30, display: "none", maxHeight: "240px", overflow: "auto", padding: "4px" });
    document.body.appendChild(lista);
    let timer;
    campo.addEventListener("input", () => {
      id.value = "";
      clearTimeout(timer);
      timer = setTimeout(async () => {
        if (campo.value.trim().length < 2) { lista.style.display = "none"; return; }
        const r = await fetch(`/buscar/${tipo}?q=${encodeURIComponent(campo.value)}`, { headers: { Accept: "application/json" } });
        const itens = r.ok ? await r.json() : [];
        lista.innerHTML = "";
        itens.forEach(i => {
          const b = document.createElement("button");
          b.type = "button";
          b.className = "btn ghost";
          Object.assign(b.style, { width: "100%", justifyContent: "flex-start", height: "auto", padding: "8px 10px" });
          b.innerHTML = `<span></span><small class="muted"></small>`;
          b.children[0].textContent = i.nome;
          b.children[1].textContent = i.extra || "";
          b.addEventListener("click", () => { campo.value = i.nome; id.value = i.id; lista.style.display = "none"; });
          lista.appendChild(b);
        });
        const r2 = campo.getBoundingClientRect();
        Object.assign(lista.style, { left: r2.left + scrollX + "px", top: r2.bottom + scrollY + 4 + "px", width: r2.width + "px", display: itens.length ? "block" : "none" });
        if (campo.closest("dialog")) campo.closest("dialog").appendChild(lista), Object.assign(lista.style, { position: "fixed", left: r2.left + "px", top: r2.bottom + 4 + "px" });
      }, 220);
    });
    campo.addEventListener("blur", () => setTimeout(() => lista.style.display = "none", 200));
  });

  $$("input[data-sugere]").forEach(inp => {
    let timer;
    inp.addEventListener("input", () => {
      clearTimeout(timer);
      timer = setTimeout(async () => {
        if (inp.value.trim().length < 2) return;
        const r = await fetch(`/buscar/${inp.dataset.sugere}?q=${encodeURIComponent(inp.value)}`, { headers: { Accept: "application/json" } });
        const dl = document.getElementById(inp.getAttribute("list"));
        if (!r.ok || !dl) return;
        dl.innerHTML = "";
        (await r.json()).forEach(i => { const o = document.createElement("option"); o.value = i.nome; dl.appendChild(o); });
      }, 220);
    });
  });

  // ---------- quadro do funil ----------
  const quadro = $("#quadro");
  if (quadro) {
    let arrastado = null, origem = null, proximoOrigem = null;
    const ph = document.createElement("div");
    ph.className = "placeholder";

    const atualizarTotais = () => $$(".coluna", quadro).forEach(col => {
      const cards = $$(".cartao", col);
      $("[data-qtd]", col).textContent = cards.length;
      $("[data-soma]", col).textContent = brl.format(cards.reduce((s, c) => s + Number(c.dataset.valor || 0), 0));
    });

    quadro.addEventListener("dragstart", (e) => {
      const c = e.target.closest(".cartao");
      if (!c) return;
      arrastado = c; origem = c.parentElement; proximoOrigem = c.nextElementSibling;
      e.dataTransfer.effectAllowed = "move";
      e.dataTransfer.setData("text/plain", c.dataset.id);
      ph.style.height = c.offsetHeight + "px";
      requestAnimationFrame(() => c.classList.add("arrastando"));
    });

    quadro.addEventListener("dragover", (e) => {
      if (!arrastado) return;
      const lista = e.target.closest(".coluna")?.querySelector("[data-lista]");
      if (!lista) return;
      e.preventDefault();
      $$(".coluna.alvo", quadro).forEach(c => c !== lista.parentElement && c.classList.remove("alvo"));
      lista.parentElement.classList.add("alvo");
      const depois = $$(".cartao:not(.arrastando)", lista).find(c => {
        const r = c.getBoundingClientRect();
        return e.clientY < r.top + r.height / 2;
      });
      if (depois) { if (ph.nextElementSibling !== depois) lista.insertBefore(ph, depois); }
      else if (lista.lastElementChild !== ph) lista.appendChild(ph);
    });

    const limpar = () => {
      ph.remove();
      $$(".coluna.alvo", quadro).forEach(c => c.classList.remove("alvo"));
      arrastado?.classList.remove("arrastando");
    };
    quadro.addEventListener("dragend", () => { limpar(); arrastado = null; });

    quadro.addEventListener("drop", async (e) => {
      if (!arrastado || !ph.parentElement) return;
      e.preventDefault();
      const c = arrastado, lista = ph.parentElement, coluna = lista.parentElement;
      lista.insertBefore(c, ph);
      limpar();
      arrastado = null;

      const ant = c.previousElementSibling?.classList.contains("cartao") ? Number(c.previousElementSibling.dataset.pos) : null;
      const prox = c.nextElementSibling?.classList.contains("cartao") ? Number(c.nextElementSibling.dataset.pos) : null;
      const pos = ant === null && prox === null ? 0 : ant === null ? prox - 1 : prox === null ? ant + 1 : (ant + prox) / 2;
      const mudouEtapa = lista !== origem;
      if (!mudouEtapa && c.dataset.pos === String(pos)) return;

      const voltar = () => { origem.insertBefore(c, proximoOrigem); atualizarTotais(); };
      let motivo = null;
      if (mudouEtapa && coluna.dataset.tipo === "Perdido") {
        motivo = await perguntarMotivo();
        if (motivo === null) { voltar(); return; }
      }
      atualizarTotais();
      c.classList.add("salvando");
      try {
        await postJson(`/leads/${c.dataset.id}/mover`, { etapaId: Number(coluna.dataset.etapa), posicao: pos, motivoPerdaId: motivo ? Number(motivo) : null });
        c.dataset.pos = String(pos);
      } catch (err) {
        voltar();
        aviso(err.message, true);
      } finally {
        c.classList.remove("salvando");
      }
    });

    // Adicionar rápido no topo da coluna
    quadro.addEventListener("click", (e) => {
      const b = e.target.closest("[data-rapido]");
      if (b) {
        const col = b.closest(".coluna");
        if ($(".form-rapido", col)) { $(".form-rapido input[name=Titulo]", col).focus(); return; }
        const f = $("#tpl-rapido").content.firstElementChild.cloneNode(true);
        f.querySelector("[name=EtapaId]").value = b.dataset.rapido;
        b.after(f);
        f.querySelector("[name=Titulo]").focus();
      }
      if (e.target.closest("[data-cancela]")) e.target.closest(".form-rapido").remove();
    });

    // Rolagem horizontal com a roda do mouse quando o cursor está no fundo do quadro
    quadro.addEventListener("wheel", (e) => {
      if (e.target === quadro && Math.abs(e.deltaY) > Math.abs(e.deltaX)) { quadro.scrollLeft += e.deltaY; e.preventDefault(); }
    }, { passive: false });
  }

  // ---------- editor de etapas (configurações) ----------
  const editor = $("#etapas-normais");
  if (editor) {
    let arr = null;
    editor.addEventListener("dragstart", (e) => {
      arr = e.target.closest(".etapa-ed");
      if (!arr || !e.target.closest(".alca") && e.target.tagName === "INPUT") return;
      e.dataTransfer.effectAllowed = "move";
      requestAnimationFrame(() => arr?.classList.add("arrastando"));
    });
    editor.addEventListener("dragover", (e) => {
      if (!arr) return;
      e.preventDefault();
      const depois = $$(".etapa-ed:not(.arrastando)", editor).find(x => e.clientY < x.getBoundingClientRect().top + x.offsetHeight / 2);
      depois ? editor.insertBefore(arr, depois) : editor.appendChild(arr);
    });
    editor.addEventListener("dragend", () => { arr?.classList.remove("arrastando"); arr = null; });
    editor.addEventListener("click", (e) => {
      const b = e.target.closest("[data-remove-etapa]");
      if (!b) return;
      if (Number(b.dataset.removeEtapa) > 0) { aviso("Esta etapa tem leads. Mova-os para outra etapa antes de excluir.", true); return; }
      b.closest(".etapa-ed").remove();
    });
    $("#add-etapa").addEventListener("click", () => {
      const n = $("#tpl-etapa").content.firstElementChild.cloneNode(true);
      editor.appendChild(n);
      n.querySelector("input[type=text]").focus();
    });
    $("#form-funil").addEventListener("submit", () => {
      $$(".etapa-ed", editor).forEach((linha, i) => $$("[data-campo]", linha).forEach(inp => inp.name = `etapas[${i}].${inp.dataset.campo}`));
    });
  }
})();
