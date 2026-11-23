---
id: architectural-floor-plan-drafting
name: Architectural Floor Plan Drafting and Wall Assemblies
category: cad_technical_drawing
application: CADSpark
application_id: cadspark
version: 1.0.0
purpose: Drawing dimensioned architectural walls, door swings, and window tags
---

# Architectural Floor Plan Drafting and Wall Assemblies

## 1. Unique Skill ID
`creative.cad_technical_drawing.architectural-floor-plan-drafting`

## 2. Skill Name
**Architectural Floor Plan Drafting and Wall Assemblies**

## 3. Description
A specialized creative engineering skill that instructs the SPARK Creative Agent on professional best practices, algorithmic rules, and production workflows for architectural floor plan drafting and wall assemblies within the CADSpark ecosystem.

## 4. Purpose
Drawing dimensioned architectural walls, door swings, and window tags. Provides the autonomous agent with domain-specific criteria to evaluate, plan, generate, and refine high-caliber creative assets that meet international production standards.

## 5. Professional Knowledge
- **Domain Foundation**: Mastery of architectural floor plan drafting and wall assemblies requires deep technical comprehension of both theoretical aesthetics and software data pipelines.
- **Cognitive and Physical Impact**: Decisions directly influence user perception, aesthetic credibility, brand trust, and ergonomic scannability.
- **Industry Standards**: Follows ISO, W3C, and professional creative guild standards for CAD and Technical Drawing.

## 6. Relevant Design or Engineering Principles
- **Aesthetic Precision**: Ensuring exact proportions, tonal consistency, and optical alignment rather than relying on unguided generative approximations.
- **Structural Integrity**: Maintaining clean document trees, non-destructive layer hierarchies, and mathematical curve continuity.
- **Accessibility & Universal Ergonomics**: Complying with contrast criteria, visual wayfinding clarity, and platform-specific guidelines.
- **Resource Efficiency**: Producing lightweight, reusable components with minimal redundant geometry or bloat.

## 7. Step-by-Step Workflow
1. **Context Analysis**: Inspect the active project document, canvas bounds, current selection, and existing asset metadata in CADSpark.
2. **Strategy Formulation**: Define the target objective, establishing key operational parameters, color palettes, and structural geometry.
3. **Execution Plan**: Deconstruct the request into discrete, atomic tool operations adhering to safety tier limits.
4. **Draft Generation**: Execute primary creative operations (e.g. creating base paths, adjustment stacks, keyframes, or nodes).
5. **Micro-Refinement**: Apply detailed tuning—optical spacing, curve smoothing, contrast balancing, or frequency filtering.
6. **Integrity Audit**: Verify structural conformance against professional benchmark criteria.
7. **Commit & Render**: Package output for viewport preview or commit modifications to project storage.

## 8. Required Inputs
- `projectId`: Unique identifier of the target active project in CADSpark.
- `documentState`: Current serialization or tree representation of the active canvas or timeline.
- `parameters`: JSON dictionary specifying operation targets (e.g. coordinates, dimensions, color tokens, curve data).
- `selectionContext`: Identifiers of currently selected layers, nodes, shapes, or timecode ranges.

## 9. Expected Outputs
- `modifiedDocument`: Updated project state with validated changes applied.
- `auditReport`: Formal report detailing modifications, execution metrics, and quality scores.
- `previewArtifact`: Rendered raster PNG, vector SVG, or audio sample for user inspection.
- `undoToken`: Reversible transaction receipt enabling instant rollback.

## 10. Compatible Applications
- Primary: **CADSpark** (`cadspark`)
- Secondary / Exchange: SPARK Creativity Studio Suite, DesignSpark, PDFSpark

## 11. Compatible Tools
- `creative.agent.orchestrator`
- `cadspark.document.inspect`
- `cadspark.element.create`
- `cadspark.element.modify`
- `cadspark.export.render`

## 12. CLI Mappings
```bash
spark creativity app inspect cadspark
spark creativity skills run creative.cad_technical_drawing.architectural-floor-plan-drafting --app cadspark --params '{"target": "active_layer"}'
cadcraft-cli run --cmd 'architectural-floor-plan-drafting.apply' --params '{"quality": "production"}'
```

## 13. MCP Mappings
- **Server**: `spark-creativity-cadspark-mcp`
- **Tool**: `creative_cadspark_architectural_floor_plan_drafting`
- **Resource**: `creative://cadspark/projects/active/architectural-floor-plan-drafting`

## 14. JSON Operation Mappings
```json
{
  "version": "1.0",
  "requestId": "req-architectural-floor-plan-drafting-001",
  "application": "cadspark",
  "operation": "cad_technical_drawing.architectural_floor_plan_drafting",
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
2. Run headless render check via `cadcraft-cli` to verify memory allocation and geometry soundness.
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
