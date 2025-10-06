// High-contrast UI + Intro modal + OSM + 4-point polygon → /analyze-polygon
// NEW: "POWER Data" tab that fetches last-30-day timeseries for the polygon centroid.

/* ---------- helpers ---------- */
const $ = (s, r=document) => r.querySelector(s);
const esc = s => String(s ?? '').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#039;'}[c]));

/* ---------- theme ---------- */
(function initTheme(){
  const sw = $('#themeSwitch');
  try {
    const prefers = window.matchMedia?.('(prefers-color-scheme: dark)').matches;
    const saved = localStorage.getItem('theme');
    const dark = saved ? saved === 'dark' : prefers;
    document.documentElement.classList.toggle('dark', dark);
    sw && (sw.checked = dark);
    sw?.addEventListener('change', e=>{
      const on = !!e.currentTarget.checked;
      document.documentElement.classList.toggle('dark', on);
      localStorage.setItem('theme', on ? 'dark' : 'light');
    });
  } catch {}
})();

/* ---------- intro modal ---------- */
(function initIntroModal(){
  const backdrop = $('#introBackdrop');
  const closeBtn = $('#introClose');
  const dontShow = $('#introDontShow');
  const KEY = 'intro_dismissed_v1';

  function open() { backdrop?.classList.add('show'); backdrop?.setAttribute('aria-hidden','false'); }
  function close() {
    if (dontShow?.checked) localStorage.setItem(KEY, '1');
    backdrop?.classList.remove('show');
    backdrop?.setAttribute('aria-hidden','true');
  }

  if (!localStorage.getItem(KEY)) open();
  closeBtn?.addEventListener('click', close);
  backdrop?.addEventListener('click', (e)=>{ if (e.target === backdrop) close(); });
  window.addEventListener('keydown', (e)=>{ if (e.key === 'Escape') close(); });
})();

/* ---------- sidebar toggle + tabs ---------- */
const sidebar    = $('#sidebar');
const collapseBtn= $('#collapseBtn');
const expandBtn  = $('#expandBtn');
const statusEl   = $('#status');

const tabs = Array.from(document.querySelectorAll('.tab'));
const panels = Array.from(document.querySelectorAll('.tabPanel'));
tabs.forEach(btn => btn.addEventListener('click', ()=>{
  tabs.forEach(b => b.classList.toggle('is-active', b===btn));
  panels.forEach(p => p.classList.toggle('is-active', p.id === `tab-${btn.dataset.tab}`));
}));

const toggleSidebar = (force) => {
  if (!sidebar) return;
  const collapsed = force ?? !sidebar.classList.contains('is-collapsed');
  sidebar.classList.toggle('is-collapsed', collapsed);
  setTimeout(()=>map?.invalidateSize?.(), 250);
};
collapseBtn?.addEventListener('click', ()=>toggleSidebar(true));
expandBtn?.addEventListener('click', ()=>toggleSidebar(false));

/* ---------- map ---------- */
if (typeof L === 'undefined') console.error('Leaflet not loaded.');
const map = (typeof L!=='undefined') ? L.map('map', { zoomControl:true }).setView([20,0], 2.5) : null;

let base = null;
if (map) {
  base = L.tileLayer('https://a.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19, attribution: '&copy; OpenStreetMap'
  }).addTo(map);

  // purge any old panes/overlays from earlier builds
  (function purge(){
    map.eachLayer(l => { if (l !== base) map.removeLayer(l); });
    ['rasters','ndvi','images','heatmap','countries'].forEach(id=>{
      const pane = map.getPane(id); if (pane) { try { pane.remove(); } catch {} }
    });
  })();

  requestAnimationFrame(()=>map.invalidateSize());
  setTimeout(()=>map.invalidateSize(), 0);
}

// coords + tile error
$('#coords') && map?.on('mousemove', e => $('#coords').textContent = `${e.latlng.lat.toFixed(4)}, ${e.latlng.lng.toFixed(4)}`);
base?.on('tileerror', () => { const el=$('#mapError'); if (el) el.hidden=false; });

/* ---------- Results refs ---------- */
const resCountry   = $('#resCountry');
const resCrop      = $('#resCrop');
const resRegional  = $('#resRegional');
const resTemp      = $('#resTemp');
const resHum       = $('#resHum');
const resSoil      = $('#resSoil');
const resArea      = $('#resArea');
const resSolar     = $('#resSolar');
const resPrecip    = $('#resPrecip');
const resRationale = $('#resRationale');

/* ---------- POWER table refs ---------- */
const powerMeta  = $('#powerMeta');
const powerTbody = $('#powerTbody');
const powerTabBtn = Array.from(document.querySelectorAll('.tab')).find(t => t.dataset.tab==='power');

/* ---------- Polygon tool (buttons are in TOPBAR) ---------- */
const startBtn = $('#startBtn');
const undoBtn  = $('#undoBtn');
const resetBtn = $('#resetBtn');

let selecting=false, points=[], markers=[], line=null, poly=null;
const drawLayer = (map && L) ? L.featureGroup().addTo(map) : null;

function setBtns(){
  undoBtn && (undoBtn.disabled = !selecting || points.length===0);
  resetBtn && (resetBtn.disabled = !selecting && !poly);
  startBtn && (startBtn.disabled = selecting);
}
function clearTemps(){
  if (!drawLayer) return;
  markers.forEach(m => drawLayer.removeLayer(m));
  markers=[]; if (line){ drawLayer.removeLayer(line); line=null; }
  points=[];
}
function resetAll(){
  clearTemps(); if (poly && drawLayer){ drawLayer.removeLayer(poly); poly=null; }
  selecting=false; statusEl && (statusEl.textContent='Click Start, then pick 4 points'); setBtns();
  writeResult(); // clear panel
  clearPowerTable();
}
function begin(){
  if (!map) return;
  resetAll(); selecting=true; statusEl && (statusEl.textContent='Click 4 points on the map'); setBtns();
}
function undo(){
  if (!points.length || !drawLayer || !L) return;
  points.pop(); const m = markers.pop(); m && drawLayer.removeLayer(m);
  if (line){ drawLayer.removeLayer(line); line=null; }
  if (points.length>=2){
    line = L.polyline(points, { color:'#ff7800', dashArray:'4,4', weight:2 }).addTo(drawLayer);
  }
  setBtns();
}
startBtn?.addEventListener('click', begin);
undoBtn?.addEventListener('click', undo);
resetBtn?.addEventListener('click', resetAll);

// keyboard shortcuts
window.addEventListener('keydown', (e)=>{
  if (['INPUT','TEXTAREA','SELECT'].includes(e.target.tagName)) return;
  const k = e.key.toLowerCase();
  if (k==='s') begin();
  if (k==='u') undo();
  if (k==='r') resetAll();
});

// click-to-place
map?.on('click', async (e)=>{
  if (!selecting || !drawLayer || !L) return;

  points.push(e.latlng);
  const mk = L.circleMarker(e.latlng, { radius:6, color:'#fff', weight:2, fillColor:'#e11d48', fillOpacity:0.9 }).addTo(drawLayer);
  mk.bindTooltip(String(points.length), { permanent:true, direction:'top' });
  markers.push(mk);

  if (line){ drawLayer.removeLayer(line); line=null; }
  if (points.length>=2 && points.length<4){
    line = L.polyline(points, { color:'#ff7800', dashArray:'4,4', weight:2 }).addTo(drawLayer);
  }

  if (points.length===4){
    if (line){ drawLayer.removeLayer(line); line=null; }
    poly = L.polygon(points, { color:'#1f2937', weight:2, fillColor:'#e11d48', fillOpacity:0.35 }).addTo(drawLayer);
    selecting=false; setBtns();
    statusEl && (statusEl.textContent='Analyzing…');

    // area & centroid
    const gj = poly.toGeoJSON();
    let area_km2=null, centroid=null;
    try{
      if (typeof turf!=='undefined'){
        area_km2 = turf.area(gj)/1e6;
        const c = turf.centroid(gj)?.geometry?.coordinates; // [lng,lat]
        if (c) centroid = { lon:c[0], lat:c[1] };
      } else {
        const b = poly.getBounds(); const c = b.getCenter();
        centroid = { lon:c.lng, lat:c.lat };
        const kmPerDeg = 111;
        area_km2 = Math.abs(b.getNorth()-b.getSouth())*kmPerDeg * Math.abs(b.getEast()-b.getWest())*kmPerDeg;
      }
    }catch{}

    // Kick off both: server analysis + POWER series (client-side)
    await Promise.allSettled([
      analyzePolygon(poly, area_km2, centroid),
      centroid ? fetchPowerSeries(centroid.lat, centroid.lon) : Promise.resolve()
    ]);

    statusEl && (statusEl.textContent='Done. Click Reset to draw again.');
  } else {
    statusEl && (statusEl.textContent = `Point ${points.length}/4 placed…`);
  }
});

/* ---------- server call (AI + POWER averages) ---------- */
async function analyzePolygon(polygonLayer, area_km2, centroidLngLat){
  const geojson = polygonLayer?.toGeoJSON?.();
  if (!geojson) return;
  try{
    const res = await fetch('/analyze-polygon', {
      method:'POST',
      headers:{ 'Content-Type':'application/json' },
      body: JSON.stringify({
        polygon: geojson,
        area_sq_km: area_km2,
        centroid: centroidLngLat || null
      })
    });
    const text = await res.text();
    if (!res.ok){
      writeResult({ error:`Server error ${res.status}`, raw:text, area:area_km2 });
      polygonLayer.bindPopup(`<b>Server error ${res.status}</b><br/><pre style="white-space:pre-wrap">${esc(text)}</pre>`).openPopup();
      statusEl && (statusEl.textContent='Server error');
      return;
    }
    let data;
    try { data = JSON.parse(text); }
    catch {
      writeResult({ error:'Bad JSON from server', raw:text, area:area_km2 });
      polygonLayer.bindPopup(`<b>Bad JSON</b><br/><pre style="white-space:pre-wrap">${esc(text)}</pre>`).openPopup();
      statusEl && (statusEl.textContent='Bad JSON'); return;
    }

    polygonLayer.bindPopup(`
      <b>Country:</b> ${esc(data.country)}<br/>
      <b>Crop:</b> ${esc(data.crop)}<br/>
      <b>Regional popular:</b> ${esc(data.regional_popular_crop)}<br/>
      <b>Temp (°C):</b> ${esc(data.temperature_c ?? data.power?.t2m_avg ?? '—')}<br/>
      <b>Humidity (%):</b> ${esc(data.humidity_relative_percent ?? data.power?.rh2m_avg ?? '—')}<br/>
      <b>Solar:</b> ${esc(data.power?.solar_mj_m2_day != null ? Number(data.power.solar_mj_m2_day).toFixed(2) : '—')} MJ/m²/day<br/>
      <b>Precip:</b> ${esc(data.power?.precip_mm_day != null ? Number(data.power.precip_mm_day).toFixed(2) : '—')} mm/day<br/>
      <b>Area:</b> ${area_km2 ? area_km2.toFixed(2) : '—'} km²<br/>
      <hr style="opacity:.4"/><small>${esc(data.rationale || '')}</small>
    `).openPopup();

    writeResult({ ...data, area: area_km2 });
  }catch(err){
    writeResult({ error:'Network error', raw:String(err), area:area_km2 });
    polygonLayer.bindPopup(`<b>Network error</b><br/>${esc(String(err))}`).openPopup();
    statusEl && (statusEl.textContent='Network error — is the server running?');
  }
}

/* ---------- POWER client-side series (last 30 days) ---------- */
function fmtDateISO(d){ return d.toISOString().slice(0,10); }
function fmtDateKey(d){ return d.toISOString().slice(0,10).replace(/-/g,''); }

async function fetchPowerSeries(lat, lon){
  try{
    // Build last 30-day range
    const end = new Date();            // today
    const start = new Date(end); start.setDate(end.getDate()-30);
    const startKey = fmtDateKey(start);
    const endKey   = fmtDateKey(end);

    const params = new URLSearchParams({
      latitude: lat, longitude: lon,
      start: startKey, end: endKey,
      parameters: 'T2M,RH2M,ALLSKY_SFC_SW_DWN,PRECTOTCORR',
      community: 'AG', format: 'JSON'
    });

    // Tell the UI
    setPowerMeta(`Loading POWER for ${lat.toFixed(4)}, ${lon.toFixed(4)} from ${fmtDateISO(start)} to ${fmtDateISO(end)}…`);
    fillPowerTableLoading();

    const url = `https://power.larc.nasa.gov/api/temporal/daily/point?${params.toString()}`;
    const r = await fetch(url);
    if (!r.ok) throw new Error(`POWER HTTP ${r.status}`);
    const j = await r.json();

    const p = j?.properties?.parameter || {};
    const T2M = p.T2M || {};
    const RH2M = p.RH2M || {};
    const SOL = p.ALLSKY_SFC_SW_DWN || {};
    const P   = p.PRECTOTCORR || {};

    const dates = Object.keys(T2M).sort();
    const rows = dates.map(dkey => {
      const yyyy = dkey.slice(0,4), mm = dkey.slice(4,6), dd = dkey.slice(6,8);
      const iso = `${yyyy}-${mm}-${dd}`;
      const t   = toNum(T2M[dkey]);
      const rh  = toNum(RH2M[dkey]);
      const sol = toNum(SOL[dkey]);
      const pr  = toNum(P[dkey]);

      // harden against null/undefined: show '—' if not finite
      return {
        date: iso,
        t: Number.isFinite(t)   ? t : null,
        rh:Number.isFinite(rh)  ? rh: null,
        sol:Number.isFinite(sol)? sol: null,
        pr: Number.isFinite(pr) ? pr : null
      };
    });

    renderPowerTable(rows);
    setPowerMeta(`POWER daily @ ${lat.toFixed(4)}, ${lon.toFixed(4)} (${rows.length} days)`);
    // Auto-switch to POWER tab so users see it immediately
    powerTabBtn?.click();
  } catch(err){
    setPowerMeta(`POWER load error: ${String(err.message || err)}`);
    renderPowerTable([]); // empties
  }
}
function toNum(v){ const n = Number(v); return Number.isFinite(n) ? n : NaN; }
function setPowerMeta(text){ if (powerMeta) powerMeta.textContent = text; }
function fillPowerTableLoading(){
  if (!powerTbody) return;
  powerTbody.innerHTML = `<tr><td colspan="5" class="muted">Loading…</td></tr>`;
}
function clearPowerTable(){
  if (!powerTbody) return;
  powerTbody.innerHTML = `<tr><td colspan="5" class="muted">—</td></tr>`;
}
function renderPowerTable(rows){
  if (!powerTbody) return;
  if (!rows?.length){
    powerTbody.innerHTML = `<tr><td colspan="5" class="muted">No data</td></tr>`;
    return;
  }
  powerTbody.innerHTML = rows.map(r => `
    <tr>
      <td class="mono">${esc(r.date)}</td>
      <td>${r.t   != null ? Number(r.t).toFixed(1)  : '—'}</td>
      <td>${r.rh  != null ? Math.round(r.rh)        : '—'}</td>
      <td>${r.sol != null ? Number(r.sol).toFixed(2): '—'}</td>
      <td>${r.pr  != null ? Number(r.pr).toFixed(2) : '—'}</td>
    </tr>
  `).join('');
}

/* ---------- write results (sidebar Results tab) ---------- */
function writeResult(d = {}){
  const temp = d.temperature_c ?? d.power?.t2m_avg;
  const rh   = d.humidity_relative_percent ?? d.power?.rh2m_avg;

  resCountry.textContent   = d.country ?? '—';
  resCrop.textContent      = d.crop ?? '—';
  resRegional.textContent  = d.regional_popular_crop ?? '—';
  resTemp.textContent      = (temp != null && Number.isFinite(+temp)) ? Number(temp).toFixed(1) : '—';
  resHum.textContent       = (rh   != null && Number.isFinite(+rh  )) ? Math.round(rh) : '—';
  resSoil && (resSoil.textContent = d.soil_water_retention ?? '—'); // optional
  resArea.textContent      = d.area != null ? d.area.toFixed(2) : '—';
  resSolar.textContent     = d.power?.solar_mj_m2_day != null ? Number(d.power.solar_mj_m2_day).toFixed(2) : '—';
  resPrecip.textContent    = d.power?.precip_mm_day    != null ? Number(d.power.precip_mm_day).toFixed(2)    : '—';
  resRationale.textContent = d.rationale ?? (d.error ? `${d.error}\n\n${d.raw ?? ''}` : '—');
}

/* ---------- controls in topbar ---------- */
const startBtnEl = $('#startBtn');
const undoBtnEl  = $('#undoBtn');
const resetBtnEl = $('#resetBtn');

function setBtnsWrapper(){ setBtns(); }
startBtnEl?.addEventListener('click', begin);
undoBtnEl?.addEventListener('click', undo);
resetBtnEl?.addEventListener('click', resetAll);
