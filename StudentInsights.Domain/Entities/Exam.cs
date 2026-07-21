using StudentInsights.Domain.Common;
using StudentInsights.Domain.ValueObjects;

namespace StudentInsights.Domain.Entities;

public class Exam : BaseEntity
{
    private Exam()
    {
    } // EF Core

    private Exam(Guid userId, Guid courseId, string title, DateTime examDateUtc, string? description)
    {
        UserId = userId;
        CourseId = courseId;
        Title = title;
        ExamDateUtc = examDateUtc;
        Description = description;
    }

    public static Exam Create(Course course, string title, DateTime examDateUtc, string? description = null)
    {
        if (course is null)
            throw new DomainException("Course is required.");
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Title is required.");

        return new Exam(course.UserId, course.Id, title.Trim(), examDateUtc, description?.Trim());
    }

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public Guid CourseId { get; private set; }

    public Course Course { get; private set; } = null!;

    /// <summary>Exam title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Exam date and time (UTC).</summary>
    public DateTime ExamDateUtc { get; private set; }

    /// <summary>Final exam grade (null until graded).</summary>
    public Grade? Grade { get; private set; }

    /// <summary>Optional notes.</summary>
    public string? Description { get; private set; }

    public void Reschedule(DateTime examDateUtc)
    {
        ExamDateUtc = examDateUtc;
        MarkModified();
    }

    public void RecordGrade(decimal grade)
    {
        Grade = new Grade(grade);
        MarkModified();
    }
}