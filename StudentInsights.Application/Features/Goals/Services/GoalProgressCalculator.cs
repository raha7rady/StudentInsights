using StudentInsights.Application.Features.Goals.Enums;
using StudentInsights.Domain.Entities;
using StudentInsights.Domain.Enums;

namespace StudentInsights.Application.Features.Goals.Services;

/// <summary>
/// Pure, static, side-effect-free progress calculation, one branch per
/// GoalType. Static rather than an injected interface -- deliberately:
/// it mirrors the project's existing convention for pure logic
/// (*MappingExtensions), has no second implementation to swap, and a
/// pure function needs no mock to unit test. Shared by Dashboard today
/// and any future standalone Goals module, since neither owns it.
/// </summary>
public static class GoalProgressCalculator
{
    public static GoalProgressResult CalculateProgress(Goal goal, GoalProgressInputs inputs)
    {
        return goal.Type switch
        {
            GoalType.GradePointAverage => CalculateGpaProgress(goal, inputs),
            GoalType.StudyHours => CalculateStudyHoursProgress(goal, inputs),
            GoalType.ProjectDeadline => CalculateProjectDeadlineProgress(inputs),
            GoalType.ChapterCount => CalculateGenericProgress(goal),
            _ => new GoalProgressResult(GoalProgressStatus.NotSupportedYet, null)
        };
    }

    private static GoalProgressResult CalculateGpaProgress(Goal goal, GoalProgressInputs inputs)
    {
        if (inputs.CreditWeightedGpa is not { } gpa)
            return new GoalProgressResult(GoalProgressStatus.NotYetAvailable, null);

        return new GoalProgressResult(GoalProgressStatus.Available, ClampPercentage(gpa / goal.TargetValue * 100m));
    }

    private static GoalProgressResult CalculateStudyHoursProgress(Goal goal, GoalProgressInputs inputs)
    {
        var hoursLogged = inputs.StudyMinutesLoggedSinceGoalCreated / 60m;
        return new GoalProgressResult(GoalProgressStatus.Available, ClampPercentage(hoursLogged / goal.TargetValue * 100m));
    }

    private static GoalProgressResult CalculateProjectDeadlineProgress(GoalProgressInputs inputs)
    {
        if (inputs.RelatedActivityStatus is not { } status)
            return new GoalProgressResult(GoalProgressStatus.NotYetAvailable, null);

        var percentage = status switch
        {
            ActivityStatus.NotStarted => 0m,
            ActivityStatus.InProgress => 50m,
            ActivityStatus.Completed => 100m,
            _ => 0m
        };

        return new GoalProgressResult(GoalProgressStatus.Available, percentage);
    }

    private static GoalProgressResult CalculateGenericProgress(Goal goal)
    {
        return new GoalProgressResult(GoalProgressStatus.Available, ClampPercentage(goal.CurrentValue / goal.TargetValue * 100m));
    }

    private static decimal ClampPercentage(decimal percentage) => Math.Clamp(percentage, 0m, 100m);
}