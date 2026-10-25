// Learning journey — the column left of the lab list: progress core, trajectory, next mission, distinctions.
// The model comes from GET /api/journey, recomputed by the server from the run history: this module only renders it,
// keeps it fresh and celebrates what changed while the page is open (never on load, language switch or reset).
import { t, language, locale } from "./i18n.js";

const SVG_NS = "http://www.w3.org/2000/svg";
const POLL_MS = 20000;
const TOAST_MS = 3400;
const ROMAN = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII"];

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
  requested: 0, applied: 0, unavailable: false, timer: null, observer: null,
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

/** Point on a circle, angle in degrees clockwise from the top. */
function polar(c, r, angle) {
  const radians = ((angle - 90) * Math.PI) / 180;
  return [c + r * Math.cos(radians), c + r * Math.sin(radians)];
}

function arc(c, r, from, to) {
  const [x0, y0] = polar(c, r, from);
  const [x1, y1] = polar(c, r, to);
  return `M${x0.toFixed(2)} ${y0.toFixed(2)}A${r} ${r} 0 ${to - from > 180 ? 1 : 0} 1 ${x1.toFixed(2)} ${y1.toFixed(2)}`;
}

/** Pointy-top hexagon in a 34×38 box: the shape of the rank emblem and of the distinctions. */
const HEXAGON = "17,1.5 32,10 32,28 17,36.5 2,28 2,10";

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

const checksText = (lab) => t("journey.state.checks", { passed: lab.bestChecks, total: lab.totalChecks });

function stateText(lab) {
  let text = lab.state === "inProgress" && lab.totalChecks && lab.bestChecks
    ? `${t("journey.state.inProgress")} · ${checksText(lab)}`
    : t(`journey.state.${lab.state}`);
  if (done(lab)) text += ` · ${t("journey.xp", { xp: fmt(lab.xp) })}`;
  if (lab.running) text += ` · ${t("journey.state.running")}`;
  return text;
}

const badgeName = (id) => t(`journey.badge.${id}`);
const badgeHint = (id) => t(`journey.badge.${id}.hint`);
const fmtDay = (iso) => new Date(iso).toLocaleDateString(locale(), { day: "numeric", month: "short" });

/** Progress toward the next rank, from 0 to 1 (1 at the highest rank). */
function rankProgress(model) {
  const { rank } = model;
  if (!rank.next) return 1;
  return Math.min(1, Math.max(0, (model.xp - rank.minXp) / (rank.next.minXp - rank.minXp)));
}

// ---------- public API ----------
/** Wires the column: lab links, distinction details, refresh when the page becomes visible and every 20 s. */
export function initJourney({ onOpen, onChange }) {
  journey.body = document.getElementById("journey-body");
  journey.onOpen = onOpen;
  journey.onChange = onChange;
  // Outside the redrawn body, so that a redraw never replays the achievement on screen.
  journey.feed = el("div", "jr-feed");
  journey.feed.setAttribute("aria-hidden", "true"); // announced by the live region instead
  document.getElementById("journey").append(journey.feed);

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

  journey.observer = new ResizeObserver(() => drawTrail());
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
  // The open lab is already shown: bring it into view (the journey sits above it on narrow screens).
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
  body.replaceChildren(renderCore(model, previous), renderPath(model), renderMission(model), renderBadges(model));
  if (focusKey) body.querySelector(`[data-focus-key="${CSS.escape(focusKey)}"]`)?.focus({ preventScroll: true });

  const path = body.querySelector(".jr-path");
  journey.observer.disconnect();
  journey.observer.observe(path);
  drawTrail();
}

function renderCore(model, previous) {
  const section = el("section", "jr-section jr-core");
  section.append(renderDial(model, previous), renderRank(model, previous), renderRules());
  return section;
}

/** The core: one arc per exercise (catalog order, wider gaps between milestones), and the XP toward the next rank inside. */
function renderDial(model, previous) {
  const size = 148, c = size / 2, r = 62, inner = 50;
  const wrap = el("div", "jr-dial");
  const dial = svg("svg", { viewBox: `0 0 ${size} ${size}`, role: "img", focusable: "false",
    "aria-label": t("journey.dialAria", { done: model.completed, total: model.total, percent: model.percent }) });

  const labs = model.labs;
  const n = labs.length;
  const boundary = (i) => i === n - 1 || labs[i].level.toLowerCase() !== labs[i + 1].level.toLowerCase();
  const gaps = labs.map((_, i) => (n === 1 ? 6 : boundary(i) ? 8 : 2.4));
  const span = (360 - gaps.reduce((sum, gap) => sum + gap, 0)) / Math.max(n, 1);
  let angle = n ? gaps[n - 1] / 2 : 0;
  labs.forEach((lab, i) => {
    const from = angle, to = angle + span;
    dial.append(svg("path", { d: arc(c, r, from, to), class: "jr-seg" }));
    if (lab.state !== "notStarted") {
      // In progress: the share of checks already passed (a sliver when none passed yet).
      const share = lab.state === "inProgress" ? Math.max(0.16, lab.totalChecks ? lab.bestChecks / lab.totalChecks : 0) : 1;
      dial.append(svg("path", { d: arc(c, r, from, from + span * share), class: `jr-seg-fill ${lab.state}`, "data-lab": lab.id }));
    }
    if (lab.running) dial.append(svg("path", { d: arc(c, r, from, to), class: "jr-seg-run" }));
    if (lab.id === journey.currentId) {
      const [x, y] = polar(c, r + 10, (from + to) / 2);
      dial.append(svg("circle", { cx: x.toFixed(2), cy: y.toFixed(2), r: 2.4, class: "jr-seg-current" }));
    }
    angle = to + gaps[i];
  });

  // Inner ring: a full circle from 12 o'clock, clockwise, measured in percent (pathLength 100).
  const progress = rankProgress(model);
  const from = previous ? (previous.rank.index === model.rank.index ? rankProgress(previous) : 0) : progress;
  const circle = `M${c} ${c - inner}A${inner} ${inner} 0 1 1 ${c} ${c + inner}A${inner} ${inner} 0 1 1 ${c} ${c - inner}`;
  dial.append(svg("path", { d: circle, class: "jr-xp-ring-track" }));
  const ring = svg("path", { d: circle, class: "jr-xp-ring", pathLength: 100 });
  const share = (value) => `${(value * 100).toFixed(2)} 100`;
  ring.style.strokeDasharray = share(from);
  ring.classList.toggle("empty", from === 0);
  dial.append(ring);
  if (from !== progress) {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      ring.style.strokeDasharray = share(progress);
      ring.classList.toggle("empty", progress === 0);
    }));
  }

  const center = el("div", "jr-dial-center");
  center.setAttribute("aria-hidden", "true");
  const percent = el("b", "jr-pct", String(model.percent));
  percent.append(el("small", null, "%"));
  center.append(percent, el("span", "jr-dial-sub", `${model.completed} / ${model.total}`), el("span", "jr-dial-cap", t("journey.done")));
  wrap.append(dial, center);
  return wrap;
}

function rankEmblem(index) {
  const emblem = svg("svg", { class: "jr-emblem", viewBox: "0 0 34 38", "aria-hidden": "true" });
  emblem.append(svg("polygon", { points: HEXAGON }), svg("polygon", { points: HEXAGON, class: "jr-emblem-inner", transform: "translate(17 19) scale(.74) translate(-17 -19)" }));
  const numeral = svg("text", { x: 17, y: 19, "text-anchor": "middle", "dominant-baseline": "central" });
  numeral.textContent = ROMAN[index - 1] ?? String(index);
  emblem.append(numeral);
  return emblem;
}

function renderRank(model, previous) {
  const { rank } = model;
  const box = el("div", "jr-rank-box");
  const row = el("div", "jr-rank");
  const text = el("div", "jr-rank-text");
  const kicker = el("span", "jr-rank-kicker", t("journey.rankKicker", { roman: ROMAN[rank.index - 1] ?? rank.index }));
  const pips = el("span", "jr-pips");
  pips.setAttribute("aria-hidden", "true");
  for (let i = 1; i <= 5; i++) pips.append(el("i", i <= rank.index ? "on" : ""));
  kicker.append(pips);
  text.append(kicker, el("b", "jr-rank-name", t(`journey.rank.${rank.id}`)));
  const xp = el("span", "jr-xp", t("journey.xp", { xp: fmt(model.xp) }));
  row.append(rankEmblem(rank.index), text, xp);

  const progress = rankProgress(model);
  const track = el("div", "jr-xp-track");
  track.setAttribute("role", "progressbar");
  track.setAttribute("aria-valuemin", "0");
  track.setAttribute("aria-valuemax", "100");
  track.setAttribute("aria-valuenow", String(Math.round(progress * 100)));
  track.setAttribute("aria-label", t("journey.xpTrack"));
  track.setAttribute("aria-valuetext", rank.next
    ? t("journey.xpAria", { xp: fmt(model.xp), rank: t(`journey.rank.${rank.id}`), left: fmt(rank.next.minXp - model.xp), next: t(`journey.rank.${rank.next.id}`) })
    : t("journey.xpAriaMax", { xp: fmt(model.xp), rank: t(`journey.rank.${rank.id}`) }));
  const fill = el("i");
  const sameRank = previous && previous.rank.index === rank.index;
  fill.style.setProperty("--w", `${(previous ? (sameRank ? rankProgress(previous) : 0) : progress) * 100}%`);
  track.append(fill);
  if (previous) {
    requestAnimationFrame(() => requestAnimationFrame(() => fill.style.setProperty("--w", `${progress * 100}%`)));
    countUp(xp, previous.xp, model.xp);
  }

  const next = el("p", "jr-xp-next");
  next.textContent = rank.next
    ? t("journey.toNext", { xp: fmt(rank.next.minXp - model.xp), rank: t(`journey.rank.${rank.next.id}`) })
    : t("journey.maxRank");

  box.append(row, track, next);
  return box;
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

function renderPath(model) {
  const section = el("section", "jr-section jr-path");
  section.setAttribute("aria-label", t("journey.pathAria"));
  const trail = svg("svg", { class: "jr-trail", "aria-hidden": "true" });
  section.append(trail);

  for (const milestone of model.milestones) {
    const gate = el("div", `jr-gate${milestone.cleared ? " cleared" : ""}`);
    const mark = svg("svg", { class: "jr-gate-mark", viewBox: "0 0 10 10", "aria-hidden": "true" });
    mark.append(svg("path", { d: "M5 .9L9.1 5 5 9.1.9 5z" }));
    const label = el("span", "jr-gate-label", t("journey.milestone", { level: levelText(milestone) }));
    const count = el("span", "jr-gate-count", `${milestone.completed}/${milestone.total}`);
    count.setAttribute("aria-label", t(milestone.cleared ? "journey.milestoneCleared" : "journey.milestoneCount",
      { level: levelText(milestone), done: milestone.completed, total: milestone.total }));
    gate.append(mark, label, count);

    const list = el("ol", "jr-nodes");
    for (const id of milestone.labIds) {
      const lab = model.labs.find((l) => l.id === id);
      const item = el("li");
      item.append(renderNode(lab));
      list.append(item);
    }
    section.append(gate, list);
  }
  return section;
}

function renderNode(lab) {
  const node = el("button", `jr-node ${lab.state}${lab.running ? " running" : ""}`);
  node.type = "button";
  node.dataset.journeyOpen = lab.id;
  node.dataset.focusKey = `lab:${lab.id}`;
  node.dataset.lab = lab.id;
  const label = t("journey.node", { lab: labTitle(lab.id), state: stateText(lab) });
  node.setAttribute("aria-label", label);
  node.title = label;
  if (lab.id === journey.currentId) node.setAttribute("aria-current", "true");
  if (lab.state === "inProgress") node.style.setProperty("--p", String(lab.totalChecks ? lab.bestChecks / lab.totalChecks : 0));

  node.append(el("span", "jr-node-num", lab.number));
  if (lab.state === "completed") {
    const mark = el("span", "jr-node-mark");
    mark.append(spriteIcon("check"));
    node.append(mark);
  } else if (lab.state === "toResume") {
    node.append(el("span", "jr-node-mark", "!"));
  }
  return node;
}

/**
 * A milestone that takes several rows is laid out as a serpentine: its second row runs right to left, and so on,
 * so that the trail reads like a route instead of jumping back to the left edge. Each milestone starts on the left.
 */
function layoutSerpentine(section) {
  for (const list of section.querySelectorAll(".jr-nodes")) {
    const columns = Math.max(1, getComputedStyle(list).gridTemplateColumns.split(" ").length);
    [...list.children].forEach((item, i) => {
      const row = Math.floor(i / columns);
      item.style.gridRow = String(row + 1);
      item.style.gridColumn = String(row % 2 ? columns - (i % columns) : (i % columns) + 1);
    });
  }
}

/** The trail between consecutive waypoints, drawn once the layout is known (and again when it changes). */
function drawTrail() {
  const section = journey.body?.querySelector(".jr-path");
  const model = journey.model;
  if (!section || !model) return;
  layoutSerpentine(section);
  const trail = section.querySelector(".jr-trail");
  const box = section.getBoundingClientRect();
  if (!box.width) return;
  trail.setAttribute("viewBox", `0 0 ${box.width.toFixed(1)} ${box.height.toFixed(1)}`);

  const byId = new Map(model.labs.map((lab) => [lab.id, lab]));
  const points = [...section.querySelectorAll(".jr-node")].map((node) => {
    const rect = node.getBoundingClientRect();
    return { lab: byId.get(node.dataset.lab), x: rect.left - box.left + rect.width / 2, y: rect.top - box.top + rect.height / 2 };
  });

  const nextId = model.next?.labId;
  const paths = [];
  for (let i = 0; i < points.length - 1; i++) {
    const a = points[i], b = points[i + 1];
    const dy = b.y - a.y;
    const d = Math.abs(dy) < 2
      ? `M${a.x.toFixed(1)} ${a.y.toFixed(1)}H${b.x.toFixed(1)}`
      : `M${a.x.toFixed(1)} ${a.y.toFixed(1)}C${a.x.toFixed(1)} ${(a.y + dy * 0.62).toFixed(1)} ${b.x.toFixed(1)} ${(b.y - dy * 0.62).toFixed(1)} ${b.x.toFixed(1)} ${b.y.toFixed(1)}`;
    const kind = done(a.lab) && done(b.lab) ? "lit" : done(a.lab) && b.lab.id === nextId ? "ahead" : "";
    paths.push(svg("path", { d, class: kind }));
  }
  trail.replaceChildren(...paths);
}

function renderMission(model) {
  const section = el("section", "jr-section jr-mission");
  if (!model.next) {
    section.classList.add("finished");
    const head = el("div", "jr-finished");
    const icon = svg("svg", { class: "ico jr-finished-ico", viewBox: "0 0 16 16", "aria-hidden": "true" });
    icon.append(svg("path", { d: BADGE_ICONS.summit }));
    head.append(icon, el("b", null, model.total ? t("journey.finishedTitle") : t("journey.emptyTitle")));
    section.append(head, el("p", "jr-mission-why", model.total ? t("journey.finishedText") : t("journey.emptyText")));
    return section;
  }

  const lab = model.labs.find((l) => l.id === model.next.labId);
  const why = el("p", "jr-mission-why");
  const reason = model.next.reason === "inProgress" && lab.totalChecks && lab.bestChecks
    ? t("journey.reason.inProgressChecks", { checks: checksText(lab) })
    : t(`journey.reason.${model.next.reason}`);
  why.append(el("i", `jr-dot ${lab.state}`), el("span", null, reason));

  const foot = el("div", "jr-mission-foot");
  if (lab.id === journey.currentId) {
    foot.append(el("span", "jr-mission-here", t("journey.openHere")));
  } else {
    const go = el("button", "btn btn-primary jr-continue");
    go.type = "button";
    go.dataset.journeyOpen = lab.id;
    go.dataset.focusKey = "continue";
    go.setAttribute("aria-label", t("journey.continueAria", { lab: labTitle(lab.id) }));
    go.append(spriteIcon("play", "ico-fill"), el("span", null, t("journey.continue")));
    foot.append(go);
  }
  const gain = lab.potentialXp - lab.xp;
  if (gain > 0) foot.append(el("span", "jr-reward", t("journey.reward", { xp: fmt(gain) })));

  section.append(el("h3", "jr-label", t("journey.next")), el("b", "jr-mission-title", labTitle(lab.id)), why, foot);
  return section;
}

function renderBadges(model) {
  const section = el("section", "jr-section jr-badges-box");
  const head = el("div", "jr-label-row");
  const unlocked = model.badges.filter((badge) => badge.unlocked).length;
  head.append(el("h3", "jr-label", t("journey.badges")), el("span", "jr-label-count", `${unlocked}/${model.badges.length}`));

  const grid = el("ul", "jr-badges");
  for (const badge of model.badges) {
    const item = el("li");
    const button = el("button", `jr-badge${badge.unlocked ? " on" : ""}`);
    button.type = "button";
    button.dataset.badge = badge.id;
    button.dataset.focusKey = `badge:${badge.id}`;
    button.setAttribute("aria-label", badge.unlocked
      ? t("journey.badgeAria.unlocked", { name: badgeName(badge.id), date: fmtDay(badge.earnedAt), hint: badgeHint(badge.id) })
      : t("journey.badgeAria.locked", { name: badgeName(badge.id), hint: badgeHint(badge.id) }));
    button.append(badgeInsignia(badge.id));
    item.append(button);
    grid.append(item);
  }
  // Back to the summary when the pointer or the focus leaves the distinctions.
  grid.addEventListener("pointerleave", () => renderBadgeDetail(null));
  grid.addEventListener("focusout", (event) => { if (!grid.contains(event.relatedTarget)) renderBadgeDetail(null); });

  const detail = el("p", "jr-badge-detail");
  detail.setAttribute("aria-hidden", "true"); // each insignia already carries the same text as its accessible name
  section.append(head, grid, detail);
  journey.badgeDetail = detail;
  renderBadgeDetail(null);
  return section;
}

function badgeInsignia(id) {
  const insignia = svg("svg", { class: "jr-insignia", viewBox: "0 0 34 38", "aria-hidden": "true" });
  insignia.append(svg("polygon", { points: HEXAGON }));
  const icon = svg("svg", { x: 9, y: 11, width: 16, height: 16, viewBox: "0 0 16 16", class: "jr-insignia-ico" });
  icon.append(svg("path", { d: BADGE_ICONS[id] ?? BADGE_ICONS.firstSteps }));
  insignia.append(icon);
  return insignia;
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
  detail.replaceChildren(el("b", null, badgeName(badge.id)), el("span", "jr-badge-status", ` · ${status}`), el("span", "jr-badge-hint", badgeHint(badge.id)));
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
    case "checks": return [t("journey.kicker.checks"), t("journey.event.checks", { xp: fmt(event.xp), lab: labTitle(event.lab.id), checks: checksText(event.lab) })];
    case "rank": return [t("journey.kicker.rank"), t("journey.event.rank", { roman: ROMAN[event.rank.index - 1], rank: t(`journey.rank.${event.rank.id}`) })];
    case "milestone": return [t("journey.kicker.milestone"), t("journey.event.milestone", { level: levelText(event.milestone) })];
    default: return [t("journey.kicker.badge"), badgeName(event.badge.id)];
  }
}

function celebrate(events) {
  if (!events.length) return;
  document.getElementById("journey-live").textContent = events.map((event) => eventTexts(event).join(" — ")).join(". ");
  for (const event of events) {
    if (event.kind === "completed" || event.kind === "fixed") ignite(event.lab.id);
    if (event.kind === "rank") journey.body.querySelector(".jr-emblem")?.classList.add("flash");
  }
  journey.queue.push(...events);
  showNextToast();
}

/** The waypoint and its arc of the core light up. */
function ignite(labId) {
  for (const node of journey.body.querySelectorAll(`[data-lab="${CSS.escape(labId)}"]`)) {
    node.classList.add("ignite");
    node.addEventListener("animationend", () => node.classList.remove("ignite"), { once: true });
  }
}

const TOAST_ICONS = { completed: "check", fixed: "reset", checks: "check" };

function showNextToast() {
  if (journey.toastBusy || !journey.queue.length) return;
  const event = journey.queue.shift();
  const [kicker, text] = eventTexts(event);
  const toast = el("div", `jr-toast ${event.kind}`);
  const badge = el("span", "jr-toast-ico");
  if (event.kind === "badge") badge.append(badgeInsignia(event.badge.id));
  else if (event.kind === "rank") badge.append(rankEmblem(event.rank.index));
  else if (event.kind === "milestone") {
    const mark = svg("svg", { class: "jr-gate-mark", viewBox: "0 0 10 10" });
    mark.append(svg("path", { d: "M5 .9L9.1 5 5 9.1.9 5z" }));
    badge.append(mark);
  } else badge.append(spriteIcon(TOAST_ICONS[event.kind]));
  const words = el("span", "jr-toast-text");
  words.append(el("b", null, kicker), el("span", null, text));
  toast.append(badge, words);

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
  node.textContent = t("journey.xp", { xp: fmt(from) });
  const step = (now) => {
    const p = Math.min(1, (now - started) / duration);
    const eased = 1 - (1 - p) ** 3;
    node.textContent = t("journey.xp", { xp: fmt(Math.round(from + (to - from) * eased)) });
    if (p < 1) requestAnimationFrame(step);
  };
  requestAnimationFrame(step);
}
