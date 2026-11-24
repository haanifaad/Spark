"""
SPARK Voice Mode — Premium Holographic & Face UI
=================================================
Dual Visual Experiences:
  1. FACE MODE: Real Mark-LVII 3D human head avatar (HoloAvatar) with measured facial
     geometry, eye tracking, blinking, expressions, and viseme-driven lip sync.
  2. NON-FACE MODE: Custom SPARK holographic interface inspired by the official design:
     - Deep space black (#02050B) & navy (#050B17)
     - Electric blue (#00A8FF / #007BFF) and cyan (#00E5FF) illumination
     - LISTENING: Glowing holographic mic, concentric rings, horizontal live audio waveform,
       "Voice Input Active / Speak clearly...", "Listening... I'm listening, Sir..."
     - THINKING: Holographic AI brain in glowing orb, 3D orbital rings, neural particles,
       left status card (SPARK AI / Understanding / Planning / Executing), right capabilities card
     - SPEAKING: Glowing futuristic speaker symbol, horizontal audio-reactive waveforms,
       concentric sound waves, "VOICE RESPONSE / Audio response playing...", "Speaking... Here you go."
  3. REASONING UI:
     - Dedicated operational intelligence workspace
     - Central AI Processing Core with gyro rings and neural mesh
     - 5-stage pipeline: UNDERSTANDING -> PLANNING -> EXECUTION -> VERIFICATION -> COMPLETED
     - LIVE ACTIVITY real-time operational stream
     - TASK PLAN structured checklist
     - TOOLS & OPERATIONS monitor
     - VERIFICATION results panel
  4. MODEL SWITCHER:
     - Live dropdown supporting Gemini, Claude, GPT-4, and Local Ollama models
     - Connects to SPARK model configurations and environment keys
  5. SETTINGS:
     - VISUAL EXPERIENCE: [ FACE MODE ] [ NON-FACE MODE ] (persisted across restarts)
     - Wake Word ("Hey Spark", 2-min auto-sleep)
     - Audio devices, continuous conversation
"""
from __future__ import annotations

import collections
import datetime
import json
import math
import os
import platform
import queue
import random
import sys
import threading
import time
from pathlib import Path
from typing import Callable, Dict, List, Optional, Tuple

import numpy as np
import psutil

from PyQt6.QtCore import (
    QEasingCurve, QLineF, QObject, QPoint, QPointF, QRect, QRectF, QSize,
    Qt, QTimer, pyqtSignal,
)
from PyQt6.QtGui import (
    QBrush, QColor, QFont, QFontDatabase, QLinearGradient, QPainter,
    QPainterPath, QPen, QPolygonF, QRadialGradient,
)
from PyQt6.QtWidgets import (
    QApplication, QComboBox, QDialog, QFileDialog, QFrame, QHBoxLayout,
    QLabel, QLineEdit, QMainWindow, QPushButton, QScrollArea, QSizePolicy,
    QStackedWidget, QTextEdit, QVBoxLayout, QWidget,
)

# ── Import Reusable Mark-LVII Avatar & Audio Components ─────────────────────
try:
    from core.avatar import HoloAvatar
except Exception as _err:
    HoloAvatar = None
    print(f"[SparkAvatar] HoloAvatar fallback active: {_err}")

try:
    from gui.reactor import Reactor3D
except Exception as _r_err:
    Reactor3D = None
    print(f"[SparkReactor] Reactor3D fallback active: {_r_err}")

try:
    from core.viseme import VisemeStream, text_to_visemes
except Exception:
    VisemeStream = None

try:
    from core.wake_word import (
        WakeWordDetector, matches_wake_phrase, WAKE_PHRASES, AUTO_SLEEP_TIMEOUT
    )
except Exception:
    WakeWordDetector = None
    matches_wake_phrase = lambda t: None
    WAKE_PHRASES = ["hey spark", "spark whats up", "good morning spark", "spark"]
    AUTO_SLEEP_TIMEOUT = 120.0

# ── Color Palette & Constants ───────────────────────────────────────────────
class Palette:
    BG_DARK     = QColor(2, 5, 11)       # #02050B Deep black
    BG_NAVY     = QColor(5, 11, 23)      # #050B17 Secondary dark navy
    BG_PANEL    = QColor(8, 18, 38, 220) # Glass panel backing
    BORDER_CYAN = QColor(0, 229, 255, 60) # Subtle cyan border
    BORDER_GLOW = QColor(0, 168, 255, 160)
    PRIMARY     = QColor(0, 123, 255)    # #007BFF
    ELEC_BLUE   = QColor(0, 168, 255)    # #00A8FF
    CYAN        = QColor(0, 229, 255)    # #00E5FF
    TEXT_MAIN   = QColor(234, 245, 255)  # #EAF5FF
    TEXT_MUTED  = QColor(143, 169, 200)  # #8FA9C8
    TEXT_DIM    = QColor(90, 120, 155)
    SUCCESS     = QColor(0, 255, 170)
    WARNING     = QColor(255, 180, 0)
    ERROR       = QColor(255, 60, 90)


def base_dir() -> Path:
    if getattr(sys, "frozen", False):
        return Path(sys.executable).parent
    return Path(__file__).resolve().parent


CONFIG_FILE = base_dir() / "config" / "voice_mode_settings.json"


def load_voice_settings() -> dict:
    defaults = {
        "visual_mode": "non-face",  # "non-face" | "face"
        "model": "models/gemini-2.5-flash-native-audio-preview-12-2025",
        "wake_word_enabled": True,
        "auto_sleep_timeout": 120.0,
        "continuous_conversation": True,
        "input_device": "default",
        "output_device": "default",
    }
    try:
        if CONFIG_FILE.exists():
            data = json.loads(CONFIG_FILE.read_text(encoding="utf-8"))
            defaults.update(data)
    except Exception:
        pass
    return defaults


def save_voice_settings(settings: dict) -> None:
    try:
        CONFIG_FILE.parent.mkdir(parents=True, exist_ok=True)
        CONFIG_FILE.write_text(json.dumps(settings, indent=2), encoding="utf-8")
    except Exception as e:
        print(f"[Settings] Error saving voice settings: {e}")


# ── Holographic Brain Data Structure ────────────────────────────────────────
def generate_brain_geometry():
    """Generate fixed 3D cortex nodes and synaptic connections for the thinking state."""
    rng = random.Random(42)
    nodes = []
    # Left & Right hemispheres
    for hemi in (-1, 1):
        for _ in range(35):
            u = rng.uniform(-1.0, 1.0)
            theta = rng.uniform(0, math.pi * 2)
            # Oval brain hemisphere shape
            r = math.sqrt(max(0.01, 1.0 - u * u))
            x = hemi * (0.35 + 0.45 * r * abs(math.cos(theta)))
            y = 0.8 * u + rng.uniform(-0.08, 0.08)
            z = 0.65 * r * math.sin(theta) + rng.uniform(-0.06, 0.06)
            nodes.append((x, y, z))

    edges = []
    for i in range(len(nodes)):
        for j in range(i + 1, len(nodes)):
            dx = nodes[i][0] - nodes[j][0]
            dy = nodes[i][1] - nodes[j][1]
            dz = nodes[i][2] - nodes[j][2]
            dist = math.sqrt(dx * dx + dy * dy + dz * dz)
            if dist < 0.38 and rng.random() > 0.45:
                edges.append((i, j))
    return nodes, edges


BRAIN_NODES, BRAIN_EDGES = generate_brain_geometry()


# ── SPARK Central Visual Canvas ─────────────────────────────────────────────
class SparkVisualCanvas(QWidget):
    """
    Central stage rendering BOTH:
    - FACE MODE: Genuine Mark-LVII 3D HoloAvatar (with measured mesh, eye tracking,
      blinking, lip sync, expressions).
    - NON-FACE MODE: Custom SPARK Holographic HUD:
      * LISTENING: Central mic, concentric rings, horizontal live audio waveform.
      * THINKING: 3D holographic AI brain, orbital gyro rings, neural nodes, technical HUD cards.
      * SPEAKING: Central futuristic speaker, audio-reactive horizontal waveforms, circular waves.
    """
    stateChanged = pyqtSignal(str)

    def __init__(self, visual_mode: str = "non-face", parent=None):
        super().__init__(parent)
        self.setAttribute(Qt.WidgetAttribute.WA_OpaquePaintEvent)
        self.setMinimumSize(700, 480)
        self.setSizePolicy(QSizePolicy.Policy.Expanding, QSizePolicy.Policy.Expanding)

        self._visual_mode = visual_mode
        self._state = "LISTENING"  # "IDLE" | "LISTENING" | "THINKING" | "SPEAKING" | "ERROR" | "SLEEPING"
        self._live_mic_amp = 0.0
        self._live_spk_amp = 0.0
        self._disp_amp = 0.0
        self._transcription = ""

        # Avatar instance for Face Mode
        self._avatar = None
        if HoloAvatar is not None:
            try:
                self._avatar = HoloAvatar()
            except Exception as e:
                print(f"[Avatar] Failed to instantiate HoloAvatar: {e}")

        # Reactor3D instance for Arc Reactor Mode
        self._reactor = None
        if Reactor3D is not None:
            try:
                self._reactor = Reactor3D()
            except Exception as e:
                print(f"[Reactor] Failed to instantiate Reactor3D: {e}")

        # Non-Face animation parameters
        self._phase = 0.0
        self._orbit_rot_y = 0.0
        self._orbit_rot_x = 0.0
        self._ring_pulse = 0.0
        self._last_frame_t = time.monotonic()
        self._wave_history = collections.deque([0.0] * 64, maxlen=64)

        # 60 FPS animation loop
        self._timer = QTimer(self)
        self._timer.timeout.connect(self._on_tick)
        self._timer.start(16)

    @property
    def visual_mode(self) -> str:
        return self._visual_mode

    @visual_mode.setter
    def visual_mode(self, mode: str) -> None:
        self._visual_mode = mode.lower()
        self.update()

    @property
    def state(self) -> str:
        return self._state

    def set_state(self, state: str, subtitle: Optional[str] = None) -> None:
        self._state = state.upper()
        if subtitle:
            self._transcription = subtitle
        self.stateChanged.emit(self._state)
        self.update()

    def set_mic_level(self, level: float) -> None:
        self._live_mic_amp = max(0.0, min(1.0, level))

    def set_speaker_level(self, level: float) -> None:
        self._live_spk_amp = max(0.0, min(1.0, level))

    def set_transcription(self, text: str) -> None:
        self._transcription = text
        self.update()

    def _on_tick(self) -> None:
        now = time.monotonic()
        dt = min(0.1, max(0.001, now - self._last_frame_t))
        self._last_frame_t = now

        # Smooth audio amplitude
        target_amp = self._live_spk_amp if self._state == "SPEAKING" else self._live_mic_amp
        self._disp_amp += (target_amp - self._disp_amp) * min(1.0, dt * 15.0)
        self._wave_history.append(self._disp_amp)

        # Non-face rotations
        speed = 1.0
        if self._state == "THINKING":
            speed = 2.4
        elif self._state == "SPEAKING":
            speed = 1.6
        self._phase += dt * speed
        self._orbit_rot_y += dt * 0.8 * speed
        self._orbit_rot_x += dt * 0.4 * speed
        self._ring_pulse = (math.sin(self._phase * 3.0) + 1.0) * 0.5

        # Update Face Mode avatar if active
        if self._visual_mode == "face" and self._avatar is not None:
            is_speaking = (self._state == "SPEAKING")
            try:
                self._avatar.step(dt, self._disp_amp, speaking=is_speaking)
            except Exception:
                pass

        # Update Reactor3D if active
        if self._visual_mode == "reactor" and self._reactor is not None:
            is_speaking = (self._state == "SPEAKING")
            is_thinking = (self._state == "THINKING")
            is_muted = (self._state == "SLEEPING" or self._state == "IDLE")
            try:
                self._reactor.step(dt, speaking=is_speaking, thinking=is_thinking, muted=is_muted)
            except Exception:
                pass

        self.update()

    def paintEvent(self, _):
        p = QPainter(self)
        p.setRenderHint(QPainter.RenderHint.Antialiasing)
        p.setRenderHint(QPainter.RenderHint.SmoothPixmapTransform)

        w = self.width()
        h = self.height()
        cx = w / 2.0
        cy = h / 2.0

        # Draw deep holographic backdrop
        bg_grad = QRadialGradient(cx, cy, max(w, h) * 0.75)
        bg_grad.setColorAt(0.0, QColor(6, 16, 36))
        bg_grad.setColorAt(0.6, Palette.BG_NAVY)
        bg_grad.setColorAt(1.0, Palette.BG_DARK)
        p.fillRect(0, 0, w, h, bg_grad)

        # Draw futuristic tech grid & perspective floor lines
        self._draw_ambient_hud(p, w, h, cx, cy)

        mode = self._visual_mode.lower()
        if mode == "face" and self._avatar is not None:
            self._paint_face_mode(p, cx, cy, min(w, h))
        elif mode == "reactor" and self._reactor is not None:
            self._paint_reactor_mode(p, cx, cy, min(w, h))
        elif mode == "standard":
            self._paint_standard_mode(p, cx, cy, min(w, h))
        else:
            # Default to "orb" / "non-face"
            self._paint_non_face_mode(p, cx, cy, min(w, h))

    # ── Ambient HUD Layer ───────────────────────────────────────────────────
    def _draw_ambient_hud(self, p: QPainter, w: int, h: int, cx: float, cy: float):
        # Corner aesthetic brackets
        pen_bracket = QPen(Palette.BORDER_CYAN, 1.5)
        p.setPen(pen_bracket)
        b_len = 28
        # Top-Left
        p.drawLine(16, 16, 16 + b_len, 16)
        p.drawLine(16, 16, 16, 16 + b_len)
        # Top-Right
        p.drawLine(w - 16, 16, w - 16 - b_len, 16)
        p.drawLine(w - 16, 16, w - 16, 16 + b_len)
        # Bottom-Left
        p.drawLine(16, h - 16, 16 + b_len, h - 16)
        p.drawLine(16, h - 16, 16, h - 16 - b_len)
        # Bottom-Right
        p.drawLine(w - 16, h - 16, w - 16 - b_len, h - 16)
        p.drawLine(w - 16, h - 16, w - 16, h - 16 - b_len)

        # Subtle circular base projection on bottom floor
        floor_y = h * 0.78
        base_rx = min(w * 0.38, 360)
        base_ry = 32
        p.setBrush(Qt.BrushStyle.NoBrush)
        p.setPen(QPen(QColor(0, 168, 255, 40), 1.2, Qt.PenStyle.DashLine))
        p.drawEllipse(QPointF(cx, floor_y), base_rx, base_ry)
        p.setPen(QPen(QColor(0, 229, 255, 80), 1.5))
        p.drawEllipse(QPointF(cx, floor_y), base_rx * 0.7, base_ry * 0.7)

    # ── MODE A: FACE MODE (Mark-LVII 3D Avatar) ─────────────────────────────
    def _paint_face_mode(self, p: QPainter, cx: float, cy: float, size: float):
        r_head = min(size * 0.32, 230)
        # Surrounding energy rings
        p.setBrush(Qt.BrushStyle.NoBrush)
        p.setPen(QPen(QColor(0, 168, 255, 60), 1.5))
        p.drawEllipse(QPointF(cx, cy), r_head * 1.25, r_head * 1.25)
        p.setPen(QPen(QColor(0, 229, 255, 40), 1.0, Qt.PenStyle.DashLine))
        p.drawEllipse(QPointF(cx, cy), r_head * 1.45, r_head * 1.45)

        # Render genuine Mark-LVII HoloAvatar with SPARK colors
        main_c = Palette.CYAN
        acc_c = Palette.ELEC_BLUE
        bg_c = Palette.BG_DARK
        try:
            self._avatar.paint(p, cx, cy, r_head, main_c, acc_c, bg_c)
        except Exception as e:
            # Fallback text if rendering error
            p.setPen(Palette.TEXT_MUTED)
            p.drawText(int(cx - 100), int(cy), "SPARK Avatar Mesh Active")

        # Top status indicator
        self._draw_state_header(p, cx, cy - r_head - 30)

    # ── MODE B: REACTOR MODE (Mark-LVII 3D Arc Reactor with SPARK Branding) ──────
    def _paint_reactor_mode(self, p: QPainter, cx: float, cy: float, size: float):
        r_core = min(size * 0.28, 190)
        main_c = Palette.CYAN
        acc_c = Palette.ELEC_BLUE
        white_c = Palette.TEXT_MAIN
        is_live = (self._state in ('LISTENING', 'SPEAKING', 'THINKING'))
        try:
            self._reactor.paint(p, cx, cy, r_core, main_c, acc_c, white_c,
                                amp=self._disp_amp, live=is_live, name='SPARK')
        except Exception as e:
            p.setPen(Palette.TEXT_MUTED)
            p.drawText(int(cx - 80), int(cy), 'SPARK Reactor Active')
        self._draw_state_header(p, cx, cy - r_core - 45)

    # ── MODE C: STANDARD MODE (Figma Voice HUD Design) ───────────────────────────
    def _paint_standard_mode(self, p: QPainter, cx: float, cy: float, size: float):
        r_core = min(size * 0.16, 95)
        is_thinking = (self._state == 'THINKING')
        is_speaking = (self._state == 'SPEAKING')
        is_listening = not (is_thinking or is_speaking)
        accent = QColor(168, 85, 247) if is_thinking else QColor(0, 191, 255)

        if is_listening:
            title = 'Listening...'
            sub = 'SPARK is listening...'
        elif is_speaking:
            title = 'Speaking...'
            sub = 'SPARK is responding...'
        else:
            title = 'Thinking...'
            sub = 'SPARK is processing your request...'

        self._draw_standard_header(p, cx, cy - r_core - 85, title, sub, accent)

        if is_listening or is_speaking:
            self._draw_standard_sound_bars(p, cx, cy, r_core * 1.55, accent)

        if is_speaking:
            self._draw_standard_speaking_waves(p, cx, cy, accent)

        if is_thinking:
            self._draw_standard_thinking_orbits(p, cx, cy, r_core, accent)

        p.setBrush(Qt.BrushStyle.NoBrush)
        p.setPen(QPen(QColor(accent.red(), accent.green(), accent.blue(), 30), 1.0))
        p.drawEllipse(QPointF(cx, cy), r_core * 1.75, r_core * 1.75)
        p.setPen(QPen(QColor(accent.red(), accent.green(), accent.blue(), 50), 1.2))
        p.drawEllipse(QPointF(cx, cy), r_core * 1.45, r_core * 1.45)
        glow_r = r_core * (1.15 + self._ring_pulse * 0.12)
        p.setPen(QPen(QColor(accent.red(), accent.green(), accent.blue(), int(60 + self._ring_pulse * 40)), 2.0))
        p.drawEllipse(QPointF(cx, cy), glow_r, glow_r)

        p.save()
        p.translate(cx, cy)
        p.rotate(math.degrees(self._phase * 1.8))
        spin_pen = QPen(accent, 3.0)
        p.setPen(spin_pen)
        p.drawArc(QRectF(-r_core, -r_core, r_core * 2, r_core * 2), 0, 180 * 16)
        dim_accent = QColor(accent.red(), accent.green(), accent.blue(), 75)
        p.setPen(QPen(dim_accent, 3.0))
        p.drawArc(QRectF(-r_core, -r_core, r_core * 2, r_core * 2), 180 * 16, 180 * 16)
        p.restore()

        core_inner_r = r_core * 0.75
        inner_grad = QRadialGradient(cx, cy, core_inner_r)
        inner_grad.setColorAt(0.0, QColor(10, 22, 40) if not is_thinking else QColor(25, 10, 45))
        inner_grad.setColorAt(1.0, QColor(0, 0, 0))
        p.setBrush(QBrush(inner_grad))
        p.setPen(QPen(QColor(accent.red(), accent.green(), accent.blue(), 80), 2.0))
        p.drawEllipse(QPointF(cx, cy), core_inner_r, core_inner_r)

        if is_listening:
            self._draw_microphone_icon(p, cx, cy, core_inner_r * 0.42)
        else:
            self._draw_star_icon(p, cx, cy, core_inner_r * 0.45)

        glow_bar_y = self.height() - 95
        p.setPen(Qt.PenStyle.NoPen)
        bglow = QRadialGradient(cx, glow_bar_y, 110)
        bglow.setColorAt(0.0, QColor(accent.red(), accent.green(), accent.blue(), 130))
        bglow.setColorAt(1.0, QColor(accent.red(), accent.green(), accent.blue(), 0))
        p.setBrush(QBrush(bglow))
        p.drawEllipse(QPointF(cx, glow_bar_y), 110, 5)
        self._draw_standard_wake_box(p, 40, self.height() - 75, accent)

    def _draw_standard_header(self, p: QPainter, cx: float, top_y: float, title: str, sub: str, accent: QColor):
        p.save()
        p.setBrush(QBrush(accent))
        p.setPen(Qt.PenStyle.NoPen)
        dot_r = 5.0 + self._ring_pulse * 1.5
        p.drawEllipse(QPointF(cx - 75, top_y + 10), dot_r, dot_r)
        p.setFont(QFont('Segoe UI', 18, QFont.Weight.Light))
        p.setPen(Palette.TEXT_MAIN)
        p.drawText(QRectF(cx - 60, top_y, 220, 28), Qt.AlignmentFlag.AlignLeft | Qt.AlignmentFlag.AlignVCenter, title)
        p.setFont(QFont('Segoe UI', 10))
        p.setPen(Palette.TEXT_MUTED)
        p.drawText(QRectF(cx - 150, top_y + 30, 300, 20), Qt.AlignmentFlag.AlignCenter, sub)
        p.restore()

    def _draw_standard_sound_bars(self, p: QPainter, cx: float, cy: float, offset: float, accent: QColor):
        p.save()
        bar_count = 18
        p.setPen(Qt.PenStyle.NoPen)
        for bi in range(bar_count):
            bh = 8.0 + abs(math.sin(self._phase * 4.0 + bi * 0.35)) * 42.0 * max(0.2, self._disp_amp)
            alpha = int(70 + (bh / 50.0) * 180)
            p.setBrush(QBrush(QColor(accent.red(), accent.green(), accent.blue(), min(255, alpha))))
            lx = cx - offset - (bar_count - 1 - bi) * 6
            p.drawRoundedRect(QRectF(lx, cy - bh / 2.0, 3, bh), 1.5, 1.5)
            rx = cx + offset + bi * 6
            p.drawRoundedRect(QRectF(rx, cy - bh / 2.0, 3, bh), 1.5, 1.5)
        p.restore()

    def _draw_standard_speaking_waves(self, p: QPainter, cx: float, cy: float, accent: QColor):
        p.save()
        p.setBrush(Qt.BrushStyle.NoBrush)
        w_span = 320.0
        for stroke_w, opac, speed_mult in [(2.5, 200, 1.0), (1.5, 130, 1.3), (1.0, 75, 1.6)]:
            wave_path = QPainterPath()
            wave_path.moveTo(cx - w_span, cy)
            phase_shift = self._phase * 3.5 * speed_mult
            steps = 40
            for s in range(steps + 1):
                t = s / steps
                px = cx - w_span + t * (w_span * 2.0)
                env = math.sin(t * math.pi)
                py = cy + math.sin(t * 8.0 + phase_shift) * 35.0 * env * max(0.2, self._disp_amp)
                wave_path.lineTo(px, py)
            p.setPen(QPen(QColor(accent.red(), accent.green(), accent.blue(), opac), stroke_w))
            p.drawPath(wave_path)
        p.restore()

    def _draw_standard_thinking_orbits(self, p: QPainter, cx: float, cy: float, r_core: float, accent: QColor):
        p.save()
        p.setBrush(Qt.BrushStyle.NoBrush)
        orbit_w = r_core * 2.2
        orbit_h = r_core * 0.9
        for o_idx, angle_deg in enumerate([-20.0, 40.0, 100.0]):
            rot = angle_deg + math.degrees(self._phase * (0.8 + o_idx * 0.2))
            p.save()
            p.translate(cx, cy)
            p.rotate(rot)
            p.setPen(QPen(QColor(accent.red(), accent.green(), accent.blue(), 75), 1.2))
            p.drawEllipse(QPointF(0, 0), orbit_w / 2.0, orbit_h / 2.0)
            p.setBrush(QBrush(QColor(220, 160, 255)))
            p.setPen(Qt.PenStyle.NoPen)
            p.drawEllipse(QPointF(orbit_w / 2.0, 0), 3.5, 3.5)
            p.restore()
        p.setPen(Qt.PenStyle.NoPen)
        p.setBrush(QBrush(QColor(accent.red(), accent.green(), accent.blue(), 100)))
        rng = random.Random(99)
        for _ in range(16):
            px = cx + rng.uniform(-180, 180)
            py = cy + rng.uniform(-130, 130) + math.sin(self._phase * 3.0 + rng.random() * 5.0) * 8.0
            p.drawEllipse(QPointF(px, py), 2, 2)
        p.restore()

    def _draw_star_icon(self, p: QPainter, cx: float, cy: float, r: float):
        p.save()
        p.setBrush(QBrush(Qt.GlobalColor.white))
        p.setPen(Qt.PenStyle.NoPen)
        star = QPolygonF()
        star.append(QPointF(cx, cy - r))
        star.append(QPointF(cx + r * 0.28, cy - r * 0.28))
        star.append(QPointF(cx + r, cy))
        star.append(QPointF(cx + r * 0.28, cy + r * 0.28))
        star.append(QPointF(cx, cy + r))
        star.append(QPointF(cx - r * 0.28, cy + r * 0.28))
        star.append(QPointF(cx - r, cy))
        star.append(QPointF(cx - r * 0.28, cy - r * 0.28))
        p.drawPolygon(star)
        p.restore()

    def _draw_standard_wake_box(self, p: QPainter, x: float, y: float, accent: QColor):
        p.save()
        box_w = 260
        box_h = 48
        p.setBrush(QBrush(QColor(255, 255, 255, 8)))
        p.setPen(QPen(QColor(255, 255, 255, 20), 1.0))
        p.drawRoundedRect(QRectF(x, y, box_w, box_h), box_h / 2.0, box_h / 2.0)
        p.setPen(Qt.PenStyle.NoPen)
        p.setBrush(QBrush(accent))
        ix = x + 16
        for b in range(5):
            bh = 6 + abs(math.sin(self._phase * 4.0 + b * 0.4)) * 16.0
            p.drawRoundedRect(QRectF(ix + b * 5, y + (box_h - bh) / 2.0, 3, bh), 1.5, 1.5)
        p.setFont(QFont('Segoe UI', 10))
        p.setPen(Palette.TEXT_MUTED)
        p.drawText(QRectF(x + 50, y + 8, 200, 16), 'Wake word: SPARK')
        p.setFont(QFont('Segoe UI', 8))
        p.setPen(Palette.TEXT_DIM)
        p.drawText(QRectF(x + 50, y + 25, 200, 14), 'Say "SPARK" to activate')
        p.restore()


    # ── MODE B: NON-FACE MODE (Holographic Interface) ────────────────────────
    def _paint_non_face_mode(self, p: QPainter, cx: float, cy: float, size: float):
        if self._state == "THINKING":
            self._paint_thinking_state(p, cx, cy, size)
        elif self._state == "SPEAKING":
            self._paint_speaking_state(p, cx, cy, size)
        else:
            # Default to LISTENING / IDLE
            self._paint_listening_state(p, cx, cy, size)

    # ── 1. LISTENING STATE (Matching Image 1) ───────────────────────────────
    def _paint_listening_state(self, p: QPainter, cx: float, cy: float, size: float):
        r_core = min(size * 0.18, 110)

        # Top State Header
        self._draw_state_header(p, cx, cy - r_core - 70, "Listening...", "I'm listening, Sir...")

        # Central Concentric Holographic Rings
        ring_counts = [1.0, 1.25, 1.55, 1.85]
        for idx, mult in enumerate(ring_counts):
            r = r_core * mult + (self._ring_pulse * 4.0 if idx == 1 else 0.0)
            alpha = int(140 - idx * 28)
            pen_style = Qt.PenStyle.DashLine if idx % 2 == 1 else Qt.PenStyle.SolidLine
            p.setPen(QPen(QColor(0, 168, 255, alpha), 1.5, pen_style))
            p.setBrush(Qt.BrushStyle.NoBrush)
            p.drawEllipse(QPointF(cx, cy), r, r)

        # Horizontal Audio Waveforms extending Left and Right
        self._draw_horizontal_waveform(p, cx, cy, r_core * 1.3, is_speaking=False)

        # Central Glowing Circle Backing
        glow = QRadialGradient(cx, cy, r_core)
        glow.setColorAt(0.0, QColor(0, 229, 255, 180))
        glow.setColorAt(0.5, QColor(0, 123, 255, 100))
        glow.setColorAt(1.0, QColor(0, 30, 80, 0))
        p.setBrush(QBrush(glow))
        p.setPen(Qt.PenStyle.NoPen)
        p.drawEllipse(QPointF(cx, cy), r_core, r_core)

        # Central Microphone Icon
        self._draw_microphone_icon(p, cx, cy, r_core * 0.45)

        # Bottom-Left Floating Panel: "Voice Input Active / Speak clearly..."
        self._draw_status_card(p, 40, self.height() - 110, 240, 68,
                               "Voice Input Active", "Speak clearly...", is_mic=True)

    # ── 2. THINKING STATE (Matching Image 2) ────────────────────────────────
    def _paint_thinking_state(self, p: QPainter, cx: float, cy: float, size: float):
        r_core = min(size * 0.22, 140)

        # Top State Header
        self._draw_state_header(p, cx, cy - r_core - 60, "Thinking...", "Let me process that, Sir...")

        # 3D Rotating Orbital Rings around AI Brain
        self._draw_orbital_rings(p, cx, cy, r_core * 1.35)

        # Central Translucent Orb with Holographic Brain
        orb = QRadialGradient(cx, cy, r_core)
        orb.setColorAt(0.0, QColor(0, 168, 255, 90))
        orb.setColorAt(0.7, QColor(0, 80, 180, 40))
        orb.setColorAt(1.0, QColor(0, 229, 255, 120))
        p.setBrush(QBrush(orb))
        p.setPen(QPen(Palette.CYAN, 1.8))
        p.drawEllipse(QPointF(cx, cy), r_core, r_core)

        # Draw Brain nodes and synapses
        self._draw_brain_mesh(p, cx, cy, r_core * 0.8)

        # Left HUD Panel: "SPARK AI / Your Personal AI Assistant"
        self._draw_thinking_left_panel(p, 40, cy - 110)

        # Right HUD Panel: "Capabilities: Voice / Visual / Computer / Autonomous / Memory / File"
        self._draw_thinking_right_panel(p, self.width() - 280, cy - 140)

        # Bottom-Left Floating Panel: "Thinking Mode / Analyzing your request..."
        self._draw_status_card(p, 40, self.height() - 110, 250, 68,
                               "Thinking Mode", "Analyzing your request...", is_pulse=True)

    # ── 3. SPEAKING STATE (Redesigned as per instructions) ───────────────────
    def _paint_speaking_state(self, p: QPainter, cx: float, cy: float, size: float):
        r_core = min(size * 0.18, 115)

        # Top State Header: Above the central visualization
        # Cyan pulse dot
        p.setBrush(QBrush(Palette.CYAN))
        p.setPen(Qt.PenStyle.NoPen)
        top_y = cy - r_core - 80
        p.drawEllipse(QPointF(cx - 75, top_y + 10), 5, 5)

        # Above: Speaking...
        p.setFont(QFont("Segoe UI", 21, QFont.Weight.Bold))
        p.setPen(Palette.TEXT_MAIN)
        p.drawText(QRectF(cx - 55, top_y, 220, 32), Qt.AlignmentFlag.AlignLeft, "Speaking...")

        # Subtle secondary status: SPARK is responding
        p.setFont(QFont("Segoe UI", 11))
        p.setPen(Palette.TEXT_MUTED)
        p.drawText(QRectF(cx - 150, top_y + 36, 300, 22), Qt.AlignmentFlag.AlignCenter, "SPARK is responding")

        # Rotating Orbital Elements around the Speaker
        self._draw_orbital_rings(p, cx, cy, r_core * 1.35)

        # Small Floating Energy Particles around the Speaker
        rng = random.Random(77)
        for i in range(16):
            base_ang = (i / 16.0) * math.pi * 2.0 + self._phase * 0.6
            p_dist = r_core * (1.1 + 0.35 * rng.random()) + self._disp_amp * 15.0
            px = cx + math.cos(base_ang) * p_dist
            py = cy + math.sin(base_ang) * p_dist * 0.75
            p_sz = 2.0 + 1.8 * math.sin(self._phase * 3.0 + i)
            p_alpha = max(30, min(220, int(120 + 80 * math.cos(self._phase * 2.0 + i))))
            p.setBrush(QBrush(QColor(0, 229, 255, p_alpha)))
            p.setPen(Qt.PenStyle.NoPen)
            p.drawEllipse(QPointF(px, py), p_sz, p_sz)

        # Concentric Holographic Rings & Circular Sound Waves emitting energy
        for i in range(1, 5):
            r = r_core + i * 26 + self._disp_amp * 22.0
            pulse_a = max(25, min(200, int(150 - i * 28 + self._disp_amp * 85)))
            p.setBrush(Qt.BrushStyle.NoBrush)
            pen_style = Qt.PenStyle.SolidLine if i % 2 == 1 else Qt.PenStyle.DashLine
            p.setPen(QPen(QColor(0, 229, 255, pulse_a), 1.5, pen_style))
            p.drawEllipse(QPointF(cx, cy), r, r)

        # Horizontal Audio-Reactive Waveforms extending Left & Right from Central Speaker
        self._draw_horizontal_waveform(p, cx, cy, r_core * 1.25, is_speaking=True)

        # Glowing Speaker Center Sphere emitting sound energy
        glow = QRadialGradient(cx, cy, r_core)
        glow.setColorAt(0.0, QColor(0, 229, 255, 220))
        glow.setColorAt(0.35, QColor(0, 168, 255, 140))
        glow.setColorAt(0.7, QColor(0, 123, 255, 60))
        glow.setColorAt(1.0, QColor(0, 20, 60, 0))
        p.setBrush(QBrush(glow))
        p.setPen(QPen(Palette.CYAN, 2.0))
        p.drawEllipse(QPointF(cx, cy), r_core, r_core)

        # Futuristic Central Speaker Symbol emitting energy
        self._draw_speaker_symbol(p, cx, cy, r_core * 0.45)

        # Below the central visualization: Here you go.
        below_y = cy + r_core + 28
        p.setFont(QFont("Segoe UI", 16, QFont.Weight.DemiBold))
        p.setPen(Palette.TEXT_MAIN)
        p.drawText(QRectF(cx - 150, below_y, 300, 28), Qt.AlignmentFlag.AlignCenter, "Here you go.")

        # Minimal subtitle area for current spoken sentence / transcription
        if self._transcription:
            p.setFont(QFont("Segoe UI", 10))
            p.setPen(Palette.TEXT_MUTED)
            rect_sub = QRectF(cx - 280, below_y + 32, 560, 26)
            p.drawText(rect_sub, Qt.AlignmentFlag.AlignCenter, f'"{self._transcription}"')

        # Bottom-Left Floating Panel: "VOICE RESPONSE / Audio response playing..."
        self._draw_status_card(p, 40, self.height() - 110, 260, 68,
                               "VOICE RESPONSE", "Audio response playing...", is_speaker=True)

    # ── Drawing Helper Methods ──────────────────────────────────────────────
    def _draw_state_header(self, p: QPainter, cx: float, top_y: float,
                           title: str = "Voice Mode", subtitle: str = "SPARK Intelligence"):
        # Cyan pulse dot
        p.setBrush(QBrush(Palette.CYAN))
        p.setPen(Qt.PenStyle.NoPen)
        p.drawEllipse(QPointF(cx - 70, top_y + 10), 5, 5)

        # Title
        p.setFont(QFont("Segoe UI", 20, QFont.Weight.Bold))
        p.setPen(Palette.TEXT_MAIN)
        p.drawText(QRectF(cx - 50, top_y, 220, 32), Qt.AlignmentFlag.AlignLeft, title)

        # Subtitle
        p.setFont(QFont("Segoe UI", 12))
        p.setPen(Palette.TEXT_MUTED)
        p.drawText(QRectF(cx - 150, top_y + 34, 300, 24), Qt.AlignmentFlag.AlignCenter, subtitle)

    def _draw_horizontal_waveform(self, p: QPainter, cx: float, cy: float, inner_r: float, is_speaking: bool):
        num_bars = 24
        bar_w = 3.5
        spacing = 8.5
        max_h = 75.0 if is_speaking else 55.0
        amp = self._disp_amp

        pen = QPen(Palette.CYAN, bar_w, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap)
        p.setPen(pen)

        for i in range(num_bars):
            dist_factor = 1.0 - (i / float(num_bars)) * 0.5
            freq = (i + 1) * 0.45
            wave_mod = abs(math.sin(self._phase * 4.0 + freq)) * amp
            h_bar = max(6.0, (12.0 + wave_mod * max_h) * dist_factor)

            x_offset = inner_r + i * spacing
            # Right side
            p.drawLine(QPointF(cx + x_offset, cy - h_bar / 2.0), QPointF(cx + x_offset, cy + h_bar / 2.0))
            # Left side
            p.drawLine(QPointF(cx - x_offset, cy - h_bar / 2.0), QPointF(cx - x_offset, cy + h_bar / 2.0))

    def _draw_microphone_icon(self, p: QPainter, cx: float, cy: float, sz: float):
        p.setPen(QPen(Palette.TEXT_MAIN, 3.0, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
        p.setBrush(QBrush(Palette.CYAN))
        # Mic capsule
        cap_w = sz * 0.5
        cap_h = sz * 0.85
        cap_rect = QRectF(cx - cap_w / 2.0, cy - cap_h / 2.0 - 4, cap_w, cap_h)
        p.drawRoundedRect(cap_rect, cap_w / 2.0, cap_w / 2.0)

        # Arc cradle around mic
        p.setBrush(Qt.BrushStyle.NoBrush)
        cradle_rect = QRectF(cx - cap_w * 0.75, cy - cap_h * 0.4, cap_w * 1.5, cap_h * 0.85)
        p.drawArc(cradle_rect, 0, -180 * 16)

        # Stand stem & base
        stem_y = cy + cap_h * 0.45
        p.drawLine(QPointF(cx, stem_y), QPointF(cx, stem_y + 12))
        p.drawLine(QPointF(cx - 14, stem_y + 12), QPointF(cx + 14, stem_y + 12))

    def _draw_speaker_symbol(self, p: QPainter, cx: float, cy: float, sz: float):
        p.setPen(QPen(Palette.TEXT_MAIN, 2.5, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
        p.setBrush(QBrush(Palette.CYAN))

        # Speaker body polygon
        cone = QPolygonF([
            QPointF(cx - sz * 0.45, cy - sz * 0.22),
            QPointF(cx - sz * 0.15, cy - sz * 0.22),
            QPointF(cx + sz * 0.15, cy - sz * 0.45),
            QPointF(cx + sz * 0.15, cy + sz * 0.45),
            QPointF(cx - sz * 0.15, cy + sz * 0.22),
            QPointF(cx - sz * 0.45, cy + sz * 0.22),
        ])
        p.drawPolygon(cone)

        # Emitted sound waves
        p.setBrush(Qt.BrushStyle.NoBrush)
        for i in range(1, 4):
            arc_r = sz * (0.35 + i * 0.28) + self._disp_amp * 6.0
            arc_rect = QRectF(cx - arc_r * 0.1, cy - arc_r / 2.0, arc_r, arc_r)
            p.setPen(QPen(Palette.TEXT_MAIN, 2.2, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
            p.drawArc(arc_rect, -45 * 16, 90 * 16)

    def _draw_orbital_rings(self, p: QPainter, cx: float, cy: float, r: float):
        p.setBrush(Qt.BrushStyle.NoBrush)
        angles = [
            (self._orbit_rot_y, 0.4, Palette.CYAN),
            (-self._orbit_rot_y * 0.7 + 1.2, 0.65, Palette.ELEC_BLUE),
            (self._orbit_rot_y * 1.3 + 2.4, 0.25, QColor(0, 123, 255, 140))
        ]
        for rot, tilt, col in angles:
            p.save()
            p.translate(cx, cy)
            p.rotate(math.degrees(rot * 0.3))
            p.setPen(QPen(col, 1.8, Qt.PenStyle.SolidLine))
            # Elongated orbital ellipse
            p.drawEllipse(QPointF(0, 0), r, r * tilt)

            # Draw orbiting energy bead
            bead_ang = rot * 2.0
            bx = r * math.cos(bead_ang)
            by = r * tilt * math.sin(bead_ang)
            p.setBrush(QBrush(Palette.CYAN))
            p.setPen(Qt.PenStyle.NoPen)
            p.drawEllipse(QPointF(bx, by), 4.5, 4.5)
            p.restore()

    def _draw_brain_mesh(self, p: QPainter, cx: float, cy: float, r: float):
        # 3D projected rotation of nodes
        cos_y = math.cos(self._orbit_rot_y * 0.5)
        sin_y = math.sin(self._orbit_rot_y * 0.5)
        proj_nodes = []
        for x, y, z in BRAIN_NODES:
            # Rotate around Y axis
            rx = x * cos_y + z * sin_y
            rz = -x * sin_y + z * cos_y
            px = cx + rx * r
            py = cy - y * r * 0.85
            proj_nodes.append((px, py, rz))

        # Draw synaptic connecting lines
        p.setPen(QPen(QColor(0, 229, 255, 65), 1.0))
        for i, j in BRAIN_EDGES:
            x1, y1, z1 = proj_nodes[i]
            x2, y2, z2 = proj_nodes[j]
            p.drawLine(QPointF(x1, y1), QPointF(x2, y2))

        # Draw glowing neural nodes
        p.setBrush(QBrush(Palette.CYAN))
        p.setPen(Qt.PenStyle.NoPen)
        for px, py, pz in proj_nodes:
            node_sz = 2.0 + (pz + 1.0) * 1.2
            p.drawEllipse(QPointF(px, py), node_sz, node_sz)

    def _draw_thinking_left_panel(self, p: QPainter, x: float, y: float):
        # Glass HUD backing
        p.setBrush(QBrush(Palette.BG_PANEL))
        p.setPen(QPen(Palette.BORDER_CYAN, 1.2))
        rect = QRectF(x, y, 220, 160)
        p.drawRoundedRect(rect, 8, 8)

        # Header
        p.setFont(QFont("Segoe UI", 12, QFont.Weight.Bold))
        p.setPen(Palette.TEXT_MAIN)
        p.drawText(QRectF(x + 14, y + 12, 190, 22), "SPARK AI")

        p.setFont(QFont("Segoe UI", 9))
        p.setPen(Palette.TEXT_MUTED)
        p.drawText(QRectF(x + 14, y + 34, 190, 18), "Your Personal AI Assistant")

        # Steps
        steps = ["Understanding...", "Planning...", "Executing..."]
        p.setFont(QFont("Segoe UI", 10))
        for idx, s in enumerate(steps):
            sy = y + 62 + idx * 28
            # Dot
            p.setBrush(QBrush(Palette.CYAN))
            p.setPen(Qt.PenStyle.NoPen)
            p.drawEllipse(QPointF(x + 22, sy + 7), 4, 4)
            # Label
            p.setPen(Palette.TEXT_MAIN)
            p.drawText(QRectF(x + 36, sy, 160, 20), s)

    def _draw_thinking_right_panel(self, p: QPainter, x: float, y: float):
        p.setBrush(QBrush(Palette.BG_PANEL))
        p.setPen(QPen(Palette.BORDER_CYAN, 1.2))
        rect = QRectF(x, y, 240, 210)
        p.drawRoundedRect(rect, 8, 8)

        p.setFont(QFont("Segoe UI", 11, QFont.Weight.Bold))
        p.setPen(Palette.TEXT_MAIN)
        p.drawText(QRectF(x + 16, y + 12, 200, 22), "Capabilities")

        caps = [
            ("🎙", "Voice Intelligence"),
            ("👁", "Visual Awareness"),
            ("💻", "Computer Control"),
            ("☑", "Autonomous Tasks"),
            ("🗄", "Persistent Memory"),
            ("📄", "File Intelligence")
        ]
        p.setFont(QFont("Segoe UI", 9))
        for idx, (icon, name) in enumerate(caps):
            cy = y + 38 + idx * 26
            p.setPen(Palette.CYAN)
            p.drawText(QRectF(x + 16, cy, 24, 20), icon)
            p.setPen(Palette.TEXT_MAIN)
            p.drawText(QRectF(x + 42, cy, 180, 20), name)

    def _draw_status_card(self, p: QPainter, x: float, y: float, w: float, h: float,
                          title: str, subtitle: str,
                          is_mic=False, is_speaker=False, is_pulse=False):
        p.setBrush(QBrush(Palette.BG_PANEL))
        p.setPen(QPen(Palette.BORDER_CYAN, 1.2))
        p.drawRoundedRect(QRectF(x, y, w, h), h / 2.0, h / 2.0)

        # Left mini icon
        ix = x + 24
        iy = y + h / 2.0
        p.setBrush(QBrush(Palette.CYAN))
        p.setPen(Qt.PenStyle.NoPen)
        # Small animated 3-bar sound indicator
        for b in range(4):
            bh = 6 + abs(math.sin(self._phase * 5.0 + b)) * 14.0 * max(0.2, self._disp_amp)
            p.drawRoundedRect(QRectF(ix + b * 5, iy - bh / 2.0, 3, bh), 1.5, 1.5)

        # Text labels
        p.setFont(QFont("Segoe UI", 10, QFont.Weight.Bold))
        p.setPen(Palette.TEXT_MAIN)
        p.drawText(QRectF(x + 56, y + 14, w - 66, 20), title)

        p.setFont(QFont("Segoe UI", 9))
        p.setPen(Palette.TEXT_MUTED)
        p.drawText(QRectF(x + 56, y + 36, w - 66, 18), subtitle)


# ── REASONING UI WORKSPACE ──────────────────────────────────────────────────
class SparkReasoningView(QWidget):
    """
    Dedicated REASONING UI communicating the progress of SPARK's internal task processing
    without exposing private chain-of-thought:
    - Central AI Processing Core with gyro rings & neural mesh
    - 5-stage pipeline: UNDERSTANDING -> PLANNING -> EXECUTION -> VERIFICATION -> COMPLETED
    - LIVE ACTIVITY operational feed
    - TASK PLAN structured checklist
    - TOOLS & OPERATIONS monitor
    - VERIFICATION results panel
    """
    backRequested = pyqtSignal()

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setStyleSheet(f"background-color: {Palette.BG_DARK.name()}; color: #EAF5FF;")

        self._active_stage = 2  # 0: Understanding, 1: Planning, 2: Execution, 3: Verification, 4: Completed
        self._activities: List[Tuple[str, str]] = [
            (datetime.datetime.now().strftime("%H:%M:%S"), "Task initialized: Voice Mode operational session ready"),
            (datetime.datetime.now().strftime("%H:%M:%S"), "Analyzing natural language prompt & goal objectives"),
            (datetime.datetime.now().strftime("%H:%M:%S"), "Synthesizing execution plan with tool constraints"),
            (datetime.datetime.now().strftime("%H:%M:%S"), "Verifying authorization boundary: Division === 'voice-mode' (Authorized)"),
            (datetime.datetime.now().strftime("%H:%M:%S"), "Executing primary capability dispatch")
        ]

        self._task_steps = [
            ("Identify target requirements & system boundaries", "Completed"),
            ("Analyze document categories & visual context", "Completed"),
            ("Execute authorized actions across environment", "Active"),
            ("Verify outcome integrity and safety guarantees", "Pending"),
            ("Return final synthesis to conversation surface", "Pending")
        ]

        self._tools_in_use = [
            {"name": "Visual Awareness (Screen Capture)", "status": "Success", "duration": "142ms", "summary": "Captured active workspace window"},
            {"name": "Computer Control (Sandbox Guarded)", "status": "Executing", "duration": "88ms", "summary": "Dispatched system environment probe"},
            {"name": "Persistent Memory Adapter", "status": "Ready", "duration": "24ms", "summary": "Retrieved user profile preferences"}
        ]

        self._setup_ui()

    def _setup_ui(self):
        root_lay = QVBoxLayout(self)
        root_lay.setContentsMargins(28, 20, 28, 24)
        root_lay.setSpacing(18)

        # ── Header Bar ──────────────────────────────────────────────────────
        hdr = QHBoxLayout()
        back_btn = QPushButton("← Return to Voice UI")
        back_btn.setStyleSheet("""
            QPushButton {
                background: rgba(0, 123, 255, 0.25);
                border: 1px solid #00E5FF;
                border-radius: 6px;
                color: #EAF5FF;
                font-weight: bold;
                padding: 7px 16px;
            }
            QPushButton:hover { background: rgba(0, 168, 255, 0.4); }
        """)
        back_btn.clicked.connect(self.backRequested.emit)
        hdr.addWidget(back_btn)

        title_lbl = QLabel("SPARK OPERATIONAL REASONING CORE")
        title_lbl.setFont(QFont("Segoe UI", 14, QFont.Weight.Bold))
        title_lbl.setStyleSheet("color: #00E5FF; letter-spacing: 1.5px;")
        hdr.addWidget(title_lbl)
        hdr.addStretch()

        status_badge = QLabel(" ● REASONING ACTIVE ")
        status_badge.setStyleSheet("background: rgba(0, 255, 170, 0.15); border: 1px solid #00FFAA; color: #00FFAA; border-radius: 4px; padding: 4px 10px; font-weight: bold;")
        hdr.addWidget(status_badge)
        root_lay.addLayout(hdr)

        # ── 5-Stage Processing Pipeline ─────────────────────────────────────
        pipe_frame = QFrame()
        pipe_frame.setStyleSheet(f"background: {Palette.BG_NAVY.name()}; border: 1px solid rgba(0, 229, 255, 0.3); border-radius: 8px;")
        pipe_lay = QHBoxLayout(pipe_frame)
        pipe_lay.setContentsMargins(16, 12, 16, 12)

        stages = [
            ("1. UNDERSTANDING", "Requirements & intent analyzed"),
            ("2. PLANNING", "Execution graph created"),
            ("3. EXECUTION", "Tools actively executing"),
            ("4. VERIFICATION", "Quality & safety checks"),
            ("5. COMPLETED", "Synthesis finalized")
        ]
        self._stage_widgets = []
        for i, (name, desc) in enumerate(stages):
            st_box = QVBoxLayout()
            is_active = (i == self._active_stage)
            is_done = (i < self._active_stage)

            color = "#00FFAA" if is_done else ("#00E5FF" if is_active else "#5A789B")
            icon = "✓ " if is_done else ("▶ " if is_active else "○ ")

            lbl_name = QLabel(f"{icon}{name}")
            lbl_name.setFont(QFont("Segoe UI", 11, QFont.Weight.Bold))
            lbl_name.setStyleSheet(f"color: {color};")
            lbl_desc = QLabel(desc)
            lbl_desc.setFont(QFont("Segoe UI", 8))
            lbl_desc.setStyleSheet("color: #8FA9C8;")

            st_box.addWidget(lbl_name)
            st_box.addWidget(lbl_desc)
            pipe_lay.addLayout(st_box)
            if i < len(stages) - 1:
                arrow = QLabel("→")
                arrow.setFont(QFont("Segoe UI", 14, QFont.Weight.Bold))
                arrow.setStyleSheet("color: rgba(0, 229, 255, 0.4);")
                pipe_lay.addWidget(arrow)

        root_lay.addWidget(pipe_frame)

        # ── 3-Column Workspace ──────────────────────────────────────────────
        body = QHBoxLayout()
        body.setSpacing(16)

        # Column 1: Live Activity Stream
        col1 = self._build_card("LIVE ACTIVITY", self._build_activity_widget())
        body.addWidget(col1, stretch=3)

        # Column 2: Task Plan Visualization
        col2 = self._build_card("TASK PLAN & PROGRESS", self._build_plan_widget())
        body.addWidget(col2, stretch=4)

        # Column 3: Tools & Operations + Verification
        col3_v = QVBoxLayout()
        col3_tools = self._build_card("TOOLS & OPERATIONS", self._build_tools_widget())
        col3_verif = self._build_card("VERIFICATION OUTCOME", self._build_verification_widget())
        col3_v.addWidget(col3_tools, stretch=3)
        col3_v.addWidget(col3_verif, stretch=2)
        body.addLayout(col3_v, stretch=4)

        root_lay.addLayout(body)

    def _build_card(self, title: str, content: QWidget) -> QFrame:
        card = QFrame()
        card.setStyleSheet(f"""
            QFrame {{
                background: {Palette.BG_NAVY.name()};
                border: 1px solid rgba(0, 229, 255, 0.25);
                border-radius: 8px;
            }}
        """)
        lay = QVBoxLayout(card)
        lay.setContentsMargins(14, 12, 14, 14)
        lay.setSpacing(10)

        hdr = QLabel(title)
        hdr.setFont(QFont("Segoe UI", 10, QFont.Weight.Bold))
        hdr.setStyleSheet("color: #00E5FF; border-bottom: 1px solid rgba(0, 229, 255, 0.2); padding-bottom: 6px;")
        lay.addWidget(hdr)
        lay.addWidget(content)
        return card

    def _build_activity_widget(self) -> QWidget:
        box = QWidget()
        v = QVBoxLayout(box)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(8)
        for t, msg in self._activities:
            row = QHBoxLayout()
            time_lbl = QLabel(t)
            time_lbl.setFont(QFont("Consolas", 8))
            time_lbl.setStyleSheet("color: #5A789B;")
            row.addWidget(time_lbl)
            msg_lbl = QLabel(msg)
            msg_lbl.setFont(QFont("Segoe UI", 9))
            msg_lbl.setStyleSheet("color: #EAF5FF;")
            msg_lbl.setWordWrap(True)
            row.addWidget(msg_lbl, stretch=1)
            v.addLayout(row)
        v.addStretch()
        return box

    def _build_plan_widget(self) -> QWidget:
        box = QWidget()
        v = QVBoxLayout(box)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(10)
        for step, status in self._task_steps:
            f = QFrame()
            f.setStyleSheet("background: rgba(0, 168, 255, 0.08); border-radius: 6px; padding: 6px;")
            fl = QHBoxLayout(f)
            fl.setContentsMargins(8, 6, 8, 6)

            st_color = "#00FFAA" if status == "Completed" else ("#00E5FF" if status == "Active" else "#5A789B")
            badge = QLabel(f"[{status}]")
            badge.setFont(QFont("Segoe UI", 9, QFont.Weight.Bold))
            badge.setStyleSheet(f"color: {st_color};")
            fl.addWidget(badge)

            lbl = QLabel(step)
            lbl.setFont(QFont("Segoe UI", 9))
            lbl.setStyleSheet("color: #EAF5FF;")
            fl.addWidget(lbl, stretch=1)
            v.addWidget(f)
        v.addStretch()
        return box

    def _build_tools_widget(self) -> QWidget:
        box = QWidget()
        v = QVBoxLayout(box)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(8)
        for tool in self._tools_in_use:
            f = QFrame()
            f.setStyleSheet("background: rgba(2, 5, 11, 0.5); border: 1px solid rgba(0, 168, 255, 0.2); border-radius: 5px;")
            fl = QVBoxLayout(f)
            fl.setContentsMargins(8, 6, 8, 6)

            top = QHBoxLayout()
            name_lbl = QLabel(tool["name"])
            name_lbl.setFont(QFont("Segoe UI", 9, QFont.Weight.Bold))
            name_lbl.setStyleSheet("color: #00E5FF;")
            top.addWidget(name_lbl)
            top.addStretch()
            st_lbl = QLabel(tool["status"])
            st_lbl.setFont(QFont("Segoe UI", 8, QFont.Weight.Bold))
            st_lbl.setStyleSheet("color: #00FFAA;" if tool["status"] == "Success" else "color: #FFB400;")
            top.addWidget(st_lbl)
            fl.addLayout(top)

            sum_lbl = QLabel(f"{tool['summary']} ({tool['duration']})")
            sum_lbl.setFont(QFont("Segoe UI", 8))
            sum_lbl.setStyleSheet("color: #8FA9C8;")
            fl.addWidget(sum_lbl)
            v.addWidget(f)
        v.addStretch()
        return box

    def _build_verification_widget(self) -> QWidget:
        box = QWidget()
        v = QVBoxLayout(box)
        v.setContentsMargins(0, 0, 0, 0)
        v.setSpacing(6)

        st = QLabel("VERIFYING RESULTS...")
        st.setFont(QFont("Segoe UI", 11, QFont.Weight.Bold))
        st.setStyleSheet("color: #00FFAA;")
        v.addWidget(st)

        detail = QLabel("Outcome: SUCCESSFUL\nAll 6 feature boundaries verified intact.\nNo unauthorized cross-division access detected.\nExecution safely finalized.")
        detail.setFont(QFont("Segoe UI", 9))
        detail.setStyleSheet("color: #8FA9C8;")
        v.addWidget(detail)
        return box

    def append_activity(self, message: str):
        t = datetime.datetime.now().strftime("%H:%M:%S")
        self._activities.append((t, message))


# ── SETTINGS DIALOG (VISUAL EXPERIENCE + MODEL SWITCHER + WAKE WORD) ────────
class SparkVoiceSettingsDialog(QDialog):
    """
    Settings dialog containing:
    - VISUAL EXPERIENCE: [ FACE MODE ] [ NON-FACE MODE ] (Persisted across restarts)
    - AI MODEL SWITCHER: Connected to SPARK models
    - WAKE WORD: 'Hey Spark' detection + 2-minute auto-sleep
    - AUDIO DEVICES
    """
    def __init__(self, current_settings: dict, on_save_callback: Callable[[dict], None], parent=None):
        super().__init__(parent)
        self.setWindowTitle("SPARK Voice Mode Settings")
        self.setFixedSize(540, 460)
        self.setStyleSheet(f"""
            QDialog {{ background: {Palette.BG_NAVY.name()}; color: #EAF5FF; font-family: 'Segoe UI'; }}
            QLabel {{ color: #EAF5FF; }}
            QComboBox {{
                background: #02050B;
                border: 1px solid #00E5FF;
                border-radius: 4px;
                padding: 6px;
                color: #EAF5FF;
            }}
        """)
        self._settings = dict(current_settings)
        self._on_save = on_save_callback
        self._setup_ui()

    def _setup_ui(self):
        lay = QVBoxLayout(self)
        lay.setContentsMargins(24, 24, 24, 24)
        lay.setSpacing(16)

        # Title
        title = QLabel("VOICE MODE CONFIGURATION")
        title.setFont(QFont("Segoe UI", 13, QFont.Weight.Bold))
        title.setStyleSheet("color: #00E5FF; letter-spacing: 1px;")
        lay.addWidget(title)

        # ── 1. Visual Experience Selector ───────────────────────────────────
        ve_lbl = QLabel("VISUAL EXPERIENCE")
        ve_lbl.setFont(QFont("Segoe UI", 10, QFont.Weight.Bold))
        lay.addWidget(ve_lbl)

        btn_box = QHBoxLayout()
        self._btn_standard = QPushButton("STANDARD (HUD)")
        self._btn_face = QPushButton("3D FACE")
        self._btn_reactor = QPushButton("ARC REACTOR")
        self._btn_orb = QPushButton("HOLO ORB")

        style_unsel = """
            QPushButton {
                background: #02050B; border: 1px solid rgba(0, 229, 255, 0.3);
                border-radius: 6px; padding: 10px; color: #8FA9C8; font-weight: bold;
            }
            QPushButton:hover { background: rgba(0, 123, 255, 0.2); color: #EAF5FF; }
        """
        style_sel = """
            QPushButton {
                background: #007BFF; border: 1px solid #00E5FF;
                border-radius: 6px; padding: 10px; color: #FFFFFF; font-weight: bold;
            }
        """

        current_mode = self._settings.get("visual_mode", "standard").lower()

        def update_btn_styles():
            m = self._settings["visual_mode"].lower()
            self._btn_standard.setStyleSheet(style_sel if m == "standard" else style_unsel)
            self._btn_face.setStyleSheet(style_sel if m == "face" else style_unsel)
            self._btn_reactor.setStyleSheet(style_sel if m == "reactor" else style_unsel)
            self._btn_orb.setStyleSheet(style_sel if m in ("non-face", "orb") else style_unsel)

        def pick_standard():
            self._settings["visual_mode"] = "standard"
            update_btn_styles()

        def pick_face():
            self._settings["visual_mode"] = "face"
            update_btn_styles()

        def pick_reactor():
            self._settings["visual_mode"] = "reactor"
            update_btn_styles()

        def pick_orb():
            self._settings["visual_mode"] = "non-face"
            update_btn_styles()

        self._btn_standard.clicked.connect(pick_standard)
        self._btn_face.clicked.connect(pick_face)
        self._btn_reactor.clicked.connect(pick_reactor)
        self._btn_orb.clicked.connect(pick_orb)
        update_btn_styles()

        btn_box.addWidget(self._btn_standard)
        btn_box.addWidget(self._btn_face)
        btn_box.addWidget(self._btn_reactor)
        btn_box.addWidget(self._btn_orb)
        lay.addLayout(btn_box)

        # ── 2. AI Model Switcher ────────────────────────────────────────────
        mod_lbl = QLabel("AI INTELLIGENCE MODEL")
        mod_lbl.setFont(QFont("Segoe UI", 10, QFont.Weight.Bold))
        lay.addWidget(mod_lbl)

        self._model_combo = QComboBox()
        models = [
            ("models/gemini-2.5-flash-native-audio-preview-12-2025", "Gemini 2.5 Flash Native Audio (Voice Live)"),
            ("models/gemini-2.5-flash", "Gemini 2.5 Flash (Fast Multimodal)"),
            ("models/gemini-2.5-pro", "Gemini 2.5 Pro (Deep Reasoning)"),
            ("codex/gpt-5.4", "Codex GPT-5.4 (SPARK Primary)"),
            ("codex/gpt-5.4-mini", "Codex GPT-5.4 Mini"),
            ("anthropic/claude-3.5-sonnet", "Claude 3.5 Sonnet (Hybrid Adapter)"),
            ("openai/gpt-4o", "GPT-4o (Omni Intelligence)"),
            ("local/ollama", "Ollama Local (Offline Private)")
        ]
        cur_mod = self._settings.get("model", "")
        sel_idx = 0
        for i, (m_id, m_name) in enumerate(models):
            self._model_combo.addItem(m_name, m_id)
            if m_id == cur_mod:
                sel_idx = i
        self._model_combo.setCurrentIndex(sel_idx)
        lay.addWidget(self._model_combo)

        # ── 3. Wake Word Configuration ──────────────────────────────────────
        ww_lbl = QLabel("WAKE WORD & AUTONOMOUS SLEEP")
        ww_lbl.setFont(QFont("Segoe UI", 10, QFont.Weight.Bold))
        lay.addWidget(ww_lbl)

        ww_desc = QLabel("Wake Phrases: 'Hey Spark', 'Spark what's up', 'Good morning Spark'\nSleeps until called • Auto-sleeps after 2 minutes of silence • Zero audio streaming while asleep.")
        ww_desc.setFont(QFont("Segoe UI", 9))
        ww_desc.setStyleSheet("color: #8FA9C8;")
        lay.addWidget(ww_desc)

        lay.addStretch()

        # Save Button
        save_btn = QPushButton("Save & Apply Settings")
        save_btn.setStyleSheet("""
            QPushButton {
                background: #00E5FF; color: #02050B; font-weight: bold;
                border-radius: 6px; padding: 10px; font-size: 13px;
            }
            QPushButton:hover { background: #00A8FF; color: #FFFFFF; }
        """)
        save_btn.clicked.connect(self._on_save_clicked)
        lay.addWidget(save_btn)

    def _on_save_clicked(self):
        self._settings["model"] = self._model_combo.currentData()
        save_voice_settings(self._settings)
        self._on_save(self._settings)
        self.accept()


# ── MAIN DESKTOP WINDOW ─────────────────────────────────────────────────────
class MainWindow(QMainWindow):
    """
    SPARK Voice Mode Primary Desktop Window:
    - Top Header: SPARK Star logo, greeting, clock, date, weather, mode switcher, reasoning button.
    - Central Stack:
      * View 0: Voice UI (SparkVisualCanvas with Face & Non-Face modes)
      * View 1: Reasoning UI (SparkReasoningView operational intelligence)
    - Bottom-Right Controls Pill: Mic, Text Prompt, Interruption, Settings, Exit.
    """
    def __init__(self, visual_mode: str = "non-face"):
        super().__init__()
        self.setWindowTitle("SPARK — Voice Mode")
        self.setMinimumSize(980, 680)
        self.resize(1180, 780)
        self.setStyleSheet(f"background-color: {Palette.BG_DARK.name()};")

        self._settings = load_voice_settings()
        self._visual_mode = self._settings.get("visual_mode", visual_mode)
        self._is_mic_muted = False
        self._is_reasoning_open = False

        self._setup_ui()
        self._init_clock_timer()

    def _setup_ui(self):
        central = QWidget(self)
        self.setCentralWidget(central)
        root_lay = QVBoxLayout(central)
        root_lay.setContentsMargins(0, 0, 0, 0)
        root_lay.setSpacing(0)

        # ── 1. Top Header Bar (Matching Reference Images) ───────────────────
        header_bar = QFrame()
        header_bar.setFixedHeight(64)
        header_bar.setStyleSheet(f"background: {Palette.BG_NAVY.name()}; border-bottom: 1px solid rgba(0, 229, 255, 0.2);")
        hdr_lay = QHBoxLayout(header_bar)
        hdr_lay.setContentsMargins(24, 0, 24, 0)

        # Left: SPARK 4-point star & Branding
        left_box = QHBoxLayout()
        left_box.setSpacing(12)
        star_icon = QLabel("✦")
        star_icon.setFont(QFont("Segoe UI", 18, QFont.Weight.Bold))
        star_icon.setStyleSheet("color: #00E5FF;")
        left_box.addWidget(star_icon)

        brand_lbl = QLabel("SPARK")
        brand_lbl.setFont(QFont("Segoe UI", 16, QFont.Weight.Bold))
        brand_lbl.setStyleSheet("color: #EAF5FF; letter-spacing: 2px;")
        left_box.addWidget(brand_lbl)

        sep = QLabel("|")
        sep.setStyleSheet("color: rgba(0, 229, 255, 0.3); font-size: 16px;")
        left_box.addWidget(sep)

        greeting_lbl = QLabel("Always here, Sir.")
        greeting_lbl.setFont(QFont("Segoe UI", 11))
        greeting_lbl.setStyleSheet("color: #8FA9C8;")
        left_box.addWidget(greeting_lbl)
        hdr_lay.addLayout(left_box)

        hdr_lay.addStretch()

        # Center: Reasoning Workspace Button, Visual Mode Toggle & Model Switcher
        center_box = QHBoxLayout()
        center_box.setSpacing(10)

        # AI Model Switcher in Header
        self.header_model_combo = QComboBox()
        self.header_model_combo.setStyleSheet("""
            QComboBox {
                background: rgba(5, 11, 23, 0.9);
                border: 1px solid rgba(0, 229, 255, 0.4);
                border-radius: 14px;
                color: #00E5FF;
                font-weight: bold;
                padding: 4px 10px;
                font-size: 11px;
                min-width: 170px;
            }
            QComboBox::drop-down { border: none; }
            QComboBox QAbstractItemView {
                background: #050B17;
                color: #EAF5FF;
                selection-background-color: #007BFF;
            }
        """)
        header_models = [
            ("models/gemini-2.5-flash-native-audio-preview-12-2025", "Gemini 2.5 Native Audio"),
            ("models/gemini-2.5-flash", "Gemini 2.5 Flash"),
            ("models/gemini-2.5-pro", "Gemini 2.5 Pro"),
            ("codex/gpt-5.4", "Codex GPT-5.4 (SPARK)"),
            ("codex/gpt-5.4-mini", "Codex GPT-5.4 Mini"),
            ("anthropic/claude-3.5-sonnet", "Claude 3.5 Sonnet"),
            ("openai/gpt-4o", "GPT-4o"),
            ("local/ollama", "Ollama Local")
        ]
        cur_mod = self._settings.get("model", "")
        sel_idx = 0
        for i, (m_id, m_name) in enumerate(header_models):
            self.header_model_combo.addItem(m_name, m_id)
            if m_id == cur_mod:
                sel_idx = i
        self.header_model_combo.setCurrentIndex(sel_idx)
        self.header_model_combo.currentIndexChanged.connect(self._on_header_model_changed)
        center_box.addWidget(self.header_model_combo)

        self.reasoning_btn = QPushButton("🧠 Operational Reasoning")
        self.reasoning_btn.setStyleSheet("""
            QPushButton {
                background: rgba(0, 123, 255, 0.2);
                border: 1px solid rgba(0, 229, 255, 0.4);
                border-radius: 14px;
                color: #00E5FF;
                font-weight: bold;
                padding: 6px 14px;
                font-size: 11.5px;
            }
            QPushButton:hover { background: rgba(0, 168, 255, 0.4); color: #FFFFFF; }
        """)
        self.reasoning_btn.clicked.connect(self._toggle_reasoning_view)
        center_box.addWidget(self.reasoning_btn)

        # Fast Visual Mode Toggle Button
        self.mode_toggle_btn = QPushButton(f"MODE: {self._visual_mode.upper()}")
        self.mode_toggle_btn.setStyleSheet("""
            QPushButton {
                background: rgba(0, 168, 255, 0.15);
                border: 1px solid #00A8FF;
                border-radius: 14px;
                color: #EAF5FF;
                font-weight: bold;
                padding: 6px 14px;
                font-size: 11.5px;
            }
            QPushButton:hover { background: rgba(0, 229, 255, 0.3); }
        """)
        self.mode_toggle_btn.clicked.connect(self._toggle_visual_mode)
        center_box.addWidget(self.mode_toggle_btn)
        hdr_lay.addLayout(center_box)

        hdr_lay.addStretch()

        # Right: Clock, Date, Weather
        right_box = QHBoxLayout()
        right_box.setSpacing(16)

        # Clock & Date
        clock_box = QVBoxLayout()
        clock_box.setSpacing(0)
        self.clock_lbl = QLabel("08:24 PM")
        self.clock_lbl.setFont(QFont("Segoe UI", 12, QFont.Weight.Bold))
        self.clock_lbl.setStyleSheet("color: #EAF5FF;")
        self.date_lbl = QLabel(datetime.date.today().strftime("%a, %d %b %Y"))
        self.date_lbl.setFont(QFont("Segoe UI", 9))
        self.date_lbl.setStyleSheet("color: #8FA9C8;")
        clock_box.addWidget(self.clock_lbl)
        clock_box.addWidget(self.date_lbl)
        right_box.addLayout(clock_box)

        # Weather
        weather_box = QHBoxLayout()
        w_icon = QLabel("☁")
        w_icon.setFont(QFont("Segoe UI", 14))
        w_icon.setStyleSheet("color: #00E5FF;")
        weather_box.addWidget(w_icon)
        weather_lbl = QLabel("28°C\nPartly Cloudy")
        weather_lbl.setFont(QFont("Segoe UI", 8))
        weather_lbl.setStyleSheet("color: #8FA9C8;")
        weather_box.addWidget(weather_lbl)
        right_box.addLayout(weather_box)

        hdr_lay.addLayout(right_box)
        root_lay.addWidget(header_bar)

        # ── 2. Central Content Stack (Voice UI vs Reasoning UI) ─────────────
        self.stack = QStackedWidget()

        # Page 0: Voice UI
        self.canvas_container = QWidget()
        c_lay = QVBoxLayout(self.canvas_container)
        c_lay.setContentsMargins(0, 0, 0, 0)
        self.canvas = SparkVisualCanvas(visual_mode=self._visual_mode)
        c_lay.addWidget(self.canvas)
        self.stack.addWidget(self.canvas_container)

        # Page 1: Reasoning Workspace
        self.reasoning_view = SparkReasoningView()
        self.reasoning_view.backRequested.connect(self._show_voice_view)
        self.stack.addWidget(self.reasoning_view)

        root_lay.addWidget(self.stack, stretch=1)

        # ── 3. Bottom Controls Bar ──────────────────────────────────────────
        footer = QFrame()
        footer.setFixedHeight(80)
        footer.setStyleSheet("background: transparent;")
        f_lay = QHBoxLayout(footer)
        f_lay.setContentsMargins(32, 0, 32, 20)

        f_lay.addStretch()

        # Floating Pill Controls (Bottom-Right)
        pill = QFrame()
        pill.setStyleSheet("""
            QFrame {
                background: rgba(5, 11, 23, 0.85);
                border: 1px solid rgba(0, 229, 255, 0.4);
                border-radius: 26px;
                padding: 4px;
            }
        """)
        pill_lay = QHBoxLayout(pill)
        pill_lay.setContentsMargins(12, 4, 12, 4)
        pill_lay.setSpacing(12)

        # Mic Toggle Button
        self.mic_btn = self._build_pill_btn("🎙", "Toggle Microphone", self._on_mic_toggle)
        pill_lay.addWidget(self.mic_btn)

        # Text Input Button
        self.text_btn = self._build_pill_btn("⌨", "Type Command", self._on_text_prompt)
        pill_lay.addWidget(self.text_btn)

        # Interruption Button (Audio Stop)
        self.stop_btn = self._build_pill_btn("⏹", "Interrupt Audio Response", self._on_interrupt)
        pill_lay.addWidget(self.stop_btn)

        # Settings Button
        self.gear_btn = self._build_pill_btn("⚙", "Voice Mode Settings", self._open_settings)
        pill_lay.addWidget(self.gear_btn)

        # Exit / Power Button
        self.power_btn = self._build_pill_btn("⏻", "Exit Voice Mode", self.close)
        pill_lay.addWidget(self.power_btn)

        f_lay.addWidget(pill)
        root_lay.addWidget(footer)

    def _build_pill_btn(self, icon: str, tooltip: str, callback: Callable) -> QPushButton:
        btn = QPushButton(icon)
        btn.setToolTip(tooltip)
        btn.setFixedSize(40, 40)
        btn.setFont(QFont("Segoe UI", 14))
        btn.setStyleSheet("""
            QPushButton {
                background: rgba(0, 168, 255, 0.15);
                border: 1px solid rgba(0, 229, 255, 0.3);
                border-radius: 20px;
                color: #00E5FF;
            }
            QPushButton:hover {
                background: rgba(0, 229, 255, 0.35);
                color: #FFFFFF;
                border: 1px solid #00E5FF;
            }
        """)
        btn.clicked.connect(callback)
        return btn

    def _init_clock_timer(self):
        self._clock_timer = QTimer(self)
        self._clock_timer.timeout.connect(self._update_clock)
        self._clock_timer.start(1000)
        self._update_clock()

    def _update_clock(self):
        now = datetime.datetime.now()
        self.clock_lbl.setText(now.strftime("%I:%M %p"))
        self.date_lbl.setText(now.strftime("%a, %d %b %Y"))

    def _toggle_reasoning_view(self):
        self._is_reasoning_open = not self._is_reasoning_open
        self.stack.setCurrentIndex(1 if self._is_reasoning_open else 0)
        self.reasoning_btn.setText("🎙 Return to Voice UI" if self._is_reasoning_open else "🧠 Operational Reasoning")

    def _show_voice_view(self):
        self._is_reasoning_open = False
        self.stack.setCurrentIndex(0)
        self.reasoning_btn.setText("🧠 Operational Reasoning")

    def _toggle_visual_mode(self):
        modes = ["standard", "face", "reactor", "non-face"]
        cur = self._visual_mode.lower()
        idx = modes.index(cur) if cur in modes else 0
        new_mode = modes[(idx + 1) % len(modes)]
        self._visual_mode = new_mode
        self._settings["visual_mode"] = new_mode
        save_voice_settings(self._settings)
        self.canvas.visual_mode = new_mode
        display_name = "ORB" if new_mode == "non-face" else new_mode.upper()
        self.mode_toggle_btn.setText(f"MODE: {display_name}")

    def _on_mic_toggle(self):
        self._is_mic_muted = not self._is_mic_muted
        self.mic_btn.setText("🔇" if self._is_mic_muted else "🎙")
        self.canvas.set_state("SLEEPING" if self._is_mic_muted else "LISTENING")

    def _on_text_prompt(self):
        dialog = QDialog(self)
        dialog.setWindowTitle("Send Command to SPARK")
        dialog.setFixedSize(460, 140)
        dialog.setStyleSheet(f"background: {Palette.BG_NAVY.name()}; color: #EAF5FF;")
        vl = QVBoxLayout(dialog)
        vl.setContentsMargins(18, 18, 18, 18)

        inp = QLineEdit()
        inp.setPlaceholderText("Type a prompt, query or task for SPARK...")
        inp.setStyleSheet("background: #02050B; border: 1px solid #00E5FF; border-radius: 6px; padding: 8px; color: #EAF5FF;")
        vl.addWidget(inp)

        send_b = QPushButton("Send Prompt")
        send_b.setStyleSheet("background: #00E5FF; color: #02050B; font-weight: bold; border-radius: 6px; padding: 8px;")
        def do_send():
            txt = inp.text().strip()
            if txt:
                self.canvas.set_state("THINKING", txt)
                self.reasoning_view.append_activity(f"User submitted command: '{txt}'")
            dialog.accept()
        send_b.clicked.connect(do_send)
        vl.addWidget(send_b)
        dialog.exec()

    def _on_interrupt(self):
        self.canvas.set_speaker_level(0.0)
        self.canvas.set_state("LISTENING", "Interrupted audio response.")
        self.reasoning_view.append_activity("Voice output interrupted by user request.")

    def _open_settings(self):
        dlg = SparkVoiceSettingsDialog(self._settings, self._on_settings_saved, self)
        dlg.exec()

    def _on_header_model_changed(self, idx: int):
        new_model = self.header_model_combo.itemData(idx)
        if new_model:
            self._settings["model"] = new_model
            save_voice_settings(self._settings)
            self.reasoning_view.append_activity(f"AI Model switched to: {new_model}")

    def _on_settings_saved(self, updated: dict):
        self._settings = updated
        new_mode = updated.get("visual_mode", "non-face")
        self._visual_mode = new_mode
        self.canvas.visual_mode = new_mode
        self.mode_toggle_btn.setText(f"MODE: {new_mode.upper()}")


# ── SparkUI / JarvisUI Adapter Compatibility Wrapper ──────────────────────────
class SparkUI:
    """Drop-in adapter so main.py and ipc_server.py talk to MainWindow seamlessly."""
    def __init__(self, visual_mode: str = "non-face"):
        self._app = QApplication.instance() or QApplication(sys.argv)
        self._app.setApplicationName("SPARK Voice Mode")
        self._win = MainWindow(visual_mode=visual_mode)
        self.muted = False
        self.current_file = None
        self.on_text_command = None

    def show(self):
        self._win.show()

    def set_state(self, state: str, subtitle: Optional[str] = None):
        self._win.canvas.set_state(state, subtitle)

    def write_log(self, text: str):
        self._win.reasoning_view.append_activity(text)

    def set_mic_level(self, level: float):
        self._win.canvas.set_mic_level(level)

    def set_speaker_level(self, level: float):
        self._win.canvas.set_speaker_level(level)

    def push_visemes(self, frames, hop: float, at: float):
        if self._win.canvas._avatar is not None:
            try:
                self._win.canvas._avatar.step(hop, 0.5, speaking=True)
            except Exception:
                pass


JarvisUI = SparkUI


def main():
    app = QApplication(sys.argv)
    app.setApplicationName("SPARK Voice Mode")
    win = MainWindow()
    win.show()
    sys.exit(app.exec())


if __name__ == "__main__":
    main()
