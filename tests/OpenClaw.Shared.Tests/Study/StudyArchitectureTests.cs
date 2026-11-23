using System;
using OpenClaw.Shared.Divisions;
using OpenClaw.Shared.Study.Contracts;
using OpenClaw.Shared.Study.Domain;
using Xunit;

namespace OpenClaw.Shared.Tests.Study;

public sealed class StudyArchitectureTests
{
    [Fact]
    public void DivisionCatalog_StudyDivision_IsConfiguredAndActive()
    {
        var studyMetadata = DivisionCatalog.Study;

        Assert.NotNull(studyMetadata);
        Assert.Equal(SparkDivision.Study, studyMetadata.Division);
        Assert.Equal("study", studyMetadata.Key);
        Assert.Equal("Study", studyMetadata.DisplayName);
        Assert.True(studyMetadata.IsActive, "Study division must be active in Phase 1 foundation.");
    }

    [Fact]
    public void DivisionCatalog_TryGetDivision_ResolvesStudyKey()
    {
        var found = DivisionCatalog.TryGetDivision("study", out var division);

        Assert.True(found);
        Assert.Equal(SparkDivision.Study, division);
    }

    [Fact]
    public void StudyDomain_EducationLevels_SupportAllRequiredTiers()
    {
        Assert.True(Enum.IsDefined(typeof(EducationLevel), EducationLevel.School));
        Assert.True(Enum.IsDefined(typeof(EducationLevel), EducationLevel.College));
        Assert.True(Enum.IsDefined(typeof(EducationLevel), EducationLevel.University));
        Assert.True(Enum.IsDefined(typeof(EducationLevel), EducationLevel.Professional));
        Assert.True(Enum.IsDefined(typeof(EducationLevel), EducationLevel.Technical));
        Assert.True(Enum.IsDefined(typeof(EducationLevel), EducationLevel.Research));
    }

    [Fact]
    public void FutureExtensionContracts_AreDefinedInContractsNamespace()
    {
        Assert.NotNull(typeof(IStudySkill));
        Assert.NotNull(typeof(IStudyTool));
        Assert.NotNull(typeof(IStudyCommand));
        Assert.NotNull(typeof(IStudyAgent));
        Assert.NotNull(typeof(IStudyExtension));
    }
}
