using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenClaw.Shared.Audio.Vocabulary;

/// <summary>
/// Configuration for user-defined personal vocabulary, technical terms,
/// and Malayalam-English mixed-language expressions.
/// </summary>
public sealed class PersonalVocabularyConfiguration
{
    public static readonly IReadOnlyList<string> DefaultTechnicalTerms =
    [
        "Kubernetes", "gRPC", "ONNX", "WinUI", "Reactor", "DeepSeek", "LLM",
        "Vercel", "SolidJS", "PostgreSQL", "WebAssembly", "Wasapi", "Piper", "Kokoro", "Whisper"
    ];

    public static readonly IReadOnlyDictionary<string, string> DefaultMalayalamExpressions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "enthaanu", "what is it" },
            { "sambhava", "happening/feature" },
            { "nokku", "look/check" },
            { "nanni", "thank you" },
            { "adipoli", "awesome/great" },
            { "shari", "ok/alright" },
            { "pinnentha", "what else" },
            { "cheyyuka", "do it" },
            { "parayu", "tell me" },
            { "ippo", "now" }
        };

    public static readonly IReadOnlyDictionary<string, string> DefaultPronunciationAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "k8s", "Kubernetes" },
            { "k-8-s", "Kubernetes" },
            { "kay-eight-ess", "Kubernetes" },
            { "g-r-p-c", "gRPC" },
            { "grpc", "gRPC" },
            { "win-u-i", "WinUI" },
            { "winui", "WinUI" },
            { "react-or", "Reactor" },
            { "deep-seek", "DeepSeek" },
            { "deepseek", "DeepSeek" },
            { "l-l-m", "LLM" },
            { "llm", "LLM" }
        };

    public List<string> TechnicalTerms { get; set; } = [..DefaultTechnicalTerms];
    public Dictionary<string, string> MalayalamExpressions { get; set; } = new(DefaultMalayalamExpressions, StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> AlternativePronunciations { get; set; } = new(DefaultPronunciationAliases, StringComparer.OrdinalIgnoreCase);
    public List<string> CustomContextHints { get; set; } = [];
}

/// <summary>
/// Service managing personal vocabulary, speech recognition context biasing,
/// and phonetic normalization. Kept completely separate from wake-word detection.
/// </summary>
public sealed class SparkPersonalVocabulary
{
    private readonly object _gate = new();
    private PersonalVocabularyConfiguration _config;

    public PersonalVocabularyConfiguration Configuration
    {
        get
        {
            lock (_gate) return _config;
        }
    }

    public SparkPersonalVocabulary(PersonalVocabularyConfiguration? configuration = null)
    {
        _config = configuration ?? new PersonalVocabularyConfiguration();
    }

    public void UpdateConfiguration(PersonalVocabularyConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        lock (_gate)
        {
            _config = configuration;
        }
    }

    public void AddTechnicalTerm(string term)
    {
        if (string.IsNullOrWhiteSpace(term)) return;
        lock (_gate)
        {
            if (!_config.TechnicalTerms.Contains(term, StringComparer.OrdinalIgnoreCase))
                _config.TechnicalTerms.Add(term.Trim());
        }
    }

    public void AddMalayalamExpression(string expression, string? meaning = null)
    {
        if (string.IsNullOrWhiteSpace(expression)) return;
        lock (_gate)
        {
            _config.MalayalamExpressions[expression.Trim().ToLowerInvariant()] = meaning ?? "";
        }
    }

    public void AddPronunciationAlias(string alias, string canonical)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(canonical)) return;
        lock (_gate)
        {
            _config.AlternativePronunciations[alias.Trim().ToLowerInvariant()] = canonical.Trim();
        }
    }

    /// <summary>
    /// Builds a context prompt for Whisper STT biasing (passed via WithPrompt),
    /// elevating token probabilities for specialized technical terminology
    /// and regional expressions.
    /// </summary>
    public string BuildWhisperBiasingPrompt()
    {
        lock (_gate)
        {
            var sb = new StringBuilder();
            sb.Append("Technical vocabulary: ");
            sb.Append(string.Join(", ", _config.TechnicalTerms));

            if (_config.MalayalamExpressions.Count > 0)
            {
                sb.Append(". Malayalam expressions: ");
                sb.Append(string.Join(", ", _config.MalayalamExpressions.Keys));
            }

            if (_config.CustomContextHints.Count > 0)
            {
                sb.Append(". Context hints: ");
                sb.Append(string.Join(", ", _config.CustomContextHints));
            }

            sb.Append('.');
            return sb.ToString();
        }
    }

    /// <summary>
    /// Post-transcription normalization that corrects phonetic aliases,
    /// applies canonical casing for technical terms, and preserves Malayalam expressions.
    /// </summary>
    public string NormalizeTranscript(string rawTranscript)
    {
        if (string.IsNullOrWhiteSpace(rawTranscript))
            return rawTranscript ?? string.Empty;

        lock (_gate)
        {
            var text = rawTranscript;

            // 1. Substitute phonetic aliases (e.g. "k8s" -> "Kubernetes")
            foreach (var (alias, canonical) in _config.AlternativePronunciations)
            {
                string pattern = $@"\b{Regex.Escape(alias)}\b";
                text = Regex.Replace(text, pattern, canonical, RegexOptions.IgnoreCase);
            }

            // 2. Normalize canonical capitalization for technical terms
            foreach (var term in _config.TechnicalTerms)
            {
                string pattern = $@"\b{Regex.Escape(term)}\b";
                text = Regex.Replace(text, pattern, term, RegexOptions.IgnoreCase);
            }

            return text.Trim();
        }
    }

    /// <summary>
    /// Checks if a given text contains technical terminology or Malayalam mixed expressions.
    /// </summary>
    public bool ContainsDomainVocabulary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        lock (_gate)
        {
            foreach (var term in _config.TechnicalTerms)
            {
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase))
                    return true;
            }

            foreach (var expr in _config.MalayalamExpressions.Keys)
            {
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(expr)}\b", RegexOptions.IgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
