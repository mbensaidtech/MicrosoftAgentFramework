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
  main.querySelector('[data-action="open-settings"]').addEventListener("click", openSettings);

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

// ---------- Azure OpenAI settings (shared by the labs; the API key is write-only) ----------
const settingsFields = [
  { key: "endpoint", input: "set-endpoint", source: "src-endpoint", error: "err-endpoint" },
  { key: "chatDeploymentName", input: "set-deployment", source: "src-deployment", error: "err-deployment" },
];

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
  document.getElementById("settings-store-error").hidden = true;
}

/** Renders the status; fills the inputs only when asked, so that a language switch keeps what the user typed. */
function renderSettings({ fill = false } = {}) {
  const settings = state.settings;
  if (!settings) return;

  for (const field of settingsFields) {
    const status = settings[field.key];
    if (fill) document.getElementById(field.input).value = status.dashboardValue ?? "";
    const source = document.getElementById(field.source);
    source.className = `field-source ${status.source}`;
    source.textContent = sourceText(field, status);
  }

  const apiKey = settings.apiKey;
  const keyInput = document.getElementById("set-apikey");
  if (fill) keyInput.value = "";
  keyInput.placeholder = apiKey.stored ? t("settings.apiKeyKeep") : t("settings.apiKeyOptional");
  document.getElementById("apikey-stored").hidden = !apiKey.stored;
  document.querySelector('[data-action="remove-key"]').hidden = !apiKey.stored;
  const keySource = document.getElementById("src-apikey");
  keySource.className = `field-source ${apiKey.source}`;
  keySource.textContent = apiKeySourceText(apiKey);

  document.getElementById("settings-file").textContent = settings.secretsFile;
  showStoreError(settings.storeError);
}

function showStoreError(code) {
  const node = document.getElementById("settings-store-error");
  node.hidden = !code;
  node.textContent = code ? t(`settings.store.${code}`, { file: state.settings?.secretsFile ?? "" }) : "";
}

async function openSettings() {
  await loadSettings();
  clearSettingsErrors();
  renderSettings({ fill: true });
  document.getElementById("settings").showModal();
  document.getElementById("set-endpoint").focus();
}

function closeSettings() {
  // Never keep the key in the page once the dialog is closed.
  document.getElementById("set-apikey").value = "";
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
  document.getElementById("config-chip").addEventListener("click", openSettings);
  dialog.querySelectorAll('[data-action="close-settings"]').forEach((b) => b.addEventListener("click", closeSettings));
  dialog.addEventListener("cancel", () => { document.getElementById("set-apikey").value = ""; });

  document.getElementById("settings-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    clearSettingsErrors();
    const submit = event.submitter ?? dialog.querySelector('[type="submit"]');
    submit.disabled = true;
    try {
      const response = await api.send("PUT", "/settings/azure-openai", {
        endpoint: document.getElementById("set-endpoint").value,
        chatDeploymentName: document.getElementById("set-deployment").value,
        apiKey: document.getElementById("set-apikey").value,
      });
      if (applySettingsResponse(response)) {
        renderSettings({ fill: true });
        toast(t("settings.saved"));
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
  setServer("connecting");

  try {
    await loadSettings();
    await refreshLabs();
    setServer("connected");
    if (state.labs.length) await selectLab(state.labs[0].id);
  } catch (error) {
    setServer("unreachable");
    document.getElementById("lab-view").replaceChildren(el("p", "placeholder", t("boot.error", { message: error.message })));
  }
})();
