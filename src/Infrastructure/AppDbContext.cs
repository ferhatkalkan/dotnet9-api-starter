using Microsoft.EntityFrameworkCore;
using Template.Domain;

namespace Template.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TodoItem> Todos => Set<TodoItem>();
}
