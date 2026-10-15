using Microsoft.EntityFrameworkCore;
using TaskApi;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.EnvironmentName != "Testing")
{
    builder.Services.AddDbContext<TaskDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<RabbitMqShutdownService>();

var app = builder.Build();

// === API Endpoints ===

// POST /tasks — create task
app.MapPost("/tasks", async (CreateTaskRequest req, TaskDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
    {
        return Results.BadRequest(new { error = "Title must be between 1 and 200 characters." });
    }

    var task = new TaskItem
    {
        Title = req.Title.Trim(),
        Priority = req.Priority,
        IsCompleted = false,
        CreatedAt = DateTimeOffset.UtcNow,
        CompletedAt = null
    };

    db.Tasks.Add(task);
    await db.SaveChangesAsync();

    return Results.Created($"/tasks/{task.Id}", task);
});

// GET /tasks — get all tasks
app.MapGet("/tasks", async (TaskDbContext db) =>
{
    var tasks = await db.Tasks.ToListAsync();
    return Results.Ok(tasks);
});

// PUT /tasks/{id}/complete complete task
app.MapPut("/tasks/{id:guid}/complete", async (Guid id, TaskDbContext db, IRabbitMqPublisher rabbitMq, ILogger<Program> logger) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task == null)
    {
        return Results.NotFound();
    }

    if (task.IsCompleted)
    {
        return Results.Conflict("Task is already completed");
    }

    task.IsCompleted = true;
    task.CompletedAt = DateTimeOffset.UtcNow;

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        logger.LogWarning("Concurrency conflict while completing task {TaskId}", id);
        return Results.Conflict("Task was already modified or completed by another request");
    }

    _ = Task.Run(async () =>
    {
        var evt = new TaskCompletedEvent(task.Id, task.Title, task.CompletedAt.Value, task.Priority);
        await rabbitMq.PublishAsync(evt, CancellationToken.None);
    });

    return Results.Ok(task);
});

// DELETE /tasks/{id} — delete task
app.MapDelete("/tasks/{id:guid}", async (Guid id, TaskDbContext db) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task == null)
    {
        return Results.NotFound();
    }

    db.Tasks.Remove(task);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.Run();

// For WebApplicationFactory<Program> in tests
public partial class Program { }