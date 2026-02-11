using Template.Domain;

namespace Template.Application;

public sealed class TodoService(ITodoRepository repository)
{
    public Task<IReadOnlyCollection<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default)
        => repository.GetAllAsync(cancellationToken);

    public Task<TodoItem> AddAsync(string title, CancellationToken cancellationToken = default)
        => repository.AddAsync(new TodoItem { Title = title }, cancellationToken);
}
