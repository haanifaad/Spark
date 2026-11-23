using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClaw.Shared.Coding.CliIntelligence;

/// <summary>
/// Built-in catalog of all 84+ CLI harnesses and workflow matrices from the SPARK CLI Hub.
/// </summary>
public static class SparkCliHubCatalog
{
    /// <summary>
    /// Built-in capability-based workflow matrices.
    /// </summary>
    public static IReadOnlyList<CliMatrixEntry> Matrices { get; } = new CliMatrixEntry[]
    {
        new()
        {
            Id = "video-creation",
            Name = "Video Creation & Editing",
            Description = "Capability-based matrix for end-to-end video production: storyboard planning, story/audio direction, internet video/music search/download with source triage, capture, generation, voice/music, sound design, footage analysis, transcription, high-end caption design, assembly, overlay, thumbnail, encode, and quality review with render-doctor investigation. Providers include harness CLIs, public CLIs, Python libs, native binaries, bundled scripts, agent skills, and cloud APIs.",
            Category = "video",
            Clis = new[] { "generate-veo-video", "jimeng", "openscreen", "obs-studio", "audacity", "minimax-cli", "elevenlabs", "suno", "kdenlive", "shotcut", "videocaptioner", "gimp", "krita", "inkscape" }
        },
        new()
        {
            Id = "knowledge-research",
            Name = "Knowledge / Office / Research",
            Description = "Capability-based matrix for research, note-taking, document authoring, and publishing: web/literature search, article retrieval, citations/references, notes/PKM, outlining, multi-doc synthesis, office document authoring (DOCX/XLSX/PPTX/PDF), format conversion, PDF manipulation, diagrams, web publishing, and LaTeX builds. Providers include harness CLIs, public CLIs, Python libs, native binaries, and cloud APIs.",
            Category = "knowledge",
            Clis = new[] { "exa", "browser", "safari", "clibrowser", "zotero", "obsidian", "mubu", "notebooklm", "intelwatch", "libreoffice", "anygen", "drawio", "mermaid" }
        },
        new()
        {
            Id = "3d-cad",
            Name = "3D & CAD",
            Description = "Capability-based matrix for 3D modeling, parametric CAD, sculpting, point clouds, photogrammetry, PBR texturing, preview/offline rendering, GPU frame debugging, fabrication (slicing and CAM), and game-engine export. Mostly offline-capable; providers include harness CLIs, Python libs, native binaries, and cloud APIs.",
            Category = "3d",
            Clis = new[] { "blender", "freecad", "cloudcompare", "cloudanalyzer", "godot", "renderdoc" }
        },
        new()
        {
            Id = "game-development",
            Name = "Game Development",
            Description = "Capability-based matrix for game development: engine authoring (Godot-first), 3D/2D/audio/notation assets, AI-generated assets, agent-driven playtesting, build packaging, store publishing, and telemetry/crash reporting. Providers include harness CLIs, public CLIs, Python libs, native binaries, and cloud APIs.",
            Category = "game",
            Clis = new[] { "godot", "blender", "freecad", "krita", "inkscape", "audacity", "musescore", "comfyui", "slay_the_spire_ii" }
        },
        new()
        {
            Id = "image-design",
            Name = "Image & Graphic Design",
            Description = "Capability-based matrix for image and graphic design: AI image generation, raster/vector editing, UI mockups/wireframes, diagrams, super-resolution upscaling, photo library management, RAW development, and CMS publishing. GIMP/Krita/Inkscape/ComfyUI cover most work offline; cloud APIs escalate quality.",
            Category = "image",
            Clis = new[] { "comfyui", "gimp", "krita", "inkscape", "sketch", "drawio", "mermaid" }
        },
    };

    /// <summary>
    /// All 84+ CLI harnesses spanning DevOps, 3D, Design, Video, Audio, Database, AI, Science, and Knowledge.
    /// </summary>
    public static IReadOnlyList<CliHarnessEntry> Harnesses { get; } = new CliHarnessEntry[]
    {
        new()
        {
            Name = "jumpserver",
            DisplayName = "JumpServer",
            Category = "devops",
            Description = "Bastion host management — manage assets, users, permissions, sessions, accounts, and audit logs via JumpServer REST API",
            Requires = "JumpServer v3.0+ instance",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=jumpserver/agent-harness",
            EntryPoint = "cli-anything-jumpserver",
            SkillPath = "skills/cli-anything-jumpserver/SKILL.md",
            Homepage = "https://www.jumpserver.org",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "openrefine",
            DisplayName = "OpenRefine",
            Category = "database",
            Description = "Agent-native CLI for OpenRefine import, operation-history cleaning, row inspection, export, and session undo/redo through the real local HTTP API.",
            Requires = "OpenRefine 3.10.x or newer running as a local web server",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=openrefine/agent-harness",
            EntryPoint = "cli-anything-openrefine",
            SkillPath = "skills/cli-anything-openrefine/SKILL.md",
            Homepage = "https://openrefine.org/",
            Version = "1.0.1",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "cc-switch",
            DisplayName = "CC Switch",
            Category = "devops",
            Description = "Manage AI coding tool configurations - inspect providers, skills, MCP servers, usage stats, and proxy settings",
            Requires = "CC Switch installed with active database",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=cc-switch/agent-harness",
            EntryPoint = "cli-anything-ccswitch",
            SkillPath = "skills/cli-anything-ccswitch/SKILL.md",
            Homepage = "https://github.com/HKUDS/CLI-Anything",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "wiremock",
            DisplayName = "WireMock",
            Category = "testing",
            Description = "HTTP mock server management — create stubs, inspect requests, record traffic, and manage scenarios via WireMock REST API",
            Requires = "WireMock server running (java -jar wiremock-standalone.jar)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=wiremock/agent-harness",
            EntryPoint = "cli-anything-wiremock",
            SkillPath = "skills/cli-anything-wiremock/SKILL.md",
            Homepage = "https://wiremock.org",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "nslogger",
            DisplayName = "NSLogger",
            Category = "devops",
            Description = "Capture, parse, filter, export, and mirror NSLogger iOS/macOS logs from the CLI",
            Requires = "macOS for native Bonjour live capture; Python 3.10+",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=nslogger/agent-harness",
            EntryPoint = "cli-anything-nslogger",
            SkillPath = "skills/cli-anything-nslogger/SKILL.md",
            Homepage = "https://github.com/fpillet/NSLogger",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "anygen",
            DisplayName = "AnyGen",
            Category = "generation",
            Description = "Generate docs, slides, websites and more via AnyGen cloud API",
            Requires = "ANYGEN_API_KEY",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=anygen/agent-harness",
            EntryPoint = "cli-anything-anygen",
            SkillPath = "skills/cli-anything-anygen/SKILL.md",
            Homepage = "https://anygen.com",
            Version = "1.0.0",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "adguardhome",
            DisplayName = "AdGuardHome",
            Category = "network",
            Description = "DNS ad-blocking and network infrastructure management via AdGuardHome REST API",
            Requires = "AdGuardHome instance running",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=adguardhome/agent-harness",
            EntryPoint = "cli-anything-adguardhome",
            SkillPath = null,
            Homepage = "https://adguard.com/adguard-home/overview.html",
            Version = "1.0.1",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "audacity",
            DisplayName = "Audacity",
            Category = "audio",
            Description = "Audio editing and processing via sox",
            Requires = "sox (apt install sox)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=audacity/agent-harness",
            EntryPoint = "cli-anything-audacity",
            SkillPath = "skills/cli-anything-audacity/SKILL.md",
            Homepage = "https://www.audacityteam.org",
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation", "game-development" },
            IsInstalled = true
        },
        new()
        {
            Name = "wavetone",
            DisplayName = "WaveTone",
            Category = "audio",
            Description = "Prepare WaveTone 2.61 audio transcription manifests, probe audio files, and launch the real Windows WaveTone executable",
            Requires = "WaveTone 2.61 extracted on Windows; set WAVETONE_EXE or WAVETONE_HOME",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=wavetone/agent-harness",
            EntryPoint = "cli-anything-wavetone",
            SkillPath = "skills/cli-anything-wavetone/SKILL.md",
            Homepage = "http://ackiesound.ifdef.jp/",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "blender",
            DisplayName = "Blender",
            Category = "3d",
            Description = "3D modeling, animation, and rendering via blender --background --python",
            Requires = "blender >= 4.2",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=blender/agent-harness",
            EntryPoint = "cli-anything-blender",
            SkillPath = "skills/cli-anything-blender/SKILL.md",
            Homepage = "https://www.blender.org",
            Version = "1.0.0",
            MatrixTags = new[] { "3d-cad", "game-development" },
            IsInstalled = true
        },
        new()
        {
            Name = "browser",
            DisplayName = "Browser",
            Category = "web",
            Description = "Browser automation via DOMShell MCP server. Maps Chrome's Accessibility Tree to a virtual filesystem for agent-native navigation.",
            Requires = "Node.js, npx, Chrome + DOMShell extension",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=browser/agent-harness",
            EntryPoint = "cli-anything-browser",
            SkillPath = "skills/cli-anything-browser/SKILL.md",
            Homepage = "https://github.com/apireno/DOMShell",
            Version = "1.0.0",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "web-yu-pri",
            DisplayName = "Web Yu-pri",
            Category = "web",
            Description = "Japan Post Web Yu-pri browser workflow automation for login, inspection, screenshots, dry-run planning, and contents-form filling",
            Requires = "Japan Post Web Yu-pri account, Playwright, Microsoft Edge/Chrome or Playwright Chromium",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=web-yu-pri/agent-harness",
            EntryPoint = "cli-anything-web-yu-pri",
            SkillPath = "skills/cli-anything-web-yu-pri/SKILL.md",
            Homepage = "https://mgr.post.japanpost.jp/C30P01Action.do",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "comfyui",
            DisplayName = "ComfyUI",
            Category = "ai",
            Description = "AI image generation workflow management via ComfyUI REST API",
            Requires = "ComfyUI running at http://localhost:8188",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=comfyui/agent-harness",
            EntryPoint = "cli-anything-comfyui",
            SkillPath = null,
            Homepage = "https://github.com/comfyanonymous/ComfyUI",
            Version = "1.0.0",
            MatrixTags = new[] { "game-development", "image-design" },
            IsInstalled = true
        },
        new()
        {
            Name = "drawio",
            DisplayName = "Draw.io",
            Category = "diagrams",
            Description = "Diagram creation and export via draw.io CLI",
            Requires = "draw.io desktop app",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=drawio/agent-harness",
            EntryPoint = "cli-anything-drawio",
            SkillPath = "skills/cli-anything-drawio/SKILL.md",
            Homepage = "https://www.drawio.com",
            Version = "1.0.0",
            MatrixTags = new[] { "knowledge-research", "image-design" },
            IsInstalled = true
        },
        new()
        {
            Name = "eez-studio",
            DisplayName = "EEZ Studio",
            Category = "automation",
            Description = "EEZ Studio project, LVGL UI, and SCPI command automation via native .eez-project JSON and real EEZ Studio backend hooks",
            Requires = "EEZ Studio source checkout built with npm run build; Docker for full LVGL simulator builds",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=eez-studio/agent-harness",
            EntryPoint = "cli-anything-eez-studio",
            SkillPath = "skills/cli-anything-eez-studio/SKILL.md",
            Homepage = "https://www.envox.eu/studio/studio-introduction/",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "eth2-quickstart",
            DisplayName = "ETH2 QuickStart",
            Category = "devops",
            Description = "Hardened Ethereum node deployment and operations via the eth2-quickstart automation scripts",
            Requires = "local eth2-quickstart checkout with scripts/eth2qs.sh; Linux host required for real install workflows",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=eth2-quickstart/agent-harness",
            EntryPoint = "cli-anything-eth2-quickstart",
            SkillPath = "skills/cli-anything-eth2-quickstart/SKILL.md",
            Homepage = "https://github.com/chimera-defi/eth2-quickstart",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "firefly-iii",
            DisplayName = "Firefly III",
            Category = "finance",
            Description = "Personal finance management via the Firefly III REST API",
            Requires = "Firefly III instance and personal access token",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=firefly-iii/agent-harness",
            EntryPoint = "cli-anything-firefly-iii",
            SkillPath = "skills/cli-anything-firefly-iii/SKILL.md",
            Homepage = "https://www.firefly-iii.org",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "gimp",
            DisplayName = "GIMP",
            Category = "image",
            Description = "Raster image processing via gimp -i -b (batch mode)",
            Requires = "gimp (apt install gimp)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=gimp/agent-harness",
            EntryPoint = "cli-anything-gimp",
            SkillPath = "skills/cli-anything-gimp/SKILL.md",
            Homepage = "https://www.gimp.org",
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation", "image-design" },
            IsInstalled = true
        },
        new()
        {
            Name = "inkscape",
            DisplayName = "Inkscape",
            Category = "image",
            Description = "SVG vector graphics with export via inkscape --export-filename",
            Requires = "inkscape (apt install inkscape)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=inkscape/agent-harness",
            EntryPoint = "cli-anything-inkscape",
            SkillPath = "skills/cli-anything-inkscape/SKILL.md",
            Homepage = "https://inkscape.org",
            Version = "1.0.1",
            MatrixTags = new[] { "video-creation", "game-development", "image-design" },
            IsInstalled = true
        },
        new()
        {
            Name = "kdenlive",
            DisplayName = "Kdenlive",
            Category = "video",
            Description = "Video editing and rendering via melt",
            Requires = "melt (apt install melt)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=kdenlive/agent-harness",
            EntryPoint = "cli-anything-kdenlive",
            SkillPath = "skills/cli-anything-kdenlive/SKILL.md",
            Homepage = "https://kdenlive.org",
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "krita",
            DisplayName = "Krita",
            Category = "image",
            Description = "Digital painting and raster image editing via Krita CLI export pipeline",
            Requires = "krita (krita.org)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=krita/agent-harness",
            EntryPoint = "cli-anything-krita",
            SkillPath = "skills/cli-anything-krita/SKILL.md",
            Homepage = "https://krita.org",
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation", "game-development", "image-design" },
            IsInstalled = true
        },
        new()
        {
            Name = "calibre",
            DisplayName = "Calibre",
            Category = "office",
            Description = "E-book library management — list, search, metadata editing, format conversion via calibredb, ebook-convert, ebook-meta",
            Requires = "Calibre (apt install calibre, brew install --cask calibre)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=calibre/agent-harness",
            EntryPoint = "cli-anything-calibre",
            SkillPath = "calibre/agent-harness/cli_anything/calibre/skills/SKILL.md",
            Homepage = "https://calibre-ebook.com",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "libreoffice",
            DisplayName = "LibreOffice",
            Category = "office",
            Description = "Create and manipulate ODF documents, export to PDF/DOCX/XLSX/PPTX via headless mode",
            Requires = "libreoffice",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=libreoffice/agent-harness",
            EntryPoint = "cli-anything-libreoffice",
            SkillPath = "skills/cli-anything-libreoffice/SKILL.md",
            Homepage = "https://www.libreoffice.org",
            Version = "1.0.1",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "lldb",
            DisplayName = "LLDB",
            Category = "debugging",
            Description = "Stateful native debugging via LLDB with JSON CLI workflows and a stdio Debug Adapter Protocol server",
            Requires = "LLDB installation with Python bindings available (for example LLVM.LLVM on Windows)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=lldb/agent-harness",
            EntryPoint = "cli-anything-lldb",
            SkillPath = "lldb/agent-harness/cli_anything/lldb/skills/SKILL.md",
            Homepage = "https://lldb.llvm.org",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "zotero",
            DisplayName = "Zotero",
            Category = "office",
            Description = "CLI & MCP server for Zotero 7/8 — 52 MCP tools + 70+ CLI commands for search, import, PDF, BibTeX, notes, and more",
            Requires = "Zotero 7/8 desktop app (running), Python 3.10+",
            InstallCmd = "pip install cli-anything-zotero",
            EntryPoint = "cli-anything-zotero",
            SkillPath = "https://github.com/PiaoyangGuohai1/cli-anything-zotero/blob/main/cli_anything/zotero/skills/SKILL.md",
            Homepage = "https://www.zotero.org",
            Version = "0.4.1",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "mubu",
            DisplayName = "Mubu",
            Category = "office",
            Description = "Knowledge management and outlining via local Mubu desktop data",
            Requires = "Mubu desktop app",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=mubu/agent-harness",
            EntryPoint = "cli-anything-mubu",
            SkillPath = "skills/cli-anything-mubu/SKILL.md",
            Homepage = "https://mubu.com",
            Version = "0.1.1",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "mermaid",
            DisplayName = "Mermaid",
            Category = "diagrams",
            Description = "Mermaid Live Editor state files and renderer URLs",
            Requires = "None",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=mermaid/agent-harness",
            EntryPoint = "cli-anything-mermaid",
            SkillPath = null,
            Homepage = "https://mermaid.js.org",
            Version = "1.0.0",
            MatrixTags = new[] { "knowledge-research", "image-design" },
            IsInstalled = true
        },
        new()
        {
            Name = "notebooklm",
            DisplayName = "NotebookLM",
            Category = "ai",
            Description = "Experimental NotebookLM harness scaffold wrapping the installed notebooklm CLI for notebook, source, chat, artifact, download, and sharing workflows",
            Requires = "notebooklm CLI from notebooklm-py + valid local NotebookLM login session",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=notebooklm/agent-harness",
            EntryPoint = "cli-anything-notebooklm",
            SkillPath = "skills/cli-anything-notebooklm/SKILL.md",
            Homepage = "https://notebooklm.google.com",
            Version = "0.1.0",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "ollama",
            DisplayName = "Ollama",
            Category = "ai",
            Description = "Local LLM inference and model management via Ollama REST API",
            Requires = "Ollama running at http://localhost:11434",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=ollama/agent-harness",
            EntryPoint = "cli-anything-ollama",
            SkillPath = "skills/cli-anything-ollama/SKILL.md",
            Homepage = "https://ollama.com",
            Version = "1.0.1",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "openwebui",
            DisplayName = "OpenWebUI",
            Category = "ai",
            Description = "Operate a running OpenWebUI backend through an agent-friendly CLI",
            Requires = "OpenWebUI instance running",
            InstallCmd = "pip install git+https://github.com/ashaiful/cli-anything-openwebui.git",
            EntryPoint = "cli-anything-openwebui",
            SkillPath = "https://github.com/ashaiful/cli-anything-openwebui/blob/main/cli_anything/openwebui/skills/SKILL.md",
            Homepage = "https://openwebui.com",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "obs-studio",
            DisplayName = "OBS Studio",
            Category = "streaming",
            Description = "Create and manage streaming/recording scenes via command line",
            Requires = "obs-studio",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=obs-studio/agent-harness",
            EntryPoint = "cli-anything-obs-studio",
            SkillPath = "skills/cli-anything-obs-studio/SKILL.md",
            Homepage = "https://obsproject.com",
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "shotcut",
            DisplayName = "Shotcut",
            Category = "video",
            Description = "Video editing and rendering via melt/ffmpeg",
            Requires = "melt (apt install melt), ffmpeg (apt install ffmpeg)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=shotcut/agent-harness",
            EntryPoint = "cli-anything-shotcut",
            SkillPath = "skills/cli-anything-shotcut/SKILL.md",
            Homepage = "https://shotcut.org",
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "openscreen",
            DisplayName = "Openscreen",
            Category = "video",
            Description = "Screen recording editor — zoom, speed ramps, trim, crop, annotations, backgrounds, and polished exports via ffmpeg",
            Requires = "ffmpeg (apt install ffmpeg)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=openscreen/agent-harness",
            EntryPoint = "cli-anything-openscreen",
            SkillPath = "skills/cli-anything-openscreen/SKILL.md",
            Homepage = "https://github.com/siddharthvaddem/openscreen",
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "zoom",
            DisplayName = "Zoom",
            Category = "communication",
            Description = "Meeting management via Zoom REST API (OAuth2)",
            Requires = "Zoom account + OAuth app credentials",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=zoom/agent-harness",
            EntryPoint = "cli-anything-zoom",
            SkillPath = "skills/cli-anything-zoom/SKILL.md",
            Homepage = "https://zoom.us",
            Version = "1.0.1",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "novita",
            DisplayName = "Novita",
            Category = "ai",
            Description = "Access AI models via Novita's OpenAI-compatible API (DeepSeek, GLM, MiniMax)",
            Requires = "NOVITA_API_KEY",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=novita/agent-harness",
            EntryPoint = "cli-anything-novita",
            SkillPath = "skills/cli-anything-novita/SKILL.md",
            Homepage = "https://novita.ai",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "seaclip",
            DisplayName = "SeaClip",
            Category = "project-management",
            Description = "Kanban board, 6-agent AI pipeline, and issue management via SeaClip-Lite FastAPI + SQLite",
            Requires = "SeaClip-Lite running at localhost:5200",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=seaclip/agent-harness",
            EntryPoint = "cli-anything-seaclip",
            SkillPath = "skills/cli-anything-seaclip/SKILL.md",
            Homepage = "https://github.com/t4tarzan/cli-anything-seaclip",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "pm2",
            DisplayName = "PM2",
            Category = "devops",
            Description = "Node.js process management — list, start, stop, restart, logs, and metrics via PM2 CLI",
            Requires = "PM2 (npm install -g pm2)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=pm2/agent-harness",
            EntryPoint = "cli-anything-pm2",
            SkillPath = "skills/cli-anything-pm2/SKILL.md",
            Homepage = "https://pm2.keymetrics.io",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "qgis",
            DisplayName = "QGIS",
            Category = "scientific",
            Description = "Geospatial project authoring, layout export, and processing via PyQGIS and qgis_process",
            Requires = "QGIS with qgis_process on PATH, PyQGIS importable",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=QGIS/agent-harness",
            EntryPoint = "cli-anything-qgis",
            SkillPath = "QGIS/agent-harness/cli_anything/qgis/skills/SKILL.md",
            Homepage = "https://qgis.org",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "chromadb",
            DisplayName = "ChromaDB",
            Category = "database",
            Description = "Vector database operations — collections, documents, semantic search via ChromaDB HTTP API",
            Requires = "ChromaDB server running at localhost:8000",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=chromadb/agent-harness",
            EntryPoint = "cli-anything-chromadb",
            SkillPath = "skills/cli-anything-chromadb/SKILL.md",
            Homepage = "https://www.trychroma.com",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "musescore",
            DisplayName = "MuseScore",
            Category = "music",
            Description = "CLI for music notation — transpose, export PDF/audio/MIDI, extract parts, manage instruments",
            Requires = "MuseScore 4 (musescore.org)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=musescore/agent-harness",
            EntryPoint = "cli-anything-musescore",
            SkillPath = "skills/cli-anything-musescore/SKILL.md",
            Homepage = "https://musescore.org",
            Version = "1.0.0",
            MatrixTags = new[] { "game-development" },
            IsInstalled = true
        },
        new()
        {
            Name = "sketch",
            DisplayName = "Sketch",
            Category = "design",
            Description = "Generate Sketch design files (.sketch) from JSON design specifications via sketch-constructor",
            Requires = "Node.js >= 16.0.0",
            InstallCmd = "cd sketch/agent-harness && npm install && npm link",
            EntryPoint = "sketch-cli",
            SkillPath = null,
            Homepage = "https://www.sketch.com",
            Version = "1.0.0",
            MatrixTags = new[] { "image-design" },
            IsInstalled = true
        },
        new()
        {
            Name = "freecad",
            DisplayName = "FreeCAD",
            Category = "3d",
            Description = "Parametric 3D CAD modeling via FreeCAD CLI (258 commands: Part, Sketcher, PartDesign, Assembly, Mesh, TechDraw, Draft, FEM, CAM, and more)",
            Requires = "FreeCAD >= 1.1 (freecad.org)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=freecad/agent-harness",
            EntryPoint = "cli-anything-freecad",
            SkillPath = "skills/cli-anything-freecad/SKILL.md",
            Homepage = "https://www.freecad.org",
            Version = "1.1.0",
            MatrixTags = new[] { "3d-cad", "game-development" },
            IsInstalled = true
        },
        new()
        {
            Name = "iterm2",
            DisplayName = "iTerm2",
            Category = "devops",
            Description = "Control a running iTerm2 instance — manage windows, tabs, split panes, send text, read output, run tmux -CC, broadcast keystrokes, and configure preferences.",
            Requires = "iTerm2.app (macOS only)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=iterm2/agent-harness",
            EntryPoint = "cli-anything-iterm2",
            SkillPath = "skills/cli-anything-iterm2/SKILL.md",
            Homepage = "https://iterm2.com",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "slay_the_spire_ii",
            DisplayName = "Slay the Spire 2",
            Category = "game",
            Description = "Control the real Slay the Spire 2 game via local STS2_Bridge HTTP API",
            Requires = "Slay the Spire 2 (Steam) with STS2_Bridge mod enabled",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=slay_the_spire_ii/agent-harness",
            EntryPoint = "cli-anything-sts2",
            SkillPath = "skills/cli-anything-slay-the-spire-ii/SKILL.md",
            Homepage = "https://github.com/HKUDS/CLI-Anything/tree/main/slay_the_spire_ii",
            Version = "1.0.0",
            MatrixTags = new[] { "game-development" },
            IsInstalled = true
        },
        new()
        {
            Name = "rms",
            DisplayName = "Teltonika RMS",
            Category = "network",
            Description = "Device management and monitoring via Teltonika RMS REST API",
            Requires = "RMS_API_TOKEN",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=rms/agent-harness",
            EntryPoint = "cli-anything-rms",
            SkillPath = "skills/cli-anything-rms/SKILL.md",
            Homepage = "https://rms.teltonika-networks.com",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "renderdoc",
            DisplayName = "RenderDoc",
            Category = "graphics",
            Description = "GPU frame capture analysis: pipeline state, shader export, texture inspection, draw call browsing",
            Requires = "renderdoc (Python bindings from RenderDoc installation)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=renderdoc/agent-harness",
            EntryPoint = "cli-anything-renderdoc",
            SkillPath = "skills/cli-anything-renderdoc/SKILL.md",
            Homepage = "https://renderdoc.org",
            Version = "0.1.0",
            MatrixTags = new[] { "3d-cad" },
            IsInstalled = true
        },
        new()
        {
            Name = "unrealinsights",
            DisplayName = "Unreal Insights",
            Category = "debugging",
            Description = "Windows-first Unreal trace capture, background session control, engine-matched UnrealInsights builds, and headless Timing Insights export workflows",
            Requires = "Windows + Unreal Engine source or installed build with UnrealEditor and Unreal trace support",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=unrealinsights/agent-harness",
            EntryPoint = "cli-anything-unrealinsights",
            SkillPath = "unrealinsights/agent-harness/cli_anything/unrealinsights/skills/SKILL.md",
            Homepage = "https://dev.epicgames.com/documentation/en-us/unreal-engine/unreal-insights-in-unreal-engine",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "ueatelier",
            DisplayName = "UEAtelier",
            Category = "gamedev",
            Description = "Unreal Editor 5.6/5.7 MCP self-extension workbench - editor automation, build diagnostics, Task Atlas workflows, PIE smoke verification, scaffold pipeline",
            Requires = "Unreal Engine 5.6 or 5.7 with the UEAtelier plugin extracted into <YourProject>/",
            InstallCmd = "pip install git+https://github.com/edwinmeng163-oss/UEAtelier.git#subdirectory=Tools/UEAtelierCli",
            EntryPoint = "cli-anything-ueatelier",
            SkillPath = "https://github.com/edwinmeng163-oss/UEAtelier/blob/main/Tools/UEAtelierCli/SKILL.md",
            Homepage = "https://github.com/edwinmeng163-oss/UEAtelier",
            Version = "0.23.1",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "nsight-graphics",
            DisplayName = "Nsight Graphics CLI",
            Category = "graphics",
            Description = "Windows-first Nsight Graphics CLI for Graphics/OpenGL capture, GPU Trace summary, Generate C++ Capture, and ngfx-replay analysis",
            Requires = "NVIDIA Nsight Graphics installation with ngfx.exe plus optional split capture/replay tools",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=nsight-graphics/agent-harness",
            EntryPoint = "cli-anything-nsight-graphics",
            SkillPath = "skills/cli-anything-nsight-graphics/SKILL.md",
            Homepage = "https://developer.nvidia.com/nsight-graphics",
            Version = "0.2.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "videocaptioner",
            DisplayName = "VideoCaptioner",
            Category = "video",
            Description = "AI-powered video captioning — transcribe speech, optimize/translate subtitles, burn styled subtitles into video",
            Requires = "videocaptioner (pip install videocaptioner), ffmpeg",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=videocaptioner/agent-harness",
            EntryPoint = "cli-anything-videocaptioner",
            SkillPath = "skills/cli-anything-videocaptioner/SKILL.md",
            Homepage = "https://github.com/WEIFENG2333/VideoCaptioner",
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "intelwatch",
            DisplayName = "Intelwatch",
            Category = "osint",
            Description = "Competitive intelligence, M&A due diligence, and OSINT directly from your terminal.",
            Requires = "Node.js >=18",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=intelwatch/agent-harness",
            EntryPoint = "cli-anything-intelwatch",
            SkillPath = "skills/cli-anything-intelwatch/SKILL.md",
            Homepage = "https://github.com/ashroth/intelwatch",
            Version = "1.0.0",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "clibrowser",
            DisplayName = "clibrowser",
            Category = "web",
            Description = "Zero-dependency CLI browser for AI agents with search, extraction, forms, RSS, crawling, auth, and WebMCP support",
            Requires = "Rust toolchain (cargo) for source install, or manual binary download from GitHub Releases",
            InstallCmd = "cargo install --git https://github.com/allthingssecurity/clibrowser.git --tag v0.1.0 --locked",
            EntryPoint = "clibrowser",
            SkillPath = null,
            Homepage = "https://github.com/allthingssecurity/clibrowser",
            Version = "0.1.0",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "cloudcompare",
            DisplayName = "CloudCompare",
            Category = "graphics",
            Description = "3D point cloud and mesh processing: load/save, color ops, normal estimation, Delaunay meshing, noise filtering, ICP registration, connected component segmentation",
            Requires = "cloudcompare (CloudCompare application installed, e.g. via Flatpak or package manager)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=cloudcompare/agent-harness",
            EntryPoint = "cli-anything-cloudcompare",
            SkillPath = "skills/cli-anything-cloudcompare/SKILL.md",
            Homepage = "https://cloudcompare.org",
            Version = "1.0.0",
            MatrixTags = new[] { "3d-cad" },
            IsInstalled = true
        },
        new()
        {
            Name = "exa",
            DisplayName = "Exa",
            Category = "search",
            Description = "AI-powered web search and content extraction via the Exa API",
            Requires = "EXA_API_KEY (free tier at exa.ai)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=exa/agent-harness",
            EntryPoint = "cli-anything-exa",
            SkillPath = "skills/cli-anything-exa/SKILL.md",
            Homepage = "https://exa.ai",
            Version = "1.0.0",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "godot",
            DisplayName = "Godot Engine",
            Category = "gamedev",
            Description = "Game engine project management, scene editing, export and GDScript execution via Godot 4 headless mode",
            Requires = "Godot 4.x (godotengine.org)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=godot/agent-harness",
            EntryPoint = "cli-anything-godot",
            SkillPath = "skills/cli-anything-godot/SKILL.md",
            Homepage = "https://godotengine.org",
            Version = "1.0.0",
            MatrixTags = new[] { "3d-cad", "game-development" },
            IsInstalled = true
        },
        new()
        {
            Name = "sbox",
            DisplayName = "s&box",
            Category = "gamedev",
            Description = "Game engine project management for s&box (Source 2): scenes, prefabs, materials, sounds, codegen, asset graph, project validation",
            Requires = "s&box installed via Steam (sbox.game)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=sbox/agent-harness",
            EntryPoint = "cli-anything-sbox",
            SkillPath = "skills/cli-anything-sbox/SKILL.md",
            Homepage = "https://sbox.game",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "dify-workflow",
            DisplayName = "Dify Workflow",
            Category = "ai",
            Description = "CLI-Anything wrapper for the Dify workflow DSL editor covering create, inspect, validate, edit, export, and layout operations",
            Requires = "Upstream dify-workflow CLI installed from https://github.com/Akabane71/dify-workflow-cli",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=dify-workflow/agent-harness",
            EntryPoint = "cli-anything-dify-workflow",
            SkillPath = "skills/cli-anything-dify-workflow/SKILL.md",
            Homepage = "https://github.com/Akabane71/dify-workflow-cli",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "n8n",
            DisplayName = "n8n",
            Category = "automation",
            Description = "Workflow automation via n8n REST API — 55+ commands",
            Requires = "n8n >= 1.0.0",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=n8n/agent-harness",
            EntryPoint = "cli-anything-n8n",
            SkillPath = "skills/cli-anything-n8n/SKILL.md",
            Homepage = "https://n8n.io",
            Version = "2.4.7",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "cloudanalyzer",
            DisplayName = "CloudAnalyzer",
            Category = "graphics",
            Description = "Point cloud and trajectory QA: Chamfer/AUC/F1, ATE/RPE/drift, ground segmentation metrics, config-driven quality gates, baseline evolution — harness wraps the CloudAnalyzer Python API",
            Requires = "Python 3.10+; cloudanalyzer (`pip install cloudanalyzer`), Open3D for full IO/viewer paths",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=cloudanalyzer/agent-harness",
            EntryPoint = "cli-anything-cloudanalyzer",
            SkillPath = "skills/cli-anything-cloudanalyzer/SKILL.md",
            Homepage = "https://github.com/rsasaki0109/CloudAnalyzer",
            Version = "1.0.0",
            MatrixTags = new[] { "3d-cad" },
            IsInstalled = true
        },
        new()
        {
            Name = "obsidian",
            DisplayName = "Obsidian",
            Category = "knowledge",
            Description = "Knowledge management and note-taking — manage notes, search vault, execute commands via Obsidian Local REST API",
            Requires = "Obsidian desktop app with Local REST API plugin enabled",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=obsidian/agent-harness",
            EntryPoint = "cli-anything-obsidian",
            SkillPath = "skills/cli-anything-obsidian/SKILL.md",
            Homepage = "https://obsidian.md",
            Version = "1.1.0",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "joplin",
            DisplayName = "Joplin",
            Category = "knowledge",
            Description = "Note-taking and to-do automation — manage notebooks, notes, to-dos, tags, attachments, search, sync, and import/export via the Joplin terminal CLI",
            Requires = "Joplin terminal CLI installed and available in PATH as `joplin` (npm install -g joplin)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=joplin/agent-harness",
            EntryPoint = "cli-anything-joplin",
            SkillPath = "skills/cli-anything-joplin/SKILL.md",
            Homepage = "https://joplinapp.org",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "unimol_tools",
            DisplayName = "Uni-Mol Tools",
            Category = "science",
            Description = "Molecular property prediction — train and predict with 5 task types (classification, regression, multiclass, multilabel) for drug discovery",
            Requires = "PyTorch 1.12+, Uni-Mol Tools backend",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=unimol_tools/agent-harness",
            EntryPoint = "cli-anything-unimol-tools",
            SkillPath = "skills/cli-anything-unimol-tools/SKILL.md",
            Homepage = "https://github.com/dptech-corp/Uni-Mol/tree/main/unimol_tools",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "safari",
            DisplayName = "Safari",
            Category = "web",
            Description = "Native macOS Safari browser automation via safari-mcp — 84 tools for navigation, DOM, forms, network capture, and screenshots",
            Requires = "macOS, Safari, safari-mcp (npm install -g safari-mcp)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=safari/agent-harness",
            EntryPoint = "cli-anything-safari",
            SkillPath = "skills/cli-anything-safari/SKILL.md",
            Homepage = "https://github.com/achiya-automation/safari-mcp",
            Version = "1.0.0",
            MatrixTags = new[] { "knowledge-research" },
            IsInstalled = true
        },
        new()
        {
            Name = "macrocli",
            DisplayName = "MacroCLI",
            Category = "automation",
            Description = "Layered macro runtime that converts GUI workflows into parameterized, agent-callable macros — record once, run anywhere via CLI with backend routing across native APIs, file transforms, accessibility controls, and visual template matching",
            Requires = "None",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=macrocli/agent-harness",
            EntryPoint = "cli-anything-macrocli",
            SkillPath = "macrocli/agent-harness/cli_anything/macrocli/skills/SKILL.md",
            Homepage = "https://github.com/HKUDS/CLI-Anything/tree/main/macrocli",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "ve-twini",
            DisplayName = "VE Twini",
            Category = "communication",
            Description = "Unified Twitter/X CLI bridging bird (GraphQL API) and opencli (browser automation)",
            Requires = "Python 3.10+, bird (Twitter API), opencli",
            InstallCmd = "pip install git+https://github.com/collectivewinca/ve-twini.git#subdirectory=agent-harness",
            EntryPoint = "cli-anything-ve-twini",
            SkillPath = "https://github.com/collectivewinca/ve-twini/blob/master/agent-harness/cli_anything/ve_twini/skills/SKILL.md",
            Homepage = "https://x.com",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "quietshrink",
            DisplayName = "QuietShrink",
            Category = "video",
            Description = "Compress macOS screen recordings on Apple Silicon — 70-90% smaller files at visually lossless quality, hardware-encoded, computer stays silent",
            Requires = "ffmpeg with hevc_videotoolbox + the quietshrink bash CLI (curl -fsSL https://raw.githubusercontent.com/achiya-automation/quietshrink/main/install.sh | bash)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=quietshrink/agent-harness",
            EntryPoint = "cli-anything-quietshrink",
            SkillPath = "skills/cli-anything-quietshrink/SKILL.md",
            Homepage = "https://github.com/achiya-automation/quietshrink",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "stata",
            DisplayName = "Stata",
            Category = "science",
            Description = "Run Stata do-files, batch jobs, and reproducible econometric projects from the terminal with JSON output, log parsing, project scaffolding, and security guard",
            Requires = "Licensed Stata installation (StataMP/SE/BE 15+), Python 3.10+",
            InstallCmd = "pip install git+https://github.com/LI-Meng420/stata-cli.git",
            EntryPoint = "cli-anything-stata",
            SkillPath = "https://raw.githubusercontent.com/LI-Meng420/stata-cli/main/cli_anything/stata/skills/SKILL.md",
            Homepage = "https://www.stata.com",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "inkstitch",
            DisplayName = "Ink/Stitch",
            Category = "design",
            Description = "Machine-embroidery digitization — set stitch params, validate, preview, and export to DST/PES/JEF/VP3 via Ink/Stitch",
            Requires = "Ink/Stitch extension installed (https://inkstitch.org/docs/install/)",
            InstallCmd = "pip install cli-anything-inkstitch",
            EntryPoint = "cli-anything-inkstitch",
            SkillPath = "https://github.com/bobbymarko/cli-anything-inkstitch/blob/main/skills/cli-anything-inkstitch/SKILL.md",
            Homepage = "https://inkstitch.org",
            Version = "0.1.1",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "hacker-feeds-cli",
            DisplayName = "Hacker Feeds CLI",
            Category = "search",
            Description = "CLI for GitHub Trending, Hacker News, Reddit, Product Hunt, DEV.to, Lobsters, EchoJS, and V2EX feeds",
            Requires = "Python 3.10+, Node.js (hf CLI)",
            InstallCmd = "pip install git+https://github.com/collectivewinca/hacker-feeds-cli.git",
            EntryPoint = "cli-anything-hacker-feeds-cli",
            SkillPath = "https://github.com/collectivewinca/hacker-feeds-cli/blob/main/cli_anything/hacker_feeds_cli/skills/SKILL.md",
            Homepage = "https://github.com/collectivewinca/hacker-feeds-cli",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "mailchimp",
            DisplayName = "Mailchimp",
            Category = "web",
            Description = "Mailchimp Marketing API v3.0 — manage audiences, campaigns, reports, automations, ecommerce, templates, and more from an agent-native CLI",
            Requires = "MAILCHIMP_API_KEY",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=mailchimp/agent-harness",
            EntryPoint = "cli-anything-mailchimp",
            SkillPath = "skills/cli-anything-mailchimp/SKILL.md",
            Homepage = "https://mailchimp.com/developer/marketing/",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "3mf",
            DisplayName = "3MF",
            Category = "graphics",
            Description = "Mesh geometry editor for 3D printing files — detect and resize cylindrical holes, repair meshes, compare 3MF files",
            Requires = "Python 3.10+; numpy, scipy, trimesh",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=3MF/agent-harness",
            EntryPoint = "cli-anything-3mf",
            SkillPath = "3MF/agent-harness/cli_anything/threemf/skills/SKILL.md",
            Homepage = "https://3mf.io",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "minimax",
            DisplayName = "MiniMax",
            Category = "ai",
            Description = "Chat and TTS via MiniMax AI API — MiniMax-M3 and MiniMax-M2.7 chat models and speech-2.x TTS",
            Requires = "MINIMAX_API_KEY",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=minimax/agent-harness",
            EntryPoint = "cli-anything-minimax",
            SkillPath = "minimax/agent-harness/cli_anything/minimax/skills/SKILL.md",
            Homepage = "https://platform.minimax.io",
            Version = "1.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "siyuan",
            DisplayName = "SiYuan CLI",
            Category = "knowledge-management",
            Description = "Knowledge management and note-taking via SiYuan HTTP API — manage notebooks, documents, blocks, and search content from the command line",
            Requires = "SiYuan running with HTTP API enabled (default http://127.0.0.1:6806)",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=siyuan/agent-harness",
            EntryPoint = "cli-anything-siyuan",
            SkillPath = "skills/cli-anything-siyuan/SKILL.md",
            Homepage = "https://github.com/siyuan-note/siyuan",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "live2d",
            DisplayName = "Live2D Cubism",
            Category = "graphics",
            Description = "Inspect, validate, edit, lint, diff, batch-manage, and deploy Live2D Cubism models (.model3.json, .moc3, .motion3.json, .exp3.json) from the command line — 42 commands covering the full model lifecycle",
            Requires = "Python 3.10+",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=live2d/agent-harness",
            EntryPoint = "cli-anything-live2d",
            SkillPath = "skills/cli-anything-live2d/SKILL.md",
            Homepage = "https://www.live2d.com",
            Version = "0.3.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "tigris",
            DisplayName = "Tigris",
            Category = "storage",
            Description = "Object storage management — buckets, objects, presigned URLs, snapshots, IAM, scoped access keys. Wraps the official `tigris` CLI (S3-compatible, globally distributed, no egress fees)",
            Requires = "Tigris CLI on PATH (`npm install -g @tigrisdata/cli` or `brew install tigrisdata/tap/tigris`) + `tigris login`",
            InstallCmd = "pip install git+https://github.com/HKUDS/CLI-Anything.git#subdirectory=tigris/agent-harness",
            EntryPoint = "cli-anything-tigris",
            SkillPath = "skills/cli-anything-tigris/SKILL.md",
            Homepage = "https://www.tigrisdata.com",
            Version = "1.0.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "tinyfish",
            DisplayName = "TinyFish Web Agent",
            Category = "web",
            Description = "All four TinyFish products from the terminal: web search, clean page extraction, natural-language browser automation, and remote CDP browser sessions — via the REST APIs.",
            Requires = "TinyFish API key (free at https://agent.tinyfish.ai/api-keys)",
            InstallCmd = "pip install cli-anything-tinyfish",
            EntryPoint = "cli-anything-tinyfish",
            SkillPath = "https://github.com/webdevtodayjason/cli-anything-tinyfish/blob/main/cli_anything/tinyfish/skills/SKILL.md",
            Homepage = "https://tinyfish.ai",
            Version = "0.1.1",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "meerk40t",
            DisplayName = "MeerK40t",
            Category = "3d",
            Description = "Laser cutting/engraving via the real MeerK40t kernel — elements, operations, SVG/G-code export with placement guard, and real GRBL hardware control (detect, preflight check, jog, frame) with machine profiles and --json output",
            Requires = "MeerK40t (installed automatically)",
            InstallCmd = "pip install cli-anything-meerk40t",
            EntryPoint = "cli-anything-meerk40t",
            SkillPath = "https://github.com/George-RD/cli-anything-meerk40t/blob/main/skills/cli-anything-meerk40t/SKILL.md",
            Homepage = "https://github.com/meerk40t/meerk40t",
            Version = "1.3.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "palmier",
            DisplayName = "Palmier Pro",
            Category = "video",
            Description = "Agent-native CLI harness for the Palmier Pro local MCP video editor — drive timeline editing, media, captions, and AI generation from the command line",
            Requires = "Palmier Pro desktop app running its local MCP server at 127.0.0.1:19789, Python 3.10+",
            InstallCmd = "pip install git+https://github.com/workcr/palmier-cli-anything.git",
            EntryPoint = "cli-anything-palmier",
            SkillPath = "https://github.com/workcr/palmier-cli-anything/blob/main/skills/cli-anything-palmier/SKILL.md",
            Homepage = "https://palmier.com",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "magnific",
            DisplayName = "Magnific",
            Category = "ai",
            Description = "Agent-native CLI harness for Magnific's remote MCP media-generation server — generate and upscale images and video via mcpc OAuth without loading the full MCP schema each turn",
            Requires = "Node.js + npm (for the mcpc transport), Python 3.10+, Magnific account (OAuth)",
            InstallCmd = "pip install git+https://github.com/workcr/magnific-cli-anything.git",
            EntryPoint = "cli-anything-magnific",
            SkillPath = "https://github.com/workcr/magnific-cli-anything/blob/main/skills/cli-anything-magnific/SKILL.md",
            Homepage = "https://magnific.com",
            Version = "0.1.0",
            MatrixTags = Array.Empty<string>(),
            IsInstalled = true
        },
        new()
        {
            Name = "suno",
            DisplayName = "Suno AI Music Generator",
            Category = "audio",
            Description = "Generate full songs, backing tracks, instrumentals, and musical arrangements via Suno API",
            Requires = "Suno API Key / Account",
            InstallCmd = "pip install suno-cli",
            EntryPoint = "suno",
            SkillPath = "skills/suno/SKILL.md",
            Homepage = null,
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "elevenlabs",
            DisplayName = "ElevenLabs Voice Engine",
            Category = "audio",
            Description = "Ultra-realistic voice synthesis, voice cloning, audio isolation, and sound effects generation",
            Requires = "ElevenLabs API Key",
            InstallCmd = "pip install elevenlabs",
            EntryPoint = "elevenlabs",
            SkillPath = "skills/elevenlabs/SKILL.md",
            Homepage = null,
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "generate-veo-video",
            DisplayName = "Veo Video Generator",
            Category = "video",
            Description = "High-definition text-to-video and image-to-video generation powered by Google DeepMind Veo",
            Requires = "Gemini / Google AI API Key",
            InstallCmd = "npm install -g @google/veo-cli",
            EntryPoint = "veo-cli",
            SkillPath = "skills/veo/SKILL.md",
            Homepage = null,
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "minimax-cli",
            DisplayName = "MiniMax Video & Audio",
            Category = "ai",
            Description = "High-fidelity video and speech synthesis using MiniMax Hailuo models",
            Requires = "MiniMax API Key",
            InstallCmd = "pip install minimax-cli",
            EntryPoint = "minimax-cli",
            SkillPath = "skills/minimax/SKILL.md",
            Homepage = null,
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
        new()
        {
            Name = "jimeng",
            DisplayName = "Jimeng AI Video Studio",
            Category = "video",
            Description = "AI text-to-video cinematic camera controls, character consistency, and frame interpolation",
            Requires = "Jimeng API Account",
            InstallCmd = "pip install jimeng-cli",
            EntryPoint = "jimeng",
            SkillPath = "skills/jimeng/SKILL.md",
            Homepage = null,
            Version = "1.0.0",
            MatrixTags = new[] { "video-creation" },
            IsInstalled = true
        },
    };

    public static CliHarnessEntry? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Harnesses.FirstOrDefault(h =>
            string.Equals(h.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(h.DisplayName, name.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(h.EntryPoint, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<CliHarnessEntry> FindByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return Harnesses;
        return Harnesses.Where(h => string.Equals(h.Category, category.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static IReadOnlyList<CliHarnessEntry> FindByMatrix(string matrixId)
    {
        if (string.IsNullOrWhiteSpace(matrixId)) return Harnesses;
        return Harnesses.Where(h => h.MatrixTags.Any(m => string.Equals(m, matrixId.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public static IReadOnlyList<CliHarnessEntry> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Harnesses;
        var q = query.Trim().ToLowerInvariant();
        return Harnesses.Where(h =>
            h.Name.ToLowerInvariant().Contains(q) ||
            h.DisplayName.ToLowerInvariant().Contains(q) ||
            h.Category.ToLowerInvariant().Contains(q) ||
            h.Description.ToLowerInvariant().Contains(q) ||
            h.EntryPoint.ToLowerInvariant().Contains(q)).ToList();
    }

    public static IReadOnlyList<string> GetCategories()
    {
        return Harnesses.Select(h => h.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
    }
}