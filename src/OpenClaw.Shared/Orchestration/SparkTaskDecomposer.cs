using System.Text.RegularExpressions;
using OpenClaw.Shared.Tools;

namespace OpenClaw.Shared.Orchestration;

/// <summary>
/// Decomposes complex Malayalam and English instructions into structured, risk-classified operation steps.
/// </summary>
public sealed class SparkTaskDecomposer : ITaskDecomposer
{
    public Task<List<SparkTaskStep>> DecomposeInstructionAsync(string taskId, string instruction, string language, ISparkToolRegistry? toolRegistry = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instruction);

        var segments = SplitIntoLogicalSegments(instruction);
        var steps = new List<SparkTaskStep>(segments.Count);
        var index = 1;

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            var (toolName, risk, requiresApproval, defaultArgs) = ClassifySegment(trimmed);

            // If a tool registry is provided and has metadata for this tool, reconcile risk and permissions
            if (toolRegistry != null)
            {
                var tool = toolRegistry.GetTool(toolName);
                if (tool != null)
                {
                    risk = tool.Metadata.RiskLevel;
                    requiresApproval = requiresApproval || risk >= SparkToolRiskLevel.High;
                }
            }

            steps.Add(new SparkTaskStep
            {
                StepId = Guid.NewGuid().ToString("N"),
                TaskId = taskId,
                Index = index++,
                Description = trimmed,
                ToolName = toolName,
                RiskClassification = risk,
                RequiresApproval = requiresApproval,
                Status = SparkStepState.Pending,
                InputArguments = defaultArgs
            });
        }

        // Fallback: If no distinct steps were extracted, generate a default general reasoning/research step
        if (steps.Count == 0)
        {
            steps.Add(new SparkTaskStep
            {
                StepId = Guid.NewGuid().ToString("N"),
                TaskId = taskId,
                Index = 1,
                Description = instruction.Trim(),
                ToolName = "agent_general_reasoning",
                RiskClassification = SparkToolRiskLevel.Low,
                RequiresApproval = false,
                Status = SparkStepState.Pending,
                InputArguments = new Dictionary<string, object?> { ["query"] = instruction.Trim() }
            });
        }

        return Task.FromResult(steps);
    }

    private static List<string> SplitIntoLogicalSegments(string text)
    {
        // Split on standard punctuation delimiters and bilingual conjunctions
        // Malayalam conjunctions / phrase markers: കൂടാതെ, പിന്നെ, ശേഷം, എന്ന്, എങ്കിൽ
        var pattern = @"(?<=[.!?;\n])\s+|(?:\s+(?:and\s+then|then|also|and|കൂടാതെ|പിന്നെ|ശേഷം)\s+)";
        var parts = Regex.Split(text, pattern, RegexOptions.IgnoreCase);

        var results = new List<string>();
        foreach (var p in parts)
        {
            var cleaned = p.Trim();
            if (cleaned.Length > 2)
            {
                results.Add(cleaned);
            }
        }

        return results.Count > 0 ? results : [text.Trim()];
    }

    private static (string ToolName, SparkToolRiskLevel Risk, bool RequiresApproval, Dictionary<string, object?> Args) ClassifySegment(string segment)
    {
        var lower = segment.ToLowerInvariant();
        var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["instruction_clause"] = segment
        };

        // 1. Email actions (English & Malayalam: ഇമെയിൽ, ഇമെയിലുകൾ, മെയിൽ)
        if (lower.Contains("email") || lower.Contains("mail") || lower.Contains("ഇമെയി") || lower.Contains("മെയി"))
        {
            if (lower.Contains("send") || lower.Contains("അയക്കൂ") || lower.Contains("അയക്കുക"))
            {
                return ("email_send", SparkToolRiskLevel.High, true, args);
            }
            if (lower.Contains("draft") || lower.Contains("reply") || lower.Contains("തയ്യാറാക്കൂ") || lower.Contains("മറുപടി"))
            {
                return ("email_draft", SparkToolRiskLevel.Low, false, args);
            }
            return ("email_search", SparkToolRiskLevel.Low, false, args);
        }

        // 2. Travel & Booking (English: train, flight, bus, hotel, book; Malayalam: ട്രെയിൻ, ഫ്ലൈറ്റ്, ബസ്, ബുക്ക്, ടിക്കറ്റ്, കൊച്ചി)
        if (lower.Contains("train") || lower.Contains("flight") || lower.Contains("flight") || lower.Contains("bus") 
            || lower.Contains("hotel") || lower.Contains("ticket") || lower.Contains("travel") || lower.Contains("book")
            || lower.Contains("ട്രെയിൻ") || lower.Contains("ഫ്ലൈറ്റ്") || lower.Contains("ബസ്") || lower.Contains("ഹോട്ടൽ") 
            || lower.Contains("ടിക്കറ്റ്") || lower.Contains("യാത്ര") || lower.Contains("ബുക്ക്"))
        {
            if (lower.Contains("book") || lower.Contains("ബുക്ക് ചെയ്യുക") || lower.Contains("ടിക്കറ്റ് എടുക്കൂ"))
            {
                return ("travel_book", SparkToolRiskLevel.Critical, true, args);
            }
            if (lower.Contains("train") || lower.Contains("ട്രെയിൻ"))
            {
                return ("travel_search_trains", SparkToolRiskLevel.Low, false, args);
            }
            if (lower.Contains("flight") || lower.Contains("ഫ്ലൈറ്റ്"))
            {
                return ("travel_search_flights", SparkToolRiskLevel.Low, false, args);
            }
            return ("travel_search_options", SparkToolRiskLevel.Low, false, args);
        }

        // 3. Shopping & Commerce (English: buy, purchase, shopping, cart, order; Malayalam: വാങ്ങുക, സാധനം, ഓർഡർ, ഷോപ്പിംഗ്)
        if (lower.Contains("buy") || lower.Contains("purchase") || lower.Contains("order") || lower.Contains("cart") 
            || lower.Contains("shop") || lower.Contains("വാങ്ങുക") || lower.Contains("ഓർഡർ") || lower.Contains("ഷോപ്പിംഗ്"))
        {
            if (lower.Contains("buy") || lower.Contains("purchase") || lower.Contains("checkout") || lower.Contains("വാങ്ങുക"))
            {
                return ("shopping_checkout", SparkToolRiskLevel.Critical, true, args);
            }
            if (lower.Contains("cart") || lower.Contains("കാർട്ട്"))
            {
                return ("shopping_cart", SparkToolRiskLevel.Medium, false, args);
            }
            return ("shopping_search", SparkToolRiskLevel.Low, false, args);
        }

        // 4. Forms (English: form, register, apply, submit; Malayalam: ഫോം, രജിസ്റ്റർ, അപേക്ഷ)
        if (lower.Contains("form") || lower.Contains("register") || lower.Contains("apply") 
            || lower.Contains("ഫോം") || lower.Contains("രജിസ്റ്റർ") || lower.Contains("അപേക്ഷ"))
        {
            if (lower.Contains("submit") || lower.Contains("സമർപ്പിക്കുക"))
            {
                return ("forms_submit", SparkToolRiskLevel.High, true, args);
            }
            return ("forms_populate", SparkToolRiskLevel.Low, false, args);
        }

        // 5. Deletion / Destruction (English: delete, remove; Malayalam: ഡിലീറ്റ്, കളയുക)
        if (lower.Contains("delete") || lower.Contains("remove") || lower.Contains("ഡിലീറ്റ്") || lower.Contains("കളയുക"))
        {
            return ("fs_delete_file", SparkToolRiskLevel.Critical, true, args);
        }

        // 6. Reports, Documents & Notes (English: report, document, write, note, summary; Malayalam: റിപ്പോർട്ട്, റിപ്പോർട്ടും, എഴുതുക, രേഖ, കുറിപ്പ്, തയ്യാറാക്കുക)
        if (lower.Contains("report") || lower.Contains("summary") || lower.Contains("document") || lower.Contains("note") 
            || lower.Contains("write") || lower.Contains("റിപ്പോർ") || lower.Contains("എഴുതുക") || lower.Contains("കുറിപ്പ്")
            || lower.Contains("തയ്യാറാക്ക"))
        {
            return ("notes_create", SparkToolRiskLevel.Low, false, args);
        }

        // 7. Development & Git (build, test, git, commit, code, run)
        if (lower.Contains("build") || lower.Contains("test") || lower.Contains("git") || lower.Contains("run"))
        {
            if (lower.Contains("run") || lower.Contains("execute") || lower.Contains("shell"))
            {
                return ("dev_run_command", SparkToolRiskLevel.High, true, args);
            }
            if (lower.Contains("test") || lower.Contains("build"))
            {
                return ("dev_build_test", SparkToolRiskLevel.Medium, false, args);
            }
            return ("dev_git_status", SparkToolRiskLevel.Low, false, args);
        }

        // 8. Browser / Search
        if (lower.Contains("search") || lower.Contains("find") || lower.Contains("browse") || lower.Contains("കണ്ടെത്തൂ") || lower.Contains("തിരയുക"))
        {
            return ("browser_search", SparkToolRiskLevel.Low, false, args);
        }

        // Default: General reasoning
        return ("agent_general_reasoning", SparkToolRiskLevel.Low, false, args);
    }
}
