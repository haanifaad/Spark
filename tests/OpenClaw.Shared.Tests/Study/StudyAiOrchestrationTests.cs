using System.Threading.Tasks;
using OpenClaw.Shared.Study.Infrastructure;
using OpenClaw.Shared.Study.Orchestration;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyAiOrchestrationTests
{
    private readonly StudyIntentRouter _router = new();

    [Theory]
    [InlineData("Can you explain how quicksort works?", StudyEducationalIntent.ConceptualExplanation)]
    [InlineData("Quiz me on binary search algorithms for my test tomorrow", StudyEducationalIntent.ExamReview)]
    [InlineData("Solve this recurrence relation step by step for homework: T(n) = 2T(n/2) + n", StudyEducationalIntent.ProblemSolvingPractice)]
    [InlineData("Summarize the key takeaways of chapter 4 into a cheat sheet", StudyEducationalIntent.SummaryAndSynthesis)]
    [InlineData("Help me think through why greedy algorithms fail here, don't tell me the answer", StudyEducationalIntent.SocraticTutoring)]
    [InlineData("What is the formula for standard deviation?", StudyEducationalIntent.ReferenceLookup)]
    public void IntentRouter_CorrectlyClassifiesEducationalQueries(string query, StudyEducationalIntent expectedIntent)
    {
        var decision = _router.Classify(query);

        Assert.Equal(expectedIntent, decision.PrimaryIntent);
        Assert.True(decision.Confidence > 0.5);
    }

    [Fact]
    public async Task StudyOrchestrator_ProcessTurn_ProducesEducationalResponseWithTelemetry()
    {
        var store = new StudyDataStore();
        var contextProvider = new StudyContextProvider(store);
        var orchestrator = new StudyOrchestrator(_router, contextProvider);

        var request = new StudyInteractionRequest(
            Query: "Explain binary search and its logarithmic time complexity",
            UserId: "test-student");

        var result = await orchestrator.ProcessTurnAsync(request);

        Assert.NotNull(result);
        Assert.Equal(StudyEducationalIntent.ConceptualExplanation, result.IdentifiedIntent);
        Assert.Contains("binary search", result.ResponseText, System.StringComparison.OrdinalIgnoreCase);
        Assert.True(result.LatencyMs >= 0);
    }
}
