# SPARK Study Division — Slash Command System

## 1. Syntax Grammar & Parser Architecture
The **SPARK Slash Command System** provides keyboard-driven interactions for power users, students, and educators. Any input beginning with `/` is routed to `StudyCommandParser`.

### Formal Grammar (EBNF)
```ebnf
CommandInvocation ::= '/' CommandIdentifier [ Whitespace ( ArgumentList | PositionalTail ) ]
CommandIdentifier ::= [a-zA-Z0-9_-]+
ArgumentList      ::= Argument ( Whitespace Argument )*
Argument          ::= NamedArgument | FlagArgument | PositionalValue
NamedArgument     ::= Key '=' ( QuotedString | BareWord )
FlagArgument      ::= '--' Key | '-' Key
QuotedString      ::= '"' [^"]* '"' | "'" [^']* "'"
BareWord          ::= [^ \t\r\n=]+
```

### Parsing Rules
1. **Command Extraction**: Strips leading `/` and normalizes the command name to lowercase.
2. **Alias Resolution**: Automatically maps short aliases to canonical names (e.g. `/h` -> `/help`, `/calc` -> `/solve`, `/q` -> `/quiz`).
3. **Quoted Value Preservation**: Handles multi-word values within double or single quotes (e.g., `topic="Binary Search Trees"`).
4. **Positional Arguments**: If arguments do not follow `key=value` format, values are assigned sequentially to the command's defined parameters.

---

## 2. Complete Inventory of Built-in Commands (25 Commands)

### 2.1 Learning Commands (5)
| Command | Aliases | Description | Required Args | Optional Args |
|---|---|---|---|---|
| `/explain` | `/exp` | Explain any concept with deep clarity and analogies | `topic` | `level`, `depth` |
| `/solve` | `/calc`, `/solution` | Solve a math or logic exercise with step-by-step reasoning | `problem` | `stepByStep` |
| `/quiz` | `/q`, `/test` | Generate interactive practice questions on a subject | `subject` | `count`, `difficulty` |
| `/revise` | `/rev` | Review and consolidate core ideas on a topic | `topic` | - |
| `/translate` | `/tr` | Translate or adapt educational content bilingually | `text` | `targetLanguage` |

### 2.2 Academic Commands (5)
| Command | Aliases | Description | Required Args | Optional Args |
|---|---|---|---|---|
| `/notes` | `/note` | Synthesize structured study notes or Cornell outlines | `topic` | `format` |
| `/chapter` | `/chap` | Break down a textbook chapter or syllabus unit | `chapter` | `subject` |
| `/evaluate` | `/eval` | Evaluate an answer or essay against rubric criteria | `answer` | `question`, `rubric` |
| `/essay` | - | Scaffold, outline, or review an academic essay | `prompt` | `wordCount` |
| `/exam` | `/prep` | Generate a targeted exam preparation study plan | `exam` | `daysLeft` |

### 2.3 Research Commands (5)
| Command | Aliases | Description | Required Args | Optional Args |
|---|---|---|---|---|
| `/research` | `/res` | Scope and outline an academic research topic | `topic` | `methodology` |
| `/paper` | - | Analyze research paper structure and findings | `paperTitle` | `doi` |
| `/cite` | `/citation` | Format bibliographic citations in APA, MLA, Chicago, IEEE | `title`, `author` | `year`, `style` |
| `/factcheck` | `/verify` | Verify factual accuracy of an academic claim | `claim` | `domain` |
| `/literature` | `/lit` | Summarize academic literature on a topic | `topic` | `maxPapers` |

### 2.4 Productivity Commands (5)
| Command | Aliases | Description | Required Args | Optional Args |
|---|---|---|---|---|
| `/flashcards` | `/cards`, `/fc`| Generate printable/reviewable study flashcard deck | `topic` | `count` |
| `/plan` | `/schedule` | Generate a personalized study schedule and milestones | `goal` | `timeframe` |
| `/mindmap` | `/tree` | Generate hierarchical concept structure for visualization | `topic` | `depth` |
| `/summary` | `/sum` | Condense long learning materials into key takeaways | `content` | `maxPoints` |
| `/code` | `/prog` | Scaffold or review code for a technical assignment | `task` | `language` |

### 2.5 System Commands (5)
| Command | Aliases | Description | Required Args | Optional Args |
|---|---|---|---|---|
| `/help` | `/?`, `/h` | Display available commands, syntax, and categories | - | `category` |
| `/skills` | `/skill` | Explore, search, or toggle AI teaching skills | - | `query`, `category` |
| `/tools` | `/tool` | Inspect available educational tools and diagnostics | - | `category` |
| `/history` | `/log` | Review recent command, skill, and tool executions | - | `limit` |
| `/clear` | `/cls` | Clear current chat context or restart session | - | - |

---

## 3. Autocomplete Engine
The `StudyCommandParser.GetAutocompleteSuggestions` engine provides instant command discovery:

- **Trigger**: Activated when prompt text starts with `/`.
- **Filtering**: Filters command names, aliases, and descriptions in real time.
- **Prefix Matching**: If user types `/ex`, returns `/explain` and `/exam`.
- **Parameter Hints**: Displays usage format (e.g., `/explain topic="string" [level=string]`).
- **Category Tagging**: Groups suggestions by `Learning`, `Academic`, `Research`, `Productivity`, and `System`.

---

## 4. Validation & Error Handling
If a command lacks required parameters or has invalid syntax, the parser produces structured validation errors without executing the underlying tools:

```
Command: /explain
Status: Invalid
ValidationErrors: ["Missing required argument 'topic'. Usage: /explain topic=\"<Topic>\" [level=beginner|intermediate|advanced]"]
```
