using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Inference.Routing;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using OpenClaw.Shared.Study.Orchestration;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyTutorTests
{
    private readonly IStudyTutorService _tutorService = new StudyTutorService();

    [Theory]
    [InlineData(TeachingMode.Beginner)]
    [InlineData(TeachingMode.Standard)]
    [InlineData(TeachingMode.Detailed)]
    [InlineData(TeachingMode.Advanced)]
    [InlineData(TeachingMode.StepByStep)]
    [InlineData(TeachingMode.Socratic)]
    [InlineData(TeachingMode.Practical)]
    [InlineData(TeachingMode.Revision)]
    public void BuildSystemPrompt_ContainsCorrectModeGuidelines(TeachingMode mode)
    {
        var prompt = _tutorService.BuildSystemPrompt(
            mode: mode,
            language: LearningLanguage.English,
            level: EducationLevel.University,
            activeSubject: "Data Structures",
            activeTopic: "Red-Black Trees");

        Assert.Contains(mode.ToString(), prompt);
        Assert.Contains("Data Structures", prompt);
        Assert.Contains("Red-Black Trees", prompt);
        Assert.Contains("PEDAGOGICAL INSTRUCTIONS", prompt);
    }

    [Theory]
    [InlineData(EducationLevel.School, "School Education")]
    [InlineData(EducationLevel.HigherSecondary, "Higher Secondary")]
    [InlineData(EducationLevel.College, "College")]
    [InlineData(EducationLevel.University, "University")]
    [InlineData(EducationLevel.Professional, "Professional")]
    [InlineData(EducationLevel.Technical, "Technical")]
    [InlineData(EducationLevel.Research, "Scientific Research")]
    public void BuildSystemPrompt_SupportsAllUniversalEducationLevels(EducationLevel level, string expectedSubstring)
    {
        var prompt = _tutorService.BuildSystemPrompt(
            mode: TeachingMode.Standard,
            language: LearningLanguage.English,
            level: level,
            activeSubject: "Physics",
            activeTopic: "Quantum Mechanics");

        Assert.Contains(expectedSubstring, prompt);
    }

    [Fact]
    public void BuildSystemPrompt_Malayalam_IncludesBilingualConventions()
    {
        var prompt = _tutorService.BuildSystemPrompt(
            mode: TeachingMode.Standard,
            language: LearningLanguage.Malayalam,
            level: EducationLevel.College,
            activeSubject: "Computer Science",
            activeTopic: "Recursion");

        Assert.Contains("Malayalam", prompt);
        Assert.Contains("BILINGUAL CONVENTION", prompt);
        Assert.Contains("മലയാളം", prompt);
    }

    [Fact]
    public async Task GenerateTutorTurnAsync_Fallback_GeneratesRichContentForAll8Modes()
    {
        foreach (TeachingMode mode in Enum.GetValues<TeachingMode>())
        {
            var session = new StudySession
            {
                TeachingMode = mode,
                Language = LearningLanguage.English,
                Level = EducationLevel.University,
                CurrentTopic = "Binary Search"
            };

            var response = await _tutorService.GenerateTutorTurnAsync(session, "How does this work?");

            Assert.NotNull(response);
            Assert.Equal(StudyMessageSender.Tutor, response.Sender);
            Assert.False(string.IsNullOrWhiteSpace(response.Content));
            Assert.Equal(mode, response.TeachingMode);
            Assert.Equal(LearningLanguage.English, response.Language);
            Assert.True(response.IsFallback);
        }
    }

    [Fact]
    public async Task GenerateTutorTurnAsync_Malayalam_ProducesMalayalamWithEnglishTerminology()
    {
        var session = new StudySession
        {
            TeachingMode = TeachingMode.Standard,
            Language = LearningLanguage.Malayalam,
            Level = EducationLevel.College,
            CurrentTopic = "Recursion"
        };

        var response = await _tutorService.GenerateTutorTurnAsync(session, "Recursion വിശദീകരിക്കാമോ?");

        Assert.NotNull(response);
        Assert.Equal(LearningLanguage.Malayalam, response.Language);
        // Contains Malayalam content and standard English technical terms
        Assert.Contains("Recursion", response.Content);
        Assert.Contains("നിർവ്വചനം", response.Content);
    }

    [Fact]
    public async Task GenerateTutorTurnAsync_AdaptsMode_WhenLearnerRequestsSimpler()
    {
        var session = new StudySession
        {
            TeachingMode = TeachingMode.Advanced,
            Language = LearningLanguage.English,
            Level = EducationLevel.University,
            CurrentTopic = "Graph Traversal"
        };

        var response = await _tutorService.GenerateTutorTurnAsync(session, "Please explain this in simpler terms");

        Assert.Equal(TeachingMode.Beginner, response.TeachingMode);
    }

    [Fact]
    public async Task GenerateTutorTurnAsync_AdaptsMode_WhenLearnerRequestsStepByStep()
    {
        var session = new StudySession
        {
            TeachingMode = TeachingMode.Standard,
            Language = LearningLanguage.English,
            Level = EducationLevel.University,
            CurrentTopic = "Merge Sort"
        };

        var response = await _tutorService.GenerateTutorTurnAsync(session, "Walk me through this step by step");

        Assert.Equal(TeachingMode.StepByStep, response.TeachingMode);
    }

    [Fact]
    public async Task GenerateTutorTurnAsync_AdaptsLanguage_WhenLearnerRequestsMalayalam()
    {
        var session = new StudySession
        {
            TeachingMode = TeachingMode.Standard,
            Language = LearningLanguage.English,
            Level = EducationLevel.University,
            CurrentTopic = "Dynamic Programming"
        };

        var response = await _tutorService.GenerateTutorTurnAsync(session, "Please explain this in malayalam");

        Assert.Equal(LearningLanguage.Malayalam, response.Language);
    }

    [Fact]
    public async Task GenerateTutorTurnAsync_MultiTurnHistory_RetainsContext()
    {
        var session = new StudySession
        {
            TeachingMode = TeachingMode.Standard,
            Language = LearningLanguage.English,
            Level = EducationLevel.University,
            CurrentTopic = "Sorting Algorithms"
        };

        session.Messages.Add(new StudySessionMessage(
            Id: "1",
            Sender: StudyMessageSender.User,
            Content: "What is Quicksort?",
            TimestampUtc: DateTime.UtcNow.AddMinutes(-5)));

        session.Messages.Add(new StudySessionMessage(
            Id: "2",
            Sender: StudyMessageSender.Tutor,
            Content: "Quicksort is a divide-and-conquer algorithm that selects a pivot element.",
            TimestampUtc: DateTime.UtcNow.AddMinutes(-4)));

        var followUp = await _tutorService.GenerateTutorTurnAsync(session, "Why is the pivot choice important?");

        Assert.NotNull(followUp);
        Assert.Equal(StudyMessageSender.Tutor, followUp.Sender);
        Assert.False(string.IsNullOrWhiteSpace(followUp.Content));
    }
}
