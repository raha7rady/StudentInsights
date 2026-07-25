using MediatR;
using Microsoft.EntityFrameworkCore;
using StudentInsights.Application.Common.Exceptions;
using StudentInsights.Application.Common.Interfaces;
using StudentInsights.Application.Features.Goals.Common;
using StudentInsights.Application.Features.Goals.DTOs;
using StudentInsights.Application.Features.Goals.Mappings;
using StudentInsights.Application.Features.Goals.Services;
using StudentInsights.Domain.Entities;

namespace StudentInsights.Application.Features.Goals.Commands.CreateGoal;

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, GoalDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateGoalCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<GoalDto> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        // Goal.Create() takes the owning User entity, not a bare Guid --
        // same pattern CreatePersonalEventCommandHandler uses. UserId is
        // resolved from a validated JWT behind [Authorize], so a missing
        // row here would only indicate a deleted/corrupted account, still
        // surfaced as 404 for consistency with the rest of the project.
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId, cancellationToken);

        if (user is null)
            throw new NotFoundException($"User '{_currentUserService.UserId}' was not found.");

        // Same 404-for-both-cases reasoning as CreateLearningActivityCommandHandler's
        // course lookup: a missing activity and one owned by someone else
        // must be indistinguishable to the caller, or RelatedActivityId
        // becomes an enumerable resource (IDOR). CreateGoalCommandValidator
        // already guarantees this is only populated for ProjectDeadline
        // goals.
        LearningActivity? relatedActivity = null;
        if (request.RelatedActivityId is not null)
        {
            relatedActivity = await _context.LearningActivities
                .FirstOrDefaultAsync(la => la.Id == request.RelatedActivityId, cancellationToken);

            if (relatedActivity is null || relatedActivity.UserId != _currentUserService.UserId)
                throw new NotFoundException($"Learning activity '{request.RelatedActivityId}' was not found.");
        }

        var goal = Goal.Create(user, request.Type, request.TargetValue, request.TargetDateUtc, relatedActivity);

        _context.Goals.Add(goal);

        await _context.SaveChangesAsync(cancellationToken);

        var progressInputs = await GoalProgressInputsProvider.GetAsync(_context, user.Id, goal, cancellationToken);
        var progress = GoalProgressCalculator.CalculateProgress(goal, progressInputs);

        return goal.ToDto(progress);
    }
}