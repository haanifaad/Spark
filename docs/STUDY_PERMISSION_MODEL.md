# SPARK Study Division — Permission & Security Model

## 1. Security Philosophy: Fail-Closed Protection
The SPARK Study Division enforces a **Fail-Closed Security Architecture**:
- All operations default to denied unless explicitly authorized.
- No background or autonomous tool execution is permitted without passing permission gates.
- Tools operating on code execution or filesystem modification mandate explicit human-in-the-loop approval.

---

## 2. Six Permission Tiers (`StudyPermissionKind`)

| Tier | Identifier | Risk Level | Human Approval Required? | Description | Example Tools |
|---|---|---|---|---|---|
| **Tier 1** | `ReadOnly` | Zero | No | Pure in-memory computation, string transformations, arithmetic | `calculator`, `unit-converter`, `json-validator` |
| **Tier 2** | `FileAccess` | Low | No | Reading student documents from approved directory | `pdf-reader`, `document-text-extractor` |
| **Tier 3** | `ExternalApiAccess`| Moderate | No | External lookup of public bibliographic sources or DOIs | `doi-metadata-lookup`, `source-metadata-reader` |
| **Tier 4** | `FileModification` | High | **Yes** | Writing notes, updating flashcard decks on disk | Custom export tools, workspace writers |
| **Tier 5** | `CodeExecution` | Critical | **Yes** | Running code snippets, compiling code, testing scripts | Sandbox code runners, automated testers |
| **Tier 6** | `DestructiveOperation`| Critical | **Yes (Strict)**| Deleting student history, dropping database tables | Schema resets, cache wipes |

---

## 3. Human-in-the-Loop Approval Workflow

When a tool requires human approval:
1. `StudyPermissionManager.AuthorizeAsync` evaluates the tool definition. If `permission >= CodeExecution` or `FileModification`:
   ```csharp
   return StudyPermissionDecision.RequiresApproval(
       $"Tool '{toolName}' requires manual confirmation because it requests {permission} privileges.");
   ```
2. The execution engine pauses tool invocation and generates a `StudyApprovalInfo` record with state `Pending`.
3. The WinUI interface intercepts `result.RequiresApproval == true` and presents a modal `ContentDialog`:
   - Tool Name and requested action.
   - Exact arguments passed.
   - Permission tier risk badge.
   - **"Approve & Execute"** and **"Cancel"** action buttons.
4. When the user approves:
   - Client calls `ApproveAndExecutePendingAsync(approvalId, userId)`.
   - Tool resumes execution and stamps the audit record with the approver's identity and timestamp.
5. If the user cancels or rejects:
   - Tool execution is aborted immediately.
   - Status transitions to `Failed` with reason `"Execution rejected by learner."`.

---

## 4. Audit Trail & Non-Repudiation
All tool executions and approval decisions are written atomically to disk:
- Stored under `study/users/{userId}/tool_executions.json`.
- Includes `RequestId`, `ApprovalRecord.ApprovedBy`, `ApprovalRecord.DecidedAtUtc`, and `ApprovalRecord.DecisionNote`.
- Tamper-evident atomic write using `.tmp` write and atomic rename ensures data store integrity even across unexpected shutdowns.
