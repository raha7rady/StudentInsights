namespace StudentInsights.Application.Common.Models;

/// <summary>
/// Shared paging input for every list Query in the project (Courses now,
/// LearningActivities/Exams/etc. later), so paging rules aren't redefined
/// per module. Defensive clamping happens here as a last-resort safety
/// net; per-module Validators (FluentValidation) are still the primary
/// place to reject out-of-range input with a proper 400 response.
/// </summary>
public class PaginationParams
{
    private const int MaxPageSize = 50;
    private const int DefaultPageSize = 10;

    private int _pageNumber = 1;
    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }
}