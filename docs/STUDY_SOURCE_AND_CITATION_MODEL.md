# SPARK Study Division — Source & Citation Model
## Academic Citation Engines, Provenance Tracking & Standards Compliance

### 1. Citation Formatting Engine

The SPARK Study citation engine supports automatic formatting across five premier academic standards: APA, MLA, Chicago, IEEE, and Harvard.

Formatting rules are implemented natively in `StudyResearchService.FormatCitationAsync`:

| Citation Style | Format Template | Example Output |
| :--- | :--- | :--- |
| **APA** (7th ed.) | `Author (Year). Title. URL/DOI` | *Abelson, H. & Sussman, G. J. (1996). Structure and Interpretation of Computer Programs.* |
| **MLA** (9th ed.) | `Author. "Title." (Year). URL/DOI` | *Norman, Donald A. "The Design of Everyday Things." (2013).* |
| **IEEE** | `[1] Author, "Title," Year. URL/DOI` | *[1] Shannon, C. E., "A Mathematical Theory of Communication," 1948.* |
| **Chicago** (17th ed.)| `Author. Year. Title. URL/DOI` | *Kuhn, Thomas S. 1962. The Structure of Scientific Revolutions.* |
| **Harvard** | `Author (Year) 'Title'. Available at: URL/DOI` | *Russell, S. & Norvig, P. (2020) 'Artificial Intelligence: A Modern Approach'.* |

---

### 2. Provenance Architecture

To prevent academic plagiarism and hallucination, every piece of evidence in the system is tied to an explicit provenance chain:

```
[Physical Document / PDF / Notes File]
        │
        ▼ (Ingestion & Checksum)
[StudyDocument (Sha256Checksum, FilePath, PageCount)]
        │
        ▼ (Semantic Segmentation)
[DocumentChunk (DocumentId, PageNumber, SectionTitle, ChunkIndex)]
        │
        ▼ (Empirical Verification)
[ResearchEvidence (DocumentId, PageNumber, Excerpt, Analysis)]
        │
        ▼ (Synthesis)
[ResearchReport & GroundedAnswerResult (Source Badges & Formatted Citations)]
```

Every AI-synthesized answer references this chain so that students can independently verify claims against their course literature.
