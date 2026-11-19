"use strict";
// Hush's window. The exe sends: init, settings, state (every second), taken (a key another app owns).
// The page sends back: ready, toggle, mute, set, open. Nothing here touches the network.

const $ = id => document.getElementById(id);
const host = window.chrome && window.chrome.webview;
const send = m => host && host.postMessage(m);
const icon = n => `<svg class="i" viewBox="0 0 24 24" aria-hidden="true">${ICONS[n] || ICONS["app-window"]}</svg>`;
const esc = s => String(s == null ? "" : s).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

const TEXT = {
  it: {
    kicker: "Il tuo microfono",
    title: {
      off: 'Microfono <span class="serif red">spento</span>',
      on: 'Microfono <span class="serif grad">acceso</span>',
      live: 'Ti stanno <span class="serif grad">ascoltando</span>',
      none: 'Nessun <span class="serif">microfono</span>',
    },
    detail: {
      off: who => "Spento per tutte le app, direttamente in Windows: nessuna ti sente." + (who ? ` ${who} ${who.includes(",") ? "ricevono" : "riceve"} solo silenzio.` : ""),
      on: () => "Acceso, ma adesso nessuna app lo sta usando.",
      live: who => `${who} ${who.includes(",") ? "ti sentono" : "ti sente"} in questo momento.`,
      none: () => "Collegane uno: Hush lo vede da solo, e se il microfono è spento spegne anche quello.",
    },
    toggle: { off: "Riaccendi il microfono", on: "Spegni il microfono" },
    keyWhat: "spegne e riaccende, in qualsiasi app", change: "Cambia", keyBad: "Questo tasto lo usa già un'altra app: scegline un altro nelle impostazioni.",
    pill: { off: "spento", on: "acceso", live: "in ascolto", none: "nessun microfono" },
    micNow: "Chi ti sente adesso", camNow: "Chi ti vede adesso", nobody: "Nessuna app.",
    since: m => m < 1 ? "da meno di un minuto" : m < 60 ? `da ${m} min` : `da ${Math.floor(m / 60)} h ${m % 60} min`,
    silence: "riceve solo silenzio", listening: "in ascolto", watching: "in onda",
    camNote: "Hush non può spegnere la webcam: Windows non lo permette senza un driver. Un pezzo di nastro adesivo sì.",
    mics: "Microfoni", def: "predefinito", comms: "per le chiamate",
    micsNote: "Spento qui vuol dire spento in Windows: le app ricevono silenzio finché non lo riaccendi.",
    recent: "Ultimi usi", recentSub: "30 giorni", noRecent: "Nessuna app ha usato microfono o webcam negli ultimi 30 giorni.",
    mic: "microfono", cam: "webcam", now: "adesso", today: "oggi", yesterday: "ieri",
    dur: m => m < 1 ? "meno di un minuto" : m < 60 ? `${m} min` : `${Math.floor(m / 60)} h ${m % 60} min`,
    settings: "Impostazioni", startup: "Avvia con Windows",
    startupText: "Il tasto funziona solo mentre Hush è acceso: così è sempre pronto, nella tray, senza finestre.",
    startupPortable: "Versione portable: le impostazioni stanno in Hush.ini, accanto all'exe, e Hush non parte da solo.",
    key: "Tasto", keyText: "Funziona in qualsiasi app, anche a schermo intero. Una lettera da sola no: la scriveresti. F13-F24 e Pausa sì.",
    taken: k => `${k} lo usa già un'altra app: resta quello di prima.`,
    osd: "Avviso a schermo", osdText: "Quando usi il tasto compare per un attimo «Microfono spento» o «acceso», sopra qualsiasi finestra.",
    about: v => `Hush ${v} · licenza MIT · niente internet, niente telemetria, niente admin.`,
    links: ["Codice sorgente", "Offrimi un caffè"],
    footer: v => `Hush ${v} · fatto da Davide Mariotti · un clic sull'icona nella tray spegne e riaccende`,
  },
  en: {
    kicker: "Your microphone",
    title: {
      off: 'Microphone <span class="serif red">off</span>',
      on: 'Microphone <span class="serif grad">on</span>',
      live: 'Someone is <span class="serif grad">listening</span>',
      none: 'No <span class="serif">microphone</span>',
    },
    detail: {
      off: who => "Off for every app, right in Windows: nobody hears you." + (who ? ` ${who} ${who.includes(",") ? "get" : "gets"} nothing but silence.` : ""),
      on: () => "On, but no app is using it right now.",
      live: who => `${who} ${who.includes(",") ? "hear" : "hears"} you right now.`,
      none: () => "Plug one in: Hush sees it by itself, and if the microphone is off, it switches that one off too.",
    },
    toggle: { off: "Turn the microphone on", on: "Turn the microphone off" },
    keyWhat: "turns it off and on, in any app", change: "Change", keyBad: "Another app already uses this key: pick another one in the settings.",
    pill: { off: "off", on: "on", live: "listening", none: "no microphone" },
    micNow: "Who hears you now", camNow: "Who sees you now", nobody: "No app.",
    since: m => m < 1 ? "for under a minute" : m < 60 ? `for ${m} min` : `for ${Math.floor(m / 60)} h ${m % 60} min`,
    silence: "gets nothing but silence", listening: "listening", watching: "on air",
    camNote: "Hush can't switch the webcam off: Windows doesn't allow it without a driver. A piece of tape does.",
    mics: "Microphones", def: "default", comms: "for calls",
    micsNote: "Off here means off in Windows: apps get silence until you turn it back on.",
    recent: "Recent use", recentSub: "30 days", noRecent: "No app used the microphone or the webcam in the last 30 days.",
    mic: "microphone", cam: "webcam", now: "now", today: "today", yesterday: "yesterday",
    dur: m => m < 1 ? "under a minute" : m < 60 ? `${m} min` : `${Math.floor(m / 60)} h ${m % 60} min`,
    settings: "Settings", startup: "Start with Windows",
    startupText: "The key only works while Hush runs: this way it's always ready, in the tray, no window.",
    startupPortable: "Portable version: settings live in Hush.ini, next to the exe, and Hush doesn't start by itself.",
    key: "Key", keyText: "Works in any app, full screen too. A letter alone won't do: you'd be typing it. F13-F24 and Pause will.",
    taken: k => `${k} is taken by another app: the old key stays.`,
    osd: "On-screen notice", osdText: "When you use the key, \"Microphone off\" or \"on\" shows up for a moment, over any window.",
    about: v => `Hush ${v} · MIT license · no internet, no telemetry, no admin.`,
    links: ["Source code", "Buy me a coffee"],
    footer: v => `Hush ${v} · made by Davide Mariotti · one click on the tray icon turns it off and on`,
  },
};
let T = TEXT.en, locale = "en-US";
const state = { init: null, settings: null, data: null, taken: null };

if (host) host.addEventListener("message", e => {
  const m = e.data;
  if (m.t === "init") { state.init = m; T = TEXT[m.lang] || TEXT.en; locale = m.lang === "it" ? "it-IT" : "en-US"; document.documentElement.lang = m.lang; build(); }
  else if (m.t === "settings") { state.settings = m; renderKeys(); renderDrawer(); }
  else if (m.t === "state") { state.data = m; render(); }
  else if (m.t === "taken") { state.taken = m.keyText; renderDrawer(); }
});
send({ t: "ready" });

function build() {
  $("logo").innerHTML = icon("mic");
  $("version").textContent = "v" + state.init.version;
  $("openSettings").innerHTML = icon("settings");
  $("openSettings").setAttribute("aria-label", T.settings);
  $("openSettings").onclick = () => document.body.classList.add("open");
  $("scrim").onclick = () => document.body.classList.remove("open");
  const card = (id, i) => `<div class="card spot reveal" id="${id}" style="--d:${i * 70}ms"></div>`;
  $("main").innerHTML = `
    <section class="hero">
      <div><p class="kicker">${esc(T.kicker)}</p><h1 id="title"></h1><p class="detail" id="detail"></p><div class="keys" id="keys"></div></div>
      <button class="big" id="toggle"><span class="ring"></span><span class="ring r2"></span><span class="core" id="core"></span></button>
    </section>
    <section class="grid2">${card("micNow", 0)}${card("camNow", 1)}</section>
    ${card("mics", 2)}${card("recent", 3)}
    <footer>${esc(T.footer(state.init.version))}</footer>`;
  $("toggle").onclick = () => send({ t: "toggle" });
  requestAnimationFrame(() => requestAnimationFrame(() => document.querySelectorAll(".reveal").forEach(el => el.classList.add("in"))));
  renderKeys();
  renderDrawer();
  render();
}

const mode = d => !d.mics.length ? "none" : d.muted ? "off" : d.mic.some(u => u.now) ? "live" : "on";
const names = list => [...new Set(list.map(u => u.n))].join(", ");
const minutes = ms => Math.max(0, Math.floor(ms / 60000));
const appIcon = u => `<span class="app">${u.icon ? `<img src="${u.icon}" alt="">` : icon("app-window")}</span>`;
const hm = new Intl.DateTimeFormat(undefined, { hour: "2-digit", minute: "2-digit" });

function when(u) {
  if (u.now) return T.now;
  const d = new Date(u.s), today = new Date(); today.setHours(0, 0, 0, 0);
  const day = d >= today ? T.today : d >= today - 864e5 ? T.yesterday : new Intl.DateTimeFormat(locale, { day: "numeric", month: "short" }).format(d);
  return `${day} ${hm.format(d)}`;
}

function render() {
  const d = state.data;
  if (!d || !state.init || !$("title")) return;
  const m = mode(d);
  document.body.className = m + (document.body.classList.contains("open") ? " open" : "");
  const live = d.mic.filter(u => u.now);
  $("title").innerHTML = T.title[m];
  $("detail").textContent = T.detail[m](names(live));
  $("core").innerHTML = icon(m === "off" ? "mic-off" : "mic");
  $("toggle").setAttribute("aria-label", m === "off" ? T.toggle.off : T.toggle.on);
  $("toggle").disabled = m === "none";
  const pill = $("pill");
  pill.hidden = false;
  pill.className = "pill " + (m === "off" ? "off" : m === "none" ? "" : "on");
  pill.innerHTML = `<span class="dot"></span>${esc(T.pill[m])}`;

  // who is using them right now
  const nowCard = (id, ic, title, list, cam) => {
    const rows = list.filter(u => u.now).map(u => `<div class="row">${appIcon(u)}<div class="who"><b>${esc(u.n)}</b>
      <span class="${!cam && d.muted ? "silence" : ""}">${esc(!cam && d.muted ? T.silence : T.since(minutes(Date.now() - u.s)))}</span></div><span class="onair">${esc(cam ? T.watching : T.listening)}</span></div>`).join("");
    $(id).innerHTML = `<div class="card-head"><span class="icon-tile">${icon(ic)}</span><h2>${esc(title)}</h2></div>
      ${rows || `<p class="empty">${icon(cam ? "video-off" : "volume-x")}${esc(T.nobody)}</p>`}${cam ? `<p class="note">${icon("info")}<span>${esc(T.camNote)}</span></p>` : ""}`;
  };
  nowCard("micNow", "ear", T.micNow, d.mic, false);
  nowCard("camNow", "eye", T.camNow, d.cam, true);

  // the microphones, one switch each
  $("mics").innerHTML = `<div class="card-head"><span class="icon-tile">${icon("mic")}</span><h2>${esc(T.mics)}</h2><span class="count">${d.mics.length}</span></div>` +
    (d.mics.map((x, i) => `<div class="row"><span class="app">${icon(x.muted ? "mic-off" : "mic")}</span><div class="who"><b>${esc(x.n)}</b></div>
      ${x.def ? `<span class="badge">${esc(T.def)}</span>` : ""}${x.comms ? `<span class="badge">${esc(T.comms)}</span>` : ""}
      <input type="checkbox" class="switch" data-mic="${i}" aria-label="${esc(x.n)}"${x.muted ? "" : " checked"}></div>`).join("") || `<p class="empty">${icon("plug-zap")}${esc(T.detail.none())}</p>`) +
    `<p class="note">${icon("info")}<span>${esc(T.micsNote)}</span></p>`;
  $("mics").querySelectorAll("[data-mic]").forEach(s => s.onchange = () => send({ t: "mute", id: d.mics[+s.dataset.mic].id, on: !s.checked }));

  // the last 30 days, microphone and webcam together, newest first
  const recent = [...d.mic.map(u => ({ ...u, cam: false })), ...d.cam.map(u => ({ ...u, cam: true }))].sort((a, b) => b.now - a.now || b.s - a.s).slice(0, 20);
  $("recent").innerHTML = `<div class="card-head"><span class="icon-tile">${icon("history")}</span><h2>${esc(T.recent)}</h2><span class="count">${esc(T.recentSub)}</span></div>` +
    (recent.map(u => `<div class="row">${appIcon(u)}<div class="who"><b>${esc(u.n)}</b><span>${esc(u.cam ? T.cam : T.mic)}</span></div>
      <span class="kind">${icon(u.cam ? "video" : "mic")}</span><span class="when">${esc(when(u))}<br>${u.now ? esc(T.dur(minutes(Date.now() - u.s))) : u.e > u.s ? esc(T.dur(minutes(u.e - u.s))) : ""}</span></div>`).join("") ||
    `<p class="empty">${icon("shield-check")}${esc(T.noRecent)}</p>`);
}

function keyParts(text) { return text.split("+").map(k => `<kbd>${esc(k)}</kbd>`).join('<span class="plus">+</span>'); }

function renderKeys() {
  const s = state.settings;
  if (!s || !$("keys")) return;
  $("keys").innerHTML = `${keyParts(s.keyText)}<span class="what">${esc(T.keyWhat)}</span><button class="link" id="changeKey">${esc(T.change)}</button>` +
    (s.keyOk ? "" : `<span class="warn">${esc(T.keyBad)}</span>`);
  $("changeKey").onclick = () => document.body.classList.add("open");
}

// keys the exe accepts: letters, digits, F1-F24, Pause (Hotkey.Valid in Hush.cs)
const KEYS = [...[..."ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"].map(c => [c.charCodeAt(0), c]), ...Array.from({ length: 24 }, (_, i) => [0x70 + i, "F" + (i + 1)]), [0x13, "Pause"]];
const MODS = [[2, "Ctrl"], [1, "Alt"], [4, "Shift"], [8, "Win"]];

function renderDrawer() {
  const s = state.settings, i = state.init;
  if (!s || !i) return;
  $("drawer").innerHTML = `
    <div class="drawer-head"><h2>${esc(T.settings)}</h2><button class="icon-btn" data-close aria-label="close">${icon("x")}</button></div>
    <div class="setting"><label class="setting-row"><span class="label">${esc(T.startup)}</span><input type="checkbox" class="switch plain" id="sStartup"${s.startup ? " checked" : ""}${i.portable ? " disabled" : ""}></label>
      <p>${esc(i.portable ? T.startupPortable : T.startupText)}</p></div>
    <div class="setting"><div class="setting-row"><span class="label">${esc(T.key)}</span><span class="keys" style="margin:0">${keyParts(s.keyText)}</span></div>
      <div class="mods">${MODS.map(([b, n]) => `<button data-mod="${b}" aria-pressed="${(s.mods & b) !== 0}">${n}</button>`).join("")}
        <select id="sKey" aria-label="${esc(T.key)}">${KEYS.map(([v, n]) => `<option value="${v}"${v === s.key ? " selected" : ""}>${n}</option>`).join("")}</select></div>
      ${state.taken ? `<p class="bad">${esc(T.taken(state.taken))}</p>` : s.keyOk ? "" : `<p class="bad">${esc(T.keyBad)}</p>`}<p>${esc(T.keyText)}</p></div>
    <div class="setting"><label class="setting-row"><span class="label">${esc(T.osd)}</span><input type="checkbox" class="switch plain" id="sOsd"${s.osd ? " checked" : ""}></label><p>${esc(T.osdText)}</p></div>
    <div class="setting"><p class="about">${esc(T.about(i.version))}</p>
      <div class="links"><button class="btn btn-ghost" data-open="https://github.com/davide-mariotti/Hush">${icon("code-xml")}${esc(T.links[0])}</button><button class="btn btn-ghost" data-open="https://ko-fi.com/Z0X32ATNHL">${icon("coffee")}${esc(T.links[1])}</button></div></div>`;
  state.taken = null;
  $("sStartup").onchange = e => send({ t: "set", startup: e.target.checked });
  $("sOsd").onchange = e => send({ t: "set", osd: e.target.checked });
  const setKey = (mods, key) => send({ t: "set", mods, key });
  $("drawer").querySelectorAll("[data-mod]").forEach(b => b.onclick = () => setKey(s.mods ^ +b.dataset.mod, s.key));
  $("sKey").onchange = e => setKey(s.mods, +e.target.value);
  $("drawer").querySelector("[data-close]").onclick = () => document.body.classList.remove("open");
  $("drawer").querySelectorAll("[data-open]").forEach(b => b.onclick = () => send({ t: "open", url: b.dataset.open }));
}

document.addEventListener("keydown", e => { if (e.key === "Escape") document.body.classList.remove("open"); });
document.addEventListener("pointermove", e => {
  const el = e.target.closest && e.target.closest(".spot");
  if (!el) return;
  const b = el.getBoundingClientRect();
  el.style.setProperty("--x", e.clientX - b.left + "px");
  el.style.setProperty("--y", e.clientY - b.top + "px");
});
