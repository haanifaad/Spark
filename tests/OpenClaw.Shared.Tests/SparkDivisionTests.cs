using System;
using System.Linq;
using OpenClaw.Shared.Divisions;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class SparkDivisionTests
{
    [Fact]
    public void Catalog_ContainsExactlyFiveDivisions()
    {
        Assert.Equal(5, DivisionCatalog.AllDivisions.Count);
        var divisions = Enum.GetValues<SparkDivision>();
        Assert.Equal(5, divisions.Length);
        Assert.Contains(SparkDivision.Chat, divisions);
        Assert.Contains(SparkDivision.Work, divisions);
        Assert.Contains(SparkDivision.Code, divisions);
        Assert.Contains(SparkDivision.Study, divisions);
        Assert.Contains(SparkDivision.Creative, divisions);
    }

    [Theory]
    [InlineData(SparkDivision.Chat, "chat", "Chat", true)]
    [InlineData(SparkDivision.Work, "work", "Work", true)]
    [InlineData(SparkDivision.Code, "code", "Code", true)]
    [InlineData(SparkDivision.Study, "study", "Study", true)]
    [InlineData(SparkDivision.Creative, "creative", "Creative", true)]
    public void Catalog_ReturnsValidMetadataForEachDivision(
        SparkDivision division, string expectedKey, string expectedName, bool expectedActive)
    {
        var meta = DivisionCatalog.GetMetadata(division);
        Assert.Equal(division, meta.Division);
        Assert.Equal(expectedKey, meta.Key);
        Assert.Equal(expectedName, meta.DisplayName);
        Assert.Equal(expectedActive, meta.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(meta.Description));
        Assert.False(string.IsNullOrWhiteSpace(meta.DefaultRoute));
        Assert.False(string.IsNullOrWhiteSpace(meta.Icon));
    }

    [Theory]
    [InlineData("chat", SparkDivision.Chat)]
    [InlineData("HOME", SparkDivision.Chat)]
    [InlineData("work", SparkDivision.Work)]
    [InlineData("WORKSPACE", SparkDivision.Work)]
    [InlineData("workboard", SparkDivision.Work)]
    [InlineData("cron", SparkDivision.Work)]
    [InlineData("sessions", SparkDivision.Work)]
    [InlineData("code", SparkDivision.Code)]
    [InlineData("study", SparkDivision.Study)]
    [InlineData("creative", SparkDivision.Creative)]
    public void Catalog_TryGetDivision_ResolvesKeys(string key, SparkDivision expected)
    {
        Assert.True(DivisionCatalog.TryGetDivision(key, out var division));
        Assert.Equal(expected, division);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void Catalog_TryGetDivision_RejectsUnknownKeys(string? key)
    {
        Assert.False(DivisionCatalog.TryGetDivision(key, out _));
    }
}
