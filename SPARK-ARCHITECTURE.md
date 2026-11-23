# SPARK — Master Architecture Specification
**Document Date:** November 23, 2026  
**Architect:** Principal Software Architect & AI Systems Engineer  
**Target:** `C:\Users\PC\Documents\Spark\Spark-main`

---

## 1. System Architecture Overview

SPARK is designed as a unified, native Windows AI workstation combining the responsiveness of .NET 10 / WinUI 3 with local & cloud AI inference, autonomous agent orchestration, computer use automation, and specialized workspace divisions.

```mermaid
graph TD
    UI["WinUI 3 Desktop Shell (Windows App SDK / Mica / 3-Theme)"]
    Title["TitleBar DivisionSwitcher (Chat / Work / Code / Study / Creative / Voice)"]
    UI --> Title
    Title --> D_Chat["Chat Division"]
    Title --> D_Work["Work Division (Autonomous Personal Agent)"]
    Title --> D_Code["Code Division (IDE / Git / Terminal)"]
    Title --> D_Study["Study Division (AI Tutor / Bilingual Socratic)"]
    Title --> D_Creative["Creative Division (16 Studio Apps / GenAI)"]
    Title --> D_Voice["Voice Division (Mark-LVII Blue HUD / Arc Reactor)"]
    Title --> D_Comp["Computer Use Division (UI-TARS Automation)"]

    subgraph CoreServices ["Shared Core Services (C# / .NET 10)"]
        Router["ISparkModelRouter (Ollama / Cloud Fallback)"]
        Memory["ISparkMemoryStore (Persistent Vector & Facts)"]
        Tools["SparkToolRegistry & MCP Manager"]
        Sentinel["SentinelReviewer & SparkApprovalManager"]
        Audit["SparkAuditLogger (Forensic Trail)"]
    end

    D_Chat --> CoreServices
    D_Work --> CoreServices
    D_Code --> CoreServices
    D_Study --> CoreServices
    D_Creative --> CoreServices
    D_Voice --> CoreServices
    D_Comp --> CoreServices

    subgraph ExternalEngines ["Underlying Runtime Engines"]
        Ollama["Local Ollama (Qwen2.5-Coder / Llama3.2 / LLaVA)"]
        PyRuntime["Python IPC Runtime (PyAutoGUI / SoundDevice / UI-TARS)"]
        CloudModels["Cloud AI (Gemini Live / Claude / OpenAI)"]
    end

    Router --> Ollama
    Router --> CloudModels
    D_Comp --> PyRuntime
    D_Voice --> PyRuntime
```

---

## 2. Core Architectural Pillars

### 2.1 Unified Shared Infrastructure
All divisions communicate through centralized, thread-safe contracts:
- **`ISparkModelRouter`:** Capability-based routing across local Ollama instances and external cloud providers with circuit breaker failovers.
- **`ISparkMemoryStore`:** Cross-division SQLite long-term memory for user facts, preferences, and workspace context.
- **`ISentinelReviewer` & `ISparkApprovalManager`:** Security gate requiring human authorization for high-risk actions (purchases, emails, external mutations).
- **`ISparkAuditLogger`:** Tamper-evident forensic audit log capturing every action, tool execution, and prompt.

### 2.2 Scoped HUD Isolation
The high-tech Mark-LVII Blue HUD (`#00060A`, `#00D4FF`) is strictly encapsulated within `VoiceModePage.xaml`. When the user switches to Chat, Work, Code, Study, or Creative, the shell seamlessly returns to the standard Windows 11 Fluent Theme (Light, Night, or System).
