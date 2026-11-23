# SPARK STUDY DIVISION — DATABASE SPECIFICATION
## Phase 1: Data Model, Storage Engine & Migration Strategy

---

## 1. Storage Architecture & Philosophy

In accordance with SPARK's architecture conventions:
1. **Zero External Server Dependency**: Desktop users must not be required to configure, maintain, or troubleshoot an external database server (e.g., PostgreSQL, MySQL, Redis).
2. **ACID Atomic Persistence**: File writes follow write-to-temporary (`.tmp`) followed by atomic rename/replace to prevent corruption on crash or sudden shutdown.
3. **Multi-User Isolation**: Data partitions reside in user-scoped folders under `%APPDATA%\Spark\Study\{userId}\`.
4. **Versioned Migrations**: Schema evolution is strictly tracked via a migration log (`schema_version.json`), ensuring backward and forward compatibility.

---

## 2. Entity Relational Design

The Study Division data model captures hierarchical education structures while providing extension hooks for future phases:

```
[ EducationLevel ]
       |
       v
 [ StudyCourse ] 1 --- * [ StudySubject ]
       |
       +-------------- 1 --- * [ StudyChapter ]
                                     |
                                     +-- 1 --- * [ StudyLesson ]
                                                       |
 [ UserProgress ] * <----------------------------------+
        |
        +-- * [ StudySession ] (Learning Encounter)
```

### Entity Schemas (Phase 1):

#### `StudyCourse`
- `Id` (string, Primary Key, GUID or slug)
- `UserId` (string, Owner)
- `Title` (string, required)
- `Description` (string)
- `Level` (enum: `School`, `College`, `University`, `Professional`, `Technical`, `Research`)
- `Subjects` (List<StudySubject>)
- `CreatedAtUtc` (DateTime)
- `UpdatedAtUtc` (DateTime)

#### `StudySubject`
- `Id` (string, Primary Key)
- `CourseId` (string, Foreign Key)
- `Name` (string)
- `Description` (string)
- `Chapters` (List<StudyChapter>)

#### `StudyChapter`
- `Id` (string, Primary Key)
- `SubjectId` (string, Foreign Key)
- `Title` (string)
- `Order` (int)
- `Lessons` (List<StudyLesson>)

#### `StudyLesson`
- `Id` (string, Primary Key)
- `ChapterId` (string, Foreign Key)
- `Title` (string)
- `Summary` (string)
- `Type` (enum: `Concept`, `Practice`, `Reading`, `Quiz`, `Lab`)
- `EstimatedMinutes` (int)
- `Order` (int)

#### `StudySession`
- `Id` (string, Primary Key)
- `UserId` (string)
- `CourseId` (string)
- `LessonId` (string?)
- `StartedAtUtc` (DateTime)
- `EndedAtUtc` (DateTime?)
- `Status` (enum: `Active`, `Paused`, `Completed`)
- `Transcript` (List<StudySessionMessage>)
- `Notes` (string)

#### `StudyProgress`
- `UserId` (string, Primary Key)
- `CourseId` (string, Primary Key)
- `CompletedLessonIds` (HashSet<string>)
- `MasteryTier` (enum: `Novice`, `Competent`, `Proficient`, `Master`)
- `OverallScore` (double)
- `LastStudiedUtc` (DateTime)
- `StreakDays` (int)

---

## 3. Migration Framework & Version 1.0.0

The migration engine `StudyDatabaseMigrations` applies versioned scripts sequentially:

| Version | Identifier | Description |
| :--- | :--- | :--- |
| `1.0.0` | `InitialStudySchema` | Establishes `courses.json`, `sessions.json`, `progress.json`, and default sample course catalogs. |

### Migration Verification:
1. Validates integrity of schema directory before running.
2. Applies migration within an isolated file transaction.
3. Records migration entry with hash and timestamp in `schema_version.json`.
4. Idempotent: running multiple times does not duplicate or overwrite existing data.
