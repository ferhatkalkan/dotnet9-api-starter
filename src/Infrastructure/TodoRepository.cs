using Microsoft.EntityFrameworkCore;
using Template.Application;
using Template.Domain;

namespace Template.Infrastructure;

public sealed class TodoRepository(AppDbContext dbContext) : ITodoRepository
{
    public async Task<IReadOnlyCollection<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => await dbContext.Todos.OrderBy(x => x.Title).ToListAsync(cancellationToken);

    public async Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        await dbContext.Todos.AddAsync(item, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return item;
    }
}
