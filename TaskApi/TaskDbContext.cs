using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;

namespace TaskApi;

public class TaskDbContext : DbContext
{
    private bool _isNpgsql;

    public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options) { }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Проверяем, используется ли Npgsql провайдер
        _isNpgsql = optionsBuilder.Options.Extensions
            .Any(e => e is NpgsqlOptionsExtension);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Применяем Xmin ТОЛЬКО для реляционных баз (PostgreSQL). 
        // Для InMemory (в тестах) это условие false, и EF Core не упадет.
        if (_isNpgsql)
        {
            // Настраиваем Xmin как concurrency token для оптимистичной блокировки
            modelBuilder.Entity<TaskItem>()
            .Property(t => t.Xmin)
            .IsRowVersion();
        }
    }
}