# SPARK STUDY DIVISION — SECURITY SPECIFICATION
## Phase 1: Security Boundaries, Execution Gates & Privacy Controls

---

## 1. Security Philosophy & Threat Model

Educational software handles user homework, academic notes, proprietary coursework, and code execution exercises. The SPARK Study Division enforces four non-negotiable security rules:

1. **Manual Execution by Default**: No autonomous background scripts, file modifications, or external network requests may execute without explicit user approval.
2. **Fail-Closed Permissions**: If a permission check fails, times out, or encounters missing configuration, access is strictly denied.
3. **Data Boundary Isolation**: User coursework and study sessions are strictly isolated per user directory.
4. **Extension Containment**: Enabling a third-party study extension or tool does NOT grant execution permissions automatically; runtime authorization is evaluated on every invocation.

---

## 2. Permission Tiers

| Permission Level | Description | Default Policy | Approval Required |
| :--- | :--- | :--- | :--- |
| `ReadOnly` | Reading courses, lessons, notes, and local learning assets. | Allowed | No |
| `FileAccess` | Loading local PDFs, text files, or homework documents. | Prompt on new folder | Yes (Folder-level) |
| `ExternalApiAccess` | Querying external search engines, Wikipedia, or educational APIs. | Blocked unless enabled | Yes |
| `FileModification` | Saving notes, exporting study plans, or altering project files. | Prompt on write | Yes |
| `CodeExecution` | Running learner Python, C#, or JavaScript code in sandbox. | Prompt with code review | Yes (Always) |
| `DestructiveOperation`| Deleting courses, clearing session history, resetting progress. | Explicit confirmation | Yes (Always) |

---

## 3. Integration with SPARK Security Infrastructure

The Study Division directly leverages SPARK's existing security components:
- `ISparkApprovalManager` (`OpenClaw.Shared.Security.Approvals`):
  - Prompts user with structured operation details before granting one-time or session approvals.
- `ISparkAuditLogger` (`OpenClaw.Shared.Security.Audit`):
  - Immutably records every security event, granted approval, and rejected operation.
- `Mxc Sandbox` (`Microsoft.Mxc.Sdk`):
  - Future code evaluation tasks will execute inside the existing SPARK MXC process sandbox with restricted process tokens and closed network pipes.
