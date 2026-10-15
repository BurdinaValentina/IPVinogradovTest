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

        _isNpgsql = optionsBuilder.Options.Extensions
            .Any(e => e is NpgsqlOptionsExtension);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (_isNpgsql)
        {
            modelBuilder.Entity<TaskItem>()
            .Property(t => t.Xmin)
            .IsRowVersion();
        }
    }
}