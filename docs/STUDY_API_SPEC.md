# SPARK STUDY DIVISION — API SPECIFICATION
## Phase 1: API Foundation & Conventions

---

## 1. API Architecture Conventions

All Study Division endpoints follow standard RESTful conventions rooted at:
```
/api/v1/study
```

### 1.1 Universal Response Envelope
Every API response returns a strongly-typed `StudyApiResponse<T>` envelope:

```json
{
  "success": true,
  "data": { ... },
  "error": null,
  "correlationId": "8f1a23e9-7c8b-4b8a-9a1f-0e1234567890",
  "timestampUtc": "2026-11-20T13:45:00Z"
}
```

### 1.2 Error Envelope
When an operation fails:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "RESOURCE_NOT_FOUND",
    "message": "The requested course with ID 'cs-101' was not found.",
    "details": []
  },
  "correlationId": "8f1a23e9-7c8b-4b8a-9a1f-0e1234567890",
  "timestampUtc": "2026-11-20T13:45:00Z"
}
```

### 1.3 Pagination Standard
Endpoints returning collections support `pageNumber` and `pageSize` parameters:

```json
{
  "items": [ ... ],
  "pageNumber": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## 2. Phase 1 Endpoint Catalog

### 2.1 Health & Subsystem Status
- **Route**: `GET /api/v1/study/health`
- **Description**: Returns operational health, schema version, and migration status.
- **Response**:
```json
{
  "status": "Healthy",
  "schemaVersion": "1.0.0",
  "databaseStatus": "Connected",
  "modelRouterStatus": "Ready",
  "activeSessions": 0
}
```

### 2.2 Courses Management
- **Route**: `GET /api/v1/study/courses`
  - Query Params: `level` (optional), `pageNumber`, `pageSize`
  - Returns paginated list of courses.
- **Route**: `GET /api/v1/study/courses/{id}`
  - Returns specific course with its subjects, chapters, and lessons.
- **Route**: `POST /api/v1/study/courses`
  - Body:
```json
{
  "title": "Introduction to Machine Learning",
  "description": "Foundational course on linear regression, neural networks, and optimization.",
  "level": "University",
  "subject": "Computer Science"
}
```

### 2.3 Learning Sessions
- **Route**: `GET /api/v1/study/sessions`
  - Returns past and active sessions for the user.
- **Route**: `POST /api/v1/study/sessions`
  - Starts or resumes a study session.
  - Body:
```json
{
  "courseId": "course-123",
  "lessonId": "lesson-456",
  "title": "Linear Algebra Review"
}
```

### 2.4 Progress Tracking
- **Route**: `GET /api/v1/study/progress`
  - Returns overall user progress, mastery breakdown, and completed lesson IDs.
- **Route**: `POST /api/v1/study/progress`
  - Records lesson completion and comprehension score.
  - Body:
```json
{
  "courseId": "course-123",
  "lessonId": "lesson-456",
  "score": 95,
  "mastery": "Proficient"
}
```

---

## 3. Security, Authentication & Request Correlation
1. **User Identity Isolation**: Every request is scoped to an authenticated user (`userId`).
2. **Correlation ID Tracking**: Each request accepts an optional `X-Correlation-ID` header; if omitted, a GUID is generated and echoed back in the response envelope.
3. **Permission Interception**: Any endpoint triggering external file writes or model calls checks permissions against `IStudyPermissionManager`.
