namespace Concorde.Infrastructure;

using Concorde.Application.Abstractions;
using Concorde.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ConcordeDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        return services;
    }
}
