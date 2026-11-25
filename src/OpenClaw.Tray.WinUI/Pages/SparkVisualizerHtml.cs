namespace OpenClawTray.Pages;

public static class SparkVisualizerHtml
{
    public static string GetHtml(string initialMode = "standard")
    {
        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>SPARK Voice Visualizer</title>
<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
html, body {
  width: 100%; height: 100%;
  background: radial-gradient(ellipse at center, #0a0e1a 0%, #000000 75%);
  color: #fff;
  font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, sans-serif;
  overflow: hidden;
  user-select: none;
}
.root {
  width: 100%; height: 100%;
  display: flex; flex-direction: column;
  align-items: center; justify-content: center;
  position: relative;
}

/* Mode Containers */
.mode-container {
  position: absolute;
  top: 0; left: 0; width: 100%; height: 100%;
  display: none;
  align-items: center; justify-content: center;
  flex-direction: column;
}
.mode-container.active { display: flex; }

/* Status Header */
.status-pill {
  position: absolute;
  top: 10px;
  display: flex; align-items: center; gap: 8px;
  background: rgba(0, 191, 255, 0.08);
  border: 1px solid rgba(0, 191, 255, 0.25);
  border-radius: 20px;
  padding: 4px 14px;
  font-size: 11px;
  letter-spacing: 1.5px;
  text-transform: uppercase;
  z-index: 20;
}
.status-dot {
  width: 8px; height: 8px; border-radius: 50%;
  background: #00bfff;
  box-shadow: 0 0 8px #00bfff;
  animation: pulseDot 1.8s infinite;
}
@keyframes pulseDot {
  0%, 100% { transform: scale(1); opacity: 1; }
  50% { transform: scale(0.7); opacity: 0.5; }
}

/* ========================================================= */
/* MODE 1: STANDARD (FIGMA DESIGN HUD)                       */
/* ========================================================= */
#mode-standard {
  position: relative;
}
.standard-core {
  position: relative;
  width: 140px; height: 140px;
  display: flex; align-items: center; justify-content: center;
  z-index: 5;
}
.core-ring {
  position: absolute;
  width: 100%; height: 100%;
  border-radius: 50%;
  border: 2.5px solid transparent;
  border-top-color: #00bfff;
  border-right-color: #00bfff;
  border-bottom-color: rgba(0,191,255,0.25);
  border-left-color: rgba(0,191,255,0.25);
  animation: spin 3s linear infinite;
  box-shadow: 0 0 25px rgba(0,191,255,0.25);
}
.core-ring.purple {
  border-top-color: #a855f7;
  border-right-color: #a855f7;
  border-bottom-color: rgba(168,85,247,0.25);
  border-left-color: rgba(168,85,247,0.25);
  box-shadow: 0 0 25px rgba(168,85,247,0.25);
}
@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
.core-inner {
  width: 105px; height: 105px;
  border-radius: 50%;
  background: radial-gradient(circle, #0a1628 0%, #000 100%);
  display: flex; align-items: center; justify-content: center;
  border: 1.5px solid rgba(0,191,255,0.3);
  box-shadow: 0 0 30px rgba(0,191,255,0.2), inset 0 0 20px rgba(0,191,255,0.1);
}
.core-icon svg {
  width: 32px; height: 32px; fill: #00bfff;
  filter: drop-shadow(0 0 8px rgba(0,191,255,0.7));
}
.glow-ring {
  position: absolute;
  width: 165px; height: 165px;
  border-radius: 50%;
  border: 1.5px solid rgba(0,191,255,0.2);
  animation: glowPulse 2.2s ease-in-out infinite;
}
@keyframes glowPulse {
  0%, 100% { transform: scale(1); opacity: 0.6; }
  50% { transform: scale(1.1); opacity: 0.2; }
}
.outer-ring {
  position: absolute;
  width: 200px; height: 200px;
  border-radius: 50%;
  border: 1px dashed rgba(0,191,255,0.15);
  animation: spin 9s linear infinite reverse;
}
.outer-ring-2 {
  position: absolute;
  width: 240px; height: 240px;
  border-radius: 50%;
  border: 1px solid rgba(0,191,255,0.06);
  animation: spin 14s linear infinite;
}

/* Soundbars Left & Right */
.sound-bars-wrap {
  position: absolute;
  display: flex; align-items: center; gap: 4px;
  height: 80px;
}
.sound-bars-wrap.left-bars { right: calc(50% + 80px); }
.sound-bars-wrap.right-bars { left: calc(50% + 80px); }
.sbar {
  width: 2.5px;
  border-radius: 2px;
  background: linear-gradient(to top, #0066ff, #00bfff);
  height: 8px;
  transition: height 0.08s ease;
}

/* Speaking waves */
.speaking-waves {
  position: absolute;
  width: 90%; max-width: 460px; height: 120px;
  top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  pointer-events: none;
  display: none;
}
.speaking-waves.active { display: block; }
.speaking-waves svg { width: 100%; height: 100%; overflow: visible; }
.wave-path {
  fill: none; stroke-linecap: round;
}
.wp-1 { stroke: #00bfff; stroke-width: 2; opacity: 0.85; }
.wp-2 { stroke: #0099ff; stroke-width: 1.5; opacity: 0.55; }
.wp-3 { stroke: #0077dd; stroke-width: 1; opacity: 0.35; }

/* Thinking orbits */
.thinking-orbits {
  position: absolute;
  width: 220px; height: 220px;
  top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  pointer-events: none;
  display: none;
}
.thinking-orbits.active { display: block; }
.orbit-ell {
  position: absolute;
  border: 1px solid rgba(168,85,247,0.35);
  border-radius: 50%;
  width: 200px; height: 80px;
  top: 50%; left: 50%;
  margin-top: -40px; margin-left: -100px;
}
.orbit-ell-1 { transform: rotate(-25deg); animation: spin 5s linear infinite; }
.orbit-ell-2 { transform: rotate(35deg); animation: spin 6.5s linear infinite; }
.orbit-ell-3 { transform: rotate(95deg); animation: spin 8s linear infinite; }
.orbit-node {
  position: absolute;
  width: 5px; height: 5px;
  background: #c084fc;
  border-radius: 50%;
  box-shadow: 0 0 6px #a855f7;
  right: -2px; top: calc(50% - 2.5px);
}

/* ========================================================= */
/* MODE 2: ARC REACTOR CORE (3D ROTATING GYRO CORE)          */
/* ========================================================= */
#mode-reactor {
  position: relative;
}
.reactor-assembly {
  position: relative;
  width: 190px; height: 190px;
  display: flex; align-items: center; justify-content: center;
}
.reactor-outer-tach {
  position: absolute;
  width: 180px; height: 180px;
  border-radius: 50%;
  border: 1px solid rgba(0, 229, 255, 0.35);
  animation: spin 16s linear infinite reverse;
}
.reactor-outer-tach::before {
  content: ''; position: absolute; inset: -4px;
  border-radius: 50%;
  border: 2px dashed rgba(0, 168, 255, 0.45);
}
.reactor-coils-ring {
  position: absolute;
  width: 140px; height: 140px;
  border-radius: 50%;
  border: 2px solid rgba(0, 229, 255, 0.25);
  animation: spin 8s linear infinite;
}
.coil-segment {
  position: absolute;
  width: 6px; height: 14px;
  background: linear-gradient(to bottom, #ff9900, #ff5500);
  border-radius: 1px;
  box-shadow: 0 0 6px rgba(255, 140, 0, 0.8);
  top: 2px; left: calc(50% - 3px);
  transform-origin: 3px 68px;
}
.reactor-inner-ring {
  position: absolute;
  width: 104px; height: 104px;
  border-radius: 50%;
  border: 2.5px solid rgba(0, 229, 255, 0.75);
  box-shadow: 0 0 20px rgba(0, 229, 255, 0.5), inset 0 0 15px rgba(0, 229, 255, 0.3);
  animation: spin 4s linear infinite reverse;
}
.reactor-core-disc {
  width: 74px; height: 74px;
  border-radius: 50%;
  background: radial-gradient(circle, #ffffff 0%, #00e5ff 40%, #0044aa 80%, #000000 100%);
  display: flex; align-items: center; justify-content: center;
  box-shadow: 0 0 35px #00e5ff, 0 0 70px rgba(0, 229, 255, 0.4);
  animation: pulseReactor 1.5s ease-in-out infinite;
}
@keyframes pulseReactor {
  0%, 100% { transform: scale(1); filter: brightness(1); }
  50% { transform: scale(1.06); filter: brightness(1.25); }
}
.reactor-symbol svg {
  width: 28px; height: 28px; fill: #00122b;
}

/* ========================================================= */
/* MODE 3: 3D HOLOGRAPHIC FACE AVATAR                        */
/* ========================================================= */
#mode-face {
  position: relative;
}
#face-canvas {
  width: 220px; height: 210px;
  display: block;
}

/* ========================================================= */
/* MODE 4: HOLOGRAPHIC ORB                                   */
/* ========================================================= */
#mode-orb {
  position: relative;
}
.orb-assembly {
  position: relative;
  width: 180px; height: 180px;
  display: flex; align-items: center; justify-content: center;
}
.orb-sphere {
  width: 80px; height: 80px;
  border-radius: 50%;
  background: radial-gradient(circle at 35% 35%, #00ffff 0%, #0066ff 60%, #001133 100%);
  box-shadow: 0 0 40px rgba(0, 191, 255, 0.6), inset 0 0 15px rgba(255,255,255,0.4);
  animation: pulseOrb 2s ease-in-out infinite;
}
@keyframes pulseOrb {
  0%, 100% { transform: scale(1); }
  50% { transform: scale(1.05); }
}
.orb-gyro-1 {
  position: absolute;
  width: 140px; height: 50px;
  border: 1.5px solid rgba(0, 191, 255, 0.5);
  border-radius: 50%;
  transform: rotate(-30deg);
  animation: spin 5s linear infinite;
}
.orb-gyro-2 {
  position: absolute;
  width: 140px; height: 50px;
  border: 1.5px solid rgba(0, 229, 255, 0.4);
  border-radius: 50%;
  transform: rotate(45deg);
  animation: spin 7s linear infinite reverse;
}
.orb-spectrum {
  position: absolute;
  bottom: -22px;
  display: flex; gap: 3px; align-items: center;
  height: 24px;
}
.orb-sbar {
  width: 2.5px; height: 6px;
  background: #00bfff;
  border-radius: 1px;
}

/* Footer Label */
.mode-label {
  position: absolute;
  bottom: 8px;
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 2.5px;
  color: rgba(255, 255, 255, 0.85);
  text-shadow: 0 0 10px rgba(0, 191, 255, 0.5);
  text-transform: uppercase;
}
</style>
</head>
<body>
<div class="root">
  <!-- Top Status Badge -->
  <div class="status-pill">
    <div class="status-dot" id="status-dot"></div>
    <span id="status-text">LISTENING</span>
  </div>

  <!-- MODE 1: STANDARD (FIGMA DESIGN HUD) -->
  <div class="mode-container" id="mode-standard">
    <div class="sound-bars-wrap left-bars" id="standard-bars-left"></div>
    <div class="standard-core">
      <div class="core-ring" id="standard-core-ring"></div>
      <div class="glow-ring" id="standard-glow-ring"></div>
      <div class="outer-ring"></div>
      <div class="outer-ring-2"></div>
      <div class="core-inner">
        <div class="core-icon" id="standard-icon">
          <!-- Mic Icon -->
          <svg viewBox="0 0 24 24"><path d="M12 14c1.66 0 3-1.34 3-3V5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3z"/><path d="M17 11c0 2.76-2.24 5-5 5s-5-2.24-5-5H5c0 3.53 2.61 6.43 6 6.92V21h2v-3.08c3.39-.49 6-3.39 6-6.92h-2z"/></svg>
        </div>
      </div>
    </div>
    <div class="sound-bars-wrap right-bars" id="standard-bars-right"></div>
    <!-- Speaking SVG Waves -->
    <div class="speaking-waves" id="standard-waves">
      <svg viewBox="0 0 460 120">
        <path class="wave-path wp-1" id="swave-1" d="M 0,60 Q 115,20 230,60 T 460,60"></path>
        <path class="wave-path wp-2" id="swave-2" d="M 0,60 Q 115,90 230,60 T 460,60"></path>
        <path class="wave-path wp-3" id="swave-3" d="M 0,60 Q 115,40 230,60 T 460,60"></path>
      </svg>
    </div>
    <!-- Thinking Orbits -->
    <div class="thinking-orbits" id="standard-orbits">
      <div class="orbit-ell orbit-ell-1"><div class="orbit-node"></div></div>
      <div class="orbit-ell orbit-ell-2"><div class="orbit-node"></div></div>
      <div class="orbit-ell orbit-ell-3"><div class="orbit-node"></div></div>
    </div>
    <div class="mode-label">SPARK STANDARD HUD</div>
  </div>

  <!-- MODE 2: ARC REACTOR CORE -->
  <div class="mode-container" id="mode-reactor">
    <div class="reactor-assembly">
      <div class="reactor-outer-tach"></div>
      <div class="reactor-coils-ring" id="reactor-coils"></div>
      <div class="reactor-inner-ring"></div>
      <div class="reactor-core-disc">
        <div class="reactor-symbol">
          <!-- Lightning Icon -->
          <svg viewBox="0 0 24 24"><path d="M7 2v11h3v9l7-12h-4l4-8z"/></svg>
        </div>
      </div>
    </div>
    <div class="mode-label">SPARK ARC REACTOR CORE</div>
  </div>

  <!-- MODE 3: 3D HOLOGRAPHIC FACE AVATAR -->
  <div class="mode-container" id="mode-face">
    <canvas id="face-canvas" width="220" height="210"></canvas>
    <div class="mode-label">SPARK 3D FACE AVATAR</div>
  </div>

  <!-- MODE 4: HOLOGRAPHIC BLUE ORB -->
  <div class="mode-container" id="mode-orb">
    <div class="orb-assembly">
      <div class="orb-gyro-1"></div>
      <div class="orb-gyro-2"></div>
      <div class="orb-sphere"></div>
      <div class="orb-spectrum" id="orb-spectrum"></div>
    </div>
    <div class="mode-label">SPARK HOLOGRAPHIC ORB</div>
  </div>
</div>

<script>
// State Management
let currentMode = '{{initialMode}}';
let currentState = 'LISTENING';
let currentAudioLevel = 0.2;
let phase = 0;

// Setup Standard Soundbars
const leftWrap = document.getElementById('standard-bars-left');
const rightWrap = document.getElementById('standard-bars-right');
const barCount = 18;
const leftBars = [];
const rightBars = [];

for (let i = 0; i < barCount; i++) {
  const lb = document.createElement('div');
  lb.className = 'sbar';
  leftWrap.appendChild(lb);
  leftBars.push(lb);

  const rb = document.createElement('div');
  rb.className = 'sbar';
  rightWrap.appendChild(rb);
  rightBars.push(rb);
}

// Setup Reactor Coils (10 copper segments)
const coilsWrap = document.getElementById('reactor-coils');
for (let i = 0; i < 10; i++) {
  const coil = document.createElement('div');
  coil.className = 'coil-segment';
  coil.style.transform = `rotate(${i * 36}deg)`;
  coilsWrap.appendChild(coil);
}

// Setup Orb Spectrum
const orbSpec = document.getElementById('orb-spectrum');
const orbBars = [];
for (let i = 0; i < 16; i++) {
  const ob = document.createElement('div');
  ob.className = 'orb-sbar';
  orbSpec.appendChild(ob);
  orbBars.push(ob);
}

// 3D Face Wireframe Setup
const faceCanvas = document.getElementById('face-canvas');
const fctx = faceCanvas ? faceCanvas.getContext('2d') : null;

// Base 3D Face Mesh Nodes (Simplified Facial Topology)
const faceNodes = [
  // Forehead & Crown
  { x: 0, y: -80, z: 20 },
  { x: -35, y: -70, z: 15 }, { x: 35, y: -70, z: 15 },
  { x: -55, y: -50, z: -5 }, { x: 55, y: -50, z: -5 },
  // Brow & Temples
  { x: -28, y: -40, z: 30 }, { x: 0, y: -38, z: 32 }, { x: 28, y: -40, z: 30 },
  // Eyes (Left / Right)
  { x: -26, y: -25, z: 24, eye: 'left' }, { x: 26, y: -25, z: 24, eye: 'right' },
  // Nose Bridge & Tip
  { x: 0, y: -20, z: 35 }, { x: 0, y: 0, z: 45 },
  { x: -14, y: 5, z: 30 }, { x: 14, y: 5, z: 30 },
  // Cheekbones
  { x: -50, y: -10, z: 10 }, { x: 50, y: -10, z: 10 },
  // Mouth / Lips
  { x: -20, y: 24, z: 28, lip: true }, { x: 0, y: 22, z: 32, lip: true }, { x: 20, y: 24, z: 28, lip: true },
  { x: 0, y: 32, z: 28, jaw: true },
  // Jawline & Chin
  { x: -35, y: 45, z: 15 }, { x: 35, y: 45, z: 15 },
  { x: -18, y: 62, z: 25, jaw: true }, { x: 18, y: 62, z: 25, jaw: true },
  { x: 0, y: 72, z: 26, jaw: true }
];

// Face Triangulation Edges
const faceEdges = [
  [0,1], [0,2], [1,3], [2,4], [1,5], [2,7], [0,6], [5,6], [6,7],
  [5,8], [7,9], [8,10], [9,10], [10,11], [11,12], [11,13],
  [3,14], [4,15], [14,12], [15,13], [12,16], [13,18], [16,17], [17,18],
  [16,19], [18,19], [17,19], [14,20], [15,21], [20,22], [21,23],
  [22,24], [23,24], [19,24]
];

let blinkT = 0;
let blinkProgress = 0;

function drawFace3D(cx, cy, scale) {
  if (!fctx) return;
  fctx.clearRect(0, 0, faceCanvas.width, faceCanvas.height);

  // Gentle head rotation
  const rotY = Math.sin(phase * 0.8) * 0.15;
  const rotX = Math.cos(phase * 0.6) * 0.08;

  // Blinking cycle every 3-4 seconds
  blinkT += 0.016;
  if (blinkT > 3.2) {
    blinkProgress = Math.sin((blinkT - 3.2) * Math.PI * 5);
    if (blinkT > 3.6) { blinkT = 0; blinkProgress = 0; }
  }

  // Mouth/Jaw movement based on audio & state
  const jawOffset = (currentState === 'SPEAKING' || currentAudioLevel > 0.25)
    ? Math.sin(phase * 12.0) * (currentAudioLevel * 9.0)
    : 0;

  // Project 3D nodes to 2D
  const proj = faceNodes.map(n => {
    let nx = n.x;
    let ny = n.y;
    let nz = n.z;

    if (n.jaw) ny += Math.max(0, jawOffset);
    if (n.eye && blinkProgress > 0) ny += blinkProgress * 4;

    // Rotate Y
    const cosY = Math.cos(rotY), sinY = Math.sin(rotY);
    const x1 = nx * cosY + nz * sinY;
    const z1 = -nx * sinY + nz * cosY;

    // Rotate X
    const cosX = Math.cos(rotX), sinX = Math.sin(rotX);
    const y1 = ny * cosX - z1 * sinX;
    const z2 = ny * sinX + z1 * cosX;

    const fov = 260;
    const pz = (z2 + 200) / fov;
    return {
      x: cx + (x1 / pz) * scale,
      y: cy + (y1 / pz) * scale,
      eye: n.eye
    };
  });

  // Draw Wireframe Lines
  fctx.lineWidth = 1.3;
  fctx.strokeStyle = 'rgba(0, 229, 255, 0.45)';
  fctx.beginPath();
  for (const [a, b] of faceEdges) {
    fctx.moveTo(proj[a].x, proj[a].y);
    fctx.lineTo(proj[b].x, proj[b].y);
  }
  fctx.stroke();

  // Draw Glowing Nodes & Eyes
  for (const pt of proj) {
    if (pt.eye) {
      // Eye glow
      fctx.fillStyle = '#00ffff';
      fctx.beginPath();
      fctx.arc(pt.x, pt.y, 3.5, 0, Math.PI * 2);
      fctx.fill();
    } else {
      fctx.fillStyle = 'rgba(0, 168, 255, 0.7)';
      fctx.beginPath();
      fctx.arc(pt.x, pt.y, 1.8, 0, Math.PI * 2);
      fctx.fill();
    }
  }

  // Holographic scanlines overlay
  fctx.fillStyle = 'rgba(0, 229, 255, 0.08)';
  const scanY = (phase * 60) % faceCanvas.height;
  fctx.fillRect(0, scanY, faceCanvas.width, 2);
}

// Main 60 FPS Render Loop
function render() {
  phase += 0.025;

  // Animate Standard Soundbars
  if (currentMode === 'standard') {
    const amp = Math.max(0.15, currentAudioLevel);
    for (let i = 0; i < barCount; i++) {
      const wave = Math.abs(Math.sin(phase * 4.0 + i * 0.35));
      const h = Math.min(60, 6 + wave * 45 * amp);
      leftBars[i].style.height = `${h}px`;
      rightBars[i].style.height = `${h}px`;
    }

    // Animate Speaking Waves
    if (currentState === 'SPEAKING') {
      const sw1 = document.getElementById('swave-1');
      const sw2 = document.getElementById('swave-2');
      const sw3 = document.getElementById('swave-3');
      const p1 = Math.sin(phase * 5.0) * 22 * amp;
      const p2 = Math.cos(phase * 4.2) * 26 * amp;
      if (sw1) sw1.setAttribute('d', `M 0,60 Q 115,${60 - p1} 230,60 T 460,60`);
      if (sw2) sw2.setAttribute('d', `M 0,60 Q 115,${60 + p2} 230,60 T 460,60`);
      if (sw3) sw3.setAttribute('d', `M 0,60 Q 115,${60 - p2 * 0.7} 230,60 T 460,60`);
    }
  }

  // Animate 3D Face
  if (currentMode === 'face') {
    drawFace3D(110, 105, 1.0);
  }

  // Animate Orb spectrum
  if (currentMode === 'non-face' || currentMode === 'orb') {
    const amp = Math.max(0.15, currentAudioLevel);
    for (let i = 0; i < 16; i++) {
      const h = 4 + Math.abs(Math.sin(phase * 3.5 + i * 0.4)) * 18 * amp;
      orbBars[i].style.height = `${h}px`;
    }
  }

  requestAnimationFrame(render);
}

// Public Bridge Functions called by WinUI C#
window.setVisualMode = function(mode) {
  if (!mode) return;
  const m = mode.toLowerCase();
  currentMode = (m === 'orb') ? 'non-face' : m;

  document.querySelectorAll('.mode-container').forEach(el => el.classList.remove('active'));
  const targetId = (currentMode === 'non-face') ? 'mode-orb' : `mode-${currentMode}`;
  const target = document.getElementById(targetId);
  if (target) {
    target.classList.add('active');
  } else {
    document.getElementById('mode-standard').classList.add('active');
  }
};

window.setVoiceState = function(state) {
  if (!state) return;
  currentState = state.toUpperCase();
  const textEl = document.getElementById('status-text');
  const dotEl = document.getElementById('status-dot');
  const wavesEl = document.getElementById('standard-waves');
  const orbitsEl = document.getElementById('standard-orbits');
  const ringEl = document.getElementById('standard-core-ring');
  const glowEl = document.getElementById('standard-glow-ring');

  if (textEl) textEl.textContent = currentState;

  if (currentState === 'THINKING') {
    if (dotEl) { dotEl.style.background = '#a855f7'; dotEl.style.boxShadow = '0 0 8px #a855f7'; }
    if (ringEl) ringEl.classList.add('purple');
    if (glowEl) glowEl.style.borderColor = 'rgba(168,85,247,0.3)';
    if (wavesEl) wavesEl.classList.remove('active');
    if (orbitsEl) orbitsEl.classList.add('active');
  } else if (currentState === 'SPEAKING') {
    if (dotEl) { dotEl.style.background = '#00bfff'; dotEl.style.boxShadow = '0 0 8px #00bfff'; }
    if (ringEl) ringEl.classList.remove('purple');
    if (glowEl) glowEl.style.borderColor = 'rgba(0,191,255,0.2)';
    if (wavesEl) wavesEl.classList.add('active');
    if (orbitsEl) orbitsEl.classList.remove('active');
  } else {
    // LISTENING / IDLE
    if (dotEl) { dotEl.style.background = '#00bfff'; dotEl.style.boxShadow = '0 0 8px #00bfff'; }
    if (ringEl) ringEl.classList.remove('purple');
    if (glowEl) glowEl.style.borderColor = 'rgba(0,191,255,0.2)';
    if (wavesEl) wavesEl.classList.remove('active');
    if (orbitsEl) orbitsEl.classList.remove('active');
  }
};

window.setAudioLevel = function(level) {
  currentAudioLevel = Math.max(0.0, Math.min(1.0, parseFloat(level) || 0.0));
};

// Initial activation
window.setVisualMode(currentMode);
window.setVoiceState(currentState);
requestAnimationFrame(render);
</script>
</body>
</html>
""";
    }
}
