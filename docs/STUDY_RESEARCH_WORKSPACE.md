# SPARK Study Division — Research Workspace
## Research Projects, Inquiry Tracking, Evidence Linking & Report Synthesis

### 1. Architectural Scope

The Research Workspace (`StudyResearchService`) empowers students and researchers to organize academic inquiries, link empirical text excerpts directly to page numbers, maintain structured working notes, format citations across 5 standard styles, and compile comprehensive markdown research synthesis reports.

---

### 2. Core Entities & Lifecycle

#### 2.1 ResearchProject
Root container for academic inquiry:
- Status lifecycle: `Active` -> `Completed` -> `Archived`.
- Holds references to linked questions, notes, source documents, and empirical evidence.
- Captures overall project hypothesis (e.g., "Spacetime ripples can be detected via interferometer strain").

#### 2.2 ResearchQuestion
Targeted inquiry under a project:
- Status lifecycle:
  - `Open`: Initial question formulated.
  - `Investigating`: Evidence is actively being accumulated.
  - `Answered`: Synthesized answer and findings documented.
- Tracks `EvidenceCount` and structured `Findings`.

#### 2.3 ResearchNote
Conceptual thinking and working notes:
- Tagged with academic topic tags (e.g., `["CRISPR", "Specificity"]`).
- Optional association with a specific study document.
- Fully searchable and compiled into final synthesis reports.

#### 2.4 ResearchEvidence
Empirical proof linked directly to source documents:
- Links `DocumentId`, `DocumentTitle`, and exact `PageNumber`.
- Records raw verbatim `Excerpt`.
- Assigns `CredibilityRating` ("Primary Source", "Peer-Reviewed Journal", "Pre-print", "Lecture Slides").
- Stores student's critical `Analysis` and interpretation.

---

### 3. Synthesis Report Generation (`GenerateReportAsync`)

The report compiler generates a formal academic research synthesis in GitHub-Flavored Markdown:

```markdown
# Research Synthesis Report: {Project.Title}
**Generated:** {Date} | **Author/Learner:** {UserId}

## Executive Summary
{ExecutiveSummary}

## Research Questions & Findings
- **Question:** {QuestionText} [{Status}]
  *Findings:* {Findings}

## Empirical Evidence
> "{Excerpt}"
  — Source: *{DocumentTitle}*, Page {PageNumber} ({CredibilityRating})
  *Analysis:* {Analysis}

## Synthesized Research Notes
### {Note.Title}
{Note.Content}

## References & Citations
1. {FormattedCitation}
2. {FormattedCitation}
```
