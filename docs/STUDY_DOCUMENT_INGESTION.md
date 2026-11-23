# SPARK Study Division — Document Ingestion Pipeline
## Parsing, Text Extraction, Format Whitelist & Error Handling

### 1. Ingestion Pipeline Architecture

The document ingestion engine (`StudyDocumentIngestionService`) transforms unstructured study documents into structured, indexed chunks while retaining exact page numbers and sectional provenance.

```
Input File / Stream
        │
        ▼
[Format Detection & Whitelist Validation]
        │
        ▼
[SHA256 Cryptographic Checksum] ──(Match Found)──► Return Existing Record
        │ (New / Modified)
        ▼
[Job Registration: DocumentProcessingJob (Pending)]
        │
        ▼
[Text Extraction Engine]
   ├─ .txt / .md / .markdown ──► Direct UTF-8 Read & Page Delimiter Parsing
   ├─ .html / .htm           ──► Regex Tag Stripping & Entity Normalization
   ├─ .json / .csv           ──► Key-Value / Row Normalization
   └─ .pdf / .docx / .epub   ──► Printable Run Tokenizer & OCR Readiness Hook
        │
        ▼
[Pagination & Section Tagging]
        │
        ▼
[Keyword Extraction & Semantic Chunking]
        │
        ▼
[Atomic Persistence & Job Status: Indexed]
```

---

### 2. Supported Formats Whitelist

| Extension | Category | Parsing Strategy | Pagination Model |
| :--- | :--- | :--- | :--- |
| `.txt` | Plain Text | UTF-8 Stream | Form-feed `\f` or 45-line blocks |
| `.md`, `.markdown` | Markdown | Heading extraction (`#`, `##`, `###`) | Explicit `---page---` or 45-line blocks |
| `.html`, `.htm` | Web/Markup | Regex tag removal & whitespace collapse | 45-line blocks |
| `.json` | Structured Data | Key-value string normalization | Hierarchical record blocks |
| `.csv` | Tabular Data | Row & column tokenization | 45-row blocks |
| `.pdf` | Portable Document | Printable run tokenizer (OCR-ready hook) | Physical page boundaries |
| `.docx` | Word Document | XML paragraph text stream | Section / break blocks |
| `.epub` | Electronic Book | Chapter & manifest run extraction | Chapter / page blocks |

---

### 3. Error Handling & Fail-Safe Architecture

1. **Missing Files**:
   - Throws explicit `FileNotFoundException` if path does not exist on disk.
   - Preserves fail-closed state without creating orphan database records.

2. **Corrupted or Unreadable Data**:
   - `DocumentProcessingJob` status set to `Failed` with full exception message captured in `Details`.
   - `StudyDocument.ErrorMessage` populated; document marked `Status = Failed`.
   - Chunks are never partially committed; rollback occurs via atomic file rewrite.

3. **In-Memory Ephemeral Storage**:
   - Ingestions from text streams store raw payload in `doc.Metadata["RawContent"]`.
   - Enables `ReprocessDocumentAsync` to rebuild chunks even without a physical file on disk.

4. **OCR Readiness**:
   - Extractor interface is modularized to allow swapping in native Windows OCR (`Windows.Media.Ocr.OcrEngine`) when scanned image PDFs or handwritten lecture notes are detected.
