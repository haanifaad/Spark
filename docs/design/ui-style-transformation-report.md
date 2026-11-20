# SPARK  -  Visual Style Transformation Report

## Executive Summary

This report documents the visual styling transformation of the **SPARK** personal AI agent desktop application, migrating its visual skin from an informal consumer chatbot aesthetic to a clean, restrained, professional **Windows 11 Dark Desktop** appearance inspired by the Google Stitch reference design (`stitch_windows_11_ai_workspace_ui.zip` / `code.html`).

In accordance with strict operational constraints:
- **No UX or structural redesign was performed**: Navigation hierarchy, information architecture, controls, settings, dialogs, sessions, tool mechanisms, and chat workflows remain 100% intact.
- **Dual-surface synchronization**: Both the Windows-native desktop shell (`Spark-main` with WinUI 3 and Microsoft.UI.Reactor) and the embedded/gateway web client (`openclaw-main/ui` with SolidJS and Vite) were systematically unified under the same Windows 11 design token specification.
- **Both production builds verified**: 
  - Web frontend (`openclaw-main/ui`): Vite production build succeeded with **0 errors**.
  - WinUI 3 desktop client (`Spark-main`): .NET 10 solution build succeeded with **0 errors and 0 warnings**.
  - Test suites: **8,046 unit tests passed**.

---

## 1. Visual Direction & Identity

### 1.1 Core Principles
- **Windows 11 Professional Dark Desktop**: Replaces playful consumer AI tropes with serious, polished productivity desktop characteristics.
- **Solid, Layered Surfaces**: Eliminates loud background gradient images, decorative wallpaper patterns, and aggressive glassmorphism in favor of restrained, flat, layered dark tones.
- **Understated Geometry**: Replaced oversized 20px gummy pill radii with compact, desktop-appropriate corner radii (4px for micro-controls, 6px for standard buttons/inputs, 8px for surface cards and flyouts).
- **Native Typography**: Standardized on `"Segoe UI Variable Text"`, `"Segoe UI"`, and system desktop fallbacks, paired with `Consolas` for code blocks.
- **Restrained SPARK Brand Identity**: SPARK's warm orange accent (`#F97316` / `#EA580C`) is used purposefully for active status, focus indicators, selection pips, and primary submission buttons, without neon halos or decorative bleed.

---

## 2. Design Token System

To prevent ad-hoc hardcoded colors, a single cohesive design token hierarchy was established across both web and native platforms.

### 2.1 Master Token Palette

| Token Name | Value | Purpose |
|---|---|---|
| `--spark-window-bg` / `SolidBackgroundFillColorBaseBrush` | `#111214` | Application base window and outermost background |
| `--spark-titlebar-bg` | `#0D0E10` | Top window chrome and draggable title bar |
| `--spark-sidebar-bg` / `NavigationViewDefaultPaneBackground` | `#17181A` | Left navigation rail, assistant switcher, session drawer |
| `--spark-workspace-bg` / `ChatCanvasBrush` | `#1A1B1E` | Primary conversation workspace canvas and active viewport |
| `--spark-surface` / `CardBackgroundFillColorDefaultBrush` | `#202124` | Assistant message containers, tool inspection cards, modal bodies |
| `--spark-input-bg` / `ChatComposerBrush` | `#27282B` | Prompt composer surface, search bars, textboxes |
| `--spark-border` / `ChatStrokeBrush` | `rgba(255, 255, 255, 0.06)` (`#0FFFFFFF`) | Crisp, understated 1px borders between panels and around cards |
| `--spark-border-subtle` | `rgba(255, 255, 255, 0.04)` (`#0AFFFFFF`) | Dividers and nested section separators |
| `--spark-hover` / `SubtleFillColorSecondaryBrush` | `rgba(255, 255, 255, 0.05)` (`#0DFFFFFF`) | Interactive hover state on buttons, list rows, tabs |
| `--spark-active` / `SubtleFillColorTertiaryBrush` | `rgba(255, 255, 255, 0.10)` (`#1AFFFFFF`) | Pressed and active selection feedback |
| `--spark-text-primary` / `TextFillColorPrimaryBrush` | `#E5E7EB` | Primary body text, labels, user prompts, assistant prose |
| `--spark-text-secondary` / `TextFillColorSecondaryBrush` | `#A0A5AD` | Subheadings, timestamps, secondary navigation items, icons |
| `--spark-text-muted` / `TextFillColorTertiaryBrush` | `#70757E` | Metadata, keyboard hints, disabled labels, placeholder text |
| `--spark-accent` | `#F97316` | SPARK brand orange accent for primary actions and highlights |
| `--spark-accent-hover` | `#EA580C` | Hover state for accent buttons and interactive links |

### 2.2 Geometry & Radius Tokens

| Token Name | Value | Target Components |
|---|---|---|
| `--spark-radius-sm` | `4px` | Badges, status pills, tags, toolbar icon buttons, check markers |
| `--spark-radius-md` / `ControlCornerRadius` | `6px` | Input fields, standard buttons, dropdown selectors, tool headers |
| `--spark-radius-lg` / `OverlayCornerRadius` | `8px` | Composer container, message cards, flyouts, menus, modal dialogs |

---

## 3. Detailed Component Transformations

### 3.1 Web UI Client (`openclaw-main/ui`)

1. **Global Stylesheet Architecture (`src/styles/base.css`)**:
   - Integrated the complete `--spark-*` token set under `:root` and `[data-theme="dark"]`.
   - Remapped existing functional variables (`--bg`, `--bg-accent`, `--bg-elevated`, `--card`, `--popover`, `--panel`, `--text`, `--muted`, `--border`, `--input`, etc.) directly to `--spark-*` tokens.
   - Constrained corner radius variables: `--radius-sm: 4px`, `--radius-md: 6px`, `--radius-lg: 8px`, `--composer-radius: 8px`, `--field-radius: 6px`.
   - Updated typography font stack to `"Segoe UI Variable Text", "Segoe UI", -apple-system, BlinkMacSystemFont, Roboto, sans-serif`.
   - Replaced default wide scrollbars with modern Windows 11 style 4px dark scrollbars (`--scrollbar-size: 4px`, thumb `#2E3036`, hover `#4A4D56`).

2. **Surface Backgrounds (`src/styles/theme-backgrounds.css`)**:
   - Disabled decorative background wallpaper and gradient imagery (`--app-background-image: none !important`).
   - Ensured solid, distraction-free surfaces across chat, dashboard, settings, and terminal views.

3. **Buttons & Controls (`src/styles/components.css`)**:
   - Stripped neon glowing shadows (`box-shadow: 0 0 20px rgba(...)`) from primary buttons.
   - Enforced restrained 6px radius and subtle 1px border contrast.

4. **Composer Surface (`src/styles/chat/composer-surface.css`)**:
   - Replaced oversized 20px pill corner radius with `--composer-radius` (8px).
   - Replaced dramatic colored shadow rings with Stitch reference elevation (`box-shadow: 0 4px 20px -2px rgba(0, 0, 0, 0.35)`).
   - Styled composer input background with `#27282B` and subtle `rgba(255, 255, 255, 0.06)` border.

### 3.2 WinUI 3 Desktop Client (`Spark-main/src/OpenClaw.Tray.WinUI`)

1. **Application Theme Dictionaries (`App.xaml`)**:
   - Configured `Default` (Dark) theme dictionary to bind native brushes:
     - `SolidBackgroundFillColorBaseBrush` $\rightarrow$ `#FF111214`
     - `ApplicationPageBackgroundThemeBrush` $\rightarrow$ `#FF1A1B1E`
     - `ControlFillColorDefaultBrush` $\rightarrow$ `#FF202124`
     - `ControlFillColorSecondaryBrush` $\rightarrow$ `#FF27282B`
     - `ControlStrokeColorDefaultBrush` $\rightarrow$ `#0FFFFFFF`
     - `CardBackgroundFillColorDefaultBrush` $\rightarrow$ `#FF202124`
     - `SubtleFillColorSecondaryBrush` $\rightarrow$ `#0DFFFFFF`
     - `TextFillColorPrimaryBrush` $\rightarrow$ `#FFE5E7EB`
     - `TextFillColorSecondaryBrush` $\rightarrow$ `#FFA0A5AD`
     - `TextFillColorTertiaryBrush` $\rightarrow$ `#FF70757E`
     - `NavigationViewExpandedPaneBackground` $\rightarrow$ `#FF17181A`
     - `NavigationViewSelectionIndicatorForeground` $\rightarrow$ `#FFF97316`

2. **Chat Native Theme Resources (`Themes/ChatResources.xaml`)**:
   - Replaced `Transparent` canvas with solid `#FF1A1B1E` for `ChatCanvasBrush`.
   - Set `ChatComposerBrush` to `#FF27282B` and `ChatCardBrush` to `#FF202124`.
   - Set `ChatStrokeBrush` to `#0FFFFFFF` (subtle 6% white border).
   - Unified text colors (`ChatTextBrush: #FFE5E7EB`, `ChatSecondaryTextBrush: #FFA0A5AD`, `ChatMutedTextBrush: #FF70757E`).
   - Mapped `ChatClawBrush` and interactive selection indicators to `#FFF97316`.
   - Restrained question card corner radii from 12px/20px to 6px/8px, and flyout/menu presenters to 8px.

3. **Reactor Chat Layout & Geometry (`Chat/ChatVisuals.cs` & `Chat/ReactorQuestionPanel.cs`)**:
   - Reduced `ComposerRadius` from `20` to `8`.
   - Reduced `SurfaceRadius` from `12` to `6`.
   - Reduced `CodeRadius` from `10` to `6`.
   - Adjusted question options from 11px gummy pills to 6px compact rounded rectangles.

4. **Window Shell & Navigation (`Windows/WorkspaceWindow.xaml`)**:
   - Root Window grid explicitly backed by `#FF111214`.
   - TitleBar explicitly backed by `#FF0D0E10`.
   - Added subtle 1px border (`#0FFFFFFF`) separating the navigation sidebar from the main content canvas.
   - Main workspace host grid explicitly backed by `#FF1A1B1E`.

---

## 4. Visual Comparison: Before vs. After

| UI Element | Original OpenClaw Styling | Target Windows 11 SPARK Aesthetic |
|---|---|---|
| **Base Window** | Semitransparent Mica / variable dark | Solid `#111214` restrained dark surface |
| **Title Bar** | Window default / blurred | Crisp `#0D0E10` with subtle bottom border |
| **Sidebar / Rail** | Generic gray / variable opacity | Solid `#17181A` with 1px border (`rgba(255,255,255,0.06)`) |
| **Workspace Canvas** | Transparent with gradient wallpapers | Solid `#1A1B1E` distraction-free canvas |
| **Composer** | 20px oversized floating gummy pill | 8px compact rounded rectangle in `#27282B` |
| **Cards & Bubbles** | 12px - 20px rounded bubbles | 6px - 8px crisp panels with subtle borders |
| **Primary Buttons** | Neon glowing halos / heavy drop shadows | Flat `#F97316` accent with clean hover states |
| **Typography** | Mixed system sans-serif | Native `Segoe UI Variable Text` & `Segoe UI` |
| **Scrollbars** | Standard wide scrollbars | Slim 4px Windows 11 style dark scrollbars |

---

## 5. Verification & Test Execution

### 5.1 Build Verification

1. **Web Frontend Build (`openclaw-main/ui`)**:
   - Command: `pnpm run build`
   - Outcome: **Passed (Exit Code 0)** in 41.88s.
   - Bundle: Produced clean distribution in `dist/control-ui`.

2. **WinUI 3 Desktop Solution Build (`Spark-main`)**:
   - Command: `dotnet build openclaw-windows-node.slnx -c Debug -p:Platform=x64`
   - Outcome: **Passed (Exit Code 0)** in 107.31s.
   - Diagnostics: **0 Warning(s), 0 Error(s)**.
   - Generated Binaries:
     - `src\OpenClaw.Tray.WinUI\bin\x64\Debug\net10.0-windows10.0.22621.0\win-x64\OpenClaw.Tray.WinUI.dll`
     - `src\OpenClaw.Tray.WinUI\bin\x64\Debug\net10.0-windows10.0.22621.0\win-x64\OpenClaw.Tray.WinUI.exe`

### 5.2 Functional & Unit Tests

- Command: `pnpm.cmd --dir openclaw-main\ui vitest run`
- Outcome: **8,046 unit tests passed** across 654 test files.
- Verified domains:
  - Chat transcript message indexing and rendering
  - Session workspace switching and state preservation
  - Composer input management, keyboard shortcuts, and submission
  - Navigation rail and destination routing
  - Gateway protocol state management and reconnections

### 5.3 Local App Launch & Process Verification

- Command: `.\run-app-local.ps1 -NoBuild -Configuration Debug -UseStoreGateway`
- Outcome: Successfully initialized unpackaged WinUI client, verified data directory allocation, acquired state lock, and launched executable `OpenClaw.Tray.WinUI.exe`.

---

## 6. Deliverable File References

- **Style Audit**: [ui-style-audit.md](file:///C:/Intel/Spark/docs/design/ui-style-audit.md)
- **Transformation Report**: [ui-style-transformation-report.md](file:///C:/Intel/Spark/docs/design/ui-style-transformation-report.md)
- **Web Stylesheets**:
  - [base.css](file:///C:/Intel/Spark/openclaw-main/ui/src/styles/base.css)
  - [theme-backgrounds.css](file:///C:/Intel/Spark/openclaw-main/ui/src/styles/theme-backgrounds.css)
  - [components.css](file:///C:/Intel/Spark/openclaw-main/ui/src/styles/components.css)
  - [composer-surface.css](file:///C:/Intel/Spark/openclaw-main/ui/src/styles/chat/composer-surface.css)
- **WinUI Styling & Geometry**:
  - [App.xaml](file:///C:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/App.xaml)
  - [ChatResources.xaml](file:///C:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Themes/ChatResources.xaml)
  - [ChatVisuals.cs](file:///C:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ChatVisuals.cs)
  - [ReactorQuestionPanel.cs](file:///C:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Chat/ReactorQuestionPanel.cs)
  - [WorkspaceWindow.xaml](file:///C:/Intel/Spark/Spark-main/src/OpenClaw.Tray.WinUI/Windows/WorkspaceWindow.xaml)
- **Stitch Reference Assets**:
  - Reference HTML: [code.html](file:///C:/Intel/Spark/stitch_reference/code.html)
  - Reference Screenshot: [reference_screenshot.jpg](file:///C:/Intel/Spark/stitch_reference/reference_screenshot.jpg)

---

## 7. Conclusion

The SPARK application visual transformation is complete. All existing OpenClaw interface functionality, navigation patterns, UX workflows, and information architectures have been strictly preserved, while the visual styling has been successfully elevated into a cohesive, restrained, and professional Windows 11 dark desktop environment.
