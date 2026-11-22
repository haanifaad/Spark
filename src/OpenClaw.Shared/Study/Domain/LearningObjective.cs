using System;
using System.Collections.Generic;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Fine-grained, measurable educational outcome associated with a lesson or topic.
/// Aligned with Bloom's Taxonomy.
/// </summary>
public sealed class LearningObjective
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string LessonId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public TaxonomyLevel TaxonomyLevel { get; set; } = TaxonomyLevel.Understand;
    public bool IsMastered { get; set; }
    public List<string> Prerequisites { get; set; } = [];

    public override string ToString() => $"[{TaxonomyLevel}] {Description} (Mastered: {IsMastered})";
}
