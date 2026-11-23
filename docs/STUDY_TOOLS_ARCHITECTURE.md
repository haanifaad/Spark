# SPARK Study Division — Tools Architecture

## 1. Overview
The **SPARK Study Division Tools Architecture** provides a sandboxed, observable, and permission-governed tool execution engine. Tools are deterministic handlers that execute mathematical calculations, document analyses, code syntax verification, quiz synthesis, and citation formatting.

All 21 tools feature **production-grade, non-mock implementations** using standard libraries, safe string parsers, and regex evaluators.

---

## 2. Tool Execution Lifecycle
Every tool execution transitions strictly through six formal states:

```
  [Requested]
       │
       ▼
  [Validating] ───────────────► [Failed] (Schema/Param Error)
       │
       ▼
 [PermissionCheck] ───────────► [Failed] (Unauthorized)
       │
       ├─ (Requires Approval) ─► [AwaitingApproval] ──► [Approved] ──┐
       │                                            └──► [Rejected]   │
       ▼                                                              │
  [Executing] ◄───────────────────────────────────────────────────────┘
       │
       ├─ (Timeout / Cancelled) ─► [Failed / Cancelled]
       │
       ▼
  [Completed] (Result Cached & Persisted to Audit Store)
```

1. **Requested**: Client or pipeline initiates tool execution.
2. **Validating**: Required input parameters are verified against schema.
3. **PermissionCheck**: `IStudyPermissionManager` checks required permission tier.
4. **AwaitingApproval**: High-privilege tools (e.g. `CodeExecution`, `FileModification`) pause for explicit user confirmation.
5. **Executing**: `IStudyToolHandler.InvokeAsync` runs within a bounded timeout.
6. **Completed / Failed**: Result payload and latency are recorded in atomic JSON storage.

---

## 3. Inventory of Built-in Tools (21 Typed Tools)

### 3.1 Mathematics Tools (6)
| Tool ID | Name | Description | Key Inputs | Outputs |
|---|---|---|---|---|
| `calculator` | Calculator | Basic arithmetic evaluator (`+`, `-`, `*`, `/`, `^`, `%`) | `expression` (string) | `result` (number) |
| `scientific-calculator` | Scientific Calculator | Trigonometric (`sin`, `cos`, `tan`), logarithmic (`log`, `ln`), square roots | `operation`, `value`, `value2` | `result` (number) |
| `unit-converter` | Unit Converter | Converts length (`km`, `m`, `mi`), mass (`kg`, `lb`), temperature (`C`, `F`, `K`), time, data | `value`, `from`, `to` | `convertedValue` (number) |
| `equation-evaluator` | Equation Evaluator | Solves linear (`ax+b=0`) and quadratic (`ax^2+bx+c=0`) equations | `equation` (string) | `roots` (array), `solution` |
| `statistics-calculator` | Statistics Calculator | Computes mean, median, mode, variance, sample standard deviation, min, max | `data` (string or array) | `mean`, `median`, `stdDev` |
| `matrix-operations` | Matrix Operations | Matrix addition, multiplication, scalar multiplication, transposition, determinants | `operation`, `matrixA`, `matrixB` | `result` (matrix/scalar) |

### 3.2 Document Tools (4)
| Tool ID | Name | Description | Key Inputs | Outputs |
|---|---|---|---|---|
| `pdf-reader` | PDF Text Reader | Extracts page text, metadata, and chapter boundaries | `filePath`, `maxPages` | `pages` (array), `pageCount` |
| `document-text-extractor`| Document Text Extractor | Extracts plain text from `.txt`, `.md`, `.json`, `.csv` | `content` or `filePath` | `text`, `characterCount` |
| `text-search` | In-Document Text Search | Fast regex and keyword search with line numbers and snippets | `text`, `pattern`, `caseSensitive` | `matches` (array), `matchCount` |
| `document-metadata-reader`| Document Metadata Reader | File size, format detection, line count, word count | `filePath` or `fileName` | `metadata` (object) |

### 3.3 Programming Tools (5)
| Tool ID | Name | Description | Key Inputs | Outputs |
|---|---|---|---|---|
| `code-formatter` | Code Formatter | Indents and formats C#, Python, JS/TS, Java, and SQL | `code`, `language`, `indentSize` | `formattedCode` (string) |
| `code-analyzer` | Code Analyzer | Bracket balancing, cyclomatic complexity estimation, metrics | `code`, `language` | `complexity`, `syntaxIssues` |
| `json-validator` | JSON Validator | Diagnostic JSON parser reporting exact error lines and byte offsets | `json` (string) | `isValid`, `elementCount`, `errors` |
| `html-preview` | HTML Preview & Sanitizer| Neutralizes `<script>` and `<iframe>` while structuring safe preview | `html` (string) | `sanitizedHtml`, `hasDangerousTags` |
| `sql-query-validator` | SQL Query Validator | Statement classifier (`SELECT`, `INSERT`, `DROP`), destructive detection | `sql` (string) | `statementType`, `isDestructive` |

### 3.4 Learning Tools (4)
| Tool ID | Name | Description | Key Inputs | Outputs |
|---|---|---|---|---|
| `flashcard-generator` | Flashcard Generator | Extracts front/back flashcard cards from concepts and colon-delimited text | `topic`, `content`, `count` | `flashcards` (array of cards) |
| `quiz-generator` | Quiz Generator | Generates multiple-choice and conceptual assessment questions | `topic`, `questions`, `difficulty` | `quiz` (questions with options) |
| `study-progress-reader`| Study Progress Reader | Aggregates mastery score, study streaks, and completed lessons | `userId` | `masteryScore`, `completedLessons` |
| `learning-objective-reader`| Learning Objective Reader | Reads Bloom's cognitive taxonomy goals for the active curriculum | `subject`, `gradeLevel` | `objectives` (array) |

### 3.5 Research Tools (2)
| Tool ID | Name | Description | Key Inputs | Outputs |
|---|---|---|---|---|
| `source-metadata-reader`| Source Metadata Reader | Parses web and bibliographic metadata from URLs and text citations | `source`, `author`, `year` | `domain`, `title`, `author` |
| `citation-formatter` | Citation Formatter | Generates formatted academic citations in APA 7, MLA 9, Chicago, IEEE | `title`, `author`, `year`, `style` | `citation` (string) |

---

## 4. Timeout & Cancellation Enforcement
Every tool execution is bounded by `StudyToolDefinition.TimeoutMs` (default 5,000 ms, configurable per tool). The execution engine creates a linked `CancellationTokenSource`:

```csharp
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(definition.TimeoutMs));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
```

If the handler exceeds its timeout:
- The running task is immediately aborted.
- The status transitions to `ToolExecutionStatus.Failed`.
- The audit record logs: `"Tool '{id}' timed out after {timeout} ms."`.

Additionally, in-flight operations can be cancelled asynchronously via `_toolRegistry.CancelExecutionAsync(requestId)`.

---

## 5. Audit Logging & Persistence
Every execution outcome is persisted into user-isolated storage (`study/users/{userId}/tool_executions.json`) via `StudyDataStore.SaveToolExecutionAsync`.
The audit record captures:
- `RequestId`, `ToolId`, `UserId`
- `Status` (`Completed`, `Failed`, `AwaitingApproval`)
- `DurationMs` (high-precision stopwatch timing)
- `ApprovalRecord` (timestamp, authorized user, notes)
- Error message (if failed).
