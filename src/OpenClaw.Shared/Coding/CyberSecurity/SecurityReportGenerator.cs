using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OpenClaw.Shared.Coding.CyberSecurity;

/// <summary>
/// Generates industry-standard Markdown and SARIF 2.1.0 security assessment reports
/// matching Shannon's reporting architecture.
/// </summary>
public static class SecurityReportGenerator
{
    /// <summary>
    /// Generates a comprehensive, professional Markdown security assessment report.
    /// </summary>
    public static string GenerateMarkdownReport(SecurityAuditSession session)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Security Assessment Report");
        sb.AppendLine();
        sb.AppendLine("## Executive Summary");
        sb.AppendLine($"- **Target Application:** {session.TargetUrl}");
        if (!string.IsNullOrWhiteSpace(session.TargetRepository))
        {
            sb.AppendLine($"- **Source Repository:** {session.TargetRepository}");
        }
        sb.AppendLine($"- **Assessment Date:** {session.StartedAt:MMMM yyyy}");
        sb.AppendLine($"- **Assessment ID:** `{session.AuditId}`");
        sb.AppendLine($"- **Scope:** {string.Join(", ", Enum.GetNames<VulnClass>())} vulnerability analysis and proof-of-concept verification");
        sb.AppendLine($"- **Status:** {session.Phase}");
        sb.AppendLine();

        sb.AppendLine("### Findings Overview");
        sb.AppendLine("| Severity | Count |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| **Critical** | {session.CriticalCount} |");
        sb.AppendLine($"| **High** | {session.HighCount} |");
        sb.AppendLine($"| **Medium** | {session.MediumCount} |");
        sb.AppendLine($"| **Low** | {session.LowCount} |");
        sb.AppendLine($"| **Total Verified Findings** | **{session.TotalFindings}** |");
        sb.AppendLine();

        sb.AppendLine("## Summary by Vulnerability Type");
        sb.AppendLine();

        foreach (var vulnClass in Enum.GetValues<VulnClass>())
        {
            var classFindings = session.Findings.Where(f => f.Class == vulnClass).ToList();
            sb.AppendLine($"### {vulnClass} Vulnerabilities ({classFindings.Count})");
            if (classFindings.Count == 0)
            {
                sb.AppendLine($"*No verified {vulnClass.ToString().ToLowerInvariant()} vulnerabilities identified in tested scope.*");
            }
            else
            {
                foreach (var f in classFindings)
                {
                    sb.AppendLine($"- **[{f.Id}] {f.Title}** ({f.Severity}): {f.Summary}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Detailed Exploitation Evidence & Proofs-of-Concept");
        sb.AppendLine();
        sb.AppendLine("> **Note:** In alignment with the Shannon testing doctrine, every finding below was independently verified with concrete impact proof.");
        sb.AppendLine();

        if (session.Findings.Count == 0)
        {
            sb.AppendLine("*No active vulnerabilities met the proof-of-concept verification criteria.*");
        }
        else
        {
            foreach (var finding in session.Findings)
            {
                sb.AppendLine($"### {finding.Id}: {finding.Title}");
                sb.AppendLine();
                sb.AppendLine("**Summary:**");
                sb.AppendLine($"- **Vulnerable Location:** `{finding.VulnerableEndpoint}`");
                if (!string.IsNullOrEmpty(finding.SourceFile))
                {
                    var lineSuffix = finding.SourceLine.HasValue ? $":{finding.SourceLine.Value}" : "";
                    sb.AppendLine($"- **Source Location:** `{finding.SourceFile}{lineSuffix}`");
                }
                sb.AppendLine($"- **Vulnerability Class:** {finding.Class}");
                sb.AppendLine($"- **Severity:** {finding.Severity}");
                sb.AppendLine($"- **Confidence:** {finding.Confidence}");
                if (!string.IsNullOrEmpty(finding.CweId))
                {
                    sb.AppendLine($"- **CWE Classification:** {finding.CweId}");
                }
                sb.AppendLine($"- **Prerequisites:** {finding.Prerequisites}");
                sb.AppendLine();
                sb.AppendLine($"**Overview:**\n{finding.Summary}");
                sb.AppendLine();

                if (finding.ExploitationSteps.Count > 0)
                {
                    sb.AppendLine("**Exploitation Steps:**");
                    for (int i = 0; i < finding.ExploitationSteps.Count; i++)
                    {
                        sb.AppendLine($"{i + 1}. {finding.ExploitationSteps[i]}");
                    }
                    sb.AppendLine();
                }

                if (!string.IsNullOrEmpty(finding.RawPayload))
                {
                    sb.AppendLine("**Payload / Proof-of-Concept Request:**");
                    sb.AppendLine("```http");
                    sb.AppendLine(finding.RawPayload);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }

                if (!string.IsNullOrEmpty(finding.HttpEvidence))
                {
                    sb.AppendLine("**Server Response Evidence:**");
                    sb.AppendLine("```json");
                    sb.AppendLine(finding.HttpEvidence);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }

                sb.AppendLine("**Proof of Impact:**");
                sb.AppendLine(finding.ProofOfImpact);
                sb.AppendLine();

                sb.AppendLine("**Remediation & Code Recommendation:**");
                sb.AppendLine(finding.Remediation);
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Security Testing Methodology & Guardrails");
        sb.AppendLine("- All tests executed locally or within explicitly authorized target parameters.");
        sb.AppendLine("- Proof-of-concept exploits executed non-destructively.");
        sb.AppendLine("- Generated by SPARK Cybersecurity Audit Engine (Shannon Integration).");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a standard SARIF 2.1.0 JSON document from the audit findings.
    /// </summary>
    public static string GenerateSarifReport(SecurityAuditSession session)
    {
        var sarifDoc = new
        {
            version = "2.1.0",
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            runs = new[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "SPARK-Shannon-Auditor",
                            version = "1.0.0",
                            informationUri = "https://github.com/haanifaad/Spark",
                            rules = session.Findings.Select(f => new
                            {
                                id = f.Id,
                                name = f.Class.ToString(),
                                shortDescription = new { text = f.Title },
                                fullDescription = new { text = f.Summary },
                                defaultConfiguration = new
                                {
                                    level = MapSeverityToSarifLevel(f.Severity)
                                },
                                help = new { text = f.Remediation },
                                properties = new
                                {
                                    cwe = f.CweId ?? "CWE-Other",
                                    confidence = f.Confidence.ToString()
                                }
                            }).ToArray()
                        }
                    },
                    results = session.Findings.Select(f => new
                    {
                        ruleId = f.Id,
                        level = MapSeverityToSarifLevel(f.Severity),
                        message = new { text = $"{f.Title}: {f.Summary}. Impact: {f.ProofOfImpact}" },
                        locations = new[]
                        {
                            new
                            {
                                physicalLocation = new
                                {
                                    artifactLocation = new
                                    {
                                        uri = f.SourceFile ?? f.VulnerableEndpoint
                                    },
                                    region = new
                                    {
                                        startLine = f.SourceLine ?? 1
                                    }
                                }
                            }
                        }
                    }).ToArray()
                }
            }
        };

        return JsonSerializer.Serialize(sarifDoc, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string MapSeverityToSarifLevel(AuditSeverity severity) => severity switch
    {
        AuditSeverity.Critical or AuditSeverity.High => "error",
        AuditSeverity.Medium => "warning",
        _ => "note"
    };
}
