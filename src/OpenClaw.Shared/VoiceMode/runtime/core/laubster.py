"""core/laubster.py — Laubster: The OpenClaw Talking Lobster Puppet.

"laubster is a puppet feature" — An expressive, animated puppet character
inspired by the OpenClaw mascot. Unlike a human wireframe face, Laubster is
built like a classic character puppet:
  - An articulating puppet mouth/jaw that hinges open and drops dynamically
    with vocal amplitude and speech formants.
  - Large expressive cartoon/cyber puppet eyes with round white scleras,
    animated eyelids (blinking & squinting), and pupil gaze tracking.
  - Articulating lobster claws that gesticulate, snap open and closed to syllables,
    and tap thoughtfully during thinking.
  - Flexible antennae that twitch and sway with spring-damper physics.
  - Head nodding and body bobbing on speech emphasis.
  - Cyber-crimson carapace with glowing SPARK cyan/blue accents and an Arc
    Reactor chest crest.

Rendered via software QPainter with smooth antialiasing, zero GPU overhead,
and fully integrated into the SPARK Voice Mode visual pipeline.
"""
from __future__ import annotations

import math
import random
from typing import Tuple

from PyQt6.QtCore import QPointF, QRectF, Qt
from PyQt6.QtGui import (
    QBrush, QColor, QFont, QLinearGradient, QPainter, QPainterPath, QPen,
    QRadialGradient,
)


def _with_alpha(c: QColor, a: float) -> QColor:
    res = QColor(c)
    res.setAlphaF(max(0.0, min(1.0, a)))
    return res


class LaubsterPuppet:
    """Kinematics, animation state, and QPainter renderer for the Laubster Talking Puppet."""

    # Shell and accent color palette
    SHELL_BASE     = QColor(225, 45, 65)    # Rich lobster crimson
    SHELL_DARK     = QColor(145, 20, 38)    # Deep shading
    SHELL_LIGHT    = QColor(255, 95, 110)   # Highlight red
    MOUTH_INTERIOR = QColor(60, 10, 20)     # Inner puppet mouth cavern
    TONGUE_COLOR   = QColor(230, 80, 100)   # Puppet tongue / palate
    CYAN_GLOW      = QColor(0, 229, 255)    # SPARK cyan illumination
    BLUE_ACCENT    = QColor(0, 132, 255)    # SPARK electric blue
    WHITE          = QColor(255, 255, 255)

    def __init__(self):
        self._t = 0.0

        # Puppet articulation state
        self.jaw_drop = 0.0         # 0.0 = mouth shut, 1.0 = wide open
        self.claw_l_open = 0.0      # 0.0 = pincer closed, 1.0 = open
        self.claw_r_open = 0.0
        self.claw_l_bob = 0.0       # Arm gesture elevation
        self.claw_r_bob = 0.0
        self.head_bob = 0.0         # Syllable nod
        self.body_sway = 0.0        # Idle sway

        # Expressive eyes
        self.blink = 0.0            # 0.0 = open, 1.0 = fully shut
        self._blink_timer = 2.5     # Seconds until next blink
        self.gaze_x = 0.0           # -1.0 (left) to 1.0 (right)
        self.gaze_y = 0.0           # -1.0 (up) to 1.0 (down)
        self._target_gaze_x = 0.0
        self._target_gaze_y = 0.0
        self._gaze_timer = 1.0

        # Antennae physics
        self.ant_l_angle = 0.0
        self.ant_r_angle = 0.0

        # Audio smoothing
        self._smoothed_amp = 0.0

    def step(self, dt: float, speaking: bool, thinking: bool, muted: bool, amp: float) -> None:
        """Advance puppet kinematics and animation by dt seconds."""
        dt = min(0.10, max(0.001, dt))
        self._t += dt

        # Smooth loudness envelope
        target_amp = max(0.0, min(1.0, amp))
        self._smoothed_amp += (target_amp - self._smoothed_amp) * min(1.0, dt * 18.0)
        curr_amp = self._smoothed_amp

        # ── 1. Puppet Talking Mouth Flap ─────────────────────────────────────────
        # When speaking, mouth drops with vocal amplitude + rapid syllable flapping
        if speaking:
            flap_osc = 0.5 + 0.5 * math.sin(self._t * 16.0)
            target_drop = curr_amp * 0.95 + flap_osc * min(0.45, curr_amp * 0.65)
            self.jaw_drop += (target_drop - self.jaw_drop) * min(1.0, dt * 26.0)
        elif thinking:
            # Thoughtful slight puckering
            target_drop = 0.08 + 0.06 * math.sin(self._t * 6.0)
            self.jaw_drop += (target_drop - self.jaw_drop) * min(1.0, dt * 10.0)
        else:
            # Gentle breathing when idle / listening
            target_drop = 0.04 * (0.5 + 0.5 * math.sin(self._t * 2.5))
            self.jaw_drop += (target_drop - self.jaw_drop) * min(1.0, dt * 8.0)

        # ── 2. Claws Snapping & Gesturing ────────────────────────────────────────
        if speaking:
            # Snapping pincers on speech syllables
            pincer_snap = 0.5 + 0.5 * math.sin(self._t * 13.0)
            self.claw_l_open = max(0.1, min(1.0, curr_amp * 1.3 + pincer_snap * 0.4))
            self.claw_r_open = max(0.1, min(1.0, curr_amp * 1.2 + (1.0 - pincer_snap) * 0.35))
            # Gesturing up/down with vocal cadence
            self.claw_l_bob = 14.0 * math.sin(self._t * 9.0) * curr_amp
            self.claw_r_bob = 16.0 * math.cos(self._t * 8.5) * curr_amp
            self.head_bob = 8.0 * math.sin(self._t * 12.0) * curr_amp
        elif thinking:
            # Thoughtful claw tapping
            tap = 0.5 + 0.5 * math.sin(self._t * 7.0)
            self.claw_l_open = 0.15 + 0.25 * tap
            self.claw_r_open = 0.15 + 0.25 * (1.0 - tap)
            self.claw_l_bob = 4.0 * math.sin(self._t * 3.0)
            self.claw_r_bob = -4.0 * math.sin(self._t * 3.0)
            self.head_bob = 2.0 * math.sin(self._t * 2.0)
        else:
            # Claws held receptive and ready
            self.claw_l_open = 0.30 + 0.10 * math.sin(self._t * 2.0)
            self.claw_r_open = 0.30 + 0.10 * math.cos(self._t * 2.0)
            self.claw_l_bob = 3.0 * math.sin(self._t * 1.8)
            self.claw_r_bob = 3.0 * math.cos(self._t * 1.8)
            self.head_bob = 0.0

        # ── 3. Idle Body Sway ───────────────────────────────────────────────────
        self.body_sway = 4.0 * math.sin(self._t * 1.6)

        # ── 4. Natural Blinking & Gaze Tracking ─────────────────────────────────
        self._blink_timer -= dt
        if self._blink_timer <= 0:
            self.blink = 1.0
            self._blink_timer = random.uniform(2.8, 5.2)
        else:
            self.blink = max(0.0, self.blink - dt * 6.5)

        self._gaze_timer -= dt
        if self._gaze_timer <= 0:
            if thinking:
                self._target_gaze_x = random.choice([-0.7, 0.7])
                self._target_gaze_y = -0.6  # Looking up thoughtfully
            elif speaking:
                self._target_gaze_x = random.uniform(-0.35, 0.35)
                self._target_gaze_y = random.uniform(-0.2, 0.2)
            else:
                self._target_gaze_x = 0.0   # Looking directly at user
                self._target_gaze_y = 0.0
            self._gaze_timer = random.uniform(1.2, 2.8)

        self.gaze_x += (self._target_gaze_x - self.gaze_x) * min(1.0, dt * 8.0)
        self.gaze_y += (self._target_gaze_y - self.gaze_y) * min(1.0, dt * 8.0)

        # ── 5. Antennae Twitch & Sway ───────────────────────────────────────────
        self.ant_l_angle = 12.0 * math.sin(self._t * 2.8) + (8.0 * math.sin(self._t * 9.0) if speaking else 0.0)
        self.ant_r_angle = 12.0 * math.cos(self._t * 2.6) + (8.0 * math.cos(self._t * 9.0) if speaking else 0.0)

    def paint(self, p: QPainter, cx: float, cy: float, size: float, speaking: bool, thinking: bool) -> None:
        """Render the complete talking lobster puppet centered at (cx, cy)."""
        scale = size / 480.0
        p.save()
        p.setRenderHint(QPainter.RenderHint.Antialiasing)

        # Apply root sway and head nod
        p.translate(cx + self.body_sway * scale, cy + self.head_bob * scale)
        p.scale(scale, scale)

        # ── Background Puppet Shadow / Projection ──────────────────────────────
        shadow_grad = QRadialGradient(0, 140, 160)
        shadow_grad.setColorAt(0.0, QColor(0, 229, 255, 65))
        shadow_grad.setColorAt(0.6, QColor(0, 132, 255, 25))
        shadow_grad.setColorAt(1.0, QColor(0, 0, 0, 0))
        p.setBrush(QBrush(shadow_grad))
        p.setPen(Qt.PenStyle.NoPen)
        p.drawEllipse(QPointF(0, 140), 160, 38)

        # ── Left and Right Claws (Drawn behind or flanking head) ────────────────
        self._draw_claw(p, is_left=True, bob=self.claw_l_bob, openness=self.claw_l_open)
        self._draw_claw(p, is_left=False, bob=self.claw_r_bob, openness=self.claw_r_open)

        # ── Carapace / Chest Segment ───────────────────────────────────────────
        self._draw_carapace(p, speaking, thinking)

        # ── Antennae ───────────────────────────────────────────────────────────
        self._draw_antennae(p)

        # ── Puppet Head & Talking Articulating Jaw ─────────────────────────────
        self._draw_head_and_jaw(p)

        # ── Expressive Puppet Eyes ─────────────────────────────────────────────
        self._draw_eyes(p)

        # ── Characteristic Puppet Status Badge ─────────────────────────────────
        p.restore()
        self._draw_puppet_badge(p, cx, cy + size * 0.42, speaking, thinking)

    def _draw_carapace(self, p: QPainter, speaking: bool, thinking: bool) -> None:
        """Draws the lobster body carapace and the glowing chest Arc Reactor."""
        # Body segments
        body_path = QPainterPath()
        body_path.moveTo(-52, 60)
        body_path.quadTo(-70, 120, -45, 145)
        body_path.quadTo(0, 160, 45, 145)
        body_path.quadTo(70, 120, 52, 60)
        body_path.closeSubpath()

        body_grad = QLinearGradient(0, 60, 0, 155)
        body_grad.setColorAt(0.0, self.SHELL_BASE)
        body_grad.setColorAt(0.7, self.SHELL_DARK)
        body_grad.setColorAt(1.0, QColor(100, 12, 25))

        p.setBrush(QBrush(body_grad))
        p.setPen(QPen(self.SHELL_LIGHT, 2.5))
        p.drawPath(body_path)

        # Segment ribs
        p.setPen(QPen(QColor(255, 255, 255, 70), 1.6))
        p.drawArc(QRectF(-48, 85, 96, 26), 25 * 16, 130 * 16)
        p.drawArc(QRectF(-42, 110, 84, 24), 25 * 16, 130 * 16)

        # Chest Medallion: Glowing SPARK Arc Reactor crest
        reactor_cx, reactor_cy = 0.0, 95.0
        r_glow = 24.0 + 6.0 * math.sin(self._t * 4.0) + (10.0 * self._smoothed_amp if speaking else 0.0)

        glow_grad = QRadialGradient(reactor_cx, reactor_cy, r_glow)
        glow_grad.setColorAt(0.0, self.CYAN_GLOW)
        glow_grad.setColorAt(0.4, self.BLUE_ACCENT)
        glow_grad.setColorAt(1.0, QColor(0, 0, 0, 0))
        p.setBrush(QBrush(glow_grad))
        p.setPen(Qt.PenStyle.NoPen)
        p.drawEllipse(QPointF(reactor_cx, reactor_cy), r_glow, r_glow)

        # Inner reactor core ring
        p.setBrush(QBrush(QColor(8, 20, 40, 200)))
        p.setPen(QPen(self.CYAN_GLOW, 2.0))
        p.drawEllipse(QPointF(reactor_cx, reactor_cy), 14, 14)

        # Little reactor triangles / spokes
        p.setPen(QPen(self.WHITE, 1.8))
        for k in range(3):
            ang = self._t * 2.5 + k * (2.0 * math.pi / 3.0)
            p.drawLine(
                int(reactor_cx + math.cos(ang) * 5),
                int(reactor_cy + math.sin(ang) * 5),
                int(reactor_cx + math.cos(ang) * 12),
                int(reactor_cy + math.sin(ang) * 12),
            )

    def _draw_head_and_jaw(self, p: QPainter) -> None:
        """Draws the puppet head and articulating lower jaw that drops on syllables."""
        # ── Lower Jaw (Puppet Flap that drops down) ─────────────────────────────
        # The jaw hinges at (-48, 20) and (48, 20) and drops down by `self.jaw_drop * 32.0`
        drop_px = self.jaw_drop * 28.0

        p.save()
        p.translate(0, drop_px)

        # Inside mouth cavity (visible when jaw drops)
        if self.jaw_drop > 0.08:
            mouth_cavity = QPainterPath()
            mouth_cavity.moveTo(-36, 14 - drop_px)
            mouth_cavity.quadTo(0, 8 - drop_px, 36, 14 - drop_px)
            mouth_cavity.quadTo(42, 28, 0, 36)
            mouth_cavity.quadTo(-42, 28, -36, 14 - drop_px)
            mouth_cavity.closeSubpath()

            p.setBrush(QBrush(self.MOUTH_INTERIOR))
            p.setPen(QPen(QColor(40, 5, 12), 2.0))
            p.drawPath(mouth_cavity)

            # Little puppet tongue / palate inside
            tongue = QRectF(-14, 20, 28, 14)
            p.setBrush(QBrush(self.TONGUE_COLOR))
            p.setPen(Qt.PenStyle.NoPen)
            p.drawEllipse(tongue)

        # Lower Jaw Shell
        jaw_path = QPainterPath()
        jaw_path.moveTo(-42, 16)
        jaw_path.quadTo(0, 18, 42, 16)
        jaw_path.quadTo(36, 44, 0, 48)
        jaw_path.quadTo(-36, 44, -42, 16)
        jaw_path.closeSubpath()

        jaw_grad = QLinearGradient(0, 16, 0, 48)
        jaw_grad.setColorAt(0.0, self.SHELL_LIGHT)
        jaw_grad.setColorAt(0.5, self.SHELL_BASE)
        jaw_grad.setColorAt(1.0, self.SHELL_DARK)

        p.setBrush(QBrush(jaw_grad))
        p.setPen(QPen(self.SHELL_LIGHT, 2.2))
        p.drawPath(jaw_path)
        p.restore()

        # ── Upper Head (Rostrum / Upper Snout) ──────────────────────────────────
        head_path = QPainterPath()
        head_path.moveTo(-58, 20)
        head_path.quadTo(-65, -45, -30, -68)
        # Rostrum beak at top
        head_path.lineTo(0, -82)
        head_path.lineTo(30, -68)
        head_path.quadTo(65, -45, 58, 20)
        head_path.quadTo(0, 28, -58, 20)
        head_path.closeSubpath()

        head_grad = QLinearGradient(0, -82, 0, 25)
        head_grad.setColorAt(0.0, self.SHELL_LIGHT)
        head_grad.setColorAt(0.4, self.SHELL_BASE)
        head_grad.setColorAt(1.0, self.SHELL_DARK)

        p.setBrush(QBrush(head_grad))
        p.setPen(QPen(self.SHELL_LIGHT, 2.5))
        p.drawPath(head_path)

        # Highlight ridges on snout
        p.setPen(QPen(QColor(255, 255, 255, 90), 1.8))
        p.drawLine(0, -78, 0, -32)
        p.drawLine(-18, -48, -12, -22)
        p.drawLine(18, -48, 12, -22)

    def _draw_eyes(self, p: QPainter) -> None:
        """Draws the animated puppet stalk eyes with eyelids and pupils."""
        for side, eye_cx in ((-1, -30.0), (1, 30.0)):
            eye_cy = -56.0
            r_eye = 19.0

            # Eye stalks
            p.setBrush(QBrush(self.SHELL_DARK))
            p.setPen(QPen(self.SHELL_LIGHT, 1.8))
            p.drawEllipse(QPointF(eye_cx, eye_cy + 8), 12, 10)

            # Sclera (White globe)
            p.setBrush(QBrush(self.WHITE))
            p.setPen(QPen(QColor(160, 20, 35), 2.2))
            p.drawEllipse(QPointF(eye_cx, eye_cy), r_eye, r_eye)

            # Pupil with gaze tracking
            pupil_r = 7.5
            px = eye_cx + self.gaze_x * 5.0
            py = eye_cy + self.gaze_y * 4.5
            p.setBrush(QBrush(QColor(15, 20, 30)))
            p.setPen(Qt.PenStyle.NoPen)
            p.drawEllipse(QPointF(px, py), pupil_r, pupil_r)

            # Cyan iris ring inside pupil
            p.setBrush(Qt.BrushStyle.NoBrush)
            p.setPen(QPen(self.CYAN_GLOW, 1.4))
            p.drawEllipse(QPointF(px, py), pupil_r - 1.5, pupil_r - 1.5)

            # Specular sparkle highlight
            p.setBrush(QBrush(self.WHITE))
            p.setPen(Qt.PenStyle.NoPen)
            p.drawEllipse(QPointF(px - 2.8, py - 2.8), 2.8, 2.8)

            # Eyelid (blinking)
            if self.blink > 0.05:
                lid_h = r_eye * 2.0 * self.blink
                lid_rect = QRectF(eye_cx - r_eye - 1, eye_cy - r_eye - 1, (r_eye + 1) * 2, lid_h)
                p.setBrush(QBrush(self.SHELL_BASE))
                p.setPen(QPen(self.SHELL_LIGHT, 1.5))
                p.save()
                # Clip to eye circle
                clip_path = QPainterPath()
                clip_path.addEllipse(QPointF(eye_cx, eye_cy), r_eye, r_eye)
                p.setClipPath(clip_path)
                p.drawRect(lid_rect)
                p.restore()

    def _draw_antennae(self, p: QPainter) -> None:
        """Draws flexible swaying antennae with glowing cyan tips."""
        p.save()
        # Left Antenna
        p.setPen(QPen(self.SHELL_LIGHT, 2.8, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
        ant_l = QPainterPath()
        ant_l.moveTo(-16, -72)
        ctrl_l_x = -75 + self.ant_l_angle * 1.5
        ctrl_l_y = -135
        end_l_x = -110 + self.ant_l_angle * 2.2
        end_l_y = -175
        ant_l.quadTo(ctrl_l_x, ctrl_l_y, end_l_x, end_l_y)
        p.drawPath(ant_l)

        # Right Antenna
        ant_r = QPainterPath()
        ant_r.moveTo(16, -72)
        ctrl_r_x = 75 + self.ant_r_angle * 1.5
        ctrl_r_y = -135
        end_r_x = 110 + self.ant_r_angle * 2.2
        end_r_y = -175
        ant_r.quadTo(ctrl_r_x, ctrl_r_y, end_r_x, end_r_y)
        p.drawPath(ant_r)

        # Glowing antenna sensor bulbs
        p.setPen(Qt.PenStyle.NoPen)
        for ex, ey in ((end_l_x, end_l_y), (end_r_x, end_r_y)):
            g = QRadialGradient(ex, ey, 8)
            g.setColorAt(0.0, self.WHITE)
            g.setColorAt(0.4, self.CYAN_GLOW)
            g.setColorAt(1.0, QColor(0, 0, 0, 0))
            p.setBrush(QBrush(g))
            p.drawEllipse(QPointF(ex, ey), 8, 8)

        p.restore()

    def _draw_claw(self, p: QPainter, is_left: bool, bob: float, openness: float) -> None:
        """Draws an articulating lobster claw with pinching pincers and arm segment."""
        p.save()
        side = -1 if is_left else 1
        arm_base_x = side * 54.0
        arm_base_y = 65.0 + bob

        p.translate(arm_base_x, arm_base_y)
        p.scale(side, 1)  # Mirror for right claw

        # ── Arm segment ─────────────────────────────────────────────────────────
        arm_path = QPainterPath()
        arm_path.moveTo(0, 0)
        arm_path.quadTo(40, -15, 68, -40)
        arm_path.lineTo(65, -20)
        arm_path.quadTo(35, 5, 0, 10)
        arm_path.closeSubpath()

        p.setBrush(QBrush(self.SHELL_DARK))
        p.setPen(QPen(self.SHELL_LIGHT, 2.0))
        p.drawPath(arm_path)

        # ── Claw Pivot Wrist ────────────────────────────────────────────────────
        p.translate(68, -35)

        # Upper fixed pincer
        upper_pincer = QPainterPath()
        upper_pincer.moveTo(0, 0)
        upper_pincer.cubicTo(25, -45, 65, -35, 78, -12)
        upper_pincer.quadTo(55, -8, 30, -5)
        upper_pincer.closeSubpath()

        claw_grad = QLinearGradient(0, -40, 80, 0)
        claw_grad.setColorAt(0.0, self.SHELL_BASE)
        claw_grad.setColorAt(0.7, self.SHELL_LIGHT)
        claw_grad.setColorAt(1.0, QColor(255, 130, 80))

        p.setBrush(QBrush(claw_grad))
        p.setPen(QPen(self.SHELL_LIGHT, 2.2))
        p.drawPath(upper_pincer)

        # Lower movable pincer (articulates open/close with `openness`)
        p.save()
        pincer_ang = openness * 32.0  # Open up to 32 degrees
        p.rotate(pincer_ang)

        lower_pincer = QPainterPath()
        lower_pincer.moveTo(0, 0)
        lower_pincer.cubicTo(25, 30, 60, 25, 74, 5)
        lower_pincer.quadTo(45, 2, 22, -2)
        lower_pincer.closeSubpath()

        p.setBrush(QBrush(claw_grad))
        p.setPen(QPen(self.SHELL_LIGHT, 2.2))
        p.drawPath(lower_pincer)

        # Cyber serrations on pincer edge
        p.setPen(QPen(self.CYAN_GLOW, 1.8))
        for sx in (25, 38, 52):
            p.drawLine(sx, -3, sx + 2, 2)

        p.restore()
        p.restore()

    def _draw_puppet_badge(self, p: QPainter, bx: float, by: float, speaking: bool, thinking: bool) -> None:
        """Renders an aesthetic holographic badge indicating Laubster Puppet mode."""
        p.save()
        p.setRenderHint(QPainter.RenderHint.Antialiasing)

        badge_w, badge_h = 240.0, 32.0
        badge_rect = QRectF(bx - badge_w / 2.0, by - badge_h / 2.0, badge_w, badge_h)

        # Background pill
        p.setBrush(QBrush(QColor(6, 16, 32, 210)))
        border_col = self.CYAN_GLOW if speaking else (self.BLUE_ACCENT if thinking else QColor(0, 168, 255, 100))
        p.setPen(QPen(border_col, 1.5))
        p.drawRoundedRect(badge_rect, 16, 16)

        # Status text
        p.setFont(QFont("Segoe UI", 9, QFont.Weight.Bold))
        status_label = "LAUBSTER TALKING" if speaking else ("LAUBSTER THINKING..." if thinking else "LAUBSTER PUPPET")
        p.setPen(self.WHITE if speaking else self.CYAN_GLOW)
        p.drawText(badge_rect, Qt.AlignmentFlag.AlignCenter, f"🦞  {status_label}")

        p.restore()
