using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Actions;

/// <summary>
/// High-performance, robust action parser adapting UI-TARS action grammar into SPARK's native Computer Use types.
/// Supports normalized coordinates [0..1000], thought chain extraction, and comprehensive GUI action spaces.
/// </summary>
public sealed class ComputerUseActionParser : IComputerUseActionParser
{
    private static readonly Regex ThoughtRegex = new(
        @"(?:Thought|Reflection):\s*([\s\S]+?)(?=\s*(?:Action[:：]|Action_Summary:|$))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ActionSummaryRegex = new(
        @"Action_Summary:\s*([\s\S]+?)(?=\s*Action[:：]|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ActionBlockRegex = new(
        @"Action[:：]\s*([\s\S]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex FunctionCallRegex = new(
        @"([a-zA-Z0-9_]+)\s*\(([\s\S]*?)\)",
        RegexOptions.Compiled);

    private static readonly Regex KeyValueArgRegex = new(
        @"([a-zA-Z0-9_]+)\s*=\s*(?:'([^']*)'|""([^""]*)""|(\[[^\]]*\]|\([^\)]*\)|[^\s,)]+))",
        RegexOptions.Compiled);

    public PredictionOutput ParsePrediction(string rawPrediction, int screenWidth = 1920, int screenHeight = 1080, double scaleFactor = 1.0)
    {
        if (string.IsNullOrWhiteSpace(rawPrediction))
        {
            return new PredictionOutput(string.Empty, null, Array.Empty<ComputerUseAction>(), rawPrediction ?? string.Empty);
        }

        string text = rawPrediction.Trim();
        string thought = string.Empty;
        string? reflection = null;
        string actionStr = text;

        // 1. Extract Thought / Reflection
        var thoughtMatch = ThoughtRegex.Match(text);
        if (thoughtMatch.Success)
        {
            thought = thoughtMatch.Groups[1].Value.Trim();
        }

        var summaryMatch = ActionSummaryRegex.Match(text);
        if (summaryMatch.Success)
        {
            var summary = summaryMatch.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(thought))
                thought = summary;
            else
                reflection = thought;
        }

        // 2. Extract Action block
        var actionMatch = ActionBlockRegex.Match(text);
        if (actionMatch.Success)
        {
            actionStr = actionMatch.Groups[1].Value.Trim();
        }

        // Clean markdown backticks if present
        actionStr = CleanMarkdownCodeBlocks(actionStr);

        // 3. Parse discrete action expressions
        var actions = new List<ComputerUseAction>();
        var matches = FunctionCallRegex.Matches(actionStr);

        if (matches.Count > 0)
        {
            foreach (Match m in matches)
            {
                var action = ParseFunctionMatch(m, thought, reflection);
                if (action != null)
                {
                    actions.Add(action);
                }
            }
        }
        else
        {
            // Try single expression parsing
            var single = ParseSingleAction(actionStr);
            if (single != null)
            {
                actions.Add(single);
            }
        }

        return new PredictionOutput(thought, reflection, actions, rawPrediction);
    }

    public ComputerUseAction? ParseSingleAction(string actionExpression)
    {
        if (string.IsNullOrWhiteSpace(actionExpression)) return null;

        var m = FunctionCallRegex.Match(actionExpression.Trim());
        if (!m.Success) return null;

        return ParseFunctionMatch(m, null, null);
    }

    private ComputerUseAction? ParseFunctionMatch(Match match, string? thought, string? reflection)
    {
        string funcName = match.Groups[1].Value.Trim().ToLowerInvariant();
        string argsStr = match.Groups[2].Value.Trim();

        var argsDict = ParseArguments(argsStr);

        var actionType = funcName switch
        {
            "click" or "left_click" or "left_single" => ComputerUseActionType.Click,
            "left_double" or "double_click" or "doubleclick" => ComputerUseActionType.DoubleClick,
            "right_click" or "right_single" or "context_click" => ComputerUseActionType.RightClick,
            "middle_click" => ComputerUseActionType.MiddleClick,
            "drag" or "left_click_drag" or "select" => ComputerUseActionType.Drag,
            "mouse_move" or "hover" or "move" => ComputerUseActionType.MouseMove,
            "type" or "input" or "write" => ComputerUseActionType.TypeText,
            "hotkey" or "key_combination" => ComputerUseActionType.Hotkey,
            "press" => ComputerUseActionType.KeyPress,
            "release" => ComputerUseActionType.KeyRelease,
            "scroll" => ComputerUseActionType.Scroll,
            "wait" or "sleep" => ComputerUseActionType.Wait,
            "finished" or "done" => ComputerUseActionType.Finished,
            "call_user" or "request_user" => ComputerUseActionType.CallUser,
            "user_stop" or "stop" => ComputerUseActionType.UserStop,
            _ => ComputerUseActionType.Unknown
        };

        // Parse StartBox and EndBox
        BoundingBox? startBox = null;
        if (argsDict.TryGetValue("start_box", out var sb) || argsDict.TryGetValue("box", out sb) || argsDict.TryGetValue("point", out sb))
        {
            startBox = ParseBoundingBox(sb);
        }

        BoundingBox? endBox = null;
        if (argsDict.TryGetValue("end_box", out var eb) || argsDict.TryGetValue("to", out eb))
        {
            endBox = ParseBoundingBox(eb);
        }

        // Text content
        string? textContent = null;
        if (argsDict.TryGetValue("content", out var c) || argsDict.TryGetValue("text", out c) || argsDict.TryGetValue("value", out c))
        {
            textContent = UnescapeText(c);
        }
        else if (actionType == ComputerUseActionType.TypeText && !string.IsNullOrWhiteSpace(argsStr) && !argsStr.Contains('='))
        {
            textContent = UnescapeText(argsStr.Trim('\'', '"'));
        }

        // Key
        string? key = null;
        if (argsDict.TryGetValue("key", out var k) || argsDict.TryGetValue("hotkey", out k))
        {
            key = k.Trim('\'', '"');
        }
        else if ((actionType == ComputerUseActionType.Hotkey || actionType == ComputerUseActionType.KeyPress || actionType == ComputerUseActionType.KeyRelease)
                 && !string.IsNullOrWhiteSpace(argsStr) && !argsStr.Contains('='))
        {
            key = argsStr.Trim('\'', '"');
        }

        // Scroll direction & amount
        var scrollDirection = ScrollDirection.Down;
        int scrollAmount = 500;
        if (argsDict.TryGetValue("direction", out var dir))
        {
            scrollDirection = dir.ToLowerInvariant() switch
            {
                "up" => ScrollDirection.Up,
                "left" => ScrollDirection.Left,
                "right" => ScrollDirection.Right,
                _ => ScrollDirection.Down
            };
        }
        if (argsDict.TryGetValue("amount", out var amtStr) && int.TryParse(amtStr, out var amt))
        {
            scrollAmount = Math.Abs(amt);
        }

        return new ComputerUseAction
        {
            ActionType = actionType,
            RawInputs = argsDict,
            StartBox = startBox,
            EndBox = endBox,
            TextContent = textContent,
            Key = key,
            Direction = scrollDirection,
            ScrollAmount = scrollAmount,
            Thought = thought,
            Reflection = reflection
        };
    }

    private static Dictionary<string, string> ParseArguments(string argsStr)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(argsStr)) return dict;

        var matches = KeyValueArgRegex.Matches(argsStr);
        foreach (Match m in matches)
        {
            string key = m.Groups[1].Value.Trim();
            string val = m.Groups[2].Success ? m.Groups[2].Value :
                         m.Groups[3].Success ? m.Groups[3].Value :
                         m.Groups[4].Success ? m.Groups[4].Value : string.Empty;
            dict[key] = val.Trim();
        }

        return dict;
    }

    public BoundingBox? ParseBoundingBox(string? boxStr)
    {
        if (string.IsNullOrWhiteSpace(boxStr)) return null;

        // Strip brackets, parens, quotes
        var cleaned = boxStr.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "").Replace("'", "").Replace("\"", "").Trim();
        var parts = cleaned.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        var numbers = new List<double>();
        foreach (var p in parts)
        {
            if (double.TryParse(p.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            {
                numbers.Add(val);
            }
        }

        if (numbers.Count < 2) return null;

        double x1 = numbers[0];
        double y1 = numbers[1];
        double x2 = numbers.Count >= 3 ? numbers[2] : x1;
        double y2 = numbers.Count >= 4 ? numbers[3] : y1;

        // If coordinates were given in 0.0..1.0 range, scale to 0..1000
        if (x1 <= 1.0 && y1 <= 1.0 && x2 <= 1.0 && y2 <= 1.0 && (x1 > 0 || y1 > 0 || x2 > 0 || y2 > 0))
        {
            x1 *= 1000.0;
            y1 *= 1000.0;
            x2 *= 1000.0;
            y2 *= 1000.0;
        }

        return new BoundingBox(
            Math.Min(x1, x2),
            Math.Min(y1, y2),
            Math.Max(x1, x2),
            Math.Max(y1, y2));
    }

    private static string CleanMarkdownCodeBlocks(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var lines = text.Split('\n');
        var cleanedLines = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal)) continue;
            cleanedLines.Add(line);
        }
        return string.Join('\n', cleanedLines).Trim();
    }

    private static string UnescapeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text
            .Replace(@"\n", "\n")
            .Replace(@"\r", "\r")
            .Replace(@"\t", "\t")
            .Replace(@"\\", @"\");
    }
}
