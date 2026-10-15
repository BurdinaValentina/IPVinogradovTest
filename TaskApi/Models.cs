using System.ComponentModel.DataAnnotations;

namespace TaskApi;

public enum Priority { Low, Medium, High }

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;

    // System column PostgreSQL for optimistic lock
    public uint Xmin { get; set; }
}

public record TaskCompletedEvent(Guid TaskId, string Title, DateTimeOffset CompletedAt, Priority Priority);

public record CreateTaskRequest(string Title, Priority Priority = Priority.Medium);