using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Template.Application;

namespace Template.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("template-db"));
        services.AddScoped<ITodoRepository, TodoRepository>();
        return services;
    }
}
