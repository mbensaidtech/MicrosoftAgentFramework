// Agent Framework Lab Bench — client. Plain ES modules, no dependencies.
import { t, setLanguage, language, locale, translateTree } from "./i18n.js";

const api = {
  async get(path) {
    const response = await fetch(`/api${path}`);
    if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
    return response.json();
  },
  async send(method, path, body) {
    const response = await fetch(`/api${path}`, {
      method,
      // The custom header is required by the local server (CSRF protection).
      headers: { "Content-Type": "application/json", "X-Lab-Dashboard": "1" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    const payload = response.headers.get("content-type")?.includes("json") ? await response.json().catch(() => ({})) : {};
    return { ok: response.ok, status: response.status, payload };
  },
  post(path, body) { return this.send("POST", path, body); },
};

const storage = {
  get(key) { try { return localStorage.getItem(key); } catch { return null; } },
  set(key, value) { try { localStorage.setItem(key, value); } catch { /* per-viewer convenience only */ } },
  remove(key) { try { localStorage.removeItem(key); } catch { /* per-viewer convenience only */ } },
};

const state = {
  labs: [], currentId: null, view: null, details: null,
  eventSource: null, clockTimer: null, runId: null, runTarget: null,
  record: null, recordIsHistory: false, target: "start",
  settings: null,
  reporting: null, reportingTimer: null, helpBusy: false,
};

// ---------- helpers ----------
const $ = (root, bind) => root.querySelector(`[data-bind="${bind}"]`);
const fmtNumber = (n) => (n == null ? "—" : n.toLocaleString(locale()));
const fmtDuration = (ms) => (ms == null ? "—" : ms < 1000 ? `${ms} ms` : `${(ms / 1000).toLocaleString(locale(), { maximumFractionDigits: 1, minimumFractionDigits: 1 })} s`);
const fmtDate = (iso) => new Date(iso).toLocaleString(locale(), { dateStyle: "medium", timeStyle: "short" });
const fmtClock = (seconds) => {
  const s = Math.floor(seconds);
  return `${String(Math.floor(s / 60)).padStart(2, "0")}:${String(s % 60).padStart(2, "0")}`;
};

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function icon(name, extra = "") {
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("class", `ico ${extra}`.trim());
  svg.setAttribute("aria-hidden", "true");
  const use = document.createElementNS("http://www.w3.org/2000/svg", "use");
  use.setAttribute("href", `#i-${name}`);
  svg.append(use);
  return svg;
}

const pill = (kind, text) => el("span", `pill ${kind ?? "none"}`, text);

let toastTimer;
function toast(message) {
  const node = document.getElementById("toast");
  node.textContent = message;
  node.classList.add("show");
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => node.classList.remove("show"), 2800);
}

// ---------- localized lab texts ----------
function labText(lab, field) {
  return lab.translations?.[language()]?.[field] ?? lab[field];
}

function checkText(lab, id, fallback) {
  return lab.translations?.[language()]?.checks?.[id] ?? fallback;
}

function targetLabel(target) {
  return t(`target.${target}`);
}

/** Localized summary built from the structured record (the server summary stays English for the API). */
function summarize(record, lab) {
  const checks = record.checks ?? [];
  const failed = checks.filter((c) => !c.passed).length;
  switch (record.status) {
    case "passed":
      return checks.length ? t("summary.passed", { count: checks.length }) : t("summary.passedNoChecks");
    case "cancelled":
      return t("summary.cancelled");
    case "timedOut":
      return record.failureStage === "input"
        ? t("summary.inputTimedOut", { seconds: lab?.inputIdleTimeoutSeconds ?? "?" })
        : t("summary.timedOut", { seconds: lab?.timeoutSeconds ?? "?" });
    default:
      if (record.failureStage === "build") return t("summary.build", { count: record.errors.length });
      if (record.failureStage === "run") {
        const reason = record.errors[0];
        return reason ? `${t("summary.run", { code: record.exitCode })} — ${reason}` : t("summary.run", { code: record.exitCode });
      }
      if (record.failureStage === "checks") return t("summary.checks", { failed, count: checks.length });
      if (record.failureStage === "ready" || record.failureStage === "companion") {
        const reason = record.errors[0];
        const text = t(record.failureStage === "ready" ? "summary.notReady" : "summary.companion");
        return reason ? `${text} — ${reason}` : text;
      }
      return t("summary.dashboard");
  }
}

// ---------- chrome: server status, language ----------
function setServer(kind) {
  const dot = document.getElementById("server-dot");
  const label = document.getElementById("server-state");
  dot.className = `status-dot ${kind === "connected" ? "ok" : kind === "connecting" ? "" : "ko"}`;
  label.dataset.i18n = `server.${kind}`;
  label.textContent = t(`server.${kind}`);
}

function wireChrome() {
  document.querySelectorAll("#lang-switch button").forEach((button) => button.addEventListener("click", () => {
    if (button.dataset.lang === language()) return;
    storage.set("labbench.lang", button.dataset.lang);
    applyLanguage(button.dataset.lang);
  }));
}

async function applyLanguage(lang) {
  setLanguage(lang);
  document.title = t("app.title");
  document.querySelectorAll("#lang-switch button").forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.lang === lang)));
  translateTree(document);
  renderConfigChip();
  renderReporting();
  if (document.getElementById("settings").open) renderSettings();
  renderRail();
  if (!state.view || !state.details) return;

  // Refresh the README for the new language, keep the terminal (it is program output).
  state.details = await api.get(`/labs/${encodeURIComponent(state.currentId)}?lang=${language()}`);
  renderLabTexts();
  renderReadme();
  renderHistory();
  renderAbout();
  renderReadouts();
  renderTerminalTitle();
  if (state.record) renderResults(state.record);
  renderInput();
}

// ---------- rail ----------
function renderRail() {
  const list = document.getElementById("lab-list");
  document.getElementById("lab-count").textContent = String(state.labs.length);
  list.replaceChildren(...state.labs.map((lab) => {
    const item = el("button", "lab-item");
    item.type = "button";
    item.setAttribute("aria-current", String(lab.id === state.currentId));
    item.setAttribute("aria-label", t("rail.select", { title: labText(lab, "title") }));
    item.append(el("span", "lab-item-num", lab.number), el("span", "lab-item-title", labText(lab, "title")));

    const meta = el("span", "lab-item-meta");
    meta.append(lab.activeRunId ? pill("running", t("status.running")) : pill(lab.progress, t(`progress.${lab.progress}`)));
    const last = latestRun(lab);
    if (last) meta.append(el("span", "num", fmtDuration(last.durationMs)));
    item.append(meta);

    item.addEventListener("click", () => selectLab(lab.id));
    return item;
  }));
}

function latestRun(summary) {
  return [summary.lastStartRun, summary.lastSolutionRun].filter(Boolean)
    .sort((a, b) => new Date(b.startedAt) - new Date(a.startedAt))[0];
}

async function refreshLabs() {
  state.labs = await api.get("/labs");
  renderRail();
}

// ---------- lab view ----------
async function selectLab(id) {
  closeStream();
  state.currentId = id;
  state.record = null;
  renderRail();

  state.details = await api.get(`/labs/${encodeURIComponent(id)}?lang=${language()}`);
  const main = document.getElementById("lab-view");
  main.replaceChildren(document.getElementById("lab-template").content.cloneNode(true));
  state.view = main;
  translateTree(main);
  $(main, "terminal").dataset.empty = t("term.idle");

  renderLabTexts();
  renderReadouts();
  renderAbout();
  renderHistory();
  renderReadme();
  wireTabs(main);
  wireRunControls(main);
  wireInput(main);
  resetInput();
  wireSolution(main);
  main.querySelector('[data-action="open-settings"]').addEventListener("click", () => openSettings("azure"));
  main.querySelector('[data-action="help"]').addEventListener("click", openHelp);
  renderHelpButtons();

  const lab = state.details.lab;
  if (lab.activeRunId) {
    attachToRun(lab.activeRunId, state.details.lastRun?.target ?? "start");
  } else if (state.details.lastRun) {
    showRecordedRun(state.details.lastRun);
  }
}

function renderLabTexts() {
  const main = state.view;
  const lab = state.details.lab;
  $(main, "kicker").textContent = t("lab.kicker", { number: lab.number });
  $(main, "track").textContent = labText(lab, "track");
  $(main, "level").textContent = labText(lab, "level");
  $(main, "title").textContent = labText(lab, "title");
  $(main, "summary").textContent = labText(lab, "summary");
  $(main, "terminal").dataset.empty = t("term.idle");
}

function renderReadouts() {
  const main = state.view;
  const { lab, lastRun } = state.details;
  $(main, "progress").replaceChildren(pill(lab.progress, t(`progress.${lab.progress}`)));
  $(main, "lastStatus").replaceChildren(lastRun
    ? pill(lastRun.status, `${t(`status.${lastRun.status}`)} · ${t(`target.${lastRun.target}Short`)}`)
    : pill("none", t("status.none")));

  const duration = $(main, "lastDuration");
  duration.textContent = lastRun ? fmtDuration(lastRun.durationMs) : "—";

  const tokens = $(main, "lastTokens");
  const total = lastRun?.tokenUsage?.total;
  tokens.textContent = total != null ? fmtNumber(total) : t("metrics.notAvailable");
  tokens.classList.toggle("muted", total == null);
}

function renderAbout() {
  const main = state.view;
  const details = state.details;
  const objectives = details.lab.translations?.[language()]?.objectives ?? details.objectives;
  $(main, "objectives").replaceChildren(...objectives.map((o) => el("li", null, o)));
  $(main, "packages").replaceChildren(...details.packages.map((p) => el("li", null, p)));
  $(main, "checkList").replaceChildren(...details.checks.map((c) => el("li", null, checkText(details.lab, c.id, c.description))));
  $(main, "commandStart").textContent = `${t("about.cmdStart")}\n${details.commands.start}`;
  $(main, "commandSolution").textContent = `${t("about.cmdSolution")}\n${details.commands.solution}`;
  $(main, "folder").textContent = details.folder;
  const note = $(main, "interactiveNote");
  const companion = details.lab.companion;
  note.hidden = !details.lab.interactive && !companion;
  note.textContent = companion
    ? t(companion.role === "client" ? "about.companionClient" : "about.companionServer", { project: companion.project })
    : t("about.interactive", { timeout: details.lab.timeoutSeconds, idle: details.lab.inputIdleTimeoutSeconds });
}

function renderHistory() {
  const main = state.view;
  const runs = state.details.history;
  $(main, "historyCount").textContent = String(runs.length);
  const body = $(main, "history");
  if (!runs.length) {
    const row = el("tr");
    const cell = el("td", "empty", t("history.empty"));
    cell.colSpan = 7;
    row.append(cell);
    body.replaceChildren(row);
    return;
  }

  body.replaceChildren(...runs.map((run, index) => {
    const row = el("tr");
    const status = el("td");
    status.append(pill(run.status, t(`status.${run.status}`)));
    row.append(
      el("td", "num", `#${runs.length - index}`),
      el("td", null, targetLabel(run.target)),
      status,
      el("td", null, fmtDate(run.startedAt)),
      el("td", "r", fmtDuration(run.durationMs)),
      el("td", "r", run.tokenUsage?.total != null ? fmtNumber(run.tokenUsage.total) : "—"),
      el("td", "summary", summarize(run, state.details.lab)));
    return row;
  }));
}

function renderReadme() {
  const main = state.view;
  const { readmeHtml, readmeLanguage } = state.details;
  const notice = $(main, "readmeNotice");
  notice.hidden = !(language() !== "en" && readmeLanguage === "en" && readmeHtml);
  notice.textContent = t("readme.englishOnly");

  const article = $(main, "readme");
  // Markdown is rendered server-side with raw HTML disabled; also neutralise script URLs.
  article.innerHTML = readmeHtml || `<p>${t("readme.missing")}</p>`;
  article.querySelectorAll("a[href]").forEach((a) => {
    const href = a.getAttribute("href");
    if (/^\s*javascript:/i.test(href)) a.removeAttribute("href");
    else if (/^https?:/i.test(href)) { a.target = "_blank"; a.rel = "noopener noreferrer"; }
  });
}

function wireTabs(main) {
  const tabs = main.querySelectorAll('[role="tab"]');
  tabs.forEach((tab) => tab.addEventListener("click", () => {
    tabs.forEach((other) => other.setAttribute("aria-selected", String(other === tab)));
    main.querySelectorAll(".panel").forEach((panel) => { panel.hidden = panel.dataset.panel !== tab.dataset.tab; });
  }));
}

function wireSolution(main) {
  main.querySelector('[data-action="reveal"]').addEventListener("click", async () => {
    const files = await api.get(`/labs/${encodeURIComponent(state.currentId)}/solution`);
    const container = $(main, "solution");
    container.replaceChildren(...files.map((file) => {
      const block = el("section", "fig source-file");
      const bar = el("div", "fig-bar");
      bar.append(el("span", null, `Solution/${file.path}`));
      block.append(bar, el("pre", null, file.content));
      return block;
    }));
    $(main, "gate").hidden = true;
    container.hidden = false;
  });
}

// ---------- running ----------
function setTarget(target) {
  state.target = target;
  state.view.querySelectorAll("[data-target]").forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.target === target)));
}

function wireRunControls(main) {
  main.querySelectorAll("[data-target]").forEach((button) => button.addEventListener("click", () => setTarget(button.dataset.target)));
  setTarget(state.target);

  main.querySelector('[data-action="run"]').addEventListener("click", async () => {
    const target = state.target;
    const { ok, status, payload } = await api.post(`/labs/${encodeURIComponent(state.currentId)}/runs`, { target });
    if (!ok) {
      toast(status === 409 ? t("toast.busy") : t("toast.startError", { status }));
      return;
    }
    toast(t("toast.started", { target: targetLabel(target) }));
    attachToRun(payload.runId, target);
  });

  main.querySelector('[data-action="cancel"]').addEventListener("click", async (event) => {
    if (!state.runId) return;
    event.currentTarget.disabled = true;
    event.currentTarget.querySelector("span").textContent = t("run.cancelling");
    await api.post(`/runs/${state.runId}/cancel`);
  });

  main.querySelector('[data-action="copy"]').addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText($(main, "terminal").innerText);
      toast(t("toast.copied"));
    } catch { /* clipboard not available */ }
  });
}

function setRunning(running) {
  const main = state.view;
  main.querySelector('[data-action="run"]').disabled = running;
  const cancel = main.querySelector('[data-action="cancel"]');
  cancel.hidden = !running;
  cancel.disabled = false;
  cancel.querySelector("span").textContent = t("run.cancel");
  main.querySelectorAll("[data-target]").forEach((b) => { b.disabled = running; });
}

function resetRunView() {
  const main = state.view;
  $(main, "terminal").replaceChildren();
  $(main, "results").hidden = true;
  $(main, "progressLine").textContent = "";
  $(main, "clock").textContent = "00:00";
  main.querySelectorAll(".step").forEach((step) => { step.className = "step"; step.querySelector(".st").textContent = ""; });
  state.currentLines = {};
  state.phase = null;
  state.phaseStartedAt = {};
  resetInput();
}

function renderTerminalTitle() {
  const title = $(state.view, "terminalTitle");
  if (state.runId && !state.record) title.textContent = t("term.running", { target: targetLabel(state.runTarget) });
  else if (state.record && state.recordIsHistory) title.textContent = t("term.last", { target: targetLabel(state.record.target), date: fmtDate(state.record.startedAt) });
  else if (state.record) title.textContent = t("term.finished", { target: targetLabel(state.record.target) });
  else title.textContent = t("term.output");
}

function attachToRun(runId, target) {
  closeStream();
  state.runId = runId;
  state.runTarget = target;
  state.record = null;
  setTarget(target);
  setRunning(true);

  const source = new EventSource(`/api/runs/${runId}/events`);
  state.eventSource = source;

  // The server replays the whole run on every (re)connection: start from a clean view each time.
  source.onopen = () => {
    resetRunView();
    renderTerminalTitle();
    startClock();
    setServer("connected");
  };

  source.addEventListener("phase", (e) => onPhase(JSON.parse(e.data)));
  source.addEventListener("output", (e) => {
    const event = JSON.parse(e.data);
    // New output means the program moved on: the last progress frame (spinner) is no longer current.
    if (event.stream === "stdout" && event.text) $(state.view, "progressLine").textContent = "";
    appendText(event.stream, event.text ?? "", event.endOfLine);
  });
  source.addEventListener("progress", (e) => { $(state.view, "progressLine").textContent = JSON.parse(e.data).text; });
  source.addEventListener("input-request", (e) => onInputRequest(JSON.parse(e.data).inputId));
  source.addEventListener("input-sent", (e) => { state.input.pending.delete(JSON.parse(e.data).inputId); renderInput(); });
  source.addEventListener("input-closed", () => { state.input.closed = true; state.input.pending.clear(); renderInput(); });
  source.addEventListener("result", async (e) => {
    closeStream();
    const record = JSON.parse(e.data).result;
    state.runId = null;
    finishRun(record);
    await refreshLabs();
    state.details = await api.get(`/labs/${encodeURIComponent(record.labId)}?lang=${language()}`);
    renderReadouts();
    renderHistory();
  });
  source.onerror = () => {
    if (source.readyState === EventSource.CLOSED) setServer("disconnected");
  };
}

function closeStream() {
  state.eventSource?.close();
  state.eventSource = null;
  clearInterval(state.clockTimer);
}

function startClock() {
  const started = performance.now();
  clearInterval(state.clockTimer);
  state.clockTimer = setInterval(() => {
    $(state.view, "clock").textContent = fmtClock((performance.now() - started) / 1000);
  }, 250);
}

function onPhase(event) {
  const step = state.view.querySelector(`.step[data-phase="${event.phase}"]`);
  if (!step) return;
  step.className = `step ${event.state}`;
  if (event.state === "started") {
    state.phase = event.phase;
    state.phaseStartedAt[event.phase] = event.elapsed;
    step.querySelector(".st").textContent = "…";
    if (event.phase === "run") { state.input.open = !!state.details.lab.interactive; renderInput(); }
  } else {
    const start = state.phaseStartedAt[event.phase] ?? event.elapsed;
    step.querySelector(".st").textContent = fmtDuration(Math.round((event.elapsed - start) * 1000));
    if (event.phase === "run") {
      $(state.view, "progressLine").textContent = "";
      state.input.open = false;
      state.input.pending.clear();
      renderInput();
    }
  }
}

function appendText(stream, text, endOfLine) {
  const terminal = $(state.view, "terminal");
  const stick = terminal.scrollHeight - terminal.scrollTop - terminal.clientHeight < 40;

  // The learner's input continues the prompt the program left open, then the line ends (Enter), as in a terminal.
  if (stream === "stdin") {
    const prompt = state.currentLines.stdout;
    const echo = el("span", prompt ? "term-echo" : "term-line stdin", text);
    if (prompt) prompt.append(echo);
    else terminal.append(echo);
    state.currentLines.stdout = null;
    if (stick) terminal.scrollTop = terminal.scrollHeight;
    return;
  }

  let line = state.currentLines[stream];
  if (!line) {
    const kind = stream === "stdout" && state.phase === "build" ? "build" : stream;
    line = el("span", `term-line ${kind}`);
    terminal.append(line);
    state.currentLines[stream] = line;
  }
  line.append(text);
  if (endOfLine) state.currentLines[stream] = null;

  if (stick) terminal.scrollTop = terminal.scrollHeight;
}

const appendLine = (stream, text) => appendText(stream, text, true);

// ---------- standard input (interactive labs) ----------
function resetInput() {
  state.input = { open: false, closed: false, pending: new Set(), announced: 0 };
  renderInput();
}

function onInputRequest(id) {
  state.input.pending.add(id);
  renderInput();
  // Announce and focus each prompt once (the stream replays past events on reconnection).
  if (id <= state.input.announced) return;
  state.input.announced = id;
  $(state.view, "inputLive").textContent = t("input.waitingLive", { id });
  const field = $(state.view, "inputField");
  const busyElsewhere = document.activeElement instanceof HTMLInputElement && document.activeElement !== field;
  if (!busyElsewhere && !$(state.view, "inputForm").closest(".panel").hidden) field.focus({ preventScroll: true });
}

function renderInput() {
  const main = state.view;
  if (!main || !state.input) return;
  const { open, closed, pending } = state.input;
  const form = $(main, "inputForm");
  form.hidden = !open;
  const waitingFor = pending.size ? Math.min(...pending) : null;
  const badge = $(main, "inputBadge");
  badge.hidden = waitingFor == null;
  badge.textContent = waitingFor == null ? "" : t("input.waiting", { id: waitingFor });
  main.querySelector(".term").classList.toggle("awaiting", open && waitingFor != null);

  $(main, "inputField").disabled = closed;
  $(main, "inputSend").disabled = closed;
  main.querySelector('[data-action="close-input"]').disabled = closed;
  $(main, "inputHint").textContent = closed ? t("input.closed") : t("input.hint");
}

function showInputError(message) {
  const error = $(state.view, "inputError");
  error.hidden = !message;
  error.textContent = message ?? "";
  $(state.view, "inputField").setAttribute("aria-invalid", String(!!message));
}

function inputErrorText({ status, payload }) {
  const code = payload?.errors?.text ?? payload?.error;
  const key = `input.error.${code}`;
  return code && t(key) !== key ? t(key) : t("input.error.other", { status });
}

function wireInput(main) {
  const field = $(main, "inputField");
  field.addEventListener("input", () => showInputError(null));

  $(main, "inputForm").addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!state.runId) return;
    const text = field.value;
    if (/[\r\n]/.test(text)) return showInputError(t("input.error.newline"));
    if (text.length > 4096) return showInputError(t("input.error.tooLong"));

    const response = await api.post(`/runs/${state.runId}/input`, { text });
    if (!response.ok) return showInputError(inputErrorText(response));
    showInputError(null);
    field.value = "";
    field.focus({ preventScroll: true });
  });

  main.querySelector('[data-action="close-input"]').addEventListener("click", async () => {
    if (!state.runId) return;
    const response = await api.post(`/runs/${state.runId}/input/close`);
    if (!response.ok && response.payload?.error !== "closed") showInputError(inputErrorText(response));
  });
}

// ---------- results ----------
function finishRun(record) {
  clearInterval(state.clockTimer);
  resetInput();
  state.record = record;
  state.recordIsHistory = false;
  setRunning(false);
  $(state.view, "progressLine").textContent = "";
  renderTerminalTitle();
  renderResults(record);

  const summary = summarize(record, state.details.lab);
  if (record.status === "passed") toast(t("toast.passed", { summary }));
  else if (record.status === "cancelled") toast(t("toast.cancelled"));
  else if (record.status === "timedOut") toast(t("toast.timedOut"));
  else toast(t("toast.failed", { summary }));
}

function showRecordedRun(record) {
  resetRunView();
  state.record = record;
  state.recordIsHistory = true;
  appendLine("note", `${t("term.lastNote")}:`);
  for (const line of record.log) appendText(line.stream, line.text, !line.prompt);
  setTarget(record.target);
  showRecordedPhases(record);
  $(state.view, "clock").textContent = fmtClock(record.durationMs / 1000);
  renderTerminalTitle();
  renderResults(record);
}

function showRecordedPhases(record) {
  const phases = [
    ["build", record.buildDurationMs, record.failureStage === "build"],
    ["run", record.runDurationMs, ["run", "ready", "companion"].includes(record.failureStage)],
    ["checks", record.checks.length ? 0 : null, record.failureStage === "checks"],
  ];
  for (const [phase, ms, failed] of phases) {
    if (ms == null) continue;
    const step = state.view.querySelector(`.step[data-phase="${phase}"]`);
    step.className = `step ${failed ? "failed" : "completed"}`;
    step.querySelector(".st").textContent = fmtDuration(ms);
  }
}

function renderResults(record) {
  const main = state.view;
  const lab = state.details.lab;
  $(main, "results").hidden = false;

  const verdict = $(main, "verdict");
  verdict.className = `verdict ${record.status}`;
  const title = record.status === "passed" ? t("verdict.passed") : record.status === "failed" ? t("verdict.failed") : t(`status.${record.status}`);
  const meta = el("span", "verdict-meta");
  for (const [key, value] of [
    ["meta.total", fmtDuration(record.durationMs)],
    ["meta.build", record.buildDurationMs != null ? fmtDuration(record.buildDurationMs) : null],
    ["meta.run", record.runDurationMs != null ? fmtDuration(record.runDurationMs) : null],
    ["meta.exit", record.exitCode],
  ]) {
    if (value == null) continue;
    const item = el("span", null, `${t(key)} `);
    item.append(el("b", null, String(value)));
    meta.append(item);
  }
  verdict.replaceChildren(icon(record.status === "passed" ? "check" : "x"), el("strong", null, title), el("span", "verdict-text", summarize(record, lab)), meta);

  const checks = record.checks;
  $(main, "checksCount").textContent = checks.length ? t("checks.passedCount", { passed: checks.filter((c) => c.passed).length, count: checks.length }) : "";
  $(main, "checks").replaceChildren(...(checks.length
    ? checks.map((c) => {
        const item = el("li", c.passed ? "ok" : "ko");
        item.append(icon(c.passed ? "check" : "x"), el("span", null, checkText(lab, c.id, c.description)));
        return item;
      })
    : [el("li", "ko", record.failureStage === "build" ? t("checks.notEvaluatedBuild") : t("checks.notEvaluated"))]));

  renderTokens($(main, "tokens"), record.tokenUsage);

  $(main, "errorsCard").hidden = record.errors.length === 0;
  $(main, "errors").textContent = record.errors.join("\n");
  // The lab stopped because its Azure OpenAI configuration is missing: point to the settings.
  const missingConfig = record.errors.some((e) => /AzureOpenAI:\w+' is not configured/.test(e));
  const fix = $(main, "errorsFix");
  fix.hidden = !missingConfig;
  $(main, "errorsFixBar").hidden = !missingConfig;
  fix.textContent = t("settings.fixLink");

  $(main, "warningsCard").hidden = record.buildWarnings.length === 0;
  $(main, "warningsCount").textContent = String(record.buildWarnings.length);
  $(main, "warnings").replaceChildren(...record.buildWarnings.map((w) => el("li", null, w)));
}

function renderTokens(container, usage) {
  if (!usage) {
    const box = el("div", "na");
    const text = el("div");
    text.append(el("strong", null, t("tokens.notAvailable")), el("p", "hint", t("tokens.notAvailableHint")));
    box.append(text);
    container.replaceChildren(box);
    return;
  }

  const totals = el("div", "token-totals");
  for (const [key, value, cls] of [["tokens.input", usage.input], ["tokens.output", usage.output], ["tokens.total", usage.total, "total"]]) {
    const cell = el("div", cls);
    cell.append(el("span", null, t(key)), el("b", null, fmtNumber(value)));
    totals.append(cell);
  }

  const table = el("table", "table tokens-table");
  const head = el("tr");
  for (const key of ["tokens.report", "tokens.input", "tokens.output", "tokens.reasoning", "tokens.total"]) head.append(el("th", null, t(key)));
  const thead = el("thead");
  thead.append(head);
  const tbody = el("tbody");
  for (const report of usage.reports) {
    const row = el("tr");
    row.append(el("td", null, report.label), el("td", "r", fmtNumber(report.input)), el("td", "r", fmtNumber(report.output)),
      el("td", "r", fmtNumber(report.reasoning)), el("td", "r", fmtNumber(report.total)));
    tbody.append(row);
  }
  table.append(thead, tbody);

  container.replaceChildren(totals, table, el("p", "hint", `${t("tokens.source")} ${t("tokens.cost")}`));
}

// ---------- Settings: Azure OpenAI (shared by the labs; the API key is write-only) and Workshop (reporting identity) ----------
const settingsFields = [
  { key: "endpoint", input: "set-endpoint", source: "src-endpoint", error: "err-endpoint", machine: "machine-endpoint" },
  { key: "chatDeploymentName", input: "set-deployment", source: "src-deployment", error: "err-deployment", machine: "machine-deployment" },
];

/** A key is never sent to the page: only its last 4 characters, shown behind a mask. */
const maskedKey = (hint) => `••••••••${hint ?? ""}`;

async function loadSettings() {
  try {
    state.settings = await api.get("/settings/azure-openai");
  } catch {
    state.settings = null;
  }
  renderConfigChip();
}

function configState(settings) {
  if (!settings) return { kind: "unknown", dot: "" };
  if (!settings.configured) return { kind: "notConfigured", dot: "ko" };
  return settings.authentication === "apiKey" ? { kind: "apiKey", dot: "ok" } : { kind: "entraId", dot: "warn" };
}

function renderConfigChip() {
  const { kind, dot } = configState(state.settings);
  document.getElementById("config-dot").className = `status-dot ${dot}`;
  document.getElementById("config-state").textContent = t(`config.${kind}`);
}

function sourceText(field, status) {
  switch (status.source) {
    case "environment": return t("settings.sourceEnvironment", { variable: status.environmentVariable, value: status.effectiveValue || "—" });
    case "dashboard": return t("settings.sourceDashboard");
    case "labDefault": return t("settings.sourceLabDefault", { value: status.effectiveValue });
    default: return t("settings.sourceMissing");
  }
}

function apiKeySourceText(apiKey) {
  if (apiKey.source === "environment") {
    return apiKey.environmentValueEmpty
      ? t("settings.keyEnvironmentEmpty", { variable: apiKey.environmentVariable })
      : t(apiKey.stored ? "settings.keyEnvironmentOverrides" : "settings.keyEnvironment", { variable: apiKey.environmentVariable });
  }
  return apiKey.source === "dashboard" ? t("settings.keyDashboard") : t("settings.keyMissing");
}

function setFieldError(errorId, inputId, code) {
  const node = document.getElementById(errorId);
  node.hidden = !code;
  node.textContent = code ? t(`settings.error.${code}`) : "";
  document.getElementById(inputId).setAttribute("aria-invalid", String(Boolean(code)));
}

function clearSettingsErrors() {
  for (const field of settingsFields) setFieldError(field.error, field.input, null);
  setFieldError("err-apikey", "set-apikey", null);
  for (const field of reportingFields) setReportingFieldError(field.error, field.input, null);
  document.getElementById("settings-store-error").hidden = true;
  markSettingsTabErrors();
}

const settingsTabs = ["azure", "workshop"];

/** Shows one tab of the settings dialog (roving tabindex), optionally focusing its first field. */
function selectSettingsTab(name, { focusField = false } = {}) {
  for (const tab of settingsTabs) {
    const selected = tab === name;
    const button = document.getElementById(`settings-tab-${tab}`);
    button.setAttribute("aria-selected", String(selected));
    button.tabIndex = selected ? 0 : -1;
    document.getElementById(`settings-panel-${tab}`).hidden = !selected;
  }
  if (focusField) document.querySelector(`#settings-panel-${name} input`)?.focus();
}

/** A red mark on each tab holding a field in error; switches to the first such tab so the error is visible. */
function markSettingsTabErrors({ reveal = false } = {}) {
  let first = null;
  for (const tab of settingsTabs) {
    const failing = !!document.querySelector(`#settings-panel-${tab} [aria-invalid="true"]`);
    document.getElementById(`settings-nav-${tab}-alert`).hidden = !failing;
    if (failing && !first) first = tab;
  }
  if (reveal && first) {
    selectSettingsTab(first);
    document.querySelector(`#settings-panel-${first} [aria-invalid="true"]`)?.focus();
  }
}

/** The one-line state under each tab name: authentication mode, and reporting state. */
function renderSettingsNav() {
  const { kind, dot } = configState(state.settings);
  document.getElementById("settings-nav-azure-dot").className = `status-dot ${dot}`;
  document.getElementById("settings-nav-azure-state").textContent = t(`config.${kind}`);

  const status = state.reporting;
  const workshop = !status ? { key: "unknown", dot: "" }
    : status.standalone ? { key: "standalone", dot: "" }
    : !status.enabled ? { key: "notJoined", dot: "" }
    : status.state === "connected" ? { key: "connected", dot: "ok" }
    : status.state === "rejected" ? { key: "rejected", dot: "ko" }
    : { key: "pending", dot: "warn" };
  document.getElementById("settings-nav-workshop-dot").className = `status-dot ${workshop.dot}`;
  document.getElementById("settings-nav-workshop-state").textContent = t(`settings.workshopState.${workshop.key}`, { username: status?.username ?? "" });
  document.getElementById("reporting-settings-dot").className = `status-dot ${workshop.dot}`;
}

/** Renders the status; fills the inputs only when asked, so that a language switch keeps what the user typed. */
function renderSettings({ fill = false } = {}) {
  const settings = state.settings;
  if (!settings) return;

  for (const field of settingsFields) {
    const status = settings[field.key];
    // Set on this computer (AzureOpenAI__* variable): shown as the labs see it, read-only, since that value wins over the form.
    const onComputer = status.source === "environment";
    const input = document.getElementById(field.input);
    if (fill) input.value = (onComputer ? status.effectiveValue : status.dashboardValue) ?? "";
    input.readOnly = onComputer;
    document.getElementById(field.machine).hidden = !onComputer;
    const source = document.getElementById(field.source);
    source.className = `field-source ${status.source}`;
    source.textContent = sourceText(field, status);
  }

  const apiKey = settings.apiKey;
  const keyInput = document.getElementById("set-apikey");
  const keyOnComputer = apiKey.source === "environment";
  if (fill) keyInput.value = "";
  keyInput.readOnly = keyOnComputer;
  keyInput.placeholder = keyOnComputer
    ? (apiKey.environmentValueEmpty ? t("settings.apiKeyOnComputerEmpty") : maskedKey(apiKey.hint))
    : apiKey.stored ? t("settings.apiKeyKeep", { masked: maskedKey(apiKey.hint) }) : t("settings.apiKeyOptional");
  document.getElementById("machine-apikey").hidden = !keyOnComputer;
  document.getElementById("hint-apikey").hidden = keyOnComputer;
  document.getElementById("apikey-stored").hidden = !apiKey.stored || keyOnComputer;
  document.querySelector('[data-action="remove-key"]').hidden = !apiKey.stored || keyOnComputer;
  const keySource = document.getElementById("src-apikey");
  keySource.className = `field-source ${apiKey.source}`;
  keySource.textContent = apiKeySourceText(apiKey);

  showStoreError(settings.storeError);
  renderReportingSettings({ fill });
  renderSettingsNav();
}

function showStoreError(code) {
  const node = document.getElementById("settings-store-error");
  node.hidden = !code;
  node.textContent = code ? t(`settings.store.${code}`, { file: state.settings?.secretsFile ?? "" }) : "";
}

/** Opens the settings on a tab: "azure" (the labs' Azure OpenAI values) or "workshop" (username and workshop key). */
async function openSettings(tab = "azure") {
  await Promise.all([loadSettings(), loadReportingStatus()]);
  clearSettingsErrors();
  renderSettings({ fill: true });
  selectSettingsTab(settingsTabs.includes(tab) ? tab : "azure");
  document.getElementById("settings").showModal();
  selectSettingsTab(settingsTabs.includes(tab) ? tab : "azure", { focusField: true });
}

function closeSettings() {
  // Never keep the keys in the page once the dialog is closed.
  document.getElementById("set-apikey").value = "";
  document.getElementById("rep-key").value = "";
  document.getElementById("settings").close();
}

function applySettingsResponse({ ok, status, payload }) {
  if (ok) {
    state.settings = payload;
    renderConfigChip();
    return true;
  }

  if (status === 400 && payload.errors) {
    const ids = { endpoint: ["err-endpoint", "set-endpoint"], chatDeploymentName: ["err-deployment", "set-deployment"], apiKey: ["err-apikey", "set-apikey"] };
    for (const [field, code] of Object.entries(payload.errors)) {
      if (ids[field]) setFieldError(ids[field][0], ids[field][1], `${field}.${code}`);
    }
  } else if (payload.error) {
    showStoreError(payload.error);
  } else {
    toast(t("settings.saveError", { status }));
  }
  return false;
}

function wireSettings() {
  const dialog = document.getElementById("settings");
  document.getElementById("config-chip").addEventListener("click", () => openSettings("azure"));
  const reportingChip = document.getElementById("reporting-chip");
  reportingChip.addEventListener("click", () => openSettings("workshop"));
  reportingChip.addEventListener("keydown", (event) => {
    if (event.key === "Enter" || event.key === " ") { event.preventDefault(); openSettings("workshop"); }
  });

  const nav = dialog.querySelector(".settings-nav");
  nav.addEventListener("click", (event) => {
    const tab = event.target.closest("[data-settings-tab]");
    if (tab) selectSettingsTab(tab.dataset.settingsTab);
  });
  nav.addEventListener("keydown", (event) => {
    const current = settingsTabs.indexOf(document.activeElement?.dataset?.settingsTab);
    if (current < 0) return;
    const moves = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 };
    let next = null;
    if (event.key in moves) next = (current + moves[event.key] + settingsTabs.length) % settingsTabs.length;
    else if (event.key === "Home") next = 0;
    else if (event.key === "End") next = settingsTabs.length - 1;
    if (next === null) return;
    event.preventDefault();
    selectSettingsTab(settingsTabs[next]);
    document.getElementById(`settings-tab-${settingsTabs[next]}`).focus();
  });
  dialog.querySelectorAll('[data-action="close-settings"]').forEach((b) => b.addEventListener("click", closeSettings));
  dialog.addEventListener("cancel", () => { document.getElementById("set-apikey").value = ""; document.getElementById("rep-key").value = ""; });

  document.getElementById("settings-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    clearSettingsErrors();
    const submit = event.submitter ?? dialog.querySelector('[type="submit"]');
    submit.disabled = true;
    try {
      // A read-only field shows a value set on this computer: send back what was saved here, never copy the computer's value.
      const formValue = (id, key) => {
        const input = document.getElementById(id);
        return input.readOnly ? (state.settings?.[key]?.dashboardValue ?? "") : input.value;
      };
      const keyInput = document.getElementById("set-apikey");
      const response = await api.send("PUT", "/settings/azure-openai", {
        endpoint: formValue("set-endpoint", "endpoint"),
        chatDeploymentName: formValue("set-deployment", "chatDeploymentName"),
        apiKey: keyInput.readOnly ? "" : keyInput.value,
      });
      const azureOk = applySettingsResponse(response);
      const reportingOk = await submitReportingSettings();
      if (azureOk && reportingOk) {
        renderSettings({ fill: true });
        toast(t("settings.saved"));
      } else {
        markSettingsTabErrors({ reveal: true });
        renderSettingsNav();
      }
    } finally {
      submit.disabled = false;
    }
  });

  dialog.querySelector('[data-action="remove-key"]').addEventListener("click", async () => {
    if (!confirm(t("settings.removeKeyConfirm"))) return;
    clearSettingsErrors();
    if (applySettingsResponse(await api.send("DELETE", "/settings/azure-openai/api-key"))) {
      renderSettings();
      toast(t("settings.keyRemoved"));
    }
  });
}

// ---------- boot ----------
(async function boot() {
  setLanguage(document.documentElement.lang);
  document.querySelectorAll("#lang-switch button").forEach((b) => b.setAttribute("aria-pressed", String(b.dataset.lang === language())));
  document.title = t("app.title");
  translateTree(document);
  storage.remove("labbench.theme"); // the dashboard has a single (dark) theme
  wireChrome();
  wireSettings();
  wireReporting();
  setServer("connecting");

  try {
    await loadSettings();
    await refreshLabs();
    setServer("connected");
    await loadReportingStatus();
    if (state.reporting?.needsIdentity) openWelcome();
    if (state.labs.length) await selectLab(state.labs[0].id);
  } catch (error) {
    setServer("unreachable");
    document.getElementById("lab-view").replaceChildren(el("p", "placeholder", t("boot.error", { message: error.message })));
  }
})();

// ---------- Reporting to the trainer's admin dashboard (identity, status, help) ----------
const reportingFields = [
  { key: "username", input: "rep-username", error: "err-rep-username" },
  { key: "workshopKey", input: "rep-key", error: "err-rep-key" },
];

const helpHelpers = {
  relative(iso) {
    const minutes = Math.round((Date.now() - new Date(iso)) / 60000);
    if (minutes < 1) return language() === "fr" ? "à l'instant" : "just now";
    const rtf = new Intl.RelativeTimeFormat(locale(), { numeric: "auto" });
    return minutes < 60 ? rtf.format(-minutes, "minute") : rtf.format(-Math.round(minutes / 60), "hour");
  },
  labTitle(labId) {
    const lab = state.labs.find((l) => l.id === labId);
    return lab ? `${t("lab.kicker", { number: lab.number })} · ${labText(lab, "title")}` : labId;
  },
};

async function loadReportingStatus() {
  try {
    state.reporting = await api.get("/reporting/status");
  } catch {
    state.reporting = null;
  }
  renderReporting();
  scheduleReportingPoll();
}

/** The status (connection, outbox, help request) is local and cheap: poll it while reporting is on, faster while a help request is active. */
function scheduleReportingPoll() {
  clearTimeout(state.reportingTimer);
  if (!state.reporting?.enabled) return;
  const activeHelp = state.reporting.help && !["resolved", "cancelled"].includes(state.reporting.help.status);
  const delay = state.reporting.state === "registering" ? 2000 : activeHelp ? 5000 : 10000;
  state.reportingTimer = setTimeout(loadReportingStatus, delay);
}

function reportingErrorText(code) {
  const key = `reporting.error.${code}`;
  return code && t(key) !== key ? t(key) : (code ?? "");
}

function renderReporting() {
  const status = state.reporting;
  const chip = document.getElementById("reporting-chip");
  const enabled = !!status?.enabled;
  chip.hidden = !enabled;
  if (enabled) {
    const kind = status.state;
    chip.className = `status-chip reporting-chip ${kind}`;
    document.getElementById("reporting-dot").className = `status-dot ${kind === "connected" ? "ok" : kind === "rejected" ? "ko" : "warn"}`;
    document.getElementById("reporting-state").textContent = t(`reporting.${kind}`);
    const params = { host: status.serverHost, username: status.username ?? "—", count: status.outboxCount, reason: reportingErrorText(status.lastError) };
    chip.title = kind === "rejected" ? t("reporting.chipTitleRejected", params)
      : kind === "connected" ? t("reporting.chipTitle", params)
      : kind === "registering" ? t("reporting.chipTitleRegistering", params)
      : t("reporting.chipTitleOffline", params);
    chip.setAttribute("aria-label", chip.title);
  }
  renderHelpButtons();
  renderHelpBanner();
  if (document.getElementById("settings").open) renderSettingsNav();
}

function renderHelpButtons() {
  const status = state.reporting;
  const show = !!status?.enabled && !status.needsIdentity;
  const active = !!status?.help && ["pending", "open", "acknowledged"].includes(status.help.status);
  for (const button of [document.getElementById("help-button"), state.view?.querySelector('[data-action="help"]')].filter(Boolean)) {
    button.hidden = !show;
    button.classList.toggle("active", active);
    button.querySelector("span").textContent = t(active ? "help.buttonActive" : "help.button");
    button.setAttribute("aria-pressed", String(active));
  }
}

function renderHelpBanner() {
  const help = state.reporting?.help;
  const banner = document.getElementById("help-banner");
  if (!help) { banner.hidden = true; return; }

  // A closed request stays visible for 5 minutes, then goes away by itself.
  if ((help.status === "resolved" || help.status === "cancelled") && help.closedAt && Date.now() - new Date(help.closedAt) > 5 * 60000) {
    banner.hidden = true;
    api.post("/help/dismiss").then(() => loadReportingStatus()).catch(() => {});
    return;
  }

  const lab = help.labId ? t("help.forLab", { lab: helpHelpers.labTitle(help.labId) }) : "";
  const title = document.getElementById("help-banner-title");
  const detail = document.getElementById("help-banner-detail");
  let kind = help.status;
  switch (help.status) {
    case "pending":
      title.textContent = t("help.pendingTitle");
      detail.textContent = t("help.pendingDetail");
      break;
    case "open":
      title.textContent = t("help.openTitle");
      detail.textContent = t("help.openDetail", { time: helpHelpers.relative(help.createdAt), lab });
      break;
    case "acknowledged":
      title.textContent = t("help.acknowledgedTitle");
      detail.textContent = t("help.acknowledgedDetail", { lab });
      break;
    case "resolved":
      title.textContent = t("help.resolvedTitle");
      detail.textContent = help.adminNote ? t("help.resolvedNote", { note: help.adminNote }) : t("help.resolvedDetail");
      break;
    default:
      kind = "cancelled";
      title.textContent = t("help.cancelledTitle");
      detail.textContent = t("help.cancelledDetail");
  }
  banner.className = `help-banner ${kind}`;
  const active = ["pending", "open", "acknowledged"].includes(help.status);
  document.getElementById("help-unblocked").hidden = !active;
  document.getElementById("help-dismiss").hidden = active;
  banner.hidden = false;
}

// --- welcome (first launch): join the workshop, or work on your own ---
function openWelcome() {
  const status = state.reporting;
  const dialog = document.getElementById("welcome");
  // The admin URL comes from the project configuration: shown, never edited. Without it, only "work on my own" is possible.
  document.getElementById("welcome-server").textContent = status.serverUrl ? t("welcome.server", { url: status.serverUrl }) : "";
  document.getElementById("welcome-no-server").hidden = !!status.serverUrl;
  dialog.querySelector('[type="submit"]').hidden = !status.serverUrl;
  document.getElementById("wel-key-field").hidden = status.workshopKeyConfigured;
  if (!dialog.open) dialog.showModal();
  document.getElementById("wel-username").focus();
}

function setReportingFieldError(errorId, inputId, code) {
  const node = document.getElementById(errorId);
  node.hidden = !code;
  node.textContent = code ? t(`reporting.error.${code}`) : "";
  document.getElementById(inputId).setAttribute("aria-invalid", String(Boolean(code)));
}

function showReportingErrors(prefix, errors) {
  const ids = { username: [`err-${prefix}-username`, `${prefix}-username`], workshopKey: [`err-${prefix}-key`, `${prefix}-key`] };
  for (const [field, code] of Object.entries(errors ?? {})) {
    if (ids[field]) setReportingFieldError(ids[field][0], ids[field][1], `${field}.${code}`);
    else if (field === "serverUrl") toast(t("welcome.noServer"));
  }
}

// --- settings section ---
function renderReportingSettings({ fill = false } = {}) {
  const status = state.reporting;
  if (!status) return;
  const summary = document.getElementById("reporting-settings-status");
  summary.textContent = status.standalone ? t("reporting.statusStandalone")
    : !status.enabled ? t("reporting.statusDisabled")
    : t("reporting.statusEnabled", { host: status.serverHost, username: status.username, id: status.userId.slice(0, 8) })
      + (status.state === "rejected" && status.lastError ? ` ${t("reporting.statusProblem", { reason: reportingErrorText(status.lastError) })}` : "");

  if (fill) {
    document.getElementById("rep-username").value = status.username ?? "";
    document.getElementById("rep-key").value = "";
  }

  document.getElementById("rep-key-stored").hidden = !status.workshopKeyConfigured;
  const key = document.getElementById("src-rep-key");
  key.className = `field-source ${status.workshopKeySource}`;
  key.textContent = status.workshopKeySource === "environment" ? t("reporting.keyEnvironment", { variable: "Dashboard__Reporting__WorkshopKey" })
    : status.workshopKeySource === "dashboard" ? t("reporting.keyDashboard")
    : status.workshopKeySource === "appsettings" ? t("reporting.keyAppsettings")
    : t("reporting.keyMissing");
}

/** Sends the reporting fields of the settings dialog when at least one was filled. Returns true when nothing failed. */
async function submitReportingSettings() {
  const values = {
    username: document.getElementById("rep-username").value.trim(),
    workshopKey: document.getElementById("rep-key").value,
  };
  const unchanged = values.username === (state.reporting?.username ?? "") && !values.workshopKey;
  if (unchanged) return true;

  const response = await api.send("PUT", "/reporting/settings", values);
  document.getElementById("rep-key").value = "";
  if (response.ok) {
    state.reporting = response.payload;
    renderReporting();
    scheduleReportingPoll();
    toast(t("reporting.saved"));
    return true;
  }
  if (response.status === 400 && response.payload.errors) showReportingErrors("rep", response.payload.errors);
  else if (response.payload.error) showStoreError(response.payload.error);
  else toast(t("settings.saveError", { status: response.status }));
  return false;
}

// --- help dialog ---
function openHelp() {
  const status = state.reporting;
  if (!status?.enabled) { toast(t("help.error.disabled")); return; }
  if (status.needsIdentity) { openWelcome(); return; }
  if (status.help && ["pending", "open", "acknowledged"].includes(status.help.status)) {
    document.getElementById("help-banner").scrollIntoView({ block: "nearest" });
    return;
  }

  const select = document.getElementById("help-lab");
  select.replaceChildren(
    el("option", null, t("help.noLab")),
    ...state.labs.map((lab) => el("option", null, `${t("lab.kicker", { number: lab.number })} · ${labText(lab, "title")}`)));
  select.options[0].value = "";
  state.labs.forEach((lab, index) => { select.options[index + 1].value = lab.id; });
  select.value = state.currentId ?? "";
  document.getElementById("help-message").value = "";
  setHelpError(null);
  document.getElementById("help-dialog").showModal();
  document.getElementById("help-message").focus();
}

function setHelpError(message) {
  const node = document.getElementById("err-help-message");
  node.hidden = !message;
  node.textContent = message ?? "";
  document.getElementById("help-message").setAttribute("aria-invalid", String(!!message));
}

function wireReporting() {
  document.getElementById("help-button").addEventListener("click", openHelp);
  const helpDialog = document.getElementById("help-dialog");
  helpDialog.querySelectorAll('[data-action="close-help"]').forEach((b) => b.addEventListener("click", () => helpDialog.close()));

  document.getElementById("help-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    if (state.helpBusy) return;
    const message = document.getElementById("help-message").value.trim();
    if (message.length > 500) return setHelpError(t("help.error.message.tooLong"));
    state.helpBusy = true;
    try {
      const response = await api.post("/help", { labId: document.getElementById("help-lab").value || null, message: message || null });
      if (!response.ok) {
        const code = response.payload?.error;
        if (response.status === 400 && response.payload?.errors?.message) return setHelpError(t(`help.error.message.${response.payload.errors.message}`));
        if (code === "active") { helpDialog.close(); await loadReportingStatus(); return; }
        return setHelpError(code && t(`help.error.${code}`) !== `help.error.${code}` ? t(`help.error.${code}`) : t("help.error.other", { status: response.status }));
      }
      helpDialog.close();
      toast(response.payload.status === "pending" ? t("help.queued") : t("help.sent"));
      await loadReportingStatus();
    } finally {
      state.helpBusy = false;
    }
  });

  document.getElementById("help-unblocked").addEventListener("click", async (event) => {
    event.currentTarget.disabled = true;
    try {
      await api.post("/help/cancel");
      toast(t("help.cancelled"));
      await loadReportingStatus();
    } finally {
      event.currentTarget.disabled = false;
    }
  });

  document.getElementById("help-dismiss").addEventListener("click", async () => {
    await api.post("/help/dismiss");
    await loadReportingStatus();
  });

  // Welcome: blocking (Escape does nothing) until the developer joins the workshop or chooses to work alone.
  const welcome = document.getElementById("welcome");
  welcome.addEventListener("cancel", (event) => event.preventDefault());
  document.getElementById("welcome-skip").addEventListener("click", async (event) => {
    event.currentTarget.disabled = true;
    try {
      const response = await api.post("/identity/skip");
      if (response.ok) {
        state.reporting = response.payload;
        welcome.close();
        renderReporting();
        toast(t("welcome.skipped"));
      } else {
        toast(t("settings.saveError", { status: response.status }));
      }
    } finally {
      document.getElementById("welcome-skip").disabled = false;
    }
  });
  document.getElementById("welcome-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    for (const [error, input] of [["err-wel-username", "wel-username"], ["err-wel-key", "wel-key"]]) setReportingFieldError(error, input, null);
    const submit = event.submitter ?? welcome.querySelector('[type="submit"]');
    submit.disabled = true;
    try {
      const response = await api.post("/identity", {
        username: document.getElementById("wel-username").value.trim(),
        workshopKey: document.getElementById("wel-key-field").hidden ? null : document.getElementById("wel-key").value,
      });
      if (response.ok) {
        document.getElementById("wel-key").value = "";
        state.reporting = response.payload;
        welcome.close();
        renderReporting();
        scheduleReportingPoll();
      } else if (response.status === 400 && response.payload.errors) {
        showReportingErrors("wel", response.payload.errors);
      } else {
        toast(t("settings.saveError", { status: response.status }));
      }
    } finally {
      submit.disabled = false;
    }
  });
}
