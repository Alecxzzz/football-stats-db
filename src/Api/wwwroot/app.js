/* 360 Stats · estadísticas de fútbol.
 *
 * Los datos salen de las APIs públicas (no oficiales) de FotMob y ESPN a
 * través del puente del propio servidor (/api/fm/... y /api/espn/...).
 * No hay cuentas, ni login, ni claves: el navegador solo habla con tu equipo.
 *   GET  /api/fm/matches?date=YYYYMMDD
 *   GET  /api/fm/matchDetails?matchId=ID
 *   GET  /api/fm/leagues?id=LEAGUE_ID
 */
const API_BASE = "/api/fm";
const REFRESH_MS = 60000;
const SPORT = "soccer";

const state = {
  offset: 0,
  league: "",
  query: "",
  games: [],
  liveCount: 0,
  leagues: [],
  updatedAt: null,
  error: null,
  fromDb: false,
  timer: null,
  view: "home",
  game: null,
  tab: "analisis",
  highlightsDone: false,
};

const $ = (sel) => document.querySelector(sel);
const el = {};

/* ---------- utilidades ---------- */
const esc = (value) =>
  String(value ?? "").replace(/[&<>"']/g, (c) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#39;",
  }[c]));

const num = (value) => {
  if (value == null) return null;
  if (typeof value === "number") return Number.isFinite(value) ? value : null;
  const m = String(value).replace(",", ".").match(/-?\d+(\.\d+)?/);
  return m ? parseFloat(m[0]) : null;
};

const pick = (obj, keys) => {
  for (const k of keys) {
    const v = obj?.[k];
    if (v != null && v !== "") return v;
  }
  return null;
};

const txt = (value) => {
  if (value == null) return "–";
  if (typeof value === "object") {
    const inner = pick(value, ["valor", "value", "displayValue", "texto"]);
    return inner == null ? "–" : txt(inner);
  }
  return String(value);
};

const pct = (a, b) => {
  const total = (a || 0) + (b || 0);
  if (!total) return 50;
  return Math.round((a / total) * 100);
};

const loadingHTML = (text = "Cargando…") =>
  `<div class="loading"><span class="spinner"></span>${esc(text)}</div>`;

const emptyHTML = (title, detail) =>
  `<div class="empty-state"><strong>${esc(title)}</strong><span>${esc(detail || "")}</span></div>`;

/* ---------- cliente HTTP (mismo origen, sin credenciales en el navegador) ---------- */
async function api(path) {
  const res = await fetch(`${API_BASE}${path}`, { headers: { Accept: "application/json" } });
  const data = await res.json().catch(() => null);
  if (!res.ok) {
    throw new Error(data?.message || data?.detail || `La API respondió ${res.status}`);
  }
  return data;
}

/* ---------- respaldo: lo que ya está guardado en MongoDB (/api/datos) ---------- */
const DB_BASE = "/api/datos";

async function dbGet(path) {
  try {
    const res = await fetch(`${DB_BASE}${path}`, { headers: { Accept: "application/json" } });
    if (!res.ok) return null;
    return await res.json().catch(() => null);
  } catch {
    return null;
  }
}

/* ---------- carga con memoización por pestaña ---------- */
const cache = new Map();

function memo(key, factory) {
  if (!cache.has(key)) {
    const p = factory();
    cache.set(key, p);
    p.catch(() => cache.delete(key));
  }
  return cache.get(key);
}

/* ---------- fuentes: FotMob ---------- */
const teamLogo = (id) =>
  id != null && id !== "" ? `https://images.fotmob.com/image_resources/logo/teamlogo/${id}.png` : null;
const playerPhoto = (id) =>
  id != null && id !== "" ? `https://images.fotmob.com/image_resources/playerimages/${id}.png` : null;

const dayKey = (offset) => {
  const d = new Date();
  d.setDate(d.getDate() + offset);
  const p = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}${p(d.getMonth() + 1)}${p(d.getDate())}`;
};

function humanDay(offset) {
  const text = new Date(new Date().setDate(new Date().getDate() + offset)).toLocaleDateString("es-ES", {
    weekday: "long",
    day: "numeric",
    month: "long",
  });
  return text.charAt(0).toUpperCase() + text.slice(1);
}

const stateOf = (st) => (st?.finished ? "post" : st?.started ? "in" : "pre");

function clockOf(st) {
  if (!st || !st.started || st.finished) return "";
  return st.liveTime?.short || st.liveTime?.long || "EN VIVO";
}

function mapListMatch(match, league) {
  const st = match.status || {};
  const side = (team) => ({
    id: team?.id != null ? String(team.id) : "",
    name: team?.name || "",
    logo: teamLogo(team?.id),
    score: team?.score ?? null,
  });
  return {
    id: String(match.id),
    league: league.name || "",
    league_code: String(league.code ?? league.id ?? ""),
    date: st.utcTime || null,
    state: stateOf(st),
    status: st.reason?.short || (st.cancelled ? "Cancelado" : ""),
    clock: clockOf(st),
    home: side(match.home),
    away: side(match.away),
  };
}

function mapStoredMatch(p) {
  return {
    id: String(p.matchId),
    league: p.liga || "",
    league_code: String(p.ligaId ?? ""),
    date: p.fecha || null,
    state: p.estado || "pre",
    status: "",
    clock: "",
    home: {
      id: p.localId != null ? String(p.localId) : "",
      name: p.local || "",
      logo: null,
      score: p.golesLocal ?? null,
    },
    away: {
      id: p.visitanteId != null ? String(p.visitanteId) : "",
      name: p.visitante || "",
      logo: null,
      score: p.golesVisitante ?? null,
    },
    desdeMongo: true,
  };
}

function ligasGuardadas(partidos) {
  const map = new Map();
  for (const p of partidos) {
    const code = String(p.ligaId ?? "");
    if (p.liga && !map.has(code)) map.set(code, { code, name: p.liga });
  }
  return [...map.values()];
}

async function fetchDay(offset) {
  const data = await api(`/matches?date=${dayKey(offset)}`);
  const games = [];
  const leagues = new Map();
  for (const league of data?.leagues || []) {
    // id = edición concreta (fase, grupo…), primaryId = competición real,
    // que es la que entiende el endpoint de clasificaciones.
    const meta = {
      code: String(league.primaryId ?? league.id ?? ""),
      name: league.name || "",
    };
    if (!meta.name) continue;
    if (!leagues.has(meta.code)) leagues.set(meta.code, meta);
    for (const match of league.matches || []) games.push(mapListMatch(match, meta));
  }
  return { games, leagues: [...leagues.values()] };
}

/* ---------- partido: detalle completo (una sola llamada por partido) ---------- */
/**
 * Si FotMob no manda un dato (alineación, estadísticas, jugadores), se rellena
 * con lo que ya está guardado en MongoDB: GET /api/datos/detalle/{id}.
 */
async function hidratarConRespaldo(id, detail) {
  const content = (detail.content = detail.content || {});
  const once =
    (content.lineup?.homeTeam?.starters || []).length + (content.lineup?.awayTeam?.starters || []).length;
  const sinStats = !(content.stats?.Periods?.All?.stats || []).length;
  const sinJugadores = !Object.keys(content.playerStats || {}).length;
  if (once && !sinStats && !sinJugadores) return detail;

  const guardado = await dbGet(`/detalle/${encodeURIComponent(id)}`);
  const c = guardado?.content || {};

  if (!once && c.lineup) {
    content.lineup = c.lineup;
    detail.__respaldoAlineacion = guardado.guardadoAlineacion || guardado.guardado || null;
  }
  if (sinStats && c.stats) content.stats = c.stats;
  if (sinJugadores && c.playerStats) content.playerStats = c.playerStats;
  if (!content.matchFacts?.events && c.matchFacts?.events) {
    content.matchFacts = content.matchFacts || {};
    content.matchFacts.events = c.matchFacts.events;
  }

  return detail;
}

const loadDetail = (id) =>
  memo(`detail:${id}`, async () => {
    let detail = null;
    let fallo = null;
    try {
      detail = await api(`/matchDetails?matchId=${encodeURIComponent(id)}`);
    } catch (err) {
      fallo = err;
    }

    if (!detail) {
      // Sin respuesta de FotMob: se abre el partido con lo guardado en Mongo.
      const guardado = await dbGet(`/detalle/${encodeURIComponent(id)}`);
      if (!guardado) throw fallo || new Error("No se pudo cargar el partido");
      const content = (guardado.content = guardado.content || {});
      return {
        ...guardado,
        content,
        __desdeMongo: true,
        __respaldoAlineacion: guardado.guardadoAlineacion || null,
      };
    }

    return hidratarConRespaldo(id, detail);
  });

function miniGame(home, away, status, date) {
  return {
    date: date || null,
    home: { name: home.name, score: home.score, winner: home.winner },
    away: { name: away.name, score: away.score, winner: away.winner },
    status: status || "",
  };
}

function h2hGames(detail) {
  const list = detail?.content?.h2h?.matches || [];
  const played = list.filter((m) => m?.status?.finished);
  const source = played.length ? played : list;
  return source.map((m) => {
    const st = m.status || {};
    const [hs, as] = String(st.scoreStr || "").split("-").map((v) => v.trim());
    const hasScore = st.scoreStr && hs !== "" && as !== "";
    return miniGame(
      { name: m.home?.name || "", score: hasScore ? hs : null, winner: hasScore && Number(hs) > Number(as) },
      { name: m.away?.name || "", score: hasScore ? as : null, winner: hasScore && Number(as) > Number(hs) },
      st.reason?.short || (st.finished ? "FT" : ""),
      m.time?.utcTime || st.utcTime
    );
  });
}

function recentGames(detail, side) {
  const list = detail?.content?.matchFacts?.teamForm?.[side] || [];
  return list.map((f) => {
    const tips = f.tooltipText || {};
    const hs = tips.homeScore;
    const as = tips.awayScore;
    return miniGame(
      { name: tips.homeTeam || "", score: hs ?? null, winner: Number(hs) > Number(as) },
      { name: tips.awayTeam || "", score: as ?? null, winner: Number(as) > Number(hs) },
      "FT",
      f.date?.utcTime || tips.utcTime
    );
  });
}

function mapGame(detail, id) {
  const general = detail?.general || {};
  const header = detail?.header || {};
  const st = header.status || {};
  const colors = general.teamColors?.darkMode || general.teamColors?.lightMode || {};
  const teams = (header.teams || []).map((team, i) => ({
    homeAway: i === 0 ? "home" : "away",
    name: team.name || "",
    logo: team.imageUrl || teamLogo(team.id),
    score: team.score ?? null,
    color: colors[i === 0 ? "home" : "away"] || null,
    records: general.leagueRoundName ? [{ summary: `Jornada ${general.leagueRoundName}` }] : [],
    recent_games: recentGames(detail, i),
  }));

  return {
    id: String(general.matchId || id),
    league: general.leagueName || "",
    league_code: String(general.parentLeagueId ?? general.leagueId ?? ""),
    date: st.utcTime || general.matchTimeUTC || null,
    state: stateOf(st),
    status: st.reason?.short || "",
    clock: clockOf(st),
    teams,
    head_to_head: h2hGames(detail),
    h2h_summary: Array.isArray(detail?.content?.h2h?.summary) ? detail.content.h2h.summary : null,
  };
}

const loadGame = (id) => loadDetail(id).then((detail) => mapGame(detail, id));

/* ---------- pestañas ---------- */
function extrasView(detail) {
  const facts = detail?.content?.matchFacts || {};
  const info = facts.infoBox || {};

  const insights = (facts.insights || [])
    .map((i) => ({ texto: i?.text || "", color: i?.color }))
    .filter((i) => i.texto)
    .slice(0, 8);

  // Si FotMob no trae "insights", se usan las cifras principales del partido
  // para que la tarjeta de análisis y los destacados no queden vacíos.
  if (!insights.length) {
    const top = detail?.content?.stats?.Periods?.All?.stats?.[0]?.stats || [];
    for (const s of top.slice(0, 2)) {
      const a = Array.isArray(s.stats) ? s.stats[0] : null;
      const b = Array.isArray(s.stats) ? s.stats[1] : null;
      if (a == null || b == null) continue;
      insights.push({ texto: `${s.title}: ${a} – ${b}` });
    }
  }

  const scorerCard = (player) => {
    if (!player) return null;
    const s = player.stats || {};
    return {
      nombre: player.lastName || player.fullName || "",
      completo: player.fullName || "",
      foto: playerPhoto(player.playerId),
      filas: [
        { etiqueta: "Goles", valor: s.goals },
        { etiqueta: "Asistencias", valor: s.goalAssist },
        { etiqueta: "Partidos", valor: s.gamesPlayed },
        { etiqueta: "Minutos", valor: s.minsPlayed },
        { etiqueta: "Nota media", valor: s.playerRating != null ? Number(s.playerRating).toFixed(2) : null },
        { etiqueta: "xG", valor: s.expectedGoals != null ? Number(s.expectedGoals).toFixed(2) : null },
      ].filter((f) => f.valor != null && f.valor !== ""),
    };
  };

  const goleadores = facts.topScorers
    ? {
        titulo: "Máximos goleadores",
        local: scorerCard(facts.topScorers.homePlayer),
        visita: scorerCard(facts.topScorers.awayPlayer),
      }
    : null;

  const forma = (facts.teamForm || []).map((list) =>
    (list || []).map((f) => {
      const tips = f.tooltipText || {};
      const rival = f.home?.isOurTeam ? tips.awayTeam : tips.homeTeam;
      const when = f.date?.utcTime || tips.utcTime;
      return {
        resultado: f.resultString || "",
        rival: rival || "",
        fecha: when ? new Date(when).toLocaleDateString("es-ES", { day: "2-digit", month: "2-digit" }) : "",
      };
    })
  );

  const ref = info.Referee;
  const arbitro =
    ref && ref.text
      ? {
          nombre: ref.text,
          bandera: ref.imgUrl || null,
          pais: ref.country || "",
          liga: ref.leagueName || "",
          stats: Object.fromEntries(
            (ref.stats || [])
              .filter((s) => s?.type)
              .map((s) => [s.type, { valor: s.value, media: s.average, pct: s.averagePercentage }])
          ),
        }
      : null;

  const stadium = info.Stadium || {};
  const estadio = stadium.name ? [stadium.name, stadium.city].filter(Boolean).join(" · ") : null;

  const ok = insights.length || goleadores || forma.length || arbitro || estadio;
  return {
    ok: !!ok,
    insights,
    goleadores,
    forma,
    arbitro,
    estadio,
    publico: info.Attendance ?? null,
    motivo: ok ? "" : "FotMob todavía no publicó datos de este partido.",
  };
}

function statsView(detail) {
  const general = detail?.general || {};
  const groups = detail?.content?.stats?.Periods?.All?.stats || [];

  const grupos = groups
    .map((group) => ({
      nombre: group.title || "",
      filas: (group.stats || [])
        .map((s) => ({
          etiqueta: s.title || "",
          local: Array.isArray(s.stats) ? s.stats[0] : null,
          visita: Array.isArray(s.stats) ? s.stats[1] : null,
        }))
        .filter((f) => f.etiqueta && (f.local != null || f.visita != null)),
    }))
    .filter((g) => g.filas.length);

  const momento = (detail?.content?.momentum?.main?.data || [])
    .filter((p) => p && p.value != null)
    .map((p) => ({ minuto: p.minute, valor: p.value }));

  const homeId = detail?.general?.homeTeam?.id;
  const tiros = (detail?.content?.shotmap?.shots || [])
    .filter((s) => s && s.x != null && s.y != null)
    .map((s) => ({
      x: s.x,
      y: s.y,
      gol: s.eventType === "Goal" || !!s.isOwnGoal,
      aPuerta: !!s.isOnTarget,
      xg: s.expectedGoals ?? 0,
      minuto: s.min ?? null,
      jugador: s.playerName || "",
      propio: !!s.isOwnGoal,
      local: s.teamId === homeId,
    }));

  const eventos = (detail?.content?.matchFacts?.events?.events || [])
    .filter((e) => ["Goal", "Card", "Substitution"].includes(e.type))
    .map((e) => ({
      tipo: e.type,
      minuto: e.time ?? e.timeStr,
      jugador:
        e.type === "Substitution"
          ? [e.swap?.[0]?.name, e.swap?.[1]?.name].filter(Boolean).join(" ⇄ ")
          : e.player?.name || e.fullName || "",
      asistente: e.type === "Goal" ? e.assistInput || e.assistPlayerName || null : null,
      propio: !!e.ownGoal,
      es_local: !!e.isHome,
      marcador: e.type === "Goal" && Array.isArray(e.newScore) ? e.newScore : null,
    }))
    .filter((e) => e.jugador);

  return {
    ok: !!(grupos.length || eventos.length || momento.length > 1 || tiros.length),
    equipos: [{ nombre: general.homeTeam?.name || "Local" }, { nombre: general.awayTeam?.name || "Visitante" }],
    grupos,
    momento,
    tiros,
    eventos,
    motivo:
      "FotMob no publica estadísticas de este partido (pasa en ligas menores, " +
      "canteras y algunas copas).",
  };
}

function playersView(detail) {
  const source = detail?.content?.playerStats || {};
  const out = {};
  for (const [id, player] of Object.entries(source)) {
    const grupos = (player?.stats || [])
      .map((group) => ({
        grupo: group.title || "",
        filas: Object.entries(group.stats || {})
          .map(([label, entry]) => ({
            etiqueta: label,
            valor: entry?.stat?.value ?? null,
            total: entry?.stat?.total ?? null,
          }))
          .filter((f) => f.valor != null && typeof f.valor !== "boolean"),
      }))
      .filter((g) => g.filas.length);
    if (!grupos.length) continue;
    out[id] = {
      nombre: player.name || "",
      equipo: player.teamName || "Equipo",
      dorsal: player.shirtNumber ?? "",
      portero: !!player.isGoalkeeper,
      grupos,
    };
  }
  return out;
}

function lineupView(detail) {
  const lineup = detail?.content?.lineup;
  const total =
    (lineup?.homeTeam?.starters || []).length +
    (lineup?.awayTeam?.starters || []).length +
    (lineup?.homeTeam?.subs || []).length +
    (lineup?.awayTeam?.subs || []).length;
  if (!total) {
    return {
      ok: false,
      motivo:
        "FotMob no tiene la alineación de este partido. En ligas menores, copas regionales y " +
        "partidos sin cubrir solo aparece cuando el proveedor la publica; los demás partidos de " +
        "la misma competición sí pueden tenerla.",
    };
  }

  const side = (team, mirror) => ({
    equipo: team?.name || "",
    formacion: team?.formation || "",
    entrenador: team?.coach?.name || "",
    nota: team?.rating != null ? `Nota media ${Number(team.rating).toFixed(2)}` : "",
    titulares: (team?.starters || []).map((p) => {
      const layout = p.horizontalLayout || {};
      const px = num(layout.x);
      const py = num(layout.y);
      return {
        nombre: p.name || "",
        dorsal: p.shirtNumber ?? "",
        foto: playerPhoto(p.id),
        // FotMob da las x de cada equipo desde su propia portería, así que ambos onces
        // se solapaban en el centro. Cada equipo se comprime en su mitad del campo.
        x: px == null ? null : mirror ? 0.95 - px * 0.4 : 0.05 + px * 0.4,
        y: py == null ? null : 0.08 + py * 0.84,
        nota: p.performance?.rating != null ? `nota ${p.performance.rating}` : "",
      };
    }),
    suplentes: (team?.subs || []).map((p) => ({
      nombre: p.name || "",
      dorsal: p.shirtNumber ?? "",
      foto: playerPhoto(p.id),
      rol: p.performance?.rating != null ? `nota ${p.performance.rating}` : "",
    })),
    bajas: (team?.unavailable || []).map((p) => ({
      nombre: p.name || "",
      motivo:
        p.unavailability?.type === "injury"
          ? ["lesión", p.unavailability?.expectedReturn].filter(Boolean).join(" · ")
          : p.unavailability?.type || "baja",
    })),
  });

  return {
    ok: true,
    tipo: detail?.general?.started ? "oficial" : "probable",
    respaldo: detail?.__respaldoAlineacion || null,
    local: side(lineup.homeTeam, false),
    visita: side(lineup.awayTeam, true),
    stats_jugador: playersView(detail),
  };
}

function standingsView(page) {
  const table = page?.table?.[0]?.data;
  const rows = table?.table?.all || [];
  if (!rows.length) return { ok: false, motivo: "Esta competición no tiene clasificación disponible." };

  const legend = table.legend || [];
  const equipos = rows.map((row) => {
    const [gf, gc] = String(row.scoresStr || "").split("-").map((v) => v.trim());
    return {
      team: row.name || "",
      logo: teamLogo(row.id),
      pj: row.played,
      g: row.wins,
      e: row.draws,
      p: row.losses,
      gf,
      gc,
      dif: row.goalConDiff,
      pts: row.pts,
      forma: [],
      nota: legend.find((l) => l.color && l.color === row.qualColor)?.title || "",
    };
  });

  return {
    ok: true,
    liga: table.leagueName || "",
    grupos: [{ nombre: "", equipos }],
    actualizado: [table.selectedSeason, table.ccode].filter(Boolean).join(" · "),
  };
}

const loadStats = (id) => loadDetail(id).then(statsView);
const loadExtras = (id) => loadDetail(id).then(extrasView);
const loadLineup = (id) => loadDetail(id).then(lineupView);
const loadStandings = (league) =>
  memo(`standings:${league}`, () => api(`/leagues?id=${encodeURIComponent(league)}`).then(standingsView));

/* ---------- datos del listado ---------- */
async function load() {
  el.subtitle.textContent = "Cargando…";

  let day = null;
  let fallo = null;
  try {
    day = await fetchDay(state.offset);
  } catch (err) {
    fallo = err;
  }

  let desdeMongo = false;
  if (!day || !day.games.length) {
    // Respaldo: la jornada que quedó guardada en MongoDB la última vez.
    const guardado = await dbGet(`/partidos?fecha=${dayKey(state.offset)}`);
    if (guardado?.partidos?.length) {
      day = { games: guardado.partidos.map(mapStoredMatch), leagues: ligasGuardadas(guardado.partidos) };
      desdeMongo = true;
    }
  }

  if (!day) throw fallo || new Error("No hay datos disponibles para este día");

  state.fromDb = desdeMongo;
  state.dayGames = day.games;
  state.leagues = [...day.leagues].sort((a, b) => a.name.localeCompare(b.name));
  state.games = state.league ? day.games.filter((g) => g.league_code === state.league) : day.games;
  state.liveCount = state.games.filter((g) => g.state === "in").length;
  state.updatedAt = new Date().toISOString();
  state.error = null;

  renderLeagueOptions();
  render();

  if (state.view === "home" && !state.highlightsDone && !state.fromDb) {
    state.highlightsDone = true;
    loadHighlights(state.games);
  }
}

/* ---------- helpers de partido ---------- */
function hue(str) {
  let h = 0;
  for (const ch of String(str || "?")) h = (h * 31 + ch.charCodeAt(0)) % 360;
  return h;
}

function initials(name) {
  return String(name || "?")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0])
    .join("")
    .toUpperCase();
}

function avatar(name, cls = "") {
  return `<span class="badge-avatar ${cls}" style="background:hsl(${hue(name)} 62% 63%)">${esc(
    initials(name) || "?"
  )}</span>`;
}

function logoHTML(team, cls = "") {
  const fallback = avatar(team?.name, cls);
  if (!team?.logo) return fallback;
  return `<span class="team-logo ${cls}">${fallback}<img src="${esc(
    team.logo
  )}" alt="" loading="lazy" onerror="this.remove()"></span>`;
}

function kickoff(date) {
  if (!date) return "--:--";
  const d = new Date(date);
  if (Number.isNaN(d.getTime())) return "--:--";
  return d.toLocaleTimeString("es-ES", { hour: "2-digit", minute: "2-digit" });
}

function longDate(date) {
  if (!date) return "";
  const d = new Date(date);
  if (Number.isNaN(d.getTime())) return "";
  const text = d.toLocaleDateString("es-ES", {
    weekday: "short",
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function statusOf(g) {
  if (g.state === "in") return { text: g.clock || g.status || "EN VIVO", cls: "live" };
  if (g.state === "post") return { text: g.status || "FINAL", cls: "finished" };
  return { text: kickoff(g.date), cls: "" };
}

function scoreOf(g) {
  const h = g.home?.score;
  const a = g.away?.score;
  if (g.state === "pre" || h == null || a == null) return "–";
  return `${h} - ${a}`;
}

function winnerSide(g) {
  const h = Number(g.home?.score);
  const a = Number(g.away?.score);
  if (g.state !== "post" || Number.isNaN(h) || Number.isNaN(a) || h === a) return null;
  return h > a ? "home" : "away";
}

function rowHTML(g) {
  const st = statusOf(g);
  const live = st.cls === "live";
  const win = winnerSide(g);
  const plain = g.state === "pre";

  return `
    <div class="row ${live ? "is-live" : ""}" role="link" tabindex="0" data-id="${esc(
    g.id
  )}" data-league="${esc(g.league_code || "")}">
      <div class="status ${st.cls}">${esc(st.text)}</div>
      <div class="team home ${win === "home" ? "winner" : ""}">
        ${logoHTML(g.home)}
        <span class="name">${esc(g.home?.name || "Local")}</span>
      </div>
      <div class="score ${live ? "live" : ""} ${plain ? "plain" : ""}">${esc(scoreOf(g))}</div>
      <div class="team away ${win === "away" ? "winner" : ""}">
        <span class="name">${esc(g.away?.name || "Visitante")}</span>
        ${logoHTML(g.away)}
      </div>
    </div>`;
}

const matchesQuery = (g) => {
  const q = state.query.trim().toLowerCase();
  if (!q) return true;
  return [g.home?.name, g.away?.name, g.league, g.league_code]
    .filter(Boolean)
    .some((v) => String(v).toLowerCase().includes(q));
};

function groupByLeague(games) {
  const groups = new Map();
  for (const g of games) {
    const name = g.league || "Otras competiciones";
    if (!groups.has(name)) groups.set(name, []);
    groups.get(name).push(g);
  }
  for (const list of groups.values()) {
    list.sort(
      (a, b) =>
        (a.state === "in" ? -1 : 0) - (b.state === "in" ? -1 : 0) ||
        String(a.date || "").localeCompare(String(b.date || ""))
    );
  }
  return [...groups.entries()].sort(
    (a, b) => b[1].length - a[1].length || a[0].localeCompare(b[0])
  );
}

/* ---------- render del listado ---------- */
function render() {
  const visible = state.games.filter(matchesQuery);
  const liveGames = visible.filter((g) => g.state === "in");
  const liveIds = new Set(liveGames.map((g) => g.id));

  el.title.textContent =
    state.offset === 0 ? "Partidos de hoy" : state.offset === -1 ? "Partidos de ayer" : "Partidos de mañana";

  const updated = state.updatedAt
    ? new Date(state.updatedAt).toLocaleTimeString("es-ES", { hour: "2-digit", minute: "2-digit" })
    : null;
  el.subtitle.textContent = `${humanDay(state.offset)} · ${visible.length} partido${
    visible.length === 1 ? "" : "s"
  }${updated ? ` · actualizado ${updated}` : ""}${state.fromDb ? " · desde MongoDB" : ""}`;
  el.liveCount.textContent = state.liveCount;

  if (liveGames.length) {
    el.liveBox.classList.remove("hidden");
    el.liveList.innerHTML = liveGames.map(rowHTML).join("");
  } else {
    el.liveBox.classList.add("hidden");
    el.liveList.innerHTML = "";
  }

  const listed = visible.filter((g) => !liveIds.has(g.id));
  el.list.innerHTML = listed.length
    ? groupByLeague(listed)
        .map(
          ([name, games]) => `
        <section class="league">
          <div class="league-head">
            ${avatar(name)}
            <span>${esc(name)}</span>
            <span class="count">${games.length} partido${games.length === 1 ? "" : "s"}</span>
          </div>
          ${games.map(rowHTML).join("")}
        </section>`
        )
        .join("")
    : "";

  const empty = !visible.length;
  el.empty.classList.toggle("hidden", !empty);
  if (empty) {
    el.empty.innerHTML = `<strong>Sin partidos para mostrar</strong><span>${
      state.error ? esc(state.error) : "La API no devolvió partidos para este día y esta competición."
    }</span>`;
  }

  renderKpis(visible);
  renderLeagueChart(visible);
  renderLeagueList();
}

function renderKpis(games) {
  const live = games.filter((g) => g.state === "in").length;
  const done = games.filter((g) => g.state === "post").length;
  const soon = games.filter((g) => g.state === "pre").length;

  el.kpis.innerHTML = `
    <div class="kpi"><span>Partidos</span><b>${games.length}</b></div>
    <div class="kpi done"><span>Finalizados</span><b>${done}</b></div>
    <div class="kpi live"><span>En vivo</span><b>${live}</b></div>
    <div class="kpi"><span>Próximos</span><b>${soon}</b></div>`;
}

function barsHTML(groups) {
  const max = Math.max(1, ...groups.map(([, list]) => list.length));
  return `<ul class="leagues">${groups
    .map(
      ([name, list]) => `
      <li>
        <span class="name">${esc(name)}</span>
        <span class="bar-track"><span class="bar-fill" style="width:${(list.length / max) * 100}%"></span></span>
        <span class="num">${list.length}</span>
      </li>`
    )
    .join("")}</ul>`;
}

function renderLeagueChart(games) {
  const groups = groupByLeague(games).slice(0, 8);
  el.chart.innerHTML = groups.length
    ? barsHTML(groups)
    : `<p class="rail-empty">Sin partidos para este día</p>`;
}

function renderLeagueList() {
  const counts = new Map();
  for (const g of state.dayGames || state.games) {
    const name = g.league || "Otras";
    counts.set(name, (counts.get(name) || 0) + 1);
  }

  const all = [{ code: "", name: "Todas las ligas" }, ...state.leagues];
  el.leagues.innerHTML = all
    .map(
      (l) => `
      <li>
        <button type="button" class="league-chip ${state.league === l.code ? "active" : ""}" data-code="${esc(l.code)}">
          <span class="name">${esc(l.name)}</span>
          <span class="num">${l.code ? counts.get(l.name) || 0 : state.games.length}</span>
        </button>
      </li>`
    )
    .join("");

  el.leagues.querySelectorAll(".league-chip").forEach((btn) => {
    btn.addEventListener("click", () => {
      state.league = btn.dataset.code;
      el.leagueFilter.value = state.league;
      load().catch(showError);
    });
  });
}

function renderLeagueOptions() {
  const current = state.league;
  el.leagueFilter.innerHTML =
    `<option value="">Todas las ligas</option>` +
    state.leagues.map((l) => `<option value="${esc(l.code)}">${esc(l.name)}</option>`).join("");
  el.leagueFilter.value = current;
}

/* ---------- destacados del día ---------- */
async function loadHighlights(games) {
  const picks = [...games.filter((g) => g.state === "in"), ...games.filter((g) => g.state === "pre")].slice(0, 4);
  if (!picks.length) {
    el.highlights.classList.add("hidden");
    return;
  }

  el.highlights.classList.remove("hidden");
  el.highlights.innerHTML = `<div class="hl-loading">Buscando lo clave del día…</div>`;

  const results = await Promise.allSettled(picks.map((g) => loadExtras(g.id, g.league_code)));

  const cards = [];
  results.forEach((r, i) => {
    if (r.status !== "fulfilled" || !r.value || r.value.ok === false) return;
    const insights = (r.value.insights || []).filter((x) => x?.texto).slice(0, 2);
    if (!insights.length) return;
    const g = picks[i];
    const st = statusOf(g);
    cards.push(`
      <article class="hl-card" role="link" tabindex="0" data-id="${esc(g.id)}" data-league="${esc(
      g.league_code || ""
    )}">
        <div class="hl-label">
          <span>${esc(g.league || g.league_code || "")}</span>
          <span class="hl-time ${st.cls}">${esc(st.text)}</span>
        </div>
        <div class="hl-teams">${esc(g.home?.name || "")} <em>vs</em> ${esc(g.away?.name || "")}</div>
        <ul class="hl-text">${insights.map((x) => `<li>${esc(x.texto)}</li>`).join("")}</ul>
      </article>`);
  });

  if (!cards.length) {
    el.highlights.classList.add("hidden");
    el.highlights.innerHTML = "";
    return;
  }

  el.highlights.innerHTML = `
    <div class="hl-head"><h2>Destacados del día</h2><span class="muted">según los datos del partido</span></div>
    <div class="hl-strip">${cards.join("")}</div>`;
}

/* ---------- router ---------- */
function parseHash() {
  const m = String(location.hash || "").match(/^#\/p\/([^/]+)(?:\/([^/?#]+))?/);
  if (!m) return null;
  return { id: decodeURIComponent(m[1]), league: m[2] ? decodeURIComponent(m[2]) : "" };
}

function go(id, league) {
  location.hash = `#/p/${encodeURIComponent(id)}${league ? `/${encodeURIComponent(league)}` : ""}`;
}

function showHome() {
  state.view = "home";
  state.game = null;
  el.detail.classList.add("hidden");
  el.detail.innerHTML = "";
  el.ligasView.classList.add("hidden");
  el.home.classList.remove("hidden");
  if (!state.games.length) load().catch(showError);
  else render();
}

async function openDetail(id, league) {
  state.view = "detail";
  state.tab = "analisis";
  el.home.classList.add("hidden");
  el.detail.classList.remove("hidden");
  el.detail.innerHTML = loadingHTML("Cargando partido…");
  window.scrollTo({ top: 0, behavior: "auto" });

  try {
    const game = await loadGame(id, league);
    if (state.view !== "detail" || parseHash()?.id !== id) return;
    state.game = { id, league: league || game?.league_code || "", raw: game };
    renderDetailShell(game);
    showTab("analisis");
  } catch (err) {
    el.detail.innerHTML = `<div class="detail-top"><a class="ghost-btn" href="#/">← Volver</a></div>${emptyHTML(
      "No se pudo abrir el partido",
      err.message
    )}`;
  }
}

function route() {
  if (String(location.hash || "").startsWith("#/ligas")) {
    showLigas();
    return;
  }

  const target = parseHash();
  if (target) openDetail(target.id, target.league);
  else showHome();
}

/* ---------- catálogo de ligas: todas las que soporta el visor ---------- */
const ligasState = { modo: "todas", pais: "", q: "", ligas: [], top: [], cargando: false };

async function cargarCatalogo() {
  ligasState.cargando = true;

  let data = await dbGet("/ligas-soportadas?limite=1000");

  if (!data?.ligas?.length) {
    // Primera vez: se pide el catálogo al puente y queda archivado en LigasSoportadas.
    try {
      await api("/allLeagues");
    } catch {}
    data = await dbGet("/ligas-soportadas?limite=1000");
  }

  ligasState.ligas = data?.ligas || [];
  ligasState.top = (await dbGet("/top-ligas"))?.ligas || [];
  ligasState.cargando = false;
}

async function showLigas() {
  state.view = "ligas";
  el.home.classList.add("hidden");
  el.detail.classList.add("hidden");
  el.ligasView.classList.remove("hidden");
  window.scrollTo({ top: 0, behavior: "auto" });

  if (!ligasState.ligas.length) {
    el.ligasView.innerHTML = loadingHTML("Cargando el catálogo de ligas…");
    await cargarCatalogo();
  }

  renderLigas();
}

function ligasFiltradas() {
  const q = ligasState.q.trim().toLowerCase();
  let lista = ligasState.ligas;

  if (ligasState.modo === "top5") {
    const ids = new Set(ligasState.top.map((t) => Number(t.ligaId)));
    lista = lista.filter((l) => ids.has(Number(l.ligaId)));
  }

  if (ligasState.pais) lista = lista.filter((l) => (l.paisNombre || l.pais) === ligasState.pais);

  if (q)
    lista = lista.filter(
      (l) =>
        String(l.nombre || "").toLowerCase().includes(q) ||
        String(l.paisNombre || l.pais || "").toLowerCase().includes(q)
    );

  return lista;
}

function renderLigas() {
  const lista = ligasFiltradas();
  const top = new Map(ligasState.top.map((t) => [Number(t.ligaId), t]));
  const paises = [...new Set(ligasState.ligas.map((l) => l.paisNombre || l.pais))]
    .filter(Boolean)
    .sort((a, b) => a.localeCompare(b, "es"));

  const grupos = new Map();
  for (const liga of lista) {
    const clave =
      ligasState.modo === "top5" ? "Ligas oficiales" : liga.paisNombre || liga.pais || "Internacional";
    if (!grupos.has(clave)) grupos.set(clave, []);
    grupos.get(clave).push(liga);
  }

  const orden = (l) => top.get(Number(l.ligaId))?.prioridad ?? 99;

  const filas = [...grupos.entries()]
    .sort((a, b) => (ligasState.modo === "top5" ? 0 : a[0].localeCompare(b[0], "es")))
    .map(
      ([pais, ligas]) => `
      <section class="ligas-group">
        <h3><span>${esc(pais)}</span><span class="count">${ligas.length} competicion${
        ligas.length === 1 ? "" : "es"
      }</span></h3>
        ${ligas
          .slice()
          .sort((a, b) => orden(a) - orden(b) || a.nombre.localeCompare(b.nombre, "es"))
          .map((liga) => {
            const oficial = top.get(Number(liga.ligaId));
            return `<button type="button" class="ligas-row" data-liga="${esc(liga.ligaId)}">
              <span class="ligas-name">${esc(liga.nombre)}</span>
              ${oficial ? `<span class="ligas-badge">Top ${oficial.prioridad}</span>` : ""}
              <span class="ligas-pais">${esc(liga.pais || "")}</span>
            </button>`;
          })
          .join("")}
      </section>`
    )
    .join("");

  el.ligasView.innerHTML = `
    <div class="content-head">
      <div>
        <h1>Ligas soportadas</h1>
        <p class="muted">${lista.length} de ${ligasState.ligas.length} competiciones · guardadas en MongoDB (LigasSoportadas)</p>
      </div>
      <a class="ghost-btn" href="#/">← Volver</a>
    </div>

    <div class="ligas-topbar">
      <div class="ligas-modes">
        <button type="button" data-modo="todas" class="${ligasState.modo === "todas" ? "active" : ""}">Todas</button>
        <button type="button" data-modo="top5" class="${ligasState.modo === "top5" ? "active" : ""}">Top 5 oficiales</button>
      </div>
      <select id="ligasPais">
        <option value="">Todos los países</option>
        ${paises
          .map((p) => `<option value="${esc(p)}" ${ligasState.pais === p ? "selected" : ""}>${esc(p)}</option>`)
          .join("")}
      </select>
      <input id="ligasBuscar" type="search" placeholder="Buscar liga o país" value="${esc(
        ligasState.q
      )}" autocomplete="off">
    </div>

    ${filas || emptyHTML("Sin competiciones", "No hay ligas que coincidan con la búsqueda.")}
  `;

  el.ligasView.querySelectorAll("[data-modo]").forEach((btn) =>
    btn.addEventListener("click", () => {
      ligasState.modo = btn.dataset.modo;
      renderLigas();
    })
  );

  el.ligasView.querySelector("#ligasPais")?.addEventListener("change", (e) => {
    ligasState.pais = e.target.value;
    renderLigas();
  });

  el.ligasView.querySelector("#ligasBuscar")?.addEventListener("input", (e) => {
    ligasState.q = e.target.value;
    renderLigas();
    const campo = el.ligasView.querySelector("#ligasBuscar");
    campo?.focus();
    campo?.setSelectionRange(campo.value.length, campo.value.length);
  });

  el.ligasView.querySelectorAll(".ligas-row").forEach((btn) =>
    btn.addEventListener("click", () => {
      state.league = btn.dataset.liga;
      state.highlightsDone = false;
      el.leagueFilter.value = state.league;
      location.hash = "#/";
    })
  );
}

/* ---------- cabecera del partido ---------- */
function sidesOf(game) {
  const list = game?.teams || [];
  const home = list.find((t) => t.homeAway === "home") || list[0] || {};
  const away = list.find((t) => t.homeAway === "away") || list[1] || {};
  return { home, away };
}

function detailStatus(game, raw) {
  const stateKey = raw?.state || game?.state;
  if (stateKey === "in") return { text: raw?.clock || game?.clock || "EN VIVO", cls: "live" };
  if (stateKey === "post") return { text: raw?.status || game?.status || "FINAL", cls: "finished" };
  return { text: kickoff(raw?.date || game?.date), cls: "" };
}

function subStatusHTML(game, st) {
  const raw = (game?.status || "").toString().trim();
  if (!raw || raw.toLowerCase() === st.text.toLowerCase()) return "";
  return `<div class="sb-date muted">${esc(raw)}</div>`;
}

function renderDetailShell(game) {
  const { home, away } = sidesOf(game);
  const st = detailStatus(state.game?.raw, game);
  const h = home.score;
  const a = away.score;
  const score = h == null || a == null ? "–" : `${h} - ${a}`;
  const leagueName = game?.league || state.game?.league || "";

  el.detail.innerHTML = `
    <div class="detail-top">
      <a class="ghost-btn" href="#/">← Volver</a>
      <span class="crumb">${esc(leagueName)}</span>
      <span class="muted sb-updated">${esc(longDate(game?.date))}</span>
    </div>

    <div class="scoreboard">
      <div class="sb-team">
        ${logoHTML(home, "lg")}
        <span class="sb-name">${esc(home.name || "Local")}</span>
        <span class="sb-record muted">${esc(home.records?.[0]?.summary || home.record || "")}</span>
      </div>
      <div class="sb-center">
        <div class="sb-score ${home.winner ? "w" : ""}">${esc(score)}</div>
        <div class="sb-state ${st.cls}">${esc(st.text)}</div>
        ${subStatusHTML(game, st)}
      </div>
      <div class="sb-team">
        ${logoHTML(away, "lg")}
        <span class="sb-name">${esc(away.name || "Visitante")}</span>
        <span class="sb-record muted">${esc(away.records?.[0]?.summary || away.record || "")}</span>
      </div>
    </div>

    <nav class="tabs" id="tabs">
      ${[
        ["analisis", "Análisis"],
        ["radar", "Radar"],
        ["estadisticas", "Estadísticas"],
        ["alineaciones", "Alineaciones"],
        ["jugadores", "Jugadores"],
        ["tabla", "Tabla"],
        ["h2h", "Enfrentamientos"],
      ]
        .map(
          ([key, label]) =>
            `<button type="button" data-tab="${key}" class="${
              key === "analisis" ? "active" : ""
            }">${label}</button>`
        )
        .join("")}
    </nav>

    <div id="panel" class="panel"></div>`;

  el.detail.querySelectorAll("#tabs button").forEach((btn) => {
    btn.addEventListener("click", () => showTab(btn.dataset.tab));
  });
}

async function showTab(tab) {
  if (!state.game) return;
  stopRadar();
  state.tab = tab;
  el.detail.querySelectorAll("#tabs button").forEach((b) => b.classList.toggle("active", b.dataset.tab === tab));
  const panel = $("#panel");
  panel.innerHTML = loadingHTML();

  const { id, league } = state.game;
  let html;
  try {
    if (tab === "analisis") html = await panelAnalisis(id, league);
    else if (tab === "radar") html = await panelRadar(id, league);
    else if (tab === "estadisticas") html = await panelEstadisticas(id, league);
    else if (tab === "alineaciones") html = await panelAlineaciones(id, league);
    else if (tab === "jugadores") html = await panelJugadores(id, league);
    else if (tab === "tabla") html = await panelTabla(league);
    else html = await panelH2H();
  } catch (err) {
    html = emptyHTML("No se pudo cargar esta pestaña", err.message);
  }

  if (state.tab !== tab || !document.body.contains(panel)) return;
  panel.innerHTML = html;
}

/* ---------- pestaña: análisis ---------- */
function playerCard(player, fallback) {
  if (!player) return "";
  const rows = (player.filas || [])
    .map(
      (f) => `<li><span>${esc(pick(f, ["etiqueta", "label", "titulo"]) || "")}</span><b>${esc(
        txt(f?.valor)
      )}</b></li>`
    )
    .join("");
  return `
    <div class="scorer">
      <div class="scorer-head">
        ${player.foto ? `<img src="${esc(player.foto)}" alt="" loading="lazy" onerror="this.remove()">` : avatar(player.nombre)}
        <div>
          <b>${esc(player.nombre || fallback)}</b>
          <small>${esc(player.completo || "")}</small>
        </div>
      </div>
      <ul class="kv">${rows}</ul>
    </div>`;
}

function formChips(list, teamName) {
  const chips = (list || [])
    .map((f) => {
      const r = String(f?.resultado || "").toUpperCase().slice(0, 1);
      const cls = r === "W" ? "w" : r === "L" ? "l" : "d";
      return `<span class="chip ${cls}" title="${esc(f?.rival || "")}${f?.fecha ? ` · ${esc(f.fecha)}` : ""}">${esc(
        r || "-"
      )}</span>`;
    })
    .join("");
  return `
    <div class="form-col">
      <span class="form-name">${esc(teamName)}</span>
      <div class="form-chips">${chips || `<span class="muted">Sin datos</span>`}</div>
    </div>`;
}

async function panelAnalisis(id, league) {
  const data = await loadExtras(id, league);
  if (!data || data.ok === false) {
    return emptyHTML("Sin análisis disponible", data?.motivo || "El backend no devolvió datos de este partido.");
  }

  const { home, away } = sidesOf(state.game?.raw);
  const insights = (data.insights || []).filter((i) => i?.texto);
  const forma = data.forma || [];
  const goleadores = data.goleadores;
  const arbitro = data.arbitro;

  const insightHTML = insights.length
    ? `<section class="dcard"><h3>Lo clave del partido</h3>
        <ul class="insight-list">${insights
          .map(
            (i) =>
              `<li class="insight" style="--dot:${esc(i.color || "#22d07a")}">${esc(i.texto)}</li>`
          )
          .join("")}</ul></section>`
    : "";

  const scorersHTML =
    goleadores && (goleadores.local || goleadores.visita)
      ? `<section class="dcard"><h3>${esc(goleadores.titulo || "Goleadores")}</h3>
          <div class="scorer-grid">
            ${playerCard(goleadores.local, home?.name)}
            ${playerCard(goleadores.visita, away?.name)}
          </div></section>`
      : "";

  const formHTML = forma.length
    ? `<section class="dcard"><h3>Forma reciente</h3>
        <div class="form-grid">
          ${formChips(forma[0], home?.name || "Local")}
          ${formChips(forma[1], away?.name || "Visitante")}
        </div></section>`
    : "";

  let refHTML = "";
  if (arbitro && (arbitro.nombre || arbitro.stats)) {
    const stats = arbitro.stats || {};
    const rows = Object.entries(stats)
      .map(([key, val]) => {
        if (!val || typeof val !== "object") return "";
        const label = key
          .replace(/([A-Z])/g, " $1")
          .replace(/^./, (c) => c.toUpperCase())
          .trim();
        const detail = [val.media != null ? `media ${txt(val.media)}` : null, val.vs != null ? `vs ${txt(val.vs)}` : null]
          .filter(Boolean)
          .join(" · ");
        return `<li><span>${esc(label)}${detail ? `<small>${esc(detail)}</small>` : ""}</span><b>${esc(
          txt(val.valor)
        )}${val.pct != null ? `<i>${esc(txt(val.pct))}%</i>` : ""}</b></li>`;
      })
      .join("");

    refHTML = `<section class="dcard"><h3>Árbitro</h3>
      <div class="referee">
        ${
          arbitro.bandera
            ? `<img class="ref-flag" src="${esc(arbitro.bandera)}" alt="" loading="lazy" onerror="this.remove()">`
            : avatar(arbitro.nombre)
        }
        <div class="ref-meta">
          <b>${esc(arbitro.nombre || "Sin asignar")}</b>
          <small class="muted">${esc([arbitro.pais, arbitro.liga].filter(Boolean).join(" · "))}</small>
        </div>
      </div>
      ${rows ? `<ul class="kv">${rows}</ul>` : ""}
    </section>`;
  }

  const venueHTML =
    data.estadio || data.publico
      ? `<section class="dcard"><h3>Partido</h3><ul class="kv">
          ${data.estadio ? `<li><span>Estadio</span><b>${esc(data.estadio)}</b></li>` : ""}
          ${data.publico ? `<li><span>Asistencia</span><b>${esc(data.publico)}</b></li>` : ""}
        </ul></section>`
      : "";

  const body = insightHTML + scorersHTML + formHTML + refHTML + venueHTML;
  return body || emptyHTML("Sin análisis disponible", "El backend respondió sin insights para este partido.");
}

/* ---------- pestaña: estadísticas ---------- */
function statRow(label, localVal, awayVal) {
  const l = num(localVal);
  const a = num(awayVal);
  const total = (l || 0) + (a || 0);
  const lp = total ? ((l || 0) / total) * 100 : 50;
  return `
    <div class="stat-row">
      <div class="stat-vals"><b>${esc(txt(localVal))}</b><span>${esc(label)}</span><b>${esc(txt(awayVal))}</b></div>
      <div class="stat-bars">
        <span class="track"><span class="fill h" style="width:${lp}%"></span></span>
        <span class="track"><span class="fill a" style="width:${100 - lp}%"></span></span>
      </div>
    </div>`;
}

function momentumSVG(momento) {
  const points = (momento || []).filter((m) => num(m?.valor) != null);
  if (points.length < 2) return "";
  const max = Math.max(1, ...points.map((p) => Math.abs(num(p.valor))));
  const step = 100 / (points.length - 1);
  const mid = 50;
  const scale = 42;

  const pts = points.map((p, i) => {
    const v = num(p.valor) / max;
    return `${(i * step).toFixed(2)},${(mid - v * scale).toFixed(2)}`;
  });

  const area = `0,${mid} ${pts.join(" ")} 100,${mid}`;
  const lastMinute = points[points.length - 1]?.minuto;
  const firstMinute = points[0]?.minuto;

  return `
    <div class="momentum">
      <div class="momentum-head"><span>Dominio del partido</span><span class="muted">${esc(
        String(firstMinute ?? "")
      )}' – ${esc(String(lastMinute ?? ""))}'</span></div>
      <svg class="momentum-svg" viewBox="0 0 100 100" preserveAspectRatio="none" role="img" aria-label="Momentum por minuto">
        <line x1="0" y1="50" x2="100" y2="50" stroke="rgba(255,255,255,.18)" stroke-width="0.4" />
        <polyline points="${area}" fill="rgba(34,208,122,.18)" stroke="none" />
        <polyline points="${pts.join(" ")}" fill="none" stroke="#22d07a" stroke-width="0.9" vector-effect="non-scaling-stroke" />
      </svg>
      <div class="momentum-legend"><span class="h">Local</span><span class="a">Visitante</span></div>
    </div>`;
}

function shotMapHTML(tiros, homeName, awayName) {
  if (!tiros || tiros.length < 3) return "";

  const raw = state.game?.raw;
  const colorOf = (side) =>
    raw?.teams?.find((t) => t.homeAway === side)?.color || (side === "home" ? "#22d07a" : "#3a7aff");
  const homeColor = colorOf("home");
  const awayColor = colorOf("away");
  const height = 64;

  const dots = tiros
    .map((t) => {
      const color = t.local ? homeColor : awayColor;
      const radio = Math.max(1.1, Math.min(4.2, 1.1 + (t.xg || 0) * 3.4));
      const title = `${t.minuto != null ? `${t.minuto}' ` : ""}${t.jugador}${t.gol ? " · gol" : t.aPuerta ? " · a puerta" : ""}`;
      // FotMob normaliza los tiros de los dos equipos hacia la misma portería:
      // el visitante se refleja para que cada lado ataque su portería.
      const x = Math.min(99, Math.max(1, t.local ? t.x : 100 - t.x));
      return `<circle cx="${x.toFixed(1)}" cy="${(t.y * (height / 100)).toFixed(1)}" r="${radio.toFixed(1)}" fill="${esc(
        color
      )}" fill-opacity="${t.gol ? 0.95 : 0.45}" stroke="${t.gol ? "#fff" : "none"}" stroke-width="${t.gol ? 0.7 : 0}"><title>${esc(
        title
      )}</title></circle>`;
    })
    .join("");

  const line = 'stroke="rgba(255,255,255,.18)" stroke-width="0.4" fill="none"';
  return `
    <section class="dcard"><h3>Mapa de tiros</h3>
      <div class="shotmap">
        <svg viewBox="0 0 100 ${height}" preserveAspectRatio="none" role="img" aria-label="Mapa de tiros">
          <line x1="50" y1="0" x2="50" y2="${height}" ${line} />
          <circle cx="50" cy="${height / 2}" r="9" ${line} />
          <rect x="0" y="20" width="16" height="24" ${line} />
          <rect x="0" y="27" width="5.5" height="10" ${line} />
          <rect x="84" y="20" width="16" height="24" ${line} />
          <rect x="94.5" y="27" width="5.5" height="10" ${line} />
          ${dots}
        </svg>
      </div>
      <div class="shotmap-legend">
        <span><i style="background:${esc(homeColor)}"></i>${esc(homeName || "Local")}</span>
        <span><i style="background:${esc(awayColor)}"></i>${esc(awayName || "Visitante")}</span>
        <span>${tiros.length} tiros · tamaño = xG · borde blanco = gol</span>
      </div>
    </section>`;
}

/* ---------- pestaña: radar (widget de Sportradar, el mismo que usa FotMob) ---------- */
// FotMob no dibuja el radar: monta el "Live Match Tracker" de Sportradar con
// su SDK. Aquí se hace igual, así que el campo, el balón y las acciones se
// mueven solos (el widget pide match_timelinedelta cada pocos segundos).
const SIR_KEY = "44ea55a6918bd96cab0f7c854ad171ab";
const SIR_LOADER = `https://widgets.sir.sportradar.com/${SIR_KEY}/widgetloader`;
const SIR_CONFIG = {
  vlmtForce2d: true,
  pitchView: "top",
  vlmtLightPitchView: "top",
  layout: "topdown",
  disableWidgetHeader: true,
  disableComponents: false,
  momentum: "disable",
  scoreboard: "disable",
  disableFooterStats: true,
  disableEventStats: true,
  tabs: { 1: ["statistics"] },
  tabsPosition: "disable",
  collapseTo: "pitch",
  expanded: false,
  hideExpand: true,
  disableOverlayPanels: true,
  vlmtEnableMilestones: false,
  detailedScoreboard: "disable",
};

let sirCarga = null;
let radarHost = null;

function cargarSIR() {
  if (window.SIR && !window.SIR.q) return Promise.resolve(true);
  if (sirCarga) return sirCarga;
  if (!window.SIR) {
    const stub = function () {
      (stub.q = stub.q || []).push(arguments);
    };
    stub.l = Date.now();
    window.SIR = stub;
  }
  sirCarga = new Promise((resolve) => {
    const s = document.createElement("script");
    s.async = true;
    s.src = SIR_LOADER;
    s.dataset.sirLoader = "1";
    const esperar = (ms) => {
      const t0 = Date.now();
      const t = setInterval(() => {
        const listo = window.SIR && !window.SIR.q;
        if (listo || Date.now() - t0 > ms) {
          clearInterval(t);
          resolve(listo);
        }
      }, 80);
    };
    s.onload = () => esperar(4000);
    s.onerror = () => esperar(1000);
    document.head.appendChild(s);
  });
  return sirCarga;
}

/** Suelta el widget al cambiar de pestaña o de partido (si no, sigue pidiendo datos). */
function stopRadar() {
  const host = radarHost;
  radarHost = null;
  if (!host) return;
  try {
    window.SIR && window.SIR("removeWidget", host);
  } catch {
    /* el widget ya no existía */
  }
}

function srMatchId(detail) {
  const url = detail?.content?.superlive?.superLiveUrl || "";
  const m = String(url).match(/srmatchid=sr:match:(\d+)/);
  return m ? m[1] : "";
}

async function loadRadar(sr) {
  try {
    const res = await fetch(`/api/radar/${encodeURIComponent(sr)}`, { headers: { Accept: "application/json" } });
    const data = await res.json().catch(() => null);
    if (!res.ok) return { ok: false, motivo: data?.message || `La API respondió ${res.status}` };
    return data;
  } catch (err) {
    return { ok: false, motivo: err.message };
  }
}

async function montarLMT(sr) {
  let host = null;
  for (let i = 0; i < 60 && !host; i++) {
    if (state.tab !== "radar" || state.view !== "detail") return;
    host = document.querySelector("#panel .lmt-host");
    if (!host) await new Promise((r) => setTimeout(r, 50));
  }
  if (!host) return;

  const listo = await cargarSIR();
  if (!host.isConnected || state.tab !== "radar") return;
  if (!listo) {
    host.innerHTML = `<p class="muted small">No se pudo cargar el radar de Sportradar. Revisa la conexión y recarga.</p>`;
    return;
  }

  stopRadar();
  radarHost = host;
  try {
    window.SIR("addWidget", host, "match.lmtPlus", { ...SIR_CONFIG, matchId: Number(sr), lang: "es" });
  } catch (err) {
    host.innerHTML = `<p class="muted small">El radar no arrancó: ${esc(err.message)}</p>`;
  }
}

async function panelRadar(id) {
  const detail = await loadDetail(id);
  const sr = srMatchId(detail);
  if (!sr) {
    return emptyHTML(
      "Sin radar en vivo",
      "FotMob no publica el radar de este partido: solo lo tiene en los que sigue en directo con datos de Sportradar."
    );
  }

  const data = await loadRadar(sr);
  if (!data?.ok) {
    return emptyHTML("Sin radar en vivo", data?.motivo || "El radar no devolvió datos de este partido.");
  }

  const local = data.local || {};
  const visita = data.visitante || {};
  const raw = state.game?.raw;
  const colorOf = (side) =>
    raw?.teams?.find((t) => t.homeAway === side)?.color || (side === "home" ? "#22d07a" : "#3a7aff");
  const marcador =
    local.marcador != null && visita.marcador != null ? `${local.marcador} - ${visita.marcador}` : "–";
  const reloj = data.minuto != null ? `${data.minuto}'` : data.estado || "";

  setTimeout(() => montarLMT(sr), 0);

  return `
    <section class="dcard radar-live">
      <h3>Radar en vivo ${data.enVivo ? '<span class="rp-live"><i></i>en directo</span>' : ""}</h3>
      <div class="rp-head">
        <span class="rp-team"><i style="background:${esc(colorOf("home"))}"></i>${esc(local.nombre || "Local")}</span>
        <span class="rp-clock">${esc(reloj)}</span>
        <b class="rp-score">${esc(marcador)}</b>
        <span class="rp-team a">${esc(visita.nombre || "Visitante")}<i style="background:${esc(
    colorOf("away")
  )}"></i></span>
      </div>
      <div class="lmt-host" id="lmtHost"><p class="muted small">Cargando el radar de Sportradar…</p></div>
      <p class="muted small radar-note">Widget de Sportradar (Live Match Tracker), el mismo que usa FotMob. Se actualiza solo.</p>
    </section>`;
}

async function panelEstadisticas(id, league) {
  const data = await loadStats(id, league);
  if (!data || data.ok === false) {
    return emptyHTML("Sin estadísticas disponibles", data?.motivo || data?.detalle || "El backend no devolvió estadísticas de este partido.");
  }

  const equipos = data.equipos || [];
  const home = equipos[0] || {};
  const away = equipos[1] || {};

  const allRows = [];
  const pushRows = (filas) => {
    for (const f of filas || []) {
      const label = pick(f, ["etiqueta", "titulo", "label", "name"]);
      const localVal = pick(f, ["local", "home", "casa"]);
      const awayVal = pick(f, ["visita", "visitante", "away"]);
      if (!label || (localVal == null && awayVal == null)) continue;
      allRows.push({ label, localVal, awayVal });
    }
  };

  const top = data.top || [];
  const grupos = data.grupos || [];
  grupos.forEach((g) => pushRows(g?.filas));
  top.forEach((t) =>
    allRows.push({ label: t.titulo, localVal: t.local, awayVal: t.visita, destaca: t.destaca })
  );

  const possRow = allRows.find((r) => /pos(es|session)/i.test(String(r.label)));
  const possLocal = possRow ? num(possRow.localVal) : null;
  const possAway = possRow ? num(possRow.awayVal) : null;
  const possPct = possLocal != null && possAway != null ? pct(possLocal, possAway) : null;

  const possessionHTML =
    possPct != null
      ? `<section class="dcard"><h3>Posesión</h3>
          <div class="poss-head"><b>${possPct}%</b><span class="muted">${esc(home.nombre || "Local")} · ${
          esc(away.nombre || "Visitante")
        }</span><b>${100 - possPct}%</b></div>
          <div class="poss-bar">
            <span class="poss-seg h" style="width:${possPct}%"></span>
            <span class="poss-seg a" style="width:${100 - possPct}%"></span>
          </div>
        </section>`
      : "";

  const rowsHTML = allRows
    .filter((r) => !/pos(es|session)/i.test(String(r.label)))
    .map((r) => statRow(r.label, r.localVal, r.awayVal))
    .join("");

  const statsHTML = rowsHTML
    ? `<section class="dcard"><h3>Comparativa</h3>
        <div class="teams-legend">
          <span><i class="dot h"></i>${esc(home.nombre || "Local")}</span>
          <span><i class="dot a"></i>${esc(away.nombre || "Visitante")}</span>
        </div>
        <div class="stat-list">${rowsHTML}</div></section>`
    : "";

  const momentum = momentumSVG(data.momento);
  const shotMap = shotMapHTML(data.tiros, home.nombre, away.nombre);

  const events = data.eventos || [];
  const eventsHTML = events.length
    ? `<section class="dcard"><h3>Acciones del partido</h3>
        <ul class="event-list">${events
          .map((e) => {
            const icon = e.tipo === "Goal" ? "⚽" : e.tipo === "Card" ? "▮" : e.tipo === "Substitution" ? "⇄" : "•";
            const extra = [e.asistente ? `asist. ${e.asistente}` : null, e.propio ? "en propia puerta" : null]
              .filter(Boolean)
              .join(" · ");
            return `<li class="event ${e.es_local ? "h" : "a"}">
              <span class="event-min">${esc(e.minuto != null ? `${e.minuto}'` : "")}</span>
              <span class="event-body"><b>${esc(icon)} ${esc(e.jugador || e.tipo || "")}</b>${
              extra ? `<small>${esc(extra)}</small>` : ""
            }</span>
              <span class="event-score">${esc(
                Array.isArray(e.marcador) ? e.marcador.join(" - ") : ""
              )}</span>
            </li>`;
          })
          .join("")}</ul></section>`
    : "";

  const body = possessionHTML + statsHTML + momentum + shotMap + eventsHTML;
  return body || emptyHTML("Sin estadísticas disponibles", "El backend respondió sin comparativas para este partido.");
}

/* ---------- pestaña: alineaciones ---------- */
function pitchHTML(local, visita) {
  const homeColor = state.game?.raw?.teams?.find((t) => t.homeAway === "home")?.color || "#22d07a";
  const awayColor = state.game?.raw?.teams?.find((t) => t.homeAway === "away")?.color || "#3a7aff";

  const chip = (p, side, color) => {
    const x = num(p?.x);
    const y = num(p?.y);
    if (x == null || y == null) return "";
    const name = String(p?.nombre || "").split(/\s+/).slice(-1)[0] || p?.nombre || "";
    return `<span class="pitch-player ${side}" style="left:${(x * 100).toFixed(1)}%;top:${(y * 100).toFixed(
      1
    )}%" title="${esc(p?.nombre || "")}">
      <span class="pp-dot" style="background:${esc(color)}">${esc(p?.dorsal ?? "")}</span>
      <span class="pp-name">${esc(name)}</span>
    </span>`;
  };

  const players = [
    ...(local?.titulares || []).map((p) => chip(p, "home", homeColor)),
    ...(visita?.titulares || []).map((p) => chip(p, "away", awayColor)),
  ].join("");

  if (!players) return "";
  return `<section class="dcard"><h3>Alineación en el campo</h3><div class="pitch">${players}</div></section>`;
}

function teamLineupHTML(team, label) {
  if (!team) return "";
  const rows = (list) =>
    (list || [])
      .map(
        (p) => `<li class="player-row">
          <span class="pr-dorsal">${esc(p?.dorsal ?? "")}</span>
          ${p?.foto ? `<img class="pr-foto" src="${esc(p.foto)}" alt="" loading="lazy" onerror="this.remove()">` : ""}
          <span class="pr-name">${esc(p?.nombre || "")}${p?.nota ? `<small>${esc(p.nota)}</small>` : ""}</span>
          <span class="pr-role muted">${esc(p?.rol || "")}</span>
        </li>`
      )
      .join("");

  return `
    <div class="lineup-col">
      <div class="lineup-head">
        ${logoHTML({ name: team.equipo, logo: team.escudo })}
        <div><b>${esc(team.equipo || label)}</b>
          <small class="muted">${esc(team.formacion || "")}${
    team.entrenador ? ` · ${esc(team.entrenador)}` : ""
  }</small></div>
      </div>
      ${team.nota ? `<p class="muted small">${esc(team.nota)}</p>` : ""}
      ${team.titulares?.length ? `<h4>Titulares</h4><ul class="lineup-list">${rows(team.titulares)}</ul>` : ""}
      ${team.suplentes?.length ? `<h4>Suplentes</h4><ul class="lineup-list">${rows(team.suplentes)}</ul>` : ""}
      ${
        team.bajas?.length
          ? `<h4>Bajas</h4><ul class="lineup-list">${team.bajas
              .map(
                (b) =>
                  `<li class="player-row"><span class="pr-dorsal">–</span><span class="pr-name">${esc(
                    b?.nombre || b?.jugador || ""
                  )}</span><span class="pr-role muted">${esc(b?.motivo || b?.tipo || "")}</span></li>`
              )
              .join("")}</ul>`
          : ""
      }
    </div>`;
}

function lineupBadge(tipo) {
  const oficial = String(tipo || "").toLowerCase() === "oficial";
  return `<span class="badge ${oficial ? "ok" : "warn"}">${esc(oficial ? "Alineación oficial" : "Once probable")}</span>`;
}

async function panelAlineaciones(id, league) {
  const data = await loadLineup(id, league);
  if (!data || data.ok === false) {
    return emptyHTML("Sin alineaciones disponibles", data?.motivo || "El backend no devolvió alineaciones de este partido.");
  }

  const local = data.local || {};
  const visita = data.visita || {};
  const { home, away } = sidesOf(state.game?.raw);

  const sinOnce = !(local.titulares?.length || visita.titulares?.length);
  const pitch = pitchHTML(local, visita);
  const aviso = (texto) => `<p class="muted small" style="margin:0 0 12px">${texto}</p>`;
  const respaldo = data.respaldo
    ? `<p class="muted small" style="margin:0 0 12px">Alineación guardada en MongoDB el ${esc(
        new Date(data.respaldo).toLocaleString("es-ES")
      )}.</p>`
    : "";
  const body =
    respaldo +
    (sinOnce
      ? aviso("FotMob todavía no publicó el once inicial de este partido; se muestra la convocatoria disponible.")
      : pitch || aviso("FotMob no publicó las posiciones en el campo; se muestra la convocatoria disponible.")) +
    `<section class="dcard"><h3>Alineaciones ${lineupBadge(data.tipo)}</h3>
      <div class="lineup-grid">
        ${teamLineupHTML({ ...local, equipo: local.equipo || home?.name }, home?.name || "Local")}
        ${teamLineupHTML({ ...visita, equipo: visita.equipo || away?.name }, away?.name || "Visitante")}
      </div>
    </section>`;

  return body || emptyHTML("Sin alineaciones disponibles", "El backend respondió sin jugadores para este partido.");
}

/* ---------- pestaña: jugadores ---------- */
async function panelJugadores(id, league) {
  const data = await loadLineup(id, league);
  const stats = data?.stats_jugador || {};
  const entries = Object.entries(stats);
  if (!entries.length) {
    return emptyHTML("Sin estadísticas de jugadores", data?.motivo || "El backend no devolvió estadísticas individuales.");
  }

  const groups = new Map();
  for (const [, p] of entries) {
    const team = p?.equipo || "Equipo";
    if (!groups.has(team)) groups.set(team, []);
    groups.get(team).push(p);
  }

  const html = [...groups.entries()]
    .map(
      ([team, players]) => `
      <section class="dcard"><h3>${esc(team)}</h3>
        <div class="player-cards">
          ${players
            .map(
              (p) => `
            <article class="player-card">
              <div class="pc-head">
                <span class="pr-dorsal">${esc(p?.dorsal ?? "")}</span>
                <div><b>${esc(p?.nombre || "")}</b>
                  <small class="muted">${p?.portero ? "Portero" : "Jugador"}${
                p?.disparos != null ? ` · ${esc(txt(p.disparos))} remates` : ""
              }</small></div>
              </div>
              ${(p?.grupos || [])
                .map(
                  (g) => `
                <div class="pc-group">
                  <span class="pc-group-title">${esc(g?.grupo || "")}</span>
                  <ul class="kv">${(g?.filas || [])
                    .map(
                      (f) =>
                        `<li><span>${esc(pick(f, ["etiqueta", "label"]) || "")}</span><b>${esc(
                          txt(f?.valor)
                        )}${f?.total != null ? `<i> / ${esc(txt(f.total))}</i>` : ""}</b></li>`
                    )
                    .join("")}</ul>
                </div>`
                )
                .join("")}
            </article>`
            )
            .join("")}
        </div>
      </section>`
    )
    .join("");

  return html;
}

/* ---------- pestaña: tabla ---------- */
async function panelTabla(league) {
  const code = league || state.game?.raw?.league_code || state.game?.league || "";
  if (!code) return emptyHTML("Sin tabla disponible", "Este partido no trae código de competición.");

  const data = await loadStandings(code);
  const grupos = data?.grupos || [];
  if (!grupos.length) return emptyHTML("Sin tabla disponible", data?.motivo || "La API no devolvió la clasificación.");

  const { home, away } = sidesOf(state.game?.raw);
  const mine = new Set([home?.name, away?.name].filter(Boolean).map((n) => String(n).toLowerCase()));

  const hasForm = (grupos || []).some((g) => (g?.equipos || []).some((t) => (t.forma || []).length));

  const table = (g) => `
    ${g?.nombre ? `<h4>${esc(g.nombre)}</h4>` : ""}
    <div class="standings">
      <table>
        <thead>
          <tr><th>#</th><th>Equipo</th><th>PJ</th><th>G</th><th>E</th><th>P</th><th>GF</th><th>GC</th><th>DG</th><th>Pts</th>${hasForm ? "<th>Últimos 5</th>" : ""}</tr>
        </thead>
        <tbody>
          ${(g?.equipos || [])
            .map((t, i) => {
              const isMine = mine.has(String(t.team || "").toLowerCase());
              return `<tr class="${isMine ? "me" : ""}">
                <td class="pos">${i + 1}</td>
                <td class="team-cell">${logoHTML({ name: t.team, logo: t.logo })}<span>${esc(t.team || "")}</span>${
                t.nota ? `<small class="zone">${esc(t.nota)}</small>` : ""
              }</td>
                <td>${esc(txt(t.pj))}</td><td>${esc(txt(t.g))}</td><td>${esc(txt(t.e))}</td><td>${esc(txt(t.p))}</td>
                <td>${esc(txt(t.gf))}</td><td>${esc(txt(t.gc))}</td><td>${esc(txt(t.dif))}</td>
                <td class="pts">${esc(txt(t.pts))}</td>
                ${hasForm ? `<td class="form-cell"><div class="form-chips sm">${(t.forma || [])
                  .map((r) => {
                    const c = String(r || "").toUpperCase().slice(0, 1);
                    return `<span class="chip ${c === "W" ? "w" : c === "L" ? "l" : "d"}">${esc(c || "-")}</span>`;
                  })
                  .join("")}</div></td>` : ""}
              </tr>`;
            })
            .join("")}
        </tbody>
      </table>
    </div>`;

  return `<section class="dcard"><h3>${esc(data.liga || code)}</h3>${grupos.map(table).join("")}
    ${data.actualizado ? `<p class="muted small">Actualizado ${esc(data.actualizado)}</p>` : ""}</section>`;
}

/* ---------- pestaña: enfrentamientos ---------- */
function miniMatch(m) {
  if (!m) return "";
  const h = m.home || {};
  const a = m.away || {};
  const d = m.date ? new Date(m.date) : null;
  const date = d && !Number.isNaN(d.getTime()) ? d.toLocaleDateString("es-ES", { day: "2-digit", month: "2-digit", year: "2-digit" }) : "";
  return `<li class="mini">
    <span class="mini-date muted">${esc(date)}</span>
    <span class="mini-team ${h.winner ? "win" : ""}">${esc(h.abbr || h.name || "")}</span>
    <span class="mini-score">${esc(h.score ?? "")} - ${esc(a.score ?? "")}</span>
    <span class="mini-team ${a.winner ? "win" : ""}">${esc(a.abbr || a.name || "")}</span>
    <span class="mini-status muted">${esc(m.status || "")}</span>
  </li>`;
}

async function panelH2H() {
  const game = state.game?.raw || {};
  const { home, away } = sidesOf(game);
  const h2h = game.head_to_head || [];
  const resumen = game.h2h_summary;
  const recent = (t) => (t?.recent_games || []).slice(0, 5);

  const block = (team, list) => `
    <div class="h2h-col">
      <h4>${esc(team?.name || "")}</h4>
      ${list.length ? `<ul class="mini-list">${list.map(miniMatch).join("")}</ul>` : `<p class="muted small">Sin partidos recientes.</p>`}
    </div>`;

  const body =
    `<section class="dcard"><h3>Últimos partidos</h3>
      <div class="h2h-grid">${block(home, recent(home))}${block(away, recent(away))}</div>
    </section>` +
    `<section class="dcard"><h3>Cara a cara</h3>
      ${
        h2h.length
          ? `${resumen ? `<p class="muted small">${esc(home?.name || "Local")} ${esc(resumen[0])} · Empates ${esc(resumen[1])} · ${esc(away?.name || "Visitante")} ${esc(resumen[2])}</p>` : ""}<ul class="mini-list">${h2h.slice(0, 8).map(miniMatch).join("")}</ul>`
          : `<p class="muted small">Estos equipos no se han enfrentado antes (o FotMob no tiene ese historial).</p>`
      }
    </section>`;

  return body;
}

/* ---------- eventos ---------- */
function wire() {
  el.home = $("#homeView");
  el.detail = $("#detailView");
  el.title = $("#title");
  el.subtitle = $("#subtitle");
  el.liveCount = $("#liveCount");
  el.liveBox = $("#liveBox");
  el.liveList = $("#liveList");
  el.list = $("#list");
  el.empty = $("#empty");
  el.highlights = $("#highlights");
  el.kpis = $("#kpis");
  el.chart = $("#chart");
  el.leagues = $("#leagues");
  el.leagueFilter = $("#leagueFilter");
  el.ligasView = $("#ligasView");

  document.querySelectorAll("#days button").forEach((btn) => {
    btn.addEventListener("click", () => {
      document.querySelectorAll("#days button").forEach((b) => b.classList.remove("active"));
      btn.classList.add("active");
      state.offset = Number(btn.dataset.offset);
      state.highlightsDone = false;
      load().catch(showError);
    });
  });

  el.leagueFilter.addEventListener("change", (e) => {
    state.league = e.target.value;
    state.highlightsDone = false;
    load().catch(showError);
  });

  $("#search").addEventListener("input", (e) => {
    state.query = e.target.value;
    render();
  });

  $("#refresh").addEventListener("click", () => {
    state.highlightsDone = false;
    cache.clear();
    load().catch(showError);
  });

  const openFromEvent = (e) => {
    const card = e.target.closest("[data-id]");
    if (!card || !card.dataset.id) return;
    if (e.type === "keydown" && e.key !== "Enter" && e.key !== " ") return;
    if (e.type === "keydown") e.preventDefault();
    go(card.dataset.id, card.dataset.league || "");
  };
  document.addEventListener("click", openFromEvent);
  document.addEventListener("keydown", openFromEvent);

  window.addEventListener("hashchange", route);
}

function showError(err) {
  console.error(err);
  const offline = /failed to fetch|networkerror|load failed/i.test(String(err.message));
  const detail = offline
    ? "No se pudo conectar con el servidor local. Comprueba que la API esté en marcha y vuelve a intentarlo."
    : err.message;
  el.subtitle.textContent = "No se pudieron cargar las estadísticas";
  el.empty.classList.remove("hidden");
  el.empty.innerHTML = `<strong>Error al consultar la API</strong><span>${esc(detail)}</span>`;
}

function startAutoRefresh() {
  if (state.timer) clearInterval(state.timer);
  state.timer = setInterval(() => {
    if (document.hidden || state.view !== "home") return;
    load().catch(() => {});
  }, REFRESH_MS);
}

wire();
route();
startAutoRefresh();
