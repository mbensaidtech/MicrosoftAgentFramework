// Learning journey — progress rings, trajectory, next mission and distinctions, laid out as a column beside the lab
// list (vertical) or as a band above it (horizontal), as the learner chooses. The model comes from GET /api/journey,
// recomputed by the server from the run history: this module only renders it, keeps it fresh and celebrates what
// changed while the page is open (never on load, language switch or reset).
import { t, language, locale } from "./i18n.js";

const SVG_NS = "http://www.w3.org/2000/svg";
const POLL_MS = 20000;
const TOAST_MS = 3400;
const ROMAN = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII"];
const LAYOUT_KEY = "labbench.journeyLayout";
const LAYOUTS = ["vertical", "horizontal"];

// 16×16 stroke icons of the distinctions, in the style of the page sprite.
const BADGE_ICONS = {
  firstSteps: "M4.5 14V2.5M4.5 3.2h7.3l-1.7 2.7 1.7 2.7H4.5",
  firstTry: "M13.6 8a5.6 5.6 0 1 1-2.2-4.4M10.6 8a2.6 2.6 0 1 1-1.5-2.4M8 8l5.3-5.3M11.4 2.7h1.9v1.9",
  solo: "M9.2 1.8L3.8 9.1h4.1l-1.1 5.1 5.4-7.3H8.1z",
  persistence: "M2 13.6h3.2v-3h3.2v-3h3.2v-3H14M11.6 2.4l2.4 2.2-2.4 2.2",
  tierCleared: "M8 2.2l5.4 3.1L8 8.4 2.6 5.3zM2.6 8.2L8 11.3l5.4-3.1M2.6 11L8 14.1l5.4-3.1",
  summit: "M1.6 13.8l4.6-7.3 2.6 4 1.7-2.5 3.9 5.8zM6.2 6.5V1.9h3.3l-1.1 1.1 1.1 1.1H6.2",
};

const journey = {
  body: null, model: null, json: null, labs: [], currentId: null, onOpen: null, onChange: null,
  requested: 0, applied: 0, unavailable: false, timer: null,
  feed: null, queue: [], toastBusy: false, badgeDetail: null, rulesOpen: false,
};

const done = (lab) => lab.state === "completed" || lab.state === "toResume";
const reducedMotion = () => matchMedia("(prefers-reduced-motion: reduce)").matches;
const fmt = (n) => n.toLocaleString(locale());

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function svg(tag, attributes = {}) {
  const node = document.createElementNS(SVG_NS, tag);
  for (const [name, value] of Object.entries(attributes)) node.setAttribute(name, value);
  return node;
}

/** An icon of the page sprite (#i-<name>). */
function spriteIcon(name, extra = "") {
  const node = svg("svg", { class: `ico ${extra}`.trim(), "aria-hidden": "true" });
  node.append(svg("use", { href: `#i-${name}` }));
  return node;
}

/** A 16×16 stroke icon from a path. */
function pathIcon(d, className) {
  const icon = svg("svg", { class: className, viewBox: "0 0 16 16", "aria-hidden": "true" });
  icon.append(svg("path", { d }));
  return icon;
}

// ---------- texts ----------
function catalogLab(id) {
  return journey.labs.find((lab) => lab.id === id);
}

function labTitle(id) {
  const lab = catalogLab(id);
  if (!lab) return id;
  return `${t("lab.kicker", { number: lab.number })} · ${lab.translations?.[language()]?.title ?? lab.title}`;
}

/** Level names come from the lab translations of the catalog. */
function levelText(milestone) {
  return catalogLab(milestone.labIds[0])?.translations?.[language()]?.level ?? milestone.level;
}

const sameTrack = (a, b) => a.trim().toLowerCase() === b.trim().toLowerCase();

/** Track names (Azure OpenAI, Multi-Agent System...) come from the lab translations of the catalog. */
function trackText(track) {
  return journey.labs.find((lab) => sameTrack(lab.track, track))?.translations?.[language()]?.track ?? track;
}

/** A level exists in several tracks: named with its track wherever the track heading is not next to it. */
const milestoneName = (milestone) => `${trackText(milestone.track)} · ${levelText(milestone)}`;

const checksText = (lab) => t("journey.state.checks", { passed: lab.bestChecks, total: lab.totalChecks });

function stateText(lab) {
  let text = lab.state === "inProgress" && lab.totalChecks && lab.bestChecks
    ? `${t("journey.state.inProgress")} · ${checksText(lab)}`
    : t(`journey.state.${lab.state}`);
  if (lab.bonusCompleted) text += ` · ${t("journey.state.bonus")}`;
  if (done(lab)) text += ` · ${t("journey.xp", { xp: fmt(lab.xp) })}`;
  if (lab.running) text += ` · ${t("journey.state.running")}`;
  return text;
}

const rankName = (rank) => t(`journey.rank.${rank.id}`);
const romanOf = (index) => ROMAN[index - 1] ?? String(index);
const badgeName = (id) => t(`journey.badge.${id}`);
const badgeHint = (id) => t(`journey.badge.${id}.hint`);
const fmtDay = (iso) => new Date(iso).toLocaleDateString(locale(), { day: "numeric", month: "short" });

/** Progress toward the next rank, from 0 to 1 (1 at the highest rank). */
function rankProgress(model) {
  const { rank } = model;
  if (!rank.next) return 1;
  return Math.min(1, Math.max(0, (model.xp - rank.minXp) / (rank.next.minXp - rank.minXp)));
}

// ---------- layout: vertical column or horizontal band ----------
function savedLayout() {
  try {
    const value = localStorage.getItem(LAYOUT_KEY);
    return LAYOUTS.includes(value) ? value : LAYOUTS[0];
  } catch {
    return LAYOUTS[0]; // storage unavailable: the default column
  }
}

/** The page grid reads the layout from <html data-journey-layout>, also set before the first paint by index.html. */
function applyLayout(layout, { save = false } = {}) {
  document.documentElement.dataset.journeyLayout = layout;
  for (const button of document.querySelectorAll("#journey-layout [data-layout]")) {
    button.setAttribute("aria-pressed", String(button.dataset.layout === layout));
  }
  if (save) {
    try { localStorage.setItem(LAYOUT_KEY, layout); } catch { /* per-viewer convenience only */ }
  }
}

// ---------- public API ----------
/** Wires the section: layout switch, lab links, distinction details, refresh when the page becomes visible and every 20 s. */
export function initJourney({ onOpen, onChange }) {
  journey.body = document.getElementById("journey-body");
  journey.onOpen = onOpen;
  journey.onChange = onChange;
  // Outside the redrawn body, so that a redraw never replays the achievement on screen.
  journey.feed = el("div", "jr-feed");
  journey.feed.setAttribute("aria-hidden", "true"); // announced by the live region instead
  document.getElementById("journey").append(journey.feed);

  applyLayout(savedLayout());
  document.getElementById("journey-layout")?.addEventListener("click", (event) => {
    const button = event.target.closest("[data-layout]");
    if (button) applyLayout(button.dataset.layout, { save: true });
  });

  journey.body.addEventListener("click", (event) => {
    const target = event.target.closest("[data-journey-open]");
    if (target) openLab(target.dataset.journeyOpen);
  });
  const showBadge = (event) => {
    const badge = event.target.closest?.("[data-badge]");
    if (badge) renderBadgeDetail(badge.dataset.badge);
  };
  journey.body.addEventListener("pointerover", showBadge);
  journey.body.addEventListener("focusin", showBadge);

  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") refreshJourney({ notify: true });
  });
  schedulePoll();
  render();
}

/**
 * Fetches the journey and renders it. Changes since the last view are celebrated; with notify, a change also
 * calls onChange (a run started, ended or was reset from another tab: the lab list is stale too).
 */
export async function refreshJourney({ notify = false } = {}) {
  if (!journey.body) return;
  const request = ++journey.requested;
  let json;
  try {
    const response = await fetch("/api/journey");
    if (!response.ok) throw new Error(`${response.status}`);
    json = await response.text();
  } catch {
    // Keep the last known view; say so only when there is nothing to show.
    if (!journey.model && request === journey.requested) {
      journey.unavailable = true;
      render();
    }
    return;
  }

  // An older response arriving after a newer one is dropped; an unchanged journey is not redrawn.
  if (request < journey.applied) return;
  journey.applied = request;
  if (json === journey.json) return;

  const previous = journey.model;
  journey.json = json;
  journey.model = JSON.parse(json);
  journey.unavailable = false;
  render({ previous });
  if (previous) celebrate(changes(previous, journey.model));
  if (notify && previous && signature(previous) !== signature(journey.model)) journey.onChange?.();
}

/** New context (catalog texts, open lab, language): redrawn from the last journey, without a request. */
export function renderJourney({ labs, currentId }) {
  journey.labs = labs;
  journey.currentId = currentId;
  render();
}

function schedulePoll() {
  clearTimeout(journey.timer);
  journey.timer = setTimeout(async () => {
    if (document.visibilityState === "visible") await refreshJourney({ notify: true });
    schedulePoll();
  }, POLL_MS);
}

const signature = (model) => model.labs.map((lab) => `${lab.id}:${lab.state}:${lab.running}:${lab.attempts}`).join("|");

function openLab(id) {
  // The open lab is already shown: bring it into view (the journey sits above it in the horizontal layout).
  if (id === journey.currentId) {
    document.getElementById("lab-view")?.scrollIntoView({ block: "start", behavior: reducedMotion() ? "auto" : "smooth" });
    return;
  }
  journey.onOpen?.(id);
}

// ---------- rendering ----------
function render({ previous = null } = {}) {
  const body = journey.body;
  if (!body) return;
  const model = journey.model;
  document.getElementById("journey-count").textContent = model ? `${model.completed}/${model.total}` : "";

  if (!model) {
    const line = el("p", "jr-placeholder");
    if (journey.unavailable) line.textContent = t("journey.unavailable");
    else line.append(el("span", "spinner"), el("span", null, t("journey.loading")));
    body.replaceChildren(line);
    return;
  }

  // Keep the keyboard focus on the same element across redraws.
  const focusKey = body.contains(document.activeElement) ? document.activeElement.dataset.focusKey : null;
  body.replaceChildren(renderSummary(model, previous), renderPath(model), renderMission(model), renderBadges(model));
  if (focusKey) body.querySelector(`[data-focus-key="${CSS.escape(focusKey)}"]`)?.focus({ preventScroll: true });
}

function tile(kind, title) {
  const section = el("section", `jr-tile jr-${kind}`);
  if (title) section.append(el("h3", "jr-tile-title", title));
  return section;
}

/** Summary: two concentric rings (exercises completed, XP toward the next rank) and their figures. */
function renderSummary(model, previous) {
  const section = tile("summary");
  const { rank } = model;
  const metrics = el("div", "jr-metrics");

  const exercises = el("div", "jr-metric ok");
  const exercisesValue = el("b", "jr-metric-value", String(model.completed));
  exercisesValue.append(el("small", null, `/${model.total}`));
  exercises.append(el("span", "jr-metric-label", `${t("journey.exercises")} · ${model.percent} %`), exercisesValue);

  const xp = el("div", "jr-metric acc");
  const xpNumber = el("span", "jr-metric-num", fmt(model.xp));
  const xpValue = el("b", "jr-metric-value");
  xpValue.append(xpNumber, el("small", null, " XP"));
  xp.append(el("span", "jr-metric-label", `${t("journey.rankKicker", { roman: romanOf(rank.index) })} · ${rankName(rank)}`), xpValue);
  if (previous) countUp(xpNumber, previous.xp, model.xp);

  metrics.append(exercises, xp);

  const caption = el("p", "jr-caption", rank.next
    ? t("journey.toNext", { xp: fmt(rank.next.minXp - model.xp), rank: t(`journey.rank.${rank.next.id}`) })
    : t("journey.maxRank"));

  const head = el("div", "jr-summary-head");
  head.append(renderRings(model, previous), metrics);
  const foot = el("div", "jr-tile-foot jr-summary-foot");
  foot.append(caption, renderRules());
  section.append(head, foot);
  return section;
}

/** Activity-style rings, drawn from 12 o'clock clockwise; each one animates from its previous value. */
function renderRings(model, previous) {
  const size = 100, c = size / 2;
  const rings = svg("svg", {
    class: "jr-rings", viewBox: `0 0 ${size} ${size}`, role: "img", focusable: "false",
    "aria-label": `${t("journey.dialAria", { done: model.completed, total: model.total, percent: model.percent })}. ${model.rank.next
      ? t("journey.xpAria", { xp: fmt(model.xp), rank: rankName(model.rank), left: fmt(model.rank.next.minXp - model.xp), next: t(`journey.rank.${model.rank.next.id}`) })
      : t("journey.xpAriaMax", { xp: fmt(model.xp), rank: rankName(model.rank) })}`,
  });

  const sameRank = previous && previous.rank.index === model.rank.index;
  const ringDefs = [
    { kind: "ok", r: 44, to: model.percent / 100, from: previous ? previous.percent / 100 : null },
    { kind: "acc", r: 31, to: rankProgress(model), from: previous ? (sameRank ? rankProgress(previous) : 0) : null },
  ];
  for (const ring of ringDefs) {
    const attributes = { cx: c, cy: c, r: ring.r, transform: `rotate(-90 ${c} ${c})` };
    rings.append(svg("circle", { ...attributes, class: `jr-ring-track ${ring.kind}` }));
    const fill = svg("circle", { ...attributes, class: `jr-ring ${ring.kind}`, pathLength: 100, "data-ring": ring.kind });
    const start = ring.from ?? ring.to;
    setRing(fill, start);
    rings.append(fill);
    if (start !== ring.to) requestAnimationFrame(() => requestAnimationFrame(() => setRing(fill, ring.to)));
  }
  return rings;
}

function setRing(circle, share) {
  circle.style.strokeDasharray = `${(share * 100).toFixed(2)} 100`;
  circle.classList.toggle("empty", share <= 0);
}

/** How XP is earned, on demand: the rules are part of the motivation. */
function renderRules() {
  const rules = el("details", "jr-rules");
  rules.open = journey.rulesOpen;
  rules.addEventListener("toggle", () => { journey.rulesOpen = rules.open; });
  const summary = el("summary", null, t("journey.rulesTitle"));
  summary.dataset.focusKey = "rules";
  rules.append(summary, el("p", null, t("journey.rules")));
  return rules;
}

/**
 * Trajectory: one section per track (its own learning path, numbered from 1), one group per level inside it, one chip per
 * exercise in the order of the path. The server sends the milestones grouped by track.
 */
function renderPath(model) {
  const section = tile("path");
  section.setAttribute("aria-label", t("journey.pathAria"));
  const tracks = el("div", "jr-tracks");
  let groups = null;
  let track = null;
  for (const milestone of model.milestones) {
    if (track === null || !sameTrack(track, milestone.track)) {
      track = milestone.track;
      const trackSection = el("section", "jr-track");
      const trackName = el("h4", "jr-track-name", trackText(track));
      trackName.id = `jr-track-${tracks.childElementCount}`;
      trackSection.setAttribute("aria-labelledby", trackName.id);
      groups = el("div", "jr-groups");
      trackSection.append(trackName, groups);
      tracks.append(trackSection);
    }
    const group = el("div", `jr-group${milestone.cleared ? " cleared" : ""}`);
    const head = el("div", "jr-group-head");
    const name = el("span", "jr-group-name", t("journey.milestone", { level: levelText(milestone) }));
    if (milestone.cleared) name.prepend(spriteIcon("check", "jr-group-check"));
    const count = el("span", "jr-group-count", `${milestone.completed}/${milestone.total}`);
    count.setAttribute("aria-label", t(milestone.cleared ? "journey.milestoneCleared" : "journey.milestoneCount",
      { level: milestoneName(milestone), done: milestone.completed, total: milestone.total }));
    head.append(name, count);

    const list = el("ol", "jr-chips");
    for (const id of milestone.labIds) {
      const lab = model.labs.find((l) => l.id === id);
      const item = el("li");
      item.append(renderChip(lab, model));
      list.append(item);
    }
    group.append(head, list);
    groups.append(group);
  }
  section.append(tracks, renderLegend(model));
  return section;
}

/** What the chips mean; "to resume" only appears when an exercise is in that state. */
function renderLegend(model) {
  const legend = el("ul", "jr-tile-foot jr-legend");
  legend.setAttribute("aria-hidden", "true"); // each chip already states its exercise and its state
  const states = ["completed", "inProgress", "notStarted"];
  if (model.labs.some((lab) => lab.bonusCompleted)) states.splice(1, 0, "plus");
  if (model.labs.some((lab) => lab.state === "toResume")) states.splice(states.indexOf("inProgress"), 0, "toResume");
  for (const state of states) {
    const item = el("li");
    item.append(el("i", `jr-key ${state}`), el("span", null, t(`journey.legend.${state}`)));
    legend.append(item);
  }
  return legend;
}

/** Position in the learning path of its track, from 1, whatever the labs of the other tracks; the lab number is the folder, not the step. */
function stepOf(lab, model) {
  const labs = journey.labs.some((l) => l.id === lab.id) ? journey.labs : model.labs;
  return labs.filter((l) => sameTrack(l.track, lab.track)).findIndex((l) => l.id === lab.id) + 1;
}

function renderChip(lab, model) {
  const chip = el("button", `jr-chip ${lab.state}${lab.bonusCompleted ? " plus" : ""}${lab.running ? " running" : ""}`);
  chip.type = "button";
  chip.dataset.journeyOpen = lab.id;
  chip.dataset.focusKey = `lab:${lab.id}`;
  chip.dataset.lab = lab.id;
  const step = stepOf(lab, model);
  const label = t("journey.node", { step, lab: labTitle(lab.id), state: stateText(lab) });
  chip.setAttribute("aria-label", label);
  chip.title = label;
  if (lab.id === journey.currentId) chip.setAttribute("aria-current", "true");
  if (lab.state === "inProgress") chip.style.setProperty("--p", String(lab.totalChecks ? lab.bestChecks / lab.totalChecks : 0));
  chip.append(el("span", "jr-chip-num", String(step)));
  return chip;
}

function renderMission(model) {
  const section = tile("mission");
  if (!model.next) {
    section.classList.add("finished");
    const head = el("div", "jr-finished");
    head.append(pathIcon(BADGE_ICONS.summit, "jr-finished-ico"), el("b", null, model.total ? t("journey.finishedTitle") : t("journey.emptyTitle")));
    section.append(head, el("p", "jr-mission-why", model.total ? t("journey.finishedText") : t("journey.emptyText")));
    return section;
  }

  const lab = model.labs.find((l) => l.id === model.next.labId);
  const why = el("p", "jr-mission-why");
  const reason = model.next.reason === "inProgress" && lab.totalChecks && lab.bestChecks
    ? t("journey.reason.inProgressChecks", { checks: checksText(lab) })
    : t(`journey.reason.${model.next.reason}`);
  why.append(el("i", `jr-dot ${lab.state}`), el("span", null, reason));

  const foot = el("div", "jr-tile-foot jr-mission-foot");
  if (lab.id === journey.currentId) {
    foot.append(el("span", "jr-mission-here", t("journey.openHere")));
  } else {
    const go = el("button", "jr-continue");
    go.type = "button";
    go.dataset.journeyOpen = lab.id;
    go.dataset.focusKey = "continue";
    go.setAttribute("aria-label", t("journey.continueAria", { lab: labTitle(lab.id) }));
    go.append(spriteIcon("play", "ico-fill"), el("span", null, t("journey.continue")));
    foot.append(go);
  }
  const gain = lab.potentialXp - lab.xp;
  if (gain > 0) foot.append(el("span", "jr-reward", t("journey.reward", { xp: fmt(gain) })));

  section.append(el("h3", "jr-tile-title", t("journey.next")), el("b", "jr-mission-title", labTitle(lab.id)), why, foot);
  return section;
}

function renderBadges(model) {
  const section = el("section", "jr-tile jr-badges-tile");
  const head = el("div", "jr-tile-head");
  const unlocked = model.badges.filter((badge) => badge.unlocked).length;
  head.append(el("h3", "jr-tile-title", t("journey.badges")), el("span", "jr-tile-count", `${unlocked}/${model.badges.length}`));

  const grid = el("ul", "jr-medals");
  for (const badge of model.badges) {
    const item = el("li");
    const button = el("button", `jr-medal${badge.unlocked ? " on" : ""}`);
    button.type = "button";
    button.dataset.badge = badge.id;
    button.dataset.focusKey = `badge:${badge.id}`;
    button.setAttribute("aria-label", badge.unlocked
      ? t("journey.badgeAria.unlocked", { name: badgeName(badge.id), date: fmtDay(badge.earnedAt), hint: badgeHint(badge.id) })
      : t("journey.badgeAria.locked", { name: badgeName(badge.id), hint: badgeHint(badge.id) }));
    button.append(pathIcon(BADGE_ICONS[badge.id] ?? BADGE_ICONS.firstSteps, "jr-medal-ico"));
    item.append(button);
    grid.append(item);
  }
  // Back to the summary when the pointer or the focus leaves the distinctions.
  grid.addEventListener("pointerleave", () => renderBadgeDetail(null));
  grid.addEventListener("focusout", (event) => { if (!grid.contains(event.relatedTarget)) renderBadgeDetail(null); });

  const detail = el("p", "jr-tile-foot jr-medal-detail");
  detail.setAttribute("aria-hidden", "true"); // each medal already carries the same text as its accessible name
  section.append(head, grid, detail);
  journey.badgeDetail = detail;
  renderBadgeDetail(null);
  return section;
}

function renderBadgeDetail(id) {
  const detail = journey.badgeDetail;
  if (!detail) return;
  const badge = id && journey.model?.badges.find((b) => b.id === id);
  if (!badge) {
    detail.classList.remove("on");
    detail.replaceChildren(t("journey.badgesHint"));
    return;
  }
  detail.classList.toggle("on", badge.unlocked);
  const status = badge.unlocked ? t("journey.badgeEarned", { date: fmtDay(badge.earnedAt) }) : t("journey.badgeLocked");
  detail.replaceChildren(el("b", null, badgeName(badge.id)), el("span", "jr-medal-status", ` · ${status}`), el("span", "jr-medal-hint", badgeHint(badge.id)));
}

// ---------- celebrations ----------
/** What improved between two journeys. A reset only removes progress: it produces nothing here. */
function changes(previous, next) {
  const events = [];
  const before = new Map(previous.labs.map((lab) => [lab.id, lab]));
  for (const lab of next.labs) {
    const old = before.get(lab.id);
    if (!old) continue;
    if (!done(old) && done(lab)) events.push({ kind: "completed", lab, xp: lab.xp - old.xp });
    else if (old.state === "toResume" && lab.state === "completed") events.push({ kind: "fixed", lab });
    else if (!done(lab) && lab.xp > old.xp) events.push({ kind: "checks", lab, xp: lab.xp - old.xp });
    if (lab.bonusCompleted && !old.bonusCompleted) events.push({ kind: "bonus", lab });
  }
  if (next.rank.index > previous.rank.index) events.push({ kind: "rank", rank: next.rank });
  for (const milestone of next.milestones) {
    const old = previous.milestones.find((m) => m.labIds[0] === milestone.labIds[0]);
    if (milestone.cleared && old && !old.cleared) events.push({ kind: "milestone", milestone });
  }
  for (const badge of next.badges) {
    if (badge.unlocked && !previous.badges.find((b) => b.id === badge.id)?.unlocked) events.push({ kind: "badge", badge });
  }
  return events;
}

function eventTexts(event) {
  switch (event.kind) {
    case "completed": return [t("journey.kicker.completed"), t("journey.event.completed", { xp: fmt(event.xp), lab: labTitle(event.lab.id) })];
    case "fixed": return [t("journey.kicker.fixed"), t("journey.event.fixed", { lab: labTitle(event.lab.id) })];
    case "bonus": return [t("journey.kicker.bonus"), t("journey.event.bonus", { lab: labTitle(event.lab.id) })];
    case "checks": return [t("journey.kicker.checks"), t("journey.event.checks", { xp: fmt(event.xp), lab: labTitle(event.lab.id), checks: checksText(event.lab) })];
    case "rank": return [t("journey.kicker.rank"), t("journey.event.rank", { roman: romanOf(event.rank.index), rank: rankName(event.rank) })];
    case "milestone": return [t("journey.kicker.milestone"), t("journey.event.milestone", { level: milestoneName(event.milestone) })];
    default: return [t("journey.kicker.badge"), badgeName(event.badge.id)];
  }
}

function celebrate(events) {
  if (!events.length) return;
  document.getElementById("journey-live").textContent = events.map((event) => eventTexts(event).join(" — ")).join(". ");
  for (const event of events) {
    if (event.kind === "completed" || event.kind === "fixed" || event.kind === "bonus") {
      pulse(`[data-lab="${CSS.escape(event.lab.id)}"]`);
      pulse(".jr-rings");
    }
    if (event.kind === "rank") pulse(".jr-metric.acc");
  }
  journey.queue.push(...events);
  showNextToast();
}

/** A short, soft emphasis on what just changed. */
function pulse(selector) {
  for (const node of journey.body.querySelectorAll(selector)) {
    node.classList.remove("pulse");
    void node.getBoundingClientRect(); // restart the animation when it is already running
    node.classList.add("pulse");
    node.addEventListener("animationend", () => node.classList.remove("pulse"), { once: true });
  }
}

const TOAST_ICONS = { completed: "check", fixed: "reset", checks: "check", bonus: "check" };

function toastIcon(event) {
  const holder = el("span", "jr-toast-ico");
  if (event.kind === "badge") holder.append(pathIcon(BADGE_ICONS[event.badge.id] ?? BADGE_ICONS.firstSteps, "jr-toast-glyph"));
  else if (event.kind === "rank") holder.append(el("span", "jr-toast-roman", romanOf(event.rank.index)));
  else if (event.kind === "milestone") holder.append(pathIcon(BADGE_ICONS.tierCleared, "jr-toast-glyph"));
  else holder.append(spriteIcon(TOAST_ICONS[event.kind]));
  return holder;
}

function showNextToast() {
  if (journey.toastBusy || !journey.queue.length) return;
  const event = journey.queue.shift();
  const [kicker, text] = eventTexts(event);
  const toast = el("div", `jr-toast ${event.kind}`);
  const words = el("span", "jr-toast-text");
  words.append(el("b", null, kicker), el("span", null, text));
  toast.append(toastIcon(event), words);

  journey.toastBusy = true;
  journey.feed.replaceChildren(toast);
  setTimeout(() => {
    toast.classList.add("out");
    setTimeout(() => {
      toast.remove();
      journey.toastBusy = false;
      showNextToast();
    }, reducedMotion() ? 0 : 280);
  }, TOAST_MS);
}

function countUp(node, from, to) {
  if (reducedMotion() || from >= to) return;
  const started = performance.now();
  const duration = 900;
  node.textContent = fmt(from);
  const step = (now) => {
    const p = Math.min(1, (now - started) / duration);
    const eased = 1 - (1 - p) ** 3;
    node.textContent = fmt(Math.round(from + (to - from) * eased));
    if (p < 1) requestAnimationFrame(step);
  };
  requestAnimationFrame(step);
}
