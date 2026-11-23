# SPARK Study Division — Retrieval & Search Architecture
## Grounded Retrieval, Multi-Factor Scoring & RAG Synthesis

### 1. Retrieval Engine Overview

The SPARK Study retrieval engine (`StudySearchService` and `StudyGroundedAnswerSynthesizer`) provides deterministic, grounded search across the learner's indexed document chunks. It guarantees complete multi-tenant user isolation and returns transparent provenance badges for educational answers.

---

### 2. Multi-Factor Scoring Formula

The relevance score for a given document chunk $C$ against a search query $Q$ is computed using a multi-factor ranking model:

$$\text{RelevanceScore}(C, Q) = S_{\text{term}} + S_{\text{phrase}} + S_{\text{section}} + S_{\text{keyword}}$$

Where:
- **Term Frequency Score ($S_{\text{term}}$)**:
  For each query token $t \in Q$, computes occurrence frequency in chunk text:
  $$S_{\text{term}} = \sum_{t \in Q} \min(3, \text{Count}(t, C.\text{Content})) \times 1.0$$
- **Exact Phrase Match Bonus ($S_{\text{phrase}}$)**:
  If the verbatim query phrase appears in the chunk:
  $$S_{\text{phrase}} = 3.0$$
- **Section Heading Bonus ($S_{\text{section}}$)**:
  If any query token matches the chunk's `SectionTitle`:
  $$S_{\text{section}} = 2.0$$
- **Pre-computed Keyword Match Bonus ($S_{\text{keyword}}$)**:
  If a token matches pre-extracted `Chunk.Keywords`:
  $$S_{\text{keyword}} = 1.5$$

Chunks falling below `MinScore` (default `0.05` for broad searches, `0.1` for RAG synthesis) are automatically pruned. Results are ranked descending by `RelevanceScore` and truncated to `MaxResults`.

---

### 3. Context Highlighting & Snippet Generation

When a chunk matches:
1. The search engine locates the first occurrence index of the highest-frequency matching token.
2. A window of up to 160 characters is extracted around the match boundary.
3. Leading and trailing ellipses (`...`) are appended if the snippet is truncated mid-paragraph.
4. Matched terms are tracked in `StudySearchResultItem.MatchedKeywords`.

---

### 4. Grounded RAG Synthesis (`StudyGroundedAnswerSynthesizer`)

The Grounded AI Synthesizer operates under a strict anti-hallucination, fail-transparent contract:

#### Case A: Grounded Matches Found
When search returns relevant passages:
1. Primary explanation is extracted directly from the top-scoring passage.
2. Explicit source badge is attached:
   `📌 *[Source: {DocumentTitle}, Page {PageNumber} § {SectionTitle}]*`
3. If secondary corroborating passages exist, a "Supporting Evidence & Details" section is generated with quotes and page citations.
4. If in `Socratic` mode, a reflective question is attached to test comprehension.

#### Case B: No Relevant Passages Found (Transparent Fallback)
If no user documents contain information on the query:
1. `IsGroundedInSources` is flagged `false`.
2. A transparent disclaimer is prepended:
   - **English**: `*Note: No relevant passages were found in your indexed study documents. The following response is synthesized from general foundational curriculum knowledge:*`
   - **Malayalam**: `ശ്രദ്ധിക്കുക: നിങ്ങളുടെ അപ്‌ലോഡ് ചെയ്ത രേഖകളിൽ ഈ ചോദ്യത്തിനാവശ്യമായ വിവരങ്ങൾ ലഭ്യമായിട്ടില്ല. പൊതുവായ അറിവിന്റെ അടിസ്ഥാനത്തിൽ നൽകുന്ന വിവരണം താഴെ നൽകുന്നു:`
3. The learner is never misled into believing general LLM knowledge originated from their uploaded textbooks.
