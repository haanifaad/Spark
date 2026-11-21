# SPARK Study Division — REST API Specification
**Phase 2: Core Learning & AI Tutor**
*Document Version: 2.0.0 | Date: November 2026*

---

## 1. Overview & Protocol Conventions

The SPARK Study Division exposes its learning services via a structured, type-safe REST API surface rooted at `/api/v1/study`.

- **Base URI**: `http://localhost:{port}/api/v1/study` (internal IPC / service host) or directly invoked via `IStudyApiService` in-process.
- **Content-Type**: `application/json; charset=utf-8`
- **Identity & Authorization**: Local user context injected via `X-User-Id` header (defaults to `"default"` for single-user desktop).
- **Execution Mode**: Strictly user-initiated (fail-closed). Background autonomous invocation is prohibited.

---

## 2. Standard Envelope & Error Format

All API responses are wrapped in a uniform envelope (`StudyApiResponse<T>`).

### 2.1 Success Envelope
```json
{
  "success": true,
  "data": { ... },
  "error": null,
  "correlationId": "5fa8c2e64b8a4521bc1e1f9d",
  "timestampUtc": "2026-11-21T07:15:00.0000000Z"
}
```

### 2.2 Error Envelope
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "SESSION_NOT_FOUND",
    "message": "Study session '9b1deb4d3b7d4badac5d10ac457004d4' was not found.",
    "details": [
      "Session may have been purged or belongs to another user."
    ]
  },
  "correlationId": "5fa8c2e64b8a4521bc1e1f9d",
  "timestampUtc": "2026-11-21T07:15:00.0000000Z"
}
```

---

## 3. Endpoints Catalog

### 3.1 Health & Diagnostics

#### `GET /api/v1/study/health`
Returns runtime diagnostic health metrics, schema migration state, model router availability, and active session counts.

**Response `200 OK`**:
```json
{
  "success": true,
  "data": {
    "status": "Healthy",
    "schemaVersion": "1.1.0",
    "databaseStatus": "Connected",
    "modelRouterStatus": "Ready",
    "activeSessions": 1
  },
  "error": null,
  "correlationId": "abc123",
  "timestampUtc": "2026-11-21T07:15:00Z"
}
```

---

### 3.2 Learning Sessions

#### `GET /api/v1/study/sessions`
Lists all study sessions for the authenticated user, optionally filtered by `courseId`.

- **Query Parameters**:
  - `courseId` *(optional, string)*: Filter sessions by course ID.

**Response `200 OK`**:
```json
{
  "success": true,
  "data": [
    {
      "sessionId": "9b1deb4d3b7d4badac5d10ac457004d4",
      "userId": "default",
      "courseId": "cs-core-101",
      "subjectId": "sub-dsa-01",
      "lessonId": "les-bst-01",
      "currentTopic": "Binary Search Trees",
      "level": "College",
      "teachingMode": "Socratic",
      "language": "English",
      "status": "Active",
      "startedAtUtc": "2026-11-21T07:00:00Z",
      "lastActiveAtUtc": "2026-11-21T07:14:00Z",
      "resumedAtUtc": null,
      "messages": [ ... ],
      "metadata": {}
    }
  ]
}
```

---

#### `GET /api/v1/study/sessions/{sessionId}`
Retrieves full state, metadata, and complete message transcript for a specific session.

**Response `200 OK`**: Returns `StudySession` object.  
**Response `404 Not Found`**: Returns `SESSION_NOT_FOUND` error.

---

#### `POST /api/v1/study/sessions`
Initializes a new pedagogical study session. Automatically pauses any prior active session for the user.

**Request Body**:
```json
{
  "courseId": "cs-core-101",
  "lessonId": "les-bst-01",
  "title": "BST Tree Traversal & Search",
  "teachingMode": "Standard",
  "language": "English",
  "level": "College",
  "subjectId": "sub-dsa-01",
  "topic": "Binary Search Trees"
}
```

**Response `201 Created`**: Returns the initialized `StudySession` object.

---

#### `POST /api/v1/study/sessions/{sessionId}/resume`
Resumes an inactive or paused study session, making it the active session for the user and recording `resumedAtUtc`.

**Request Body**:
```json
{
  "notes": "Resuming after reviewing lecture slides"
}
```

**Response `200 OK`**: Returns the resumed `StudySession` object with `status = "Active"`.

---

#### `PATCH /api/v1/study/sessions/{sessionId}/preferences`
Dynamically updates session pedagogy, educational tier, language, or active topic without restarting the session.

**Request Body**:
```json
{
  "teachingMode": "Socratic",
  "language": "Malayalam",
  "level": "University",
  "currentTopic": "AVL Tree Rotations"
}
```

**Response `200 OK`**: Returns updated `StudySession` object.

---

#### `POST /api/v1/study/sessions/{sessionId}/messages`
Submits a learner inquiry or answer to the AI Tutor and receives the structured pedagogical response.

**Request Body**:
```json
{
  "content": "Can you explain how BST deletion works?",
  "overrideMode": "StepByStep",
  "overrideLanguage": null
}
```

**Response `200 OK`**:
```json
{
  "success": true,
  "data": {
    "messageId": "f784e56598c44d18898126f4f2ab7d45",
    "sender": "tutor",
    "content": "### Step-by-Step BST Node Deletion\n\nDeleting a node requires handling three mutually exclusive structural cases...",
    "timestampUtc": "2026-11-21T07:15:30Z",
    "teachingMode": "StepByStep",
    "explanationStyle": "StepByStep",
    "language": "English",
    "modelUsed": "gemini-2.5-pro",
    "isFallback": false,
    "metadata": {
      "TriggerDetected": "Mode:StepByStep"
    }
  }
}
```

---

### 3.3 Learning Paths & Objectives

#### `GET /api/v1/study/paths`
Returns curated Universal Learning Paths, optionally filtered by `level`.

- **Query Parameters**:
  - `level` *(optional, string)*: `School`, `HigherSecondary`, `College`, `University`, `Professional`, `Technical`, `Research`.

**Response `200 OK`**:
```json
{
  "success": true,
  "data": [
    {
      "pathId": "path-cs-undergrad-core",
      "courseId": "cs-core-101",
      "title": "Computer Science Foundations",
      "description": "Comprehensive undergraduate curriculum spanning algorithms, systems, and software engineering.",
      "level": "College",
      "targetAudience": "Undergraduate B.Tech / B.Sc Computer Science students",
      "subjectIds": ["sub-dsa-01", "sub-os-01"],
      "milestones": [
        {
          "milestoneId": "m1",
          "title": "Algorithmic Complexity & Basic Structures",
          "subjectId": "sub-dsa-01",
          "lessonIds": ["les-complexity-01", "les-arrays-01"],
          "order": 1,
          "isCompleted": false
        }
      ],
      "estimatedHours": 120
    }
  ]
}
```

---

#### `GET /api/v1/study/paths/{pathId}`
Retrieves a specific learning path by ID.

---

#### `GET /api/v1/study/lessons/{lessonId}/objectives`
Retrieves all Bloom's Taxonomy learning objectives anchored to a given lesson.

**Response `200 OK`**:
```json
{
  "success": true,
  "data": [
    {
      "objectiveId": "obj-bst-01",
      "lessonId": "les-bst-01",
      "statement": "State the Binary Search Tree invariant property.",
      "taxonomyLevel": "Remember",
      "isMastered": true,
      "masteredAtUtc": "2026-11-21T07:10:00Z",
      "bloomOrder": 1
    },
    {
      "objectiveId": "obj-bst-02",
      "lessonId": "les-bst-01",
      "statement": "Implement recursive and iterative node insertion in C#.",
      "taxonomyLevel": "Apply",
      "isMastered": false,
      "masteredAtUtc": null,
      "bloomOrder": 3
    }
  ]
}
```

---

#### `POST /api/v1/study/objectives/{objectiveId}/master`
Updates the mastery status of a learning objective.

- **Query Parameters**:
  - `isMastered` *(boolean, default: true)*

**Response `200 OK`**:
```json
{
  "success": true,
  "data": true
}
```

---

## 4. Error Code Reference

| Error Code | HTTP Status | Description |
| :--- | :--- | :--- |
| `SESSION_NOT_FOUND` | 404 | The requested `sessionId` does not exist in the user's data store. |
| `OBJECTIVE_NOT_FOUND` | 404 | The specified `objectiveId` does not exist. |
| `PATH_NOT_FOUND` | 404 | The specified `pathId` does not exist. |
| `INVALID_REQUEST` | 400 | Missing required fields, invalid enum string, or malformed JSON payload. |
| `UNAUTHORIZED` | 403 | Attempt to access another user's isolated data directory. |
| `DATA_STORE_ERROR` | 500 | Unhandled disk I/O, file lock failure, or migration corruption. |
| `TUTOR_SERVICE_ERROR` | 500 | AI model routing failure when local fallback is also unavailable. |

---

## 5. Client Integration Examples

### 5.1 C# / WinUI Client (`StudyDivisionService`)
```csharp
// 1. Create a session
var response = await studyApi.CreateSessionAsync(new CreateSessionRequest(
    CourseId: "cs-core-101",
    LessonId: "les-bst-01",
    Title: "Binary Search Trees",
    TeachingMode: TeachingMode.Socratic,
    Language: LearningLanguage.English,
    Level: EducationLevel.College
));

// 2. Post a learner message
var reply = await studyApi.AddSessionMessageAsync(
    response.Data.SessionId,
    new PostSessionMessageRequest("How do I maintain tree balance?")
);
```

### 5.2 HTTP / curl
```bash
curl -X POST "http://localhost:5000/api/v1/study/sessions" \
  -H "Content-Type: application/json" \
  -H "X-User-Id: default" \
  -d '{
    "courseId": "cs-core-101",
    "lessonId": "les-bst-01",
    "title": "BST Traversal",
    "teachingMode": "Standard",
    "language": "English",
    "level": "College"
  }'
```
