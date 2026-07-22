namespace StudentInsights.Application.Common.Models;

/// <summary>
/// Generic paged response shape reused by every list Query in the project.
/// GetCoursesQuery returns PaginatedResult&lt;CourseDto&gt; directly instead
/// of a module-specific "PagedCourseResponse" wrapper, so the same shape
/// is reused for LearningActivities, Exams, etc. in later sprints.
/// </summary>
public class PaginatedResult<T>
{
    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages { get; }

    public PaginatedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}