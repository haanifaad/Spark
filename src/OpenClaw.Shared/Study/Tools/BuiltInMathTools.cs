using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.Study.Contracts;

namespace OpenClaw.Shared.Study.Tools;

/// <summary>
/// Arithmetic calculator supporting +, -, *, /, %, ^, parentheses and decimal calculations.
/// </summary>
public sealed class CalculatorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("expression", out var exprObj) || exprObj is not string expression || string.IsNullOrWhiteSpace(expression))
        {
            throw new ArgumentException("Missing required parameter 'expression'.");
        }

        var cleaned = expression.Trim();
        // Check for safe arithmetic characters only
        if (!Regex.IsMatch(cleaned, @"^[0-9\+\-\*\/\%\^\(\)\.\s]+$"))
        {
            throw new InvalidOperationException($"Invalid or unsafe arithmetic expression: '{expression}'.");
        }

        // Handle power operator ^ by converting e.g. a^b to Math.Pow or computing
        var result = EvaluateMathExpression(cleaned);
        return Task.FromResult<object?>(new
        {
            expression = cleaned,
            result = result,
            formatted = string.Format(CultureInfo.InvariantCulture, "{0:G12}", result)
        });
    }

    private static double EvaluateMathExpression(string expr)
    {
        // Simple shunting-yard or DataTable compute for basic arithmetic
        // Convert ^ to power if present
        if (expr.Contains('^'))
        {
            expr = Regex.Replace(expr, @"(\d+(\.\d+)?)\s*\^\s*(\d+(\.\d+)?)", match =>
            {
                var b = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var exp = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                return Math.Pow(b, exp).ToString(CultureInfo.InvariantCulture);
            });
        }

        using var table = new DataTable { Locale = CultureInfo.InvariantCulture };
        var val = table.Compute(expr, string.Empty);
        return Convert.ToDouble(val, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Scientific calculator supporting trigonometric, logarithmic, square root, and exponential functions.
/// </summary>
public sealed class ScientificCalculatorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("operation", out var opObj) || opObj is not string operation || string.IsNullOrWhiteSpace(operation))
        {
            throw new ArgumentException("Missing required parameter 'operation'. Examples: sin, cos, tan, sqrt, log, ln, exp, abs, pow.");
        }

        var op = operation.Trim().ToLowerInvariant();
        double val = 0;
        if (arguments.TryGetValue("value", out var valObj) && valObj != null)
        {
            val = Convert.ToDouble(valObj, CultureInfo.InvariantCulture);
        }

        double? val2 = null;
        if (arguments.TryGetValue("value2", out var val2Obj) && val2Obj != null)
        {
            val2 = Convert.ToDouble(val2Obj, CultureInfo.InvariantCulture);
        }

        bool isDegree = arguments.TryGetValue("useDegrees", out var degObj) && degObj is bool b && b;

        double result = op switch
        {
            "sin" => Math.Sin(isDegree ? val * Math.PI / 180.0 : val),
            "cos" => Math.Cos(isDegree ? val * Math.PI / 180.0 : val),
            "tan" => Math.Tan(isDegree ? val * Math.PI / 180.0 : val),
            "asin" => isDegree ? Math.Asin(val) * 180.0 / Math.PI : Math.Asin(val),
            "acos" => isDegree ? Math.Acos(val) * 180.0 / Math.PI : Math.Acos(val),
            "atan" => isDegree ? Math.Atan(val) * 180.0 / Math.PI : Math.Atan(val),
            "sqrt" => val < 0 ? throw new ArgumentException("Square root of negative number is undefined in real numbers.") : Math.Sqrt(val),
            "cbrt" => Math.Cbrt(val),
            "log" => val <= 0 ? throw new ArgumentException("Logarithm of non-positive number is undefined.") : Math.Log10(val),
            "ln" => val <= 0 ? throw new ArgumentException("Natural logarithm of non-positive number is undefined.") : Math.Log(val),
            "exp" => Math.Exp(val),
            "abs" => Math.Abs(val),
            "pow" => Math.Pow(val, val2 ?? 2.0),
            _ => throw new NotSupportedException($"Scientific operation '{operation}' is not supported.")
        };

        return Task.FromResult<object?>(new
        {
            operation = op,
            input = val,
            input2 = val2,
            useDegrees = isDegree,
            result = result,
            formatted = string.Format(CultureInfo.InvariantCulture, "{0:G12}", result)
        });
    }
}

/// <summary>
/// Unit converter for length, mass, temperature, time, and digital storage.
/// </summary>
public sealed class UnitConverterTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("value", out var valObj) || valObj == null)
            throw new ArgumentException("Missing parameter 'value'.");

        var value = Convert.ToDouble(valObj, CultureInfo.InvariantCulture);
        var fromUnit = (arguments.TryGetValue("from", out var f) ? f?.ToString() : null)?.Trim().ToLowerInvariant()
            ?? throw new ArgumentException("Missing parameter 'from' unit.");
        var toUnit = (arguments.TryGetValue("to", out var t) ? t?.ToString() : null)?.Trim().ToLowerInvariant()
            ?? throw new ArgumentException("Missing parameter 'to' unit.");

        var result = ConvertUnits(value, fromUnit, toUnit, out var category);

        return Task.FromResult<object?>(new
        {
            category,
            from = fromUnit,
            to = toUnit,
            originalValue = value,
            convertedValue = result,
            summary = $"{value} {fromUnit} = {result:G8} {toUnit}"
        });
    }

    private static double ConvertUnits(double val, string from, string to, out string category)
    {
        // Length in meters
        var lengthMeters = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["m"] = 1.0, ["meter"] = 1.0, ["meters"] = 1.0,
            ["km"] = 1000.0, ["kilometer"] = 1000.0,
            ["cm"] = 0.01, ["centimeter"] = 0.01,
            ["mm"] = 0.001, ["millimeter"] = 0.001,
            ["mi"] = 1609.344, ["mile"] = 1609.344, ["miles"] = 1609.344,
            ["ft"] = 0.3048, ["foot"] = 0.3048, ["feet"] = 0.3048,
            ["in"] = 0.0254, ["inch"] = 0.0254, ["inches"] = 0.0254
        };

        if (lengthMeters.TryGetValue(from, out var fromFactor) && lengthMeters.TryGetValue(to, out var toFactor))
        {
            category = "Length";
            return (val * fromFactor) / toFactor;
        }

        // Mass in grams
        var massGrams = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["g"] = 1.0, ["gram"] = 1.0, ["grams"] = 1.0,
            ["kg"] = 1000.0, ["kilogram"] = 1000.0,
            ["mg"] = 0.001, ["milligram"] = 0.001,
            ["lb"] = 453.59237, ["pound"] = 453.59237, ["pounds"] = 453.59237,
            ["oz"] = 28.3495, ["ounce"] = 28.3495, ["ounces"] = 28.3495
        };

        if (massGrams.TryGetValue(from, out var fromMass) && massGrams.TryGetValue(to, out var toMass))
        {
            category = "Mass";
            return (val * fromMass) / toMass;
        }

        // Temperature
        if ((from is "c" or "celsius" or "f" or "fahrenheit" or "k" or "kelvin") &&
            (to is "c" or "celsius" or "f" or "fahrenheit" or "k" or "kelvin"))
        {
            category = "Temperature";
            double kelvin = from switch
            {
                "c" or "celsius" => val + 273.15,
                "f" or "fahrenheit" => (val - 32.0) * 5.0 / 9.0 + 273.15,
                _ => val
            };

            return to switch
            {
                "c" or "celsius" => kelvin - 273.15,
                "f" or "fahrenheit" => (kelvin - 273.15) * 9.0 / 5.0 + 32.0,
                _ => kelvin
            };
        }

        // Digital in bytes
        var digitalBytes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["b"] = 1.0, ["byte"] = 1.0,
            ["kb"] = 1024.0, ["kilobyte"] = 1024.0,
            ["mb"] = 1024.0 * 1024.0, ["megabyte"] = 1024.0 * 1024.0,
            ["gb"] = 1024.0 * 1024.0 * 1024.0, ["gigabyte"] = 1024.0 * 1024.0 * 1024.0,
            ["tb"] = 1024.0 * 1024.0 * 1024.0 * 1024.0, ["terabyte"] = 1024.0 * 1024.0 * 1024.0 * 1024.0
        };

        if (digitalBytes.TryGetValue(from, out var fromByte) && digitalBytes.TryGetValue(to, out var toByte))
        {
            category = "Digital";
            return (val * fromByte) / toByte;
        }

        throw new ArgumentException($"Cannot convert from '{from}' to '{to}'. Unknown or incompatible units.");
    }
}

/// <summary>
/// Equation evaluator and solver for linear (ax + b = 0) and quadratic (ax^2 + bx + c = 0) equations.
/// </summary>
public sealed class EquationEvaluatorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetValue("equation", out var eqObj) || eqObj is not string equation || string.IsNullOrWhiteSpace(equation))
            throw new ArgumentException("Missing parameter 'equation'.");

        var clean = equation.Replace(" ", "");

        // Quadratic: a*x^2 + b*x + c = 0
        var quadMatch = Regex.Match(clean, @"^([+-]?\d*(?:\.\d+)?)x\^2([+-]?\d*(?:\.\d+)?)x([+-]?\d*(?:\.\d+)?)=0$", RegexOptions.IgnoreCase);
        if (quadMatch.Success)
        {
            double a = ParseCoeff(quadMatch.Groups[1].Value, 1.0);
            double b = ParseCoeff(quadMatch.Groups[2].Value, 1.0);
            double c = ParseCoeff(quadMatch.Groups[3].Value, 0.0);

            double disc = (b * b) - (4 * a * c);
            if (disc > 0)
            {
                double r1 = (-b + Math.Sqrt(disc)) / (2 * a);
                double r2 = (-b - Math.Sqrt(disc)) / (2 * a);
                return Task.FromResult<object?>(new
                {
                    type = "Quadratic",
                    discriminant = disc,
                    roots = new[] { r1, r2 },
                    solution = $"Two real roots: x1 = {r1:G6}, x2 = {r2:G6}"
                });
            }
            if (Math.Abs(disc) < 1e-9)
            {
                double r = -b / (2 * a);
                return Task.FromResult<object?>(new
                {
                    type = "Quadratic",
                    discriminant = disc,
                    roots = new[] { r },
                    solution = $"One double real root: x = {r:G6}"
                });
            }

            return Task.FromResult<object?>(new
            {
                type = "Quadratic",
                discriminant = disc,
                solution = $"Complex conjugate roots: ({-b / (2 * a):G6} ± {Math.Sqrt(-disc) / (2 * a):G6}i)"
            });
        }

        // Linear: ax + b = 0 or ax = b
        var linMatch = Regex.Match(clean, @"^([+-]?\d*(?:\.\d+)?)x([+-]?\d*(?:\.\d+)?)=0$", RegexOptions.IgnoreCase);
        if (linMatch.Success)
        {
            double a = ParseCoeff(linMatch.Groups[1].Value, 1.0);
            double b = ParseCoeff(linMatch.Groups[2].Value, 0.0);
            if (Math.Abs(a) < 1e-9) throw new InvalidOperationException("Coefficient of x is zero; no unique solution.");

            double x = -b / a;
            return Task.FromResult<object?>(new
            {
                type = "Linear",
                root = x,
                solution = $"Linear solution: x = {x:G6}"
            });
        }

        return Task.FromResult<object?>(new
        {
            equation,
            type = "General",
            solution = $"Analyzed expression '{equation}'. Please specify standard form like 'ax^2+bx+c=0' or 'ax+b=0'."
        });
    }

    private static double ParseCoeff(string s, double defaultIfEmpty)
    {
        if (string.IsNullOrEmpty(s) || s == "+") return defaultIfEmpty;
        if (s == "-") return -defaultIfEmpty;
        return double.Parse(s, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Statistical summary calculations: mean, median, mode, variance, standard deviation, min, max, count.
/// </summary>
public sealed class StatisticsCalculatorTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        List<double> numbers = [];
        if (arguments.TryGetValue("data", out var dataObj))
        {
            if (dataObj is IEnumerable<object> list)
            {
                numbers = list.Select(o => Convert.ToDouble(o, CultureInfo.InvariantCulture)).ToList();
            }
            else if (dataObj is string str)
            {
                numbers = str.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(s => double.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToList();
            }
        }

        if (numbers.Count == 0)
        {
            throw new ArgumentException("Parameter 'data' must contain at least one numerical value.");
        }

        numbers.Sort();
        int count = numbers.Count;
        double sum = numbers.Sum();
        double mean = sum / count;
        double min = numbers[0];
        double max = numbers[^1];

        // Median
        double median = (count % 2 == 1)
            ? numbers[count / 2]
            : (numbers[(count / 2) - 1] + numbers[count / 2]) / 2.0;

        // Variance & StdDev
        double variance = numbers.Select(x => Math.Pow(x - mean, 2)).Sum() / count;
        double sampleVariance = count > 1 ? numbers.Select(x => Math.Pow(x - mean, 2)).Sum() / (count - 1) : 0;
        double stdDev = Math.Sqrt(variance);
        double sampleStdDev = Math.Sqrt(sampleVariance);

        // Mode
        var modeGroup = numbers.GroupBy(n => n).OrderByDescending(g => g.Count()).FirstOrDefault();
        double? mode = modeGroup != null && modeGroup.Count() > 1 ? modeGroup.Key : null;

        return Task.FromResult<object?>(new
        {
            count,
            sum,
            mean,
            median,
            mode,
            min,
            max,
            range = max - min,
            variance,
            sampleVariance,
            standardDeviation = stdDev,
            sampleStandardDeviation = sampleStdDev
        });
    }
}

/// <summary>
/// Matrix operations: addition, subtraction, multiplication, transpose, and determinant (2x2 and 3x3).
/// </summary>
public sealed class MatrixOperationsTool : IStudyToolHandler
{
    public Task<object?> InvokeAsync(IReadOnlyDictionary<string, object?> arguments, CancellationToken ct = default)
    {
        var op = (arguments.TryGetValue("operation", out var opObj) ? opObj?.ToString() : "determinant")?.ToLowerInvariant() ?? "determinant";

        double[][]? a = null;
        if (arguments.TryGetValue("matrixA", out var aObj))
        {
            if (aObj is double[][] direct) a = direct;
            else a = ParseMatrix(aObj);
        }

        if (a == null || a.Length == 0)
            throw new ArgumentException("Missing or invalid 'matrixA'. Must be a 2D array of numbers.");

        if (op == "transpose")
        {
            int r = a.Length, c = a[0].Length;
            var t = new double[c][];
            for (int j = 0; j < c; j++)
            {
                t[j] = new double[r];
                for (int i = 0; i < r; i++) t[j][i] = a[i][j];
            }
            return Task.FromResult<object?>(new { operation = op, result = t });
        }

        if (op == "determinant")
        {
            if (a.Length != a[0].Length) throw new InvalidOperationException("Determinant requires a square matrix.");
            double det = a.Length switch
            {
                1 => a[0][0],
                2 => (a[0][0] * a[1][1]) - (a[0][1] * a[1][0]),
                3 => (a[0][0] * ((a[1][1] * a[2][2]) - (a[1][2] * a[2][1]))) -
                     (a[0][1] * ((a[1][0] * a[2][2]) - (a[1][2] * a[2][0]))) +
                     (a[0][2] * ((a[1][0] * a[2][1]) - (a[1][1] * a[2][0]))),
                _ => throw new NotSupportedException("Determinant calculation is currently supported up to 3x3 matrices.")
            };
            return Task.FromResult<object?>(new { operation = op, determinant = det });
        }

        if (op is "add" or "subtract" or "multiply")
        {
            var b = ParseMatrix(arguments.TryGetValue("matrixB", out var bObj) ? bObj : null);
            if (b == null || b.Length == 0) throw new ArgumentException($"Operation '{op}' requires 'matrixB'.");

            if (op == "add")
            {
                if (a.Length != b.Length || a[0].Length != b[0].Length) throw new InvalidOperationException("Matrices must have identical dimensions for addition.");
                var res = new double[a.Length][];
                for (int i = 0; i < a.Length; i++)
                {
                    res[i] = new double[a[0].Length];
                    for (int j = 0; j < a[0].Length; j++) res[i][j] = a[i][j] + b[i][j];
                }
                return Task.FromResult<object?>(new { operation = op, result = res });
            }

            if (op == "subtract")
            {
                if (a.Length != b.Length || a[0].Length != b[0].Length) throw new InvalidOperationException("Matrices must have identical dimensions for subtraction.");
                var res = new double[a.Length][];
                for (int i = 0; i < a.Length; i++)
                {
                    res[i] = new double[a[0].Length];
                    for (int j = 0; j < a[0].Length; j++) res[i][j] = a[i][j] - b[i][j];
                }
                return Task.FromResult<object?>(new { operation = op, result = res });
            }

            if (op == "multiply")
            {
                if (a[0].Length != b.Length) throw new InvalidOperationException("Columns of matrixA must match rows of matrixB for multiplication.");
                var res = new double[a.Length][];
                for (int i = 0; i < a.Length; i++)
                {
                    res[i] = new double[b[0].Length];
                    for (int j = 0; j < b[0].Length; j++)
                    {
                        double s = 0;
                        for (int k = 0; k < a[0].Length; k++) s += a[i][k] * b[k][j];
                        res[i][j] = s;
                    }
                }
                return Task.FromResult<object?>(new { operation = op, result = res });
            }
        }

        throw new NotSupportedException($"Matrix operation '{op}' is not supported.");
    }

    private static double[][] ParseMatrix(object? obj)
    {
        if (obj is double[][] d) return d;
        if (obj is List<List<double>> list) return list.Select(r => r.ToArray()).ToArray();
        if (obj is IEnumerable<object> rows)
        {
            var parsed = new List<double[]>();
            foreach (var r in rows)
            {
                if (r is IEnumerable<object> cols)
                    parsed.Add(cols.Select(c => Convert.ToDouble(c, CultureInfo.InvariantCulture)).ToArray());
            }
            return parsed.ToArray();
        }
        return [];
    }
}
