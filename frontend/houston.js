"use strict";

// Houston — live readout for the Kitten Remote Control Telemachus datalink.
// Plain (non-module) script so it loads from file:// without CORS restrictions.

// ---- odometer gauge --------------------------------------------------------
// Layout: 5 integer digits, decimal point, 1 fractional digit, then a unit tab.
const INT_DIGITS = 5, FRAC_DIGITS = 1;
const REEL_HEIGHT = 52; // px, must match .reel height in houston.css

function buildGauge(el) {
  el.innerHTML = "";
  const reels = [];
  const total = INT_DIGITS + FRAC_DIGITS;
  for (let i = 0; i < total; i++) {
    if (i === INT_DIGITS) {
      const sep = document.createElement("div");
      sep.className = "sep"; sep.textContent = ".";
      el.appendChild(sep);
    }
    const reel = document.createElement("div");
    reel.className = "reel";
    const strip = document.createElement("div");
    strip.className = "strip";
    for (let d = 0; d <= 9; d++) {
      const s = document.createElement("span"); s.textContent = d; strip.appendChild(s);
    }
    reel.appendChild(strip);
    el.appendChild(reel);
    reels.push(strip);
  }
  const unit = document.createElement("div");
  unit.className = "unit"; unit.textContent = "KM";
  el.appendChild(unit);
  el._reels = reels;
}

// value in km -> roll each reel. null/negative -> dim (no data).
function setGauge(el, km) {
  const reels = el._reels;
  const missing = km === null || km === undefined || Number.isNaN(km) || km < 0;
  let digits = null;
  if (!missing) {
    const scaled = Math.round(km * Math.pow(10, FRAC_DIGITS));
    const max = Math.pow(10, INT_DIGITS + FRAC_DIGITS) - 1;
    const str = String(Math.min(scaled, max)).padStart(INT_DIGITS + FRAC_DIGITS, "0");
    digits = str.split("").map(Number);
  }
  reels.forEach((strip, i) => {
    if (missing) {
      strip.style.transform = "translateY(0)";      // rest on 0
      strip.parentElement.style.opacity = "0.28";   // dim = no data
    } else {
      strip.parentElement.style.opacity = "1";
      strip.style.transform = `translateY(${-digits[i] * REEL_HEIGHT}px)`;
    }
  });
}

const gauges = {};
document.querySelectorAll(".readout").forEach(el => {
  buildGauge(el);
  gauges[el.dataset.gauge] = el;
});

// ---- polling ---------------------------------------------------------------
const statusEl = document.getElementById("status");
const statusText = document.getElementById("statusText");
const verEl = document.getElementById("ver");
const errEl = document.getElementById("err");
let timer = null;

function baseUrl() {
  const host = document.getElementById("host").value.trim() || "localhost";
  const port = document.getElementById("port").value.trim() || "8085";
  return `http://${host}:${port}/telemachus/datalink`;
}

function setOnline(on) {
  statusEl.classList.toggle("live", on);
  statusText.textContent = on ? "CONNECTED" : "OFFLINE";
}

function num(v) { return (typeof v === "number") ? v : -1; }

async function poll() {
  const q = "ap=o.ApAkm&pe=o.PeAkm&alt=v.altitude&ver=a.version";
  try {
    const ctrl = new AbortController();
    const to = setTimeout(() => ctrl.abort(), 2000);
    const res = await fetch(baseUrl() + "?" + q, { signal: ctrl.signal, cache: "no-store" });
    clearTimeout(to);
    const data = await res.json();

    const alt = typeof data.alt === "number" && data.alt >= 0 ? data.alt / 1000 : -1; // m -> km
    setGauge(gauges.apoapsis, num(data.ap));
    setGauge(gauges.periapsis, num(data.pe));
    setGauge(gauges.altitude, alt);

    const online = typeof data.ver === "string" && data.ver.length > 0;
    setOnline(online);
    verEl.textContent = online ? `version ${data.ver}` : "version —";
    errEl.textContent = "";
    errEl.className = "";
  } catch (e) {
    setOnline(false);
    verEl.textContent = "version —";
    errEl.textContent = "no datalink — is KSA running and reachable at " + baseUrl() + " ?";
    errEl.className = "err";
    setGauge(gauges.apoapsis, -1);
    setGauge(gauges.periapsis, -1);
    setGauge(gauges.altitude, -1);
  }
}

function restart() {
  if (timer) clearInterval(timer);
  const rate = Math.max(100, parseInt(document.getElementById("rate").value, 10) || 500);
  poll();
  timer = setInterval(poll, rate);
}

["host", "port", "rate"].forEach(id =>
  document.getElementById(id).addEventListener("change", restart));

// When served over HTTP (e.g. python http.server), default the datalink host to the page's own
// host so it works from a phone without manually entering the Mac's IP. (file:// keeps "localhost".)
if (location.protocol.startsWith("http") && location.hostname) {
  document.getElementById("host").value = location.hostname;
}

restart();
