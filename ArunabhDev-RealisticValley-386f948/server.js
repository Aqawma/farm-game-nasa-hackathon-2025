// server.js — AgroScope backend (POWER cleaned + AI guardrails)
import express from "express";
import path from "path";
import { fileURLToPath } from "url";
import "dotenv/config";
import OpenAI from "openai";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// ----------------------------- Express -----------------------------
const app = express();
app.use(express.json());

// Static files (public/)
const publicDir = path.join(__dirname, "public");
app.use(express.static(publicDir));
app.get("/", (_req, res) => res.sendFile(path.join(publicDir, "index.html")));

// ----------------------------- OpenAI (optional) -------------------
const openai = process.env.OPENAI_API_KEY ? new OpenAI({ apiKey: process.env.OPENAI_API_KEY }) : null;

// ===================================================================
//                        NASA POWER (robust)
// Clean series, trim outliers, clamp to physical ranges,
// try daily → monthly → climatology → neighbor average,
// and finally impute so we never return nulls.
// ===================================================================
const COMMUNITIES = ["AG", "RE", "SB"];
const DAILY_WINDOWS = [30, 60, 90, 180, 365];
const MONTH_WINDOWS = [24, 60, 120];
const NEIGHBOR_OFFSETS = [
  [ 0,   0   ], [ 0.2, 0   ], [-0.2, 0],
  [ 0,   0.2 ], [ 0,  -0.2], [ 0.2, 0.2 ],
  [ 0.2,-0.2 ], [-0.2,0.2 ], [-0.2,-0.2],
];

const MASKS = new Set([null, undefined, "", -999, -9999, -999.0, -99, 1e20, -1e20]);
const isNum = (v) => Number.isFinite(Number(v));
const clamp = (v, lo, hi) => Math.min(Math.max(v, lo), hi);
const nonneg = (v) => Math.max(0, v);
const yyyymmdd = (d) => d.toISOString().slice(0, 10).replaceAll("-", "");

function seriesFromParam(paramObj) {
  return Object.values(paramObj || {}).map(Number).filter(v => isNum(v) && !MASKS.has(v));
}
function quantile(a, q) {
  if (!a.length) return null;
  const s = [...a].sort((x, y) => x - y);
  const i = (s.length - 1) * q;
  const lo = Math.floor(i), hi = Math.ceil(i);
  if (lo === hi) return s[lo];
  const t = i - lo;
  return s[lo] * (1 - t) + s[hi] * t;
}
function trimMeanArr(a, loQ = 0.1, hiQ = 0.9) {
  if (!a.length) return null;
  const lo = quantile(a, loQ), hi = quantile(a, hiQ);
  const t = a.filter(v => (lo == null || v >= lo) && (hi == null || v <= hi));
  if (!t.length) return null;
  return t.reduce((s, v) => s + v, 0) / t.length;
}

async function getJSON(url) {
  try {
    const r = await fetch(url, { headers: { "User-Agent": "AgroScope/strict-power" } });
    const txt = await r.text();
    if (!r.ok) {
      console.warn("[POWER]", r.status, url, txt.slice(0, 160));
      return null;
    }
    try { return JSON.parse(txt); } catch { console.warn("[POWER] bad JSON"); return null; }
  } catch (e) {
    console.warn("[POWER] network error:", e?.message || e);
    return null;
  }
}

function summarizeDailyMonthly(props) {
  const p = props?.parameter || {};
  const T = seriesFromParam(p.T2M);
  const R = seriesFromParam(p.RH2M);
  const P = seriesFromParam(p.PRECTOTCORR);
  const S = seriesFromParam(p.ALLSKY_SFC_SW_DWN);

  const t2m   = T.length ? clamp(trimMeanArr(T, 0.1, 0.9), -60, 60) : null;   // °C
  const rh2m  = R.length ? clamp(trimMeanArr(R, 0.1, 0.9),   0, 100) : null;  // %
  const precip= P.length ? nonneg(trimMeanArr(P, 0.1, 0.9)) : null;           // mm/day
  const solar = S.length ? nonneg(trimMeanArr(S, 0.1, 0.9)) : null;           // MJ/m²/day

  return { t2m_avg: t2m, rh2m_avg: rh2m, precip_mm_day: precip, solar_mj_m2_day: solar };
}

function summarizeClimatology(props) {
  const p = props?.parameter || {};
  const avg = (obj) => {
    const s = seriesFromParam(obj);
    if (!s.length) return null;
    return trimMeanArr(s, 0.1, 0.9);
  };
  return {
    t2m_avg:         avg(p.T2M),
    rh2m_avg:        avg(p.RH2M),
    precip_mm_day:   avg(p.PRECTOTCORR),
    solar_mj_m2_day: avg(p.ALLSKY_SFC_SW_DWN),
  };
}

async function tryDaily(lat, lon, community, days) {
  const end = new Date();
  const start = new Date(); start.setDate(end.getDate() - days);
  const url = `https://power.larc.nasa.gov/api/temporal/daily/point?parameters=T2M,RH2M,PRECTOTCORR,ALLSKY_SFC_SW_DWN&community=${community}&latitude=${lat}&longitude=${lon}&start=${yyyymmdd(start)}&end=${yyyymmdd(end)}&format=JSON`;
  const j = await getJSON(url);
  if (!j) return null;
  const s = summarizeDailyMonthly(j.properties);
  return { ...s, meta: { source: "daily", community, window_days: days } };
}

async function tryMonthly(lat, lon, community, months) {
  const end = new Date();
  const start = new Date(); start.setMonth(end.getMonth() - (months - 1));
  const s = start.toISOString().slice(0, 7).replace("-", "") + "01";
  const e = end.toISOString().slice(0, 7).replace("-", "") + "28";
  const url = `https://power.larc.nasa.gov/api/temporal/monthly/point?parameters=T2M,RH2M,PRECTOTCORR,ALLSKY_SFC_SW_DWN&community=${community}&latitude=${lat}&longitude=${lon}&start=${s}&end=${e}&format=JSON`;
  const j = await getJSON(url);
  if (!j) return null;
  const ssum = summarizeDailyMonthly(j.properties);
  return { ...ssum, meta: { source: "monthly", community, window_months: months } };
}

async function tryClimatology(lat, lon, community) {
  const url = `https://power.larc.nasa.gov/api/temporal/climatology/point?parameters=T2M,RH2M,PRECTOTCORR,ALLSKY_SFC_SW_DWN&community=${community}&latitude=${lat}&longitude=${lon}&format=JSON`;
  const j = await getJSON(url);
  if (!j) return null;
  const s = summarizeClimatology(j.properties);
  s.t2m_avg         = s.t2m_avg  == null ? null : clamp(s.t2m_avg, -60, 60);
  s.rh2m_avg        = s.rh2m_avg == null ? null : clamp(s.rh2m_avg, 0, 100);
  s.precip_mm_day   = s.precip_mm_day == null ? null : nonneg(s.precip_mm_day);
  s.solar_mj_m2_day = s.solar_mj_m2_day == null ? null : nonneg(s.solar_mj_m2_day);
  return { ...s, meta: { source: "climatology", community, window_months: 12 } };
}

// Always return numbers (no nulls)
function finalizeStrict(powerLike) {
  const out = { ...powerLike };

  // First pass: clamp existing values
  if (out.t2m_avg         != null) out.t2m_avg         = clamp(Number(out.t2m_avg), -60, 60);
  if (out.rh2m_avg        != null) out.rh2m_avg        = clamp(Number(out.rh2m_avg), 0, 100);
  if (out.precip_mm_day   != null) out.precip_mm_day   = nonneg(Number(out.precip_mm_day));
  if (out.solar_mj_m2_day != null) out.solar_mj_m2_day = nonneg(Number(out.solar_mj_m2_day));

  // Impute sensible defaults if missing
  const DEFAULTS = { t: 20, rh: 60, p: 2, s: 18 }; // °C, %, mm/day, MJ/m²/day

  if (out.t2m_avg == null) {
    out.t2m_avg = out.solar_mj_m2_day != null
      ? clamp(8 + out.solar_mj_m2_day * 0.8, -60, 60)
      : DEFAULTS.t;
  }
  if (out.rh2m_avg == null) {
    out.rh2m_avg = out.precip_mm_day != null
      ? clamp(40 + Math.min(out.precip_mm_day, 10) * 6, 0, 100)
      : DEFAULTS.rh;
  }
  if (out.solar_mj_m2_day == null) {
    const est = (out.t2m_avg - 8) / 0.8;
    out.solar_mj_m2_day = nonneg(isNum(est) ? est : DEFAULTS.s);
  }
  if (out.precip_mm_day == null) {
    out.precip_mm_day = DEFAULTS.p;
  }

  // Final clamps
  out.t2m_avg         = clamp(out.t2m_avg, -60, 60);
  out.rh2m_avg        = clamp(out.rh2m_avg, 0, 100);
  out.solar_mj_m2_day = nonneg(out.solar_mj_m2_day);
  out.precip_mm_day   = nonneg(out.precip_mm_day);

  return out;
}

async function fetchPowerStrict(lat, lon) {
  if (!Number.isFinite(lat) || !Number.isFinite(lon)) return finalizeStrict({});

  for (const community of COMMUNITIES) {
    // exact point: daily → monthly → climatology
    for (const d of DAILY_WINDOWS) {
      const r = await tryDaily(lat, lon, community, d);
      if (r && (r.t2m_avg!=null || r.rh2m_avg!=null || r.precip_mm_day!=null || r.solar_mj_m2_day!=null)) {
        return finalizeStrict({ ...r, community });
      }
    }
    for (const m of MONTH_WINDOWS) {
      const r = await tryMonthly(lat, lon, community, m);
      if (r && (r.t2m_avg!=null || r.rh2m_avg!=null || r.precip_mm_day!=null || r.solar_mj_m2_day!=null)) {
        return finalizeStrict({ ...r, community });
      }
    }
    {
      const r = await tryClimatology(lat, lon, community);
      if (r && (r.t2m_avg!=null || r.rh2m_avg!=null || r.precip_mm_day!=null || r.solar_mj_m2_day!=null)) {
        return finalizeStrict({ ...r, community });
      }
    }

    // neighbor sampling ±0.2° → average what we get
    let acc = { t:[], rh:[], p:[], s:[] };
    for (const [dlat, dlon] of NEIGHBOR_OFFSETS) {
      const la = lat + dlat, lo = lon + dlon;
      let rr = null;
      for (const d of DAILY_WINDOWS) { rr = await tryDaily(la, lo, community, d); if (rr) break; }
      if (!rr) for (const m of MONTH_WINDOWS) { rr = await tryMonthly(la, lo, community, m); if (rr) break; }
      if (!rr) rr = await tryClimatology(la, lo, community);
      if (!rr) continue;
      if (rr.t2m_avg!=null) acc.t.push(rr.t2m_avg);
      if (rr.rh2m_avg!=null) acc.rh.push(rr.rh2m_avg);
      if (rr.precip_mm_day!=null) acc.p.push(rr.precip_mm_day);
      if (rr.solar_mj_m2_day!=null) acc.s.push(rr.solar_mj_m2_day);
    }
    const mean = arr => arr.length ? arr.reduce((a,b)=>a+b,0)/arr.length : null;
    const merged = {
      t2m_avg: mean(acc.t),
      rh2m_avg: mean(acc.rh),
      precip_mm_day: mean(acc.p),
      solar_mj_m2_day: mean(acc.s),
      meta: { source: "neighbor-avg", community, notes: "±0.2° averaged" }
    };
    if (merged.t2m_avg!=null || merged.rh2m_avg!=null || merged.precip_mm_day!=null || merged.solar_mj_m2_day!=null) {
      return finalizeStrict(merged);
    }
  }

  // nothing usable → safe non-null defaults
  return finalizeStrict({});
}

// ===================================================================
//                    AI sanity check (optional)
// Adjusts values gently if still implausible; adds ai_notes.
// ===================================================================
async function aiSanityCheckPower({ openai, lat, lon, power, whenRange }) {
  try {
    if (!openai) return { ...power, ai_notes: "AI off" };

    const system = `
You are a climate sanity-checker. Input is lat/lon and recent POWER summary.
Return ONLY JSON:
{
  "t2m_avg": number,
  "rh2m_avg": number,
  "precip_mm_day": number,
  "solar_mj_m2_day": number,
  "notes": string
}
Rules: keep values realistic for place/season. Temp -60..60 °C; RH 0..100 %; Precip/Solar >=0.
Be conservative. "notes" ≤ 180 chars.`.trim();

    const user = JSON.stringify({
      lat, lon, whenRange,
      power_input: {
        t2m_avg: power?.t2m_avg ?? null,
        rh2m_avg: power?.rh2m_avg ?? null,
        precip_mm_day: power?.precip_mm_day ?? null,
        solar_mj_m2_day: power?.solar_mj_m2_day ?? null,
      }
    });

    const resp = await openai.responses.create({
      model: "gpt-5-mini",
      input: [
        { role: "system", content: [{ type: "input_text", text: system }] },
        { role: "user",   content: [{ type: "input_text", text: user   }] }
      ]
    });

    const text =
      resp.output_text ||
      (resp.output?.[0]?.content?.find?.(c => c.type === "output_text")?.text) ||
      "";

    let data;
    try { data = JSON.parse(text); }
    catch {
      const m = text.match(/\{[\s\S]*\}/);
      if (!m) return { ...power, ai_notes: "AI non-JSON; using POWER" };
      try { data = JSON.parse(m[0]); } catch { return { ...power, ai_notes: "AI malformed; using POWER" }; }
    }

    const out = {
      t2m_avg: clamp(Number(data.t2m_avg ?? power.t2m_avg), -60, 60),
      rh2m_avg: clamp(Number(data.rh2m_avg ?? power.rh2m_avg), 0, 100),
      precip_mm_day: nonneg(Number(data.precip_mm_day ?? power.precip_mm_day)),
      solar_mj_m2_day: nonneg(Number(data.solar_mj_m2_day ?? power.solar_mj_m2_day)),
      meta: { ...(power.meta || {}), ai: true },
      ai_notes: String(data.notes || "Checked; no change"),
    };
    return out;
  } catch (e) {
    console.warn("[AI sanity-check] error:", e?.message || e);
    return { ...power, ai_notes: "AI check failed; using POWER" };
  }
}

// ===================================================================
//                        Analyze Polygon API
// ===================================================================
app.post("/analyze-polygon", async (req, res) => {
  try {
    const { polygon, area_sq_km, centroid } = req.body || {};
    if (!polygon?.geometry?.type || polygon.geometry.type !== "Polygon") {
      return res.status(400).json({ error: "Expected GeoJSON Polygon Feature" });
    }

    // Representative point (prefer provided centroid)
    let lat = null, lon = null;
    if (Number.isFinite(centroid?.lat) && Number.isFinite(centroid?.lon)) {
      ({ lat, lon } = centroid);
    } else {
      const ring = polygon.geometry.coordinates?.[0] ?? [];
      const xs = ring.map(c => Number(c[0])).filter(Number.isFinite);
      const ys = ring.map(c => Number(c[1])).filter(Number.isFinite);
      if (xs.length && ys.length) {
        lon = (Math.min(...xs) + Math.max(...xs)) / 2;
        lat = (Math.min(...ys) + Math.max(...ys)) / 2;
      }
    }

    // POWER (robust, non-null), then AI sanity check
    const rawPower = (lat != null && lon != null)
      ? await fetchPowerStrict(lat, lon)
      : finalizeStrict({});
    const whenRange =
      rawPower?.meta?.source === "daily"    ? `daily ~${rawPower?.meta?.window_days}d` :
      rawPower?.meta?.source === "monthly"  ? `monthly ~${rawPower?.meta?.window_months}m` :
      rawPower?.meta?.source || "climatology/other";

    const power = await aiSanityCheckPower({ openai, lat, lon, power: rawPower, whenRange });

    // If no OpenAI for labels, return a mock label set + strict POWER numbers
    if (!openai) {
      return res.json({
        country: "Unknown",
        crop: "rice",
        regional_popular_crop: "wheat",
        temperature_c: power.t2m_avg,                 // guaranteed number
        humidity_relative_percent: power.rh2m_avg,    // guaranteed number
        soil_water_retention: "medium",
        rationale: `Mock (no OPENAI_API_KEY). Area ~ ${area_sq_km?.toFixed?.(2) ?? "?"} km²`,
        power
      });
    }

    // Strict JSON schema for labels
    const systemPrompt = `
You are a strict JSON generator. Return ONLY:
{
  "country": string,
  "crop": "rice" | "corn" | "potato",
  "regional_popular_crop": string,
  "temperature_c": number,
  "humidity_relative_percent": number,
  "soil_water_retention": "low" | "medium" | "high",
  "rationale": string
}
No prose outside JSON. "regional_popular_crop" must differ from "crop".`.trim();

    const userPrompt =
      `Analyze this polygon & return that JSON.\n` +
      `GeoJSON:\n${JSON.stringify(polygon)}\n` +
      `Approx area_km2: ${area_sq_km ?? "unknown"}\n` +
      `Representative lat,lon: ${lat}, ${lon}\n` +
      `NOTE: Server will overwrite temperature/humidity with NASA POWER values.`;

    const resp = await openai.responses.create({
      model: "gpt-5-mini",
      input: [
        { role: "system", content: [{ type: "input_text", text: systemPrompt }] },
        { role: "user",   content: [{ type: "input_text", text: userPrompt   }] }
      ]
    });

    const rawText =
      resp.output_text ||
      (resp.output?.[0]?.content?.find?.(c => c.type === "output_text")?.text) ||
      "";

    let data;
    try {
      data = JSON.parse(rawText);
    } catch {
      const match = rawText.match(/\{[\s\S]*\}/);
      if (!match) return res.status(502).json({ error: "Model did not return JSON", raw: rawText });
      try { data = JSON.parse(match[0]); }
      catch { return res.status(502).json({ error: "Model returned malformed JSON", raw: rawText }); }
    }

    const okCrop = ["rice", "corn", "potato"].includes(data.crop);
    const okSoil = ["low", "medium", "high"].includes(data.soil_water_retention);
    if (!data.country || !okCrop || !okSoil || !data.regional_popular_crop) {
      return res.status(502).json({ error: "Invalid schema from model", raw: data });
    }

    // Overwrite with POWER (guaranteed sane numbers) + include ai_notes
    data.temperature_c = power.t2m_avg;
    data.humidity_relative_percent = power.rh2m_avg;
    data.power = power;

    res.json(data);
  } catch (e) {
    console.error(e);
    res.status(500).json({ error: "AI/POWER analysis failed", details: String(e?.message || e) });
  }
});

// Simple health check
app.get("/healthz", (_req, res) => res.json({ ok: true }));

// ----------------------------- Start ------------------------------
const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
  console.log(`Server running → http://localhost:${PORT}`);
});
