namespace Template.Domain;

public sealed class TodoItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; set; }
    public bool IsCompleted { get; set; }
}
