using System.Collections.Generic;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Actions;

/// <summary>
/// Contract for parsing VLM predictions and model reasoning into structured computer use actions.
/// </summary>
public interface IComputerUseActionParser
{
    /// <summary>
    /// Parses raw model completion text into thoughts and executable actions.
    /// </summary>
    PredictionOutput ParsePrediction(string rawPrediction, int screenWidth = 1920, int screenHeight = 1080, double scaleFactor = 1.0);

    /// <summary>
    /// Parses a single action expression string (e.g. "click(start_box='[100, 200, 300, 400]')").
    /// </summary>
    ComputerUseAction? ParseSingleAction(string actionExpression);

    /// <summary>
    /// Parses normalized bounding box numbers string into a <see cref="BoundingBox"/>.
    /// </summary>
    BoundingBox? ParseBoundingBox(string? boxStr);
}
