using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenClaw.Shared.DecisionIntelligence;

/// <summary>
/// Stable SPARK native Decision Intelligence interface.
/// Insulates all SPARK subsystems from direct dependencies on underlying Laya runtime details.
/// </summary>
public interface ISparkDecisionIntelligence
{
    /// <summary>
    /// Master switch status.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Indicates whether the Laya runtime is currently operational and ready for inference.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Evaluates a single-pass decision over a document or state text with a set of typed questions.
    /// Returns calibrated choices, scores, yes/no outcomes, confidence ratings, and abstention markers.
    /// </summary>
    Task<DecisionIntelligenceResult> EvaluateAsync(
        string stateText,
        IEnumerable<LayaQuestion> questions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// High-level choice classification helper.
    /// </summary>
    Task<LayaAnswer> ClassifyChoiceAsync(
        string stateText,
        string instruction,
        IReadOnlyList<string> options,
        double confidenceThreshold = 0.5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// High-level binary (yes/no) classification helper.
    /// </summary>
    Task<LayaAnswer> DecideBinaryAsync(
        string stateText,
        string question,
        double confidenceThreshold = 0.5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// High-level score evaluation helper (e.g., risk scoring, urgency, quality).
    /// </summary>
    Task<LayaAnswer> EvaluateScoreAsync(
        string stateText,
        string criteria,
        double minScore = 0.0,
        double maxScore = 1.0,
        CancellationToken cancellationToken = default);
}
