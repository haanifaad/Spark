---
id: customer-persona-profile-slide
name: Customer Persona Profiles and Empathy Map Slides
category: presentations_visualization
application: DeckSpark
application_id: deckspark
version: 1.0.0
purpose: Displaying target demographics, pain points, and quote callouts
---

# Customer Persona Profiles and Empathy Map Slides

## 1. Unique Skill ID
`creative.presentations_visualization.customer-persona-profile-slide`

## 2. Skill Name
**Customer Persona Profiles and Empathy Map Slides**

## 3. Description
A specialized creative engineering skill that instructs the SPARK Creative Agent on professional best practices, algorithmic rules, and production workflows for customer persona profiles and empathy map slides within the DeckSpark ecosystem.

## 4. Purpose
Displaying target demographics, pain points, and quote callouts. Provides the autonomous agent with domain-specific criteria to evaluate, plan, generate, and refine high-caliber creative assets that meet international production standards.

## 5. Professional Knowledge
- **Domain Foundation**: Mastery of customer persona profiles and empathy map slides requires deep technical comprehension of both theoretical aesthetics and software data pipelines.
- **Cognitive and Physical Impact**: Decisions directly influence user perception, aesthetic credibility, brand trust, and ergonomic scannability.
- **Industry Standards**: Follows ISO, W3C, and professional creative guild standards for Presentations and Data Visualization.

## 6. Relevant Design or Engineering Principles
- **Aesthetic Precision**: Ensuring exact proportions, tonal consistency, and optical alignment rather than relying on unguided generative approximations.
- **Structural Integrity**: Maintaining clean document trees, non-destructive layer hierarchies, and mathematical curve continuity.
- **Accessibility & Universal Ergonomics**: Complying with contrast criteria, visual wayfinding clarity, and platform-specific guidelines.
- **Resource Efficiency**: Producing lightweight, reusable components with minimal redundant geometry or bloat.

## 7. Step-by-Step Workflow
1. **Context Analysis**: Inspect the active project document, canvas bounds, current selection, and existing asset metadata in DeckSpark.
2. **Strategy Formulation**: Define the target objective, establishing key operational parameters, color palettes, and structural geometry.
3. **Execution Plan**: Deconstruct the request into discrete, atomic tool operations adhering to safety tier limits.
4. **Draft Generation**: Execute primary creative operations (e.g. creating base paths, adjustment stacks, keyframes, or nodes).
5. **Micro-Refinement**: Apply detailed tuning—optical spacing, curve smoothing, contrast balancing, or frequency filtering.
6. **Integrity Audit**: Verify structural conformance against professional benchmark criteria.
7. **Commit & Render**: Package output for viewport preview or commit modifications to project storage.

## 8. Required Inputs
- `projectId`: Unique identifier of the target active project in DeckSpark.
- `documentState`: Current serialization or tree representation of the active canvas or timeline.
- `parameters`: JSON dictionary specifying operation targets (e.g. coordinates, dimensions, color tokens, curve data).
- `selectionContext`: Identifiers of currently selected layers, nodes, shapes, or timecode ranges.

## 9. Expected Outputs
- `modifiedDocument`: Updated project state with validated changes applied.
- `auditReport`: Formal report detailing modifications, execution metrics, and quality scores.
- `previewArtifact`: Rendered raster PNG, vector SVG, or audio sample for user inspection.
- `undoToken`: Reversible transaction receipt enabling instant rollback.

## 10. Compatible Applications
- Primary: **DeckSpark** (`deckspark`)
- Secondary / Exchange: SPARK Creativity Studio Suite, DesignSpark, PDFSpark

## 11. Compatible Tools
- `creative.agent.orchestrator`
- `deckspark.document.inspect`
- `deckspark.element.create`
- `deckspark.element.modify`
- `deckspark.export.render`

## 12. CLI Mappings
```bash
spark creativity app inspect deckspark
spark creativity skills run creative.presentations_visualization.customer-persona-profile-slide --app deckspark --params '{"target": "active_layer"}'
deckcraft-cli run --cmd 'customer-persona-profile-slide.apply' --params '{"quality": "production"}'
```

## 13. MCP Mappings
- **Server**: `spark-creativity-deckspark-mcp`
- **Tool**: `creative_deckspark_customer_persona_profile_slide`
- **Resource**: `creative://deckspark/projects/active/customer-persona-profile-slide`

## 14. JSON Operation Mappings
```json
{
  "version": "1.0",
  "requestId": "req-customer-persona-profile-slide-001",
  "application": "deckspark",
  "operation": "presentations_visualization.customer_persona_profile_slide",
  "arguments": {
    "mode": "professional",
    "strict": true
  },
  "context": {
    "activeSession": true
  }
}
```

## 15. Model Requirements
- Recommended Model: High-Reasoning Vision/Creative LLM (Gemini 2.5 Pro / Claude 3.7 Sonnet / GPT-4o)
- Minimum Fallback: Local Open-Weight Creative Model (Qwen-2.5-Coder / Llama-3.3 70B via Ollama)
- Vision Capability Required: Yes (when evaluating visual spatial rendering and raster/vector composition)

## 16. Quality Criteria
- Objective evaluation metric threshold > 95% conformance.
- Zero non-manifold geometry, orphaned nodes, or clipped dynamic ranges.
- Precise alignment with existing brand tokens and style guidelines.
- Preserved non-destructive layers allowing manual human refinement.

## 17. Common Mistakes
- Over-complicating node graphs or layer stacks when simple solutions suffice.
- Failing to inspect existing project scale and DPI before introducing new assets.
- Applying destructive filters instead of reversible smart adjustment layers.
- Neglecting optical balance in favor of raw mathematical centering.

## 18. Validation Procedure
1. Execute automated schema validation on generated parameter payloads.
2. Run headless render check via `deckcraft-cli` to verify memory allocation and geometry soundness.
3. Compare output histogram / vector node counts against baseline threshold limits.
4. Present side-by-side before/after preview in SPARK Creative Agent panel for final approval.

## 19. Practical Examples
- **Scenario A**: High-priority commercial asset requiring rapid styling and mathematical precision.
- **Scenario B**: Automated cleanup and standardization of legacy imported files into modern SPARK formats.
- **Scenario C**: Multi-application asset handoff pipeline incorporating cross-domain transformation.

## 20. Limitations
- Direct hardware GPU acceleration depends on host driver compatibility.
- Extremely large multi-gigabyte project files require chunked streaming execution.
- Complex third-party proprietary plugin effects must be rasterized or substituted with native shaders.
