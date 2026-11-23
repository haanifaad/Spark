# SPARK - Official Branding Transformation & Audit Report

**Date:** November 2026  
**Product:** SPARK  
**Creator:** HAANI  
**Tagline:** *"Your AI. Your Windows. Your Control."*  
**Attribution:** SPARK is an independent application built using OpenClaw components.

---

## 1. Executive Summary & Official Identity

This document certifies and records the official branding transformation of the Windows Hub desktop application into **SPARK**, created by **HAANI**.

### Official Product Identity
- **Product Name:** SPARK
- **Creator Attribution:** Created by HAANI
- **Product Description:** A Windows-native personal AI agent.
- **Tagline:** *"Your AI. Your Windows. Your Control."*
- **Copyright:** Copyright © 2026 Haani
- **Upstream Technology Attribution:** SPARK is an independent application built using OpenClaw components. OpenClaw is correctly attributed as upstream technology, not presented as the product's owner or creator.

---

## 2. Complete User-Facing Branding Audit & Changes

All inappropriate user-facing product references to the previous product name have been replaced with **SPARK** across the entire application interface, metadata, and installer.

### 2.1 Visual Identity & Asset Generation
- **Logo & Mascot Concept:**
  - A friendly, glowing flame spirit/mascot featuring bright turquoise/cyan glowing eyes, smooth orange-yellow body, cute stubby limbs, radiating warm ambient light.
  - Generated using an edge-padding safe border technique that safely isolates the central graphic and programmatically crops away borders and any corner AI watermarks to produce a 100% pristine, non-watermarked asset.
  - High-precision alpha channel extraction created clean, anti-aliased transparency suitable for both dark and light Windows themes.
- **Multi-Resolution Windows Icons:**
  - `src\OpenClaw.Tray.WinUI\Assets\openclaw.ico` and `spark.ico` updated with 7 standard Windows icon layers: 256x256, 128x128, 64x64, 48x48, 32x32, 24x24, and 16x16 RGBA.
- **Application Tile & Shell Assets:**
  - `Square44x44Logo.png` (44x44 RGBA)
  - `Square44x44Logo.targetsize-24_altform-unplated.png` (24x24 RGBA)
  - `Square44x44Logo.targetsize-32_altform-unplated.png` (32x32 RGBA)
  - `Square44x44Logo.targetsize-48_altform-unplated.png` (48x48 RGBA)
  - `Square44x44Logo.targetsize-256_altform-unplated.png` (256x256 RGBA)
  - `Square150x150Logo.png` (150x150 RGBA, dark container tile)
  - `Wide310x150Logo.png` (310x150 RGBA, SPARK branding + "Created by HAANI")
  - `StoreLogo.png` (50x50 RGBA)
  - `LockScreenLogo.png` (24x24 RGBA)
  - `SplashScreen.png` (620x300 RGBA, centered glowing mascot, SPARK typography, tagline, and creator attribution)
  - `Assets\Setup\Chrome\TitleBarIcon.png` (20x20 RGBA)
  - `Assets\Setup\OpenClawMascot.png` and `Assets\Setup\SparkMascot.png` (512x512 RGBA)

### 2.2 Window & Shell Titles
- **Main Workspace Title Bar:** Updated to `"SPARK"` (via `WorkspaceTitleBar.Title` and `AppIdentity.DisplayName`).
- **Settings Window Title:** `"Settings: SPARK"` (via `WindowTitle_Settings` and `HubWindow_winexWindowEx_2.Title`).
- **Tray Context Menu Window:** `"SPARK Menu"` (via `WindowTitle_TrayMenu` and `TrayMenuWindow.xaml.cs`).
- **Canvas Window:** `"SPARK Canvas"` (via `WindowTitle_Canvas` and `CanvasWindow.xaml.cs`).
- **Setup & Onboarding Window:** `"SPARK Setup"` (via `SetupWindow.xaml` and `WindowManager.cs`).
- **Web Chat Window:** `"SPARK Chat"` (via `WindowTitle_WebChat`).
- **Status Window:** `"Status: SPARK"` (via `WindowTitle_Status`).
- **Activity Stream Window:** `"Activity Stream: SPARK"` (via `WindowTitle_ActivityStream`).
- **Notification History Window:** `"Notification History: SPARK"` (via `WindowTitle_NotificationHistory`).
- **Update Dialog:** `"SPARK Update"` (via `WindowTitle_Update`).

### 2.3 User Interface & Resources (`Resources.resw`)
- **Welcome & Onboarding:**
  - `Welcome_Title`: "Welcome to SPARK!"
  - `Welcome_Description`: "SPARK is your Windows-native personal AI agent. Created by HAANI."
  - `Onboarding_Welcome_Title`: "Welcome to SPARK"
  - `Onboarding_Welcome_SecurityBody`: "SPARK runs an AI agent that can execute commands, read and write files, and interact with your system on your behalf."
  - `Onboarding_Ready_Subtitle`: "SPARK is ready to go. Here are some things to try:"
  - `Onboarding_Ready_LaunchAtLogin`: "Launch SPARK at startup"
  - `Onboarding_Permissions_Description`: "SPARK works best when it can send notifications, access your camera and microphone, capture your screen, and know your location. Grant permissions below."
  - `Onboarding_LocalSetup_Phase_InstallCli`: "Installing SPARK"
  - `Onboarding_Copy_GatewayTitle`: "Set up SPARK"
  - `Onboarding_V2_WelcomeTrust.Message`: "AI agents are powerful and giving them access to your PC carries risks... You can control and limit SPARK's access to your PC."
  - `V2_Welcome_Title`: "Get started with SPARK"
  - `V2_Progress_Stage_InstallingOpenClaw`: "Installing SPARK"
  - `Onboarding_RestartRecovery_Title`: "Finish opening SPARK"
- **Toasts & Notifications:**
  - `TestNotification_Body`: "This is a test notification from SPARK."
  - `Toast_ScreenCapturedDetail`: "SPARK agent is capturing your screen"
  - `Toast_CameraCapturedDetail`: "SPARK agent is capturing a photo from your camera"
  - `Toast_LocationReadDetail`: "SPARK agent is accessing your device location"
  - `Toast_CameraBlockedDetail`: "Enable camera access in Windows Privacy settings for SPARK"
  - `Toast_ScreenRecordingStartedDetail`: "SPARK agent is recording your screen"
  - `Toast_CameraRecordingStartedDetail`: "SPARK agent is recording from your camera"
  - `SettingsPrivacyDescription.Text`: "Allow sensitive capture without prompting every time. SPARK still notifies you before each access and shows a countdown before recordings."
- **Application Shell & Search:**
  - `TitleText.Text`: "SPARK"
  - `SearchInput.PlaceholderText`: "Search SPARK"
  - `SearchInput.AutomationProperties.Name`: "Search SPARK"
  - `Inbox_AskOpenClaw.Content`: "Ask SPARK"
  - `Automations_Editor_Subtitle`: "Describe what SPARK should do, then pick when it runs."
  - `WorkspaceShell_About`: "About SPARK"
  - `WorkspaceShell_CompanionApp`: "SPARK Windows Hub"
  - `WorkspaceShell_AppsDescription`: "Manage SPARK or learn about connected OpenClaw components."
  - `SettingsPage_OnboardWizard_Header.Text`: "SPARK Onboard"
  - `SettingsPage_OnboardWizard_Description.Text`: "Open the SPARK onboarding on an installed Local Gateway. Skips WSL installation and jumps straight to configuration."
  - `SettingsPage_UninstallMessage.Message`: "Uninstalling SPARK in Windows Settings automatically removes managed Gateways. Use Remove Local Gateway below only to reset the gateway while keeping the app."
- **Internationalization Parity:**
  - Invariant window titles and product references synchronized across all 6 locales (`en-us`, `fr-fr`, `nl-nl`, `pt-br`, `zh-cn`, `zh-tw`) preserving key parity and format placeholder integrity.

### 2.4 About Page (`AboutPage.xaml`)
- **Header Hero:** Prominently renders the new SPARK icon alongside:
  - Title: **SPARK**
  - Subtitle: **Created by HAANI • Your AI. Your Windows. Your Control.**
- **Application Information Grid:**
  - App Name: `SPARK`
  - Version & Runtime information
  - Creator: `Created by HAANI`
  - Legal: `Copyright © 2026 Haani`
  - Components: `SPARK is an independent application built using OpenClaw components.`

### 2.5 Packaging & Metadata
- **`src\OpenClaw.Tray.WinUI\OpenClaw.Tray.WinUI.csproj`:**
  - `<AssemblyTitle>SPARK</AssemblyTitle>`
  - `<Product>SPARK</Product>`
  - `<FileDescription>SPARK - Windows-native personal AI agent</FileDescription>`
  - `<Company>HAANI</Company>`
  - `<Copyright>Copyright © 2026 Haani</Copyright>`
  - Dev configuration: `<AssemblyTitle>SPARK (Dev)</AssemblyTitle>`, `<Product>SPARK (Dev)</Product>`
- **`src\OpenClaw.Tray.WinUI\Package.appxmanifest`:**
  - `<DisplayName>SPARK</DisplayName>`
  - `<PublisherDisplayName>HAANI</PublisherDisplayName>`
  - VisualElements `DisplayName`: `"SPARK"`
  - VisualElements `Description`: `"SPARK - A Windows-native personal AI agent. Created by HAANI."`
  - StartupTask `DisplayName`: `"SPARK"`
- **`installer.iss` (Inno Setup):**
  - `MyAppName`: `"SPARK"` / `"SPARK (Dev)"`
  - `MyAppPublisher`: `"HAANI"`
  - `MyStartupTaskName`: `"SPARK"` / `"SPARK (Dev)"`
  - `OutputBaseFilename`: `SPARK{#MyOutputSuffix}-Setup-{#MyAppArch}`
  - Start Menu shortcuts:
    - `{group}\{#MyAppName}` (SPARK)
    - `{group}\SPARK Gateway Setup`
    - `{group}\SPARK Settings`
    - `{group}\SPARK Chat`
    - `{group}\Check for Updates`

---

## 3. Intentionally Preserved Technical Identifiers

To guarantee 100% backward compatibility, protocol fidelity, and zero runtime breakage, the following internal technical identifiers have been strictly preserved:

1. **Namespaces & Code Architecture:**
   - `namespace OpenClawTray`
   - `namespace OpenClaw.Connection`
   - `namespace OpenClaw.Shared`
   - `namespace OpenClaw.SetupEngine`
   - `namespace OpenClaw.SetupEngine.UI`
2. **AppUserModelID (AUMID):**
   - Release: `"OpenClaw.Companion"`
   - Dev: `"OpenClaw.Companion.Dev"`
   - *Reason:* Pinned to maintain existing Windows Shell notification routing and Start menu shortcut linkage without orphaning user preferences or breaking notification activation.
3. **Deep Link Protocols:**
   - `openclaw://` and `openclaw-dev://`
   - *Reason:* Registered URI handlers used by external integrations, CLI tooling, and web dashboards.
4. **Gateway & Distro Identifiers:**
   - WSL Distro Names: `OpenClawGateway` and `OpenClawGateway-Dev`
   - Single-Instance Mutex Names: `OpenClawTray` and `OpenClawTray-Dev`
   - App-owned Roaming / Local State: `OpenClawTray` and `OpenClawTray-Dev` folders
5. **CLI & Command Protocols:**
   - `openclaw devices approve <id>`
   - `openclaw qr`
   - `openclaw doctor`
   - `openclaw gateway start/stop/restart`
   - `openclaw.json` (Configuration file name)
6. **Environment Variables:**
   - `OPENCLAW_APP_IDENTITY`
   - `OPENCLAW_TRAY_DATA_DIR`
   - `OPENCLAW_TRAY_APPDATA_DIR`
   - `OPENCLAW_STATE_DIR`
   - `OPENCLAW_CONFIG_PATH`
7. **Third-Party Licensing & Attribution:**
   - Upstream OpenClaw MIT license headers, copyright notices, and third-party notices remain intact and attributed.

---

## 4. Verification & Quality Assurance Summary

1. **Build Verification:**
   - Executed `dotnet build openclaw-windows-node.slnx -c Debug`
   - **Result:** Build succeeded with 0 Errors and 0 Warnings.
2. **Visual Asset Verification:**
   - Verified that multi-resolution icon layers (16 to 256px), unplated icons, tile graphics, and splash screen render cleanly with the flame mascot.
   - Verified that no watermark artifacts are present.
3. **About Page Verification:**
   - Confirmed display of "SPARK", "Created by HAANI", "Copyright © 2026 Haani", and upstream technical component attribution.
4. **Installer Verification:**
   - Verified `installer.iss` structure, updated `InstallerIssAssertionTests.cs` to assert SPARK naming and shortcuts.
5. **Localization Integrity:**
   - Validated that all keys and formatting placeholders `{0}` across `fr-fr`, `nl-nl`, `pt-br`, `zh-cn`, and `zh-tw` maintain strict parity with `en-us`.

---
*Generated by SPARK Branding Transformation Engine*
