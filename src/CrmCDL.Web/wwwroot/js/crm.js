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

  // ---------- busca global (Ctrl+K) ----------
  const bg = $("#busca-global");
  if (bg) {
    const inp = $("input", bg), lista = $(".bg-lista", bg);
    let timer, itens = [], sel = 0;
    const desenhar = () => {
      lista.innerHTML = "";
      if (!itens.length) {
        const p = document.createElement("p");
        p.className = "muted bg-dica";
        p.textContent = inp.value.trim().length < 2 ? "Digite pelo menos 2 letras." : "Nada encontrado.";
        lista.appendChild(p);
        return;
      }
      itens.forEach((i, n) => {
        const a = document.createElement("a");
        a.className = "bg-item" + (n === sel ? " on" : "");
        a.href = i.url;
        a.innerHTML = `<span class="tipo"></span><span><b></b><small></small></span>`;
        a.querySelector(".tipo").textContent = i.tipo;
        a.querySelector("b").textContent = i.nome;
        a.querySelector("small").textContent = i.extra || "";
        lista.appendChild(a);
      });
    };
    inp.addEventListener("input", () => {
      clearTimeout(timer);
      timer = setTimeout(async () => {
        if (inp.value.trim().length < 2) { itens = []; desenhar(); return; }
        const r = await fetch(`/busca-global?q=${encodeURIComponent(inp.value.trim())}`, { headers: { Accept: "application/json" } });
        itens = r.ok ? await r.json() : [];
        sel = 0;
        desenhar();
      }, 180);
    });
    inp.addEventListener("keydown", (e) => {
      if (e.key === "ArrowDown" || e.key === "ArrowUp") {
        e.preventDefault();
        if (!itens.length) return;
        sel = (sel + (e.key === "ArrowDown" ? 1 : itens.length - 1)) % itens.length;
        desenhar();
      } else if (e.key === "Enter" && itens[sel]) location.href = itens[sel].url;
      else if (e.key === "Escape") { e.preventDefault(); bg.close(); }
    });
    document.addEventListener("keydown", (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") { e.preventDefault(); bg.showModal(); inp.select(); }
    });
  }

  // ---------- aviso de contato duplicado ----------
  $$("[data-dup]").forEach(campo => {
    const form = campo.closest("form");
    const caixa = $(".aviso-dup", form);
    if (!caixa) return;
    let timer;
    const checar = () => {
      clearTimeout(timer);
      timer = setTimeout(async () => {
        const fone = $('[data-dup="fone"]', form)?.value || "";
        const email = $('[data-dup="email"]', form)?.value || "";
        if (fone.replace(/\D/g, "").length < 8 && !email.includes("@")) { caixa.hidden = true; return; }
        const r = await fetch(`/duplicados?fone=${encodeURIComponent(fone)}&email=${encodeURIComponent(email)}`, { headers: { Accept: "application/json" } });
        const achados = r.ok ? await r.json() : [];
        const idCampo = $('[name="ContatoId"]', form);
        if (!achados.length) { caixa.hidden = true; if (idCampo) idCampo.value = ""; return; }
        caixa.textContent = "Já existe no CRM: ";
        achados.forEach((c, i) => {
          const a = document.createElement("a");
          a.href = `/contatos/${c.id}`; a.target = "_blank"; a.textContent = `${c.nome}${c.extra ? " (" + c.extra + ")" : ""}`;
          caixa.append(i ? ", " : "", a);
          if (idCampo && i === 0) {
            const b = document.createElement("button");
            b.type = "button"; b.className = "btn sm"; b.textContent = "Usar este contato";
            b.addEventListener("click", () => { idCampo.value = c.id; caixa.textContent = `Vai usar o contato existente: ${c.nome}.`; });
            caixa.append(b);
          }
        });
        caixa.hidden = false;
      }, 350);
    };
    campo.addEventListener("input", checar);
  });

  // ---------- produto: preço sugerido ----------
  $$("select[data-produto]").forEach(sel => sel.addEventListener("change", () => {
    const preco = sel.selectedOptions[0]?.dataset.preco;
    const campo = sel.form.querySelector("[name=preco]");
    if (campo) campo.placeholder = preco ? Number(preco).toLocaleString("pt-BR", { style: "currency", currency: "BRL" }) : "0,00";
  }));

  // ---------- formulário do site: mostra só os campos do tipo de distribuição ----------
  $$("select[data-distribuicao]").forEach(sel => {
    const aplicar = () => $$("[data-so]", sel.form).forEach(el => el.hidden = el.dataset.so !== sel.value);
    sel.addEventListener("change", aplicar);
    aplicar();
  });

  // ---------- copiar ----------
  document.addEventListener("click", async (e) => {
    const b = e.target.closest("[data-copiar]");
    if (!b) return;
    try { await navigator.clipboard.writeText(b.dataset.copiar); aviso("Copiado."); } catch { aviso("Não foi possível copiar. Selecione e copie o texto.", true); }
  });

  // ---------- lista de leads: seleção e ações em massa ----------
  const fm = $("#form-massa");
  if (fm) {
    const barra = $(".barra-massa", fm);
    const marcados = () => $$("[data-sel]:checked", fm);
    const atualizar = () => { const n = marcados().length; barra.hidden = n === 0; $("[data-sel-qtd]", fm).textContent = n; };
    fm.addEventListener("change", (e) => {
      if (e.target.matches("[data-sel-todos]")) $$("[data-sel]", fm).forEach(c => c.checked = e.target.checked);
      if (e.target.matches("[data-sel], [data-sel-todos]")) atualizar();
    });
    const enviar = (acao, valorId, texto) => {
      fm.elements.acao.value = acao; fm.elements.valorId.value = valorId ?? ""; fm.elements.texto.value = texto ?? "";
      fm.submit();
    };
    $("[data-massa-resp]", fm)?.addEventListener("change", (e) => e.target.value && enviar("Responsavel", e.target.value));
    $("[data-massa-etapa]", fm)?.addEventListener("change", (e) => e.target.value && enviar("Etapa", e.target.value));
    $("[data-massa-tag-ok]", fm)?.addEventListener("click", () => { const t = $("[data-massa-tag]", fm).value.trim(); if (t) enviar("Tag", null, t); });
    $("[data-massa-excluir]", fm)?.addEventListener("click", () => {
      if (confirm(`Excluir ${marcados().length} lead(s) com todo o histórico? Não dá para desfazer.`)) enviar("Excluir");
    });
  }
})();

// ---------- assistente de IA (painel lateral) ----------
(() => {
  "use strict";
  const painel = document.getElementById("assistente");
  if (!painel) return;
  const csrf = document.querySelector('meta[name="csrf"]')?.content || "";
  const msgs = painel.querySelector(".assist-msgs");
  const form = painel.querySelector(".assist-form");
  const campo = form.querySelector("textarea");
  const boasVindas = painel.querySelector(".assist-boas").outerHTML;
  const CHAVE = "crm-assistente";
  let historico = [];
  try { historico = JSON.parse(sessionStorage.getItem(CHAVE) || "[]"); } catch { historico = []; }
  const guardar = () => { try { sessionStorage.setItem(CHAVE, JSON.stringify(historico.slice(-30))); } catch { } };

  // Markdown mínimo e seguro: escapa tudo e só depois aplica negrito, listas e links internos (/leads/…).
  const esc = (s) => s.replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  function formatar(texto) {
    const linhas = esc(texto).split("\n");
    let html = "", lista = false;
    for (let l of linhas) {
      l = l.replace(/\*\*(.+?)\*\*/g, "<b>$1</b>")
           .replace(/\[([^\]]+)\]\((\/[a-z0-9\-/?=&;]*)\)/gi, '<a href="$2">$1</a>')
           .replace(/`([^`]+)`/g, "<code>$1</code>");
      const item = /^\s*([-*•]|\d+\.)\s+(.*)$/.exec(l);
      if (item) { if (!lista) { html += "<ul>"; lista = true; } html += `<li>${item[2]}</li>`; continue; }
      if (lista) { html += "</ul>"; lista = false; }
      const titulo = /^#{1,4}\s+(.*)$/.exec(l);
      html += titulo ? `<p><b>${titulo[1]}</b></p>` : l.trim() ? `<p>${l}</p>` : "";
    }
    if (lista) html += "</ul>";
    return html;
  }

  function bolha(papel, texto) {
    const d = document.createElement("div");
    d.className = "assist-msg " + (papel === "user" ? "eu" : "ia");
    if (papel === "user") d.textContent = texto; else d.innerHTML = formatar(texto);
    msgs.appendChild(d);
    msgs.scrollTop = msgs.scrollHeight;
    return d;
  }

  function redesenhar() {
    msgs.innerHTML = historico.length ? "" : boasVindas;
    historico.forEach(f => bolha(f.papel, f.texto));
  }

  async function perguntar(pergunta) {
    pergunta = pergunta.trim();
    if (!pergunta || form.classList.contains("ocupado")) return;
    msgs.querySelector(".assist-boas")?.remove();
    bolha("user", pergunta);
    const espera = bolha("assistant", "");
    espera.classList.add("digitando");
    espera.innerHTML = "<span></span><span></span><span></span>";
    form.classList.add("ocupado");
    campo.value = "";
    try {
      const r = await fetch("/assistente", {
        method: "POST",
        headers: { "Content-Type": "application/json", "Accept": "application/json", "X-CSRF": csrf },
        body: JSON.stringify({ historico, pergunta })
      });
      const dados = await r.json().catch(() => ({}));
      const resposta = r.status === 429 ? "Muitas perguntas em pouco tempo. Aguarde um minuto." : dados.resposta || dados.erro || "Não consegui responder agora.";
      espera.remove();
      bolha("assistant", resposta);
      if (r.ok) { historico.push({ papel: "user", texto: pergunta }, { papel: "assistant", texto: resposta }); guardar(); }
    } catch {
      espera.remove();
      bolha("assistant", "Sem conexão com o servidor. Tente de novo.");
    } finally {
      form.classList.remove("ocupado");
      campo.focus();
    }
  }

  const abrir = () => { painel.hidden = false; requestAnimationFrame(() => painel.classList.add("aberto")); redesenhar(); campo.focus(); };
  const fechar = () => { painel.classList.remove("aberto"); setTimeout(() => painel.hidden = true, 250); };
  document.querySelector("[data-assistente-abre]")?.addEventListener("click", () => painel.hidden ? abrir() : fechar());
  painel.querySelector("[data-assistente-fecha]").addEventListener("click", fechar);
  painel.querySelector("[data-assistente-limpa]").addEventListener("click", () => { historico = []; guardar(); redesenhar(); });
  painel.addEventListener("click", (e) => { const s = e.target.closest("[data-sugestao]"); if (s) perguntar(s.textContent); });
  form.addEventListener("submit", (e) => { e.preventDefault(); perguntar(campo.value); });
  campo.addEventListener("keydown", (e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); perguntar(campo.value); } });
  campo.addEventListener("input", () => { campo.style.height = "auto"; campo.style.height = Math.min(140, campo.scrollHeight) + "px"; });
  document.addEventListener("keydown", (e) => { if (e.key === "Escape" && !painel.hidden) fechar(); });
})();
