using Template.Domain;

namespace Template.Application;

public interface ITodoRepository
{
    Task<IReadOnlyCollection<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TodoItem> AddAsync(TodoItem item, CancellationToken cancellationToken = default);
}
