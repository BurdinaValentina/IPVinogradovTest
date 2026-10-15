using Microsoft.EntityFrameworkCore;
using TaskApi;

var builder = WebApplication.CreateBuilder(args);

// 1. PostgreSQL (в тестах будет перехвачен и заменЄн на InMemory)
if (builder.Environment.EnvironmentName != "Testing")
{
    builder.Services.AddDbContext<TaskDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// 2. RabbitMQ
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<RabbitMqShutdownService>();

var app = builder.Build();

// === API Endpoints ===

// POST /tasks Ч создание задачи
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

// GET /tasks Ч получить все задачи
app.MapGet("/tasks", async (TaskDbContext db) =>
{
    var tasks = await db.Tasks.ToListAsync();
    return Results.Ok(tasks);
});

// PUT /tasks/{id}/complete Ч завершить задачу
app.MapPut("/tasks/{id:guid}/complete", async (Guid id, TaskDbContext db, IRabbitMqPublisher rabbitMq, ILogger<Program> logger) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task == null)
    {
        return Results.NotFound();
    }

    // Ѕыстра€ проверка на уровне приложени€ (работает и в тестах, и в проде)
    if (task.IsCompleted)
    {
        return Results.Conflict("Task is already completed");
    }

    task.IsCompleted = true;
    task.CompletedAt = DateTimeOffset.UtcNow;

    try
    {
        // Ќадежна€ проверка на уровне Ѕƒ через xmin (только в PostgreSQL)
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        logger.LogWarning("Concurrency conflict while completing task {TaskId}", id);
        return Results.Conflict("Task was already modified or completed by another request");
    }

    // ѕубликуем в RabbitMQ ѕќ—Ћ≈ успешного сохранени€ в Ѕƒ
    _ = Task.Run(async () =>
    {
        var evt = new TaskCompletedEvent(task.Id, task.Title, task.CompletedAt.Value, task.Priority);
        await rabbitMq.PublishAsync(evt, CancellationToken.None);
    });

    return Results.Ok(task);
});

// DELETE /tasks/{id} Ч удалить задачу
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

// ќб€зательно дл€ WebApplicationFactory<Program> в тестах
public partial class Program { }