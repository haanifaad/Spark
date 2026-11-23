---
id: slow-in-slow-out-easing-curves
name: Slow In and Slow Out: Cubic Bézier Easing Curves
category: animation_2d
application: AniSpark
application_id: anispark
version: 1.0.0
purpose: Sculpting velocity graphs for natural organic acceleration
---

# Slow In and Slow Out: Cubic Bézier Easing Curves

## 1. Unique Skill ID
`creative.animation_2d.slow-in-slow-out-easing-curves`

## 2. Skill Name
**Slow In and Slow Out: Cubic Bézier Easing Curves**

## 3. Description
A specialized creative engineering skill that instructs the SPARK Creative Agent on professional best practices, algorithmic rules, and production workflows for slow in and slow out: cubic bézier easing curves within the AniSpark ecosystem.

## 4. Purpose
Sculpting velocity graphs for natural organic acceleration. Provides the autonomous agent with domain-specific criteria to evaluate, plan, generate, and refine high-caliber creative assets that meet international production standards.

## 5. Professional Knowledge
- **Domain Foundation**: Mastery of slow in and slow out: cubic bézier easing curves requires deep technical comprehension of both theoretical aesthetics and software data pipelines.
- **Cognitive and Physical Impact**: Decisions directly influence user perception, aesthetic credibility, brand trust, and ergonomic scannability.
- **Industry Standards**: Follows ISO, W3C, and professional creative guild standards for 2D Animation.

## 6. Relevant Design or Engineering Principles
- **Aesthetic Precision**: Ensuring exact proportions, tonal consistency, and optical alignment rather than relying on unguided generative approximations.
- **Structural Integrity**: Maintaining clean document trees, non-destructive layer hierarchies, and mathematical curve continuity.
- **Accessibility & Universal Ergonomics**: Complying with contrast criteria, visual wayfinding clarity, and platform-specific guidelines.
- **Resource Efficiency**: Producing lightweight, reusable components with minimal redundant geometry or bloat.

## 7. Step-by-Step Workflow
1. **Context Analysis**: Inspect the active project document, canvas bounds, current selection, and existing asset metadata in AniSpark.
2. **Strategy Formulation**: Define the target objective, establishing key operational parameters, color palettes, and structural geometry.
3. **Execution Plan**: Deconstruct the request into discrete, atomic tool operations adhering to safety tier limits.
4. **Draft Generation**: Execute primary creative operations (e.g. creating base paths, adjustment stacks, keyframes, or nodes).
5. **Micro-Refinement**: Apply detailed tuning—optical spacing, curve smoothing, contrast balancing, or frequency filtering.
6. **Integrity Audit**: Verify structural conformance against professional benchmark criteria.
7. **Commit & Render**: Package output for viewport preview or commit modifications to project storage.

## 8. Required Inputs
- `projectId`: Unique identifier of the target active project in AniSpark.
- `documentState`: Current serialization or tree representation of the active canvas or timeline.
- `parameters`: JSON dictionary specifying operation targets (e.g. coordinates, dimensions, color tokens, curve data).
- `selectionContext`: Identifiers of currently selected layers, nodes, shapes, or timecode ranges.

## 9. Expected Outputs
- `modifiedDocument`: Updated project state with validated changes applied.
- `auditReport`: Formal report detailing modifications, execution metrics, and quality scores.
- `previewArtifact`: Rendered raster PNG, vector SVG, or audio sample for user inspection.
- `undoToken`: Reversible transaction receipt enabling instant rollback.

## 10. Compatible Applications
- Primary: **AniSpark** (`anispark`)
- Secondary / Exchange: SPARK Creativity Studio Suite, DesignSpark, PDFSpark

## 11. Compatible Tools
- `creative.agent.orchestrator`
- `anispark.document.inspect`
- `anispark.element.create`
- `anispark.element.modify`
- `anispark.export.render`

## 12. CLI Mappings
```bash
spark creativity app inspect anispark
spark creativity skills run creative.animation_2d.slow-in-slow-out-easing-curves --app anispark --params '{"target": "active_layer"}'
anicraft run --cmd 'slow-in-slow-out-easing-curves.apply' --params '{"quality": "production"}'
```

## 13. MCP Mappings
- **Server**: `spark-creativity-anispark-mcp`
- **Tool**: `creative_anispark_slow_in_slow_out_easing_curves`
- **Resource**: `creative://anispark/projects/active/slow-in-slow-out-easing-curves`

## 14. JSON Operation Mappings
```json
{
  "version": "1.0",
  "requestId": "req-slow-in-slow-out-easing-curves-001",
  "application": "anispark",
  "operation": "animation_2d.slow_in_slow_out_easing_curves",
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
2. Run headless render check via `anicraft` to verify memory allocation and geometry soundness.
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
