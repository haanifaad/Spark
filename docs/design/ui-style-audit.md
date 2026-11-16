# SPARK UI Style Audit: OpenClaw UI to Windows 11 Desktop Transformation

## Executive Summary

This document performs an exhaustive visual style audit comparing the existing **SPARK** (OpenClaw) interface against the target **Windows 11 Professional Dark Desktop** aesthetic, derived from the Google Stitch reference (`stitch_windows_11_ai_workspace_ui.zip` / Stitch project `8935867021820891994`).

The transformation is strictly a **VISUAL STYLE TRANSFORMATION**. The structural layout, information architecture, navigation, interactions, and functionality of OpenClaw are preserved in their entirety. Only the visual skin, tokens, surfaces, borders, radii, and typography are updated.

---

## 1. Current Visual Appearance

The existing SPARK / OpenClaw interface combines a native Windows App SDK (WinUI 3) host with a shared OpenClaw design language:
- **Geometry**: Characterized by oversized radii (`--radius-md: 10px`, `--radius-lg: 14px`, `--radius-xl: 20px`, `ComposerRadius: 20px`, `SurfaceRadius: 12px`). The chat composer and input areas use oversized pill shapes and bubbly floating card contours.
- **Surfaces**: Heavy reliance on varying dark tones (`#0e1015`, `#161920`, `#191c24`, `#1f2330`) with inconsistent boundary separation and excessive glass/translucency.
- **Typography**: Uses `Instrument Sans` with fallback to generic system fonts. Text sizing leans slightly large with loose mobile-style vertical rhythm.
- **Borders & Dividers**: Highly variable border colors and opacities across different panels and controls, leading to visual clutter or indistinct grouping.
- **Hover & Active States**: Intense, high-contrast hover highlights rather than subtle desktop surface shifts.
- **Scrollbars**: Default browser / system scrollbars that feel chunky and unstyled on desktop canvases.

---

## 2. Target Visual Direction (Windows 11 Professional Dark Desktop)

The target visual direction reflects a native Windows 11 desktop productivity application:
- **Design Identity**: Restrained, flat, polished dark desktop environment built on clear surface hierarchy.
- **Geometry**: Compact, rectilinear to slightly rounded corners (4px small, 6px medium, 8px large). Complete elimination of oversized gummy pills and toy-like floating bubbles.
- **Surfaces**: Solid dark layered backgrounds with precise subtle surface contrast:
  - Base Window / Navigation Rail: `#111214`
  - Window Header / Title Bar: `#0d0e10`
  - Sidebar / Sessions Tree: `#17181a`
  - Main Workspace Canvas: `#1a1b1e`
  - Cards & Content Surfaces: `#202124`
  - Inputs & Text Areas: `#27282b`
- **Borders**: Thin, understated 1px borders (`rgba(255, 255, 255, 0.06)`).
- **Typography**: Windows-native typography using `Segoe UI Variable Text`, `Segoe UI`, and system fallbacks. Compact, readable 13px/14px baseline with crisp legibility.
- **Branding & Accent**: Restrained SPARK orange/amber accent (`#F97316` / `#EA580C`) applied purposefully to primary action points (e.g. submit button, active indicator) without gratuitous glowing halos or neon borders.
- **Scrollbars**: Streamlined 4px Windows 11 dark scrollbar with `#2e3036` thumb and `#4a4d56` hover state.

---

## 3. Colour System Comparison

| Token / Role | Current OpenClaw Value | Target Windows 11 Token | Target Value | Visual Impact |
|---|---|---|---|---|
| Window Base Background | `#0e1015` | `--spark-window-bg` | `#111214` | Solid, neutral dark canvas |
| Title Bar Background | Variable / `#0e1015` | `--spark-titlebar-bg` | `#0d0e10` | Distinct top chrome separation |
| Sidebar Background | `#13151b` / `#0e1015` | `--spark-sidebar-bg` | `#17181a` | Clear acrylic-like sidebar layering |
| Main Workspace Canvas | `#0e1015` / `#161920` | `--spark-workspace-bg` | `#1a1b1e` | Deep canvas surface with subtle contrast |
| Card / Panel Surface | `#161920` / `#191c24` | `--spark-surface` | `#202124` | Restrained content containment |
| Input / Composer Background | `#191c24` / `#1f2330` | `--spark-input-bg` | `#27282b` | High-readability input surface |
| Primary Text | `#f0f0f2` / `#f4f4f5` | `--spark-text-primary` | `#E5E7EB` | Softened, high-contrast readable ink |
| Secondary Text | `#bcbcc0` | `--spark-text-secondary` | `#A0A5AD` | Clear secondary visual hierarchy |
| Muted / Tertiary Text | `#8b8b94` | `--spark-text-muted` | `#70757E` | Subtle labels, timestamps, metadata |
| Subtle Border | `rgba(255,255,255,0.12)` | `--spark-border` | `rgba(255, 255, 255, 0.06)` | Understated, clean partition line |
| Subtle Hover Surface | `rgba(255,255,255,0.08)` | `--spark-hover` | `rgba(255, 255, 255, 0.05)` | Restrained desktop hover feedback |
| Selected Surface | Variable mix | `--spark-selected` | `rgba(255, 255, 255, 0.08)` | Clean selection highlight |
| Brand Primary Accent | Generic blue / orange mix | `--spark-accent` | `#F97316` | Distinctive SPARK brand signature |
| Brand Accent Gradient/Active | Bright glow | `--spark-accent-hover` | `#EA580C` | Crisp, non-neon button interaction |

---

## 4. Typography Differences

| Property | Current SPARK / OpenClaw | Target Windows 11 Specification | Rationale |
|---|---|---|---|
| Primary Font Family | `Instrument Sans`, sans-serif | `"Segoe UI Variable Text", "Segoe UI", -apple-system, BlinkMacSystemFont, Roboto, sans-serif` | Native Windows desktop character |
| Monospace Font Family | `JetBrains Mono`, monospace | `Consolas, "Segoe UI Mono", "Courier New", monospace` | Crisp native desktop code rendering |
| Base Body Font Size | 14px / line-height 1.55 | 13px – 14px / line-height 1.45 – 1.5 | Compact desktop density |
| Heading 1 | 24px – 28px bold | 22px – 24px semi-bold (600) | Eliminates oversized mobile banners |
| Heading 2 | 20px semi-bold | 16px – 18px semi-bold (600) | Balanced visual hierarchy |
| Section Labels / Eyebrows | 11px uppercase | 10px uppercase tracking-wider (600) | Precision sidebar taxonomy |
| Button Labels | 14px | 12px – 13px medium (500) | Compact desktop button typography |

---

## 5. Border and Radius Differences

| Element | Current Radius | Target Radius | Visual Outcome |
|---|---|---|---|
| Small controls (tags, chips) | 6px (`--radius-sm`) | 4px (`--spark-radius-small`) | Crisp, restrained pill elimination |
| Medium controls (buttons, inputs) | 10px (`--radius-md`) | 6px (`--spark-radius-medium`) | Modern Windows 11 desktop corner |
| Large containers (cards, dialogs) | 14px – 20px (`--radius-lg`, `--radius-xl`) | 8px (`--spark-radius-large`) | Eliminates bubbly card appearance |
| Chat Composer Shell | 20px pill | 8px – 10px restrained rounded-rect | Professional desktop input box |
| Segmented Switchers | Full round pill | 6px – 8px compact capsule | Streamlined mode switching |
| Tool Cards & Expander Blocks | 12px | 6px – 8px | Structured technical view |
| Borders | Inconsistent 1px to 2px | Uniform 1px `rgba(255, 255, 255, 0.06)` | Clean, non-distracting separation |

---

## 6. Surface and Shadow Differences

| Characteristic | Current State | Target State |
|---|---|---|
| Background layers | Heterogeneous dark gradients | Solid, flat layered dark surfaces |
| Elevation & Depth | Translucent glassmorphism, blur | Layered solid surface luminance (#111214 < #17181a < #1a1b1e < #202124 < #27282b) |
| Box Shadows | Heavy drop-shadows on floating cards | Minimal, crisp shadows (e.g. `0 4px 20px -2px rgba(0,0,0,0.35)` on focused inputs only) |
| Glow & Neon Effects | Neon border highlights and ambient blurs | Completely removed; pure solid boundaries with subtle alpha strokes |

---

## 7. Component-Level Restyling Inventory

The following components across the frontend stack require visual restyling:

1. **Title Bar & Window Chrome**:
   - Background `#0d0e10`, bottom border `1px solid rgba(255,255,255,0.04)`.
   - Windows 11 native caption controls (Minimize, Maximize, Close with red hover).
   - Centered subtle title "Spark" in `#70757E` (11px).
   - Compact navigation actions (Back, Forward, Sidebar Toggle) and menu tabs.

2. **Navigation Rail**:
   - Background `#111214`, right border `1px solid rgba(255,255,255,0.04)`.
   - Width 48px – 56px, centered 36px – 40px icon buttons with 6px – 8px radii.
   - Restrained active state (`rgba(255,255,255,0.1)`), muted inactive state (`#70757E`).

3. **Sidebar & Session List**:
   - Background `#17181a`, right border `1px solid rgba(255,255,255,0.05)`.
   - Workspace header with Spark logo and dropdown in `#E5E7EB`.
   - New Chat button with 6px radius, hover `rgba(255,255,255,0.05)`.
   - Section headers (PINNED, PROJECTS, RECENTS) in 10px uppercase `#70757E`.
   - Session rows with 4px radius, clean text truncation, and subtle hover.

4. **Chat Workspace Canvas**:
   - Background `#1a1b1e`.
   - Top action bar with compact segmented mode switcher (Chat / Work).
   - Quick action pill buttons (Upgrade with subtle amber accent, New session `+`).

5. **Chat Composer**:
   - Restrained `#27282b` surface, 8px – 10px corner radius (replaces 20px gummy pill).
   - Border `1px solid rgba(255, 255, 255, 0.08)`, focus border `rgba(255, 255, 255, 0.2)`.
   - Interior controls: circular or compact 6px buttons with subtle hover.
   - Primary send button in SPARK orange (`#F97316`).

6. **Message Bubbles & Transcript**:
   - Assistant turns: flat canvas presentation without heavy card backgrounds.
   - User messages: subtle surface `#202124` or restrained accent tint (`rgba(249, 115, 22, 0.1)`), 6px – 8px radius.
   - Code blocks: `#17181a` background, 6px radius, Consolas monospace font, clean syntax highlighting.

7. **Tool Panels & Cards**:
   - 6px radius, `#202124` background, 1px border `rgba(255,255,255,0.06)`.
   - Compact status badges and progress indicators.

8. **Settings, Dialogs & Menus**:
   - Dialog backplates `#17181a`, card groups `#202124`.
   - 6px – 8px radius, clean typography, compact control sizing.
   - No gummy cards or excessive padding.

9. **Scrollbars**:
   - Width 4px, track transparent, thumb `#2e3036`, hover `#4a4d56`.

---

## 8. Conclusion

All structural layouts, navigation flows, and interactive state machines in OpenClaw are preserved. By establishing a unified design token system and applying the Windows 11 styling across both the web stylesheets and WinUI native theme resources, SPARK achieves an authoritative, professional desktop appearance.
