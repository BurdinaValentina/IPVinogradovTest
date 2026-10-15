using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions; // КРИТИЧЕСКИ ВАЖНО для RemoveAll!
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using TaskApi;

namespace TaskApi.Tests;

public class MockRabbitMqPublisher : IRabbitMqPublisher
{
    public TaskCompletedEvent? LastPublishedEvent { get; private set; }
    public int PublishCount { get; private set; }

    public Task PublishAsync(TaskCompletedEvent evt, CancellationToken ct)
    {
        LastPublishedEvent = evt;
        PublishCount++;
        return Task.CompletedTask;
    }
}

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public MockRabbitMqPublisher MockRabbitMq { get; } = new MockRabbitMqPublisher();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 1. ЭТО КРИТИЧЕСКИ ВАЖНО: задаём окружение ДО того, как сработает Program.cs
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // 2. На всякий случай удаляем, если вдруг проскочило
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TaskDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // 3. Добавляем InMemory
            services.AddDbContext<TaskDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));

            // 4. Заменяем RabbitMQ на мок
            services.Replace(ServiceDescriptor.Singleton<IRabbitMqPublisher>(MockRabbitMq));
        });
    }
}

[TestFixture]
public class TaskApiIntegrationTests
{
    private TestWebApplicationFactory _factory = null!;

    [SetUp]
    public void Setup()
    {
        _factory = new TestWebApplicationFactory();
    }

    [TearDown]
    public void Teardown()
    {
        _factory?.Dispose();
    }

    [Test]
    public async Task CompleteTask_ShouldSaveToDbAndPublishToRabbitMq()
    {
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/tasks", new { title = "Buy milk", priority = 1 });
        createResponse.EnsureSuccessStatusCode();
        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

        var completeResponse = await client.PutAsync($"/tasks/{createdTask!.Id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();

        Assert.That(_factory.MockRabbitMq.PublishCount, Is.EqualTo(1));
        Assert.That(_factory.MockRabbitMq.LastPublishedEvent, Is.Not.Null);
        Assert.That(_factory.MockRabbitMq.LastPublishedEvent!.TaskId, Is.EqualTo(createdTask.Id));
        Assert.That(_factory.MockRabbitMq.LastPublishedEvent.Title, Is.EqualTo("Buy milk"));
    }

    [Test]
    public async Task CreateTask_WithInvalidTitle_ShouldReturn400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/tasks", new { title = "" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CompleteTask_AlreadyCompleted_ShouldReturn409()
    {
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/tasks", new { title = "Test task" });
        createResponse.EnsureSuccessStatusCode();
        var createdTask = await createResponse.Content.ReadFromJsonAsync<TaskItem>();

        var completeResponse1 = await client.PutAsync($"/tasks/{createdTask!.Id}/complete", null);
        Assert.That(completeResponse1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var completeResponse2 = await client.PutAsync($"/tasks/{createdTask.Id}/complete", null);
        Assert.That(completeResponse2.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }
}