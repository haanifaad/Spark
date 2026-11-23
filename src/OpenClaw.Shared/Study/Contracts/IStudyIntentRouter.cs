using OpenClaw.Shared.Study.Orchestration;

namespace OpenClaw.Shared.Study.Contracts;

/// <summary>
/// Educational intent classification contract.
/// </summary>
public interface IStudyIntentRouter
{
    StudyIntentDecision Classify(string query, StudyIntentContext? context = null);
}
