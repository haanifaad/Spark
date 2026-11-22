using System;

namespace OpenClaw.Shared.Study.Domain;

/// <summary>
/// Pedagogical teaching modes supported by the SPARK AI Tutor.
/// Adapts tone, structure, depth, and cognitive scaffolding.
/// </summary>
public enum TeachingMode
{
    /// <summary>
    /// Gentle, intuitive explanations using everyday analogies and low technical jargon.
    /// </summary>
    Beginner,

    /// <summary>
    /// Balanced conceptual clarity, formal definitions, and standard instructional flow.
    /// </summary>
    Standard,

    /// <summary>
    /// Deep, comprehensive theoretical exploration covering edge cases and underlying mechanics.
    /// </summary>
    Detailed,

    /// <summary>
    /// Rigorous academic and mathematical treatment, formal proofs, and state-of-the-art nuances.
    /// </summary>
    Advanced,

    /// <summary>
    /// Sequential procedural breakdown with incremental checkpoints and algorithmic steps.
    /// </summary>
    StepByStep,

    /// <summary>
    /// Inquiry-based guided discovery; questions and counter-questions rather than direct answers.
    /// </summary>
    Socratic,

    /// <summary>
    /// Hands-on, code/lab-focused, actionable implementation and real-world engineering examples.
    /// </summary>
    Practical,

    /// <summary>
    /// High-density bulleted summary, core formulas, key takeaways, and rapid recall drills.
    /// </summary>
    Revision
}

/// <summary>
/// Supported natural languages for instruction and tutoring.
/// </summary>
public enum LearningLanguage
{
    /// <summary>
    /// Global academic and technical English.
    /// </summary>
    English,

    /// <summary>
    /// Natural Malayalam instructional explanations paired with standard English technical terms.
    /// </summary>
    Malayalam
}

/// <summary>
/// Bloom's revised cognitive taxonomy levels for learning objectives.
/// </summary>
public enum TaxonomyLevel
{
    Remember,
    Understand,
    Apply,
    Analyze,
    Evaluate,
    Create,

    Remembering = Remember,
    Understanding = Understand,
    Applying = Apply,
    Analyzing = Analyze
}
