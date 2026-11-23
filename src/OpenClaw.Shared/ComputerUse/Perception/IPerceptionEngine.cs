using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using OpenClaw.Shared.ComputerUse.Types;

namespace OpenClaw.Shared.ComputerUse.Perception;

/// <summary>
/// Perception Engine contract responsible for screenshot observation, accessibility tree extraction,
/// semantic element grounding, and coordinate resolution.
/// </summary>
public interface IPerceptionEngine
{
    /// <summary>
    /// Captures a complete perception snapshot (screenshot + UI tree + active window).
    /// </summary>
    Task<PerceptionSnapshot> CapturePerceptionAsync(
        int monitorIndex = 0,
        int maxWidth = 1920,
        bool inspectAccessibility = true,
        CancellationToken ct = default);

    /// <summary>
    /// Grounds a predicted bounding box or query against semantic UI elements, falling back to visual coordinates.
    /// </summary>
    Point GroundToLogicalPoint(BoundingBox? box, PerceptionSnapshot snapshot, string? semanticQuery = null);

    /// <summary>
    /// Finds UI elements matching a textual query or accessibility role in the active perception snapshot.
    /// </summary>
    IReadOnlyList<PerceptionElement> QueryElements(PerceptionSnapshot snapshot, string query, string? requiredRole = null);
}
