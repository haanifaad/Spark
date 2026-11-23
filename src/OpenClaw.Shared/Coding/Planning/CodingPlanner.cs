using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenClaw.Shared.Coding.Models;

namespace OpenClaw.Shared.Coding.Planning;

/// <summary>
/// Autonomous coding planner responsible for breaking complex development goals into
/// structured, verifiable execution plans and tracking task state transitions.
/// </summary>
public sealed class CodingPlanner
{
    /// <summary>
    /// Creates an initial plan based on user goal, tech stack, and detected repository files.
    /// </summary>
    public CodingPlan CreatePlan(string goal, RepositoryAnalysis analysis)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);

        var steps = new List<CodingPlanStep>();
        var normalizedGoal = goal.Trim();

        // 1. Analyze existing code step
        steps.Add(new CodingPlanStep
        {
            Id = "step_1_inspect",
            Title = "Inspect Project Architecture & Context",
            Description = $"Review existing {analysis.ProjectName} components, dependencies, and configuration.",
            Status = PlanStepStatus.Pending,
            TargetFiles = analysis.EntryPoints.Concat(analysis.PackageConfigFiles).Take(3).ToList()
        });

        // Heuristic task planning based on user goal keywords
        if (normalizedGoal.Contains("test", StringComparison.OrdinalIgnoreCase))
        {
            steps.Add(new CodingPlanStep
            {
                Id = "step_2_identify_tests",
                Title = "Identify Modules Requiring Tests",
                Description = "Locate source files, methods, and edge cases to test.",
                Status = PlanStepStatus.Pending
            });
            steps.Add(new CodingPlanStep
            {
                Id = "step_3_implement_tests",
                Title = "Author Unit & Integration Tests",
                Description = "Write comprehensive test cases verifying expected behaviors.",
                Status = PlanStepStatus.Pending
            });
            steps.Add(new CodingPlanStep
            {
                Id = "step_4_run_tests",
                Title = "Execute Test Suite",
                Description = "Run project test runner and verify all tests pass cleanly.",
                Status = PlanStepStatus.Pending
            });
        }
        else if (normalizedGoal.Contains("fix", StringComparison.OrdinalIgnoreCase) ||
                 normalizedGoal.Contains("bug", StringComparison.OrdinalIgnoreCase) ||
                 normalizedGoal.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            steps.Add(new CodingPlanStep
            {
                Id = "step_2_locate_root_cause",
                Title = "Locate Failing Code & Trace Errors",
                Description = "Identify failing functions, exception causes, or incorrect logic paths.",
                Status = PlanStepStatus.Pending
            });
            steps.Add(new CodingPlanStep
            {
                Id = "step_3_apply_fix",
                Title = "Apply Corrective Code Edits",
                Description = "Modify source files to resolve bug without introducing regressions.",
                Status = PlanStepStatus.Pending
            });
            steps.Add(new CodingPlanStep
            {
                Id = "step_4_verify_fix",
                Title = "Validate Resolution & Build",
                Description = "Run compiler, linter, or tests to verify defect is resolved.",
                Status = PlanStepStatus.Pending
            });
        }
        else if (normalizedGoal.Contains("refactor", StringComparison.OrdinalIgnoreCase) ||
                 normalizedGoal.Contains("clean", StringComparison.OrdinalIgnoreCase))
        {
            steps.Add(new CodingPlanStep
            {
                Id = "step_2_plan_refactoring",
                Title = "Analyze Dependencies & Coupling",
                Description = "Map existing callers and identify clean abstraction boundaries.",
                Status = PlanStepStatus.Pending
            });
            steps.Add(new CodingPlanStep
            {
                Id = "step_3_refactor_code",
                Title = "Apply Clean Code Refactoring",
                Description = "Restructure classes and methods while preserving public contract semantics.",
                Status = PlanStepStatus.Pending
            });
            steps.Add(new CodingPlanStep
            {
                Id = "step_4_verify_build",
                Title = "Verify Build & Regression Check",
                Description = "Ensure all projects compile cleanly with zero diagnostic regressions.",
                Status = PlanStepStatus.Pending
            });
        }
        else
        {
            // General feature implementation plan
            steps.Add(new CodingPlanStep
            {
                Id = "step_2_define_contracts",
                Title = "Design Models, Interfaces & Contracts",
                Description = "Define data models, API signatures, and component contracts.",
                Status = PlanStepStatus.Pending
            });
            steps.Add(new CodingPlanStep
            {
                Id = "step_3_implement_logic",
                Title = "Implement Core Functionality",
                Description = "Generate and update target source files to fulfill requested features.",
                Status = PlanStepStatus.Pending
            });
            steps.Add(new CodingPlanStep
            {
                Id = "step_4_build_and_validate",
                Title = "Validate Build & Run Diagnostics",
                Description = "Compile workspace and verify type safety and error-free execution.",
                Status = PlanStepStatus.Pending
            });
        }

        // Final review & summary step
        steps.Add(new CodingPlanStep
        {
            Id = $"step_{steps.Count + 1}_review",
            Title = "Review Changes & Summarize",
            Description = "Review unified diffs, ensure no regressions, and present completed work.",
            Status = PlanStepStatus.Pending
        });

        return new CodingPlan
        {
            Goal = goal,
            Steps = steps,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Updates the status of a specific step in the plan.
    /// </summary>
    public CodingPlan UpdateStepStatus(
        CodingPlan plan,
        string stepId,
        PlanStepStatus status,
        string? resultSummary = null)
    {
        var updatedSteps = plan.Steps.Select(step =>
        {
            if (string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase))
            {
                return step with
                {
                    Status = status,
                    ResultSummary = resultSummary ?? step.ResultSummary,
                    StartedAt = status == PlanStepStatus.InProgress ? DateTimeOffset.UtcNow : step.StartedAt,
                    CompletedAt = status is PlanStepStatus.Completed or PlanStepStatus.Failed ? DateTimeOffset.UtcNow : step.CompletedAt
                };
            }
            return step;
        }).ToList();

        return plan with
        {
            Steps = updatedSteps,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
