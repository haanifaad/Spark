using System;
using OpenClaw.Shared.Audio.Vocabulary;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class PersonalVocabularyTests
{
    [Fact]
    public void Configuration_DefaultValues_AreLoaded()
    {
        var vocab = new SparkPersonalVocabulary();
        var config = vocab.Configuration;

        Assert.Contains("Kubernetes", config.TechnicalTerms);
        Assert.Contains("gRPC", config.TechnicalTerms);
        Assert.Contains("ONNX", config.TechnicalTerms);

        Assert.True(config.MalayalamExpressions.ContainsKey("enthaanu"));
        Assert.True(config.MalayalamExpressions.ContainsKey("adipoli"));
        Assert.True(config.MalayalamExpressions.ContainsKey("sambhava"));

        Assert.True(config.AlternativePronunciations.ContainsKey("k8s"));
        Assert.True(config.AlternativePronunciations.ContainsKey("grpc"));
    }

    [Fact]
    public void BuildWhisperBiasingPrompt_ContainsTermsAndExpressions()
    {
        var vocab = new SparkPersonalVocabulary();
        string prompt = vocab.BuildWhisperBiasingPrompt();

        Assert.Contains("Kubernetes", prompt);
        Assert.Contains("gRPC", prompt);
        Assert.Contains("enthaanu", prompt);
        Assert.Contains("adipoli", prompt);
    }

    [Fact]
    public void NormalizeTranscript_ReplacesAliasesAndNormalizesCapitalization()
    {
        var vocab = new SparkPersonalVocabulary();

        string raw = "deploy to k8s service using grpc and onnx model";
        string normalized = vocab.NormalizeTranscript(raw);

        Assert.Equal("deploy to Kubernetes service using gRPC and ONNX model", normalized);
    }

    [Fact]
    public void NormalizeTranscript_HandlesMalayalamMixedExpressions()
    {
        var vocab = new SparkPersonalVocabulary();

        string raw = "enthaanu ee sambhava ithu adipoli aanu";
        string normalized = vocab.NormalizeTranscript(raw);

        Assert.Equal("enthaanu ee sambhava ithu adipoli aanu", normalized);
        Assert.True(vocab.ContainsDomainVocabulary(normalized));
    }

    [Fact]
    public void AddTechnicalTerm_And_AddPronunciationAlias_WorkDynamically()
    {
        var vocab = new SparkPersonalVocabulary();

        vocab.AddTechnicalTerm("LangGraph");
        vocab.AddPronunciationAlias("lang-graph", "LangGraph");

        string raw = "check this lang-graph workflow";
        string normalized = vocab.NormalizeTranscript(raw);

        Assert.Equal("check this LangGraph workflow", normalized);
        Assert.Contains("LangGraph", vocab.BuildWhisperBiasingPrompt());
        Assert.True(vocab.ContainsDomainVocabulary(normalized));
    }
}
