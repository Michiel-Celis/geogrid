using Geogrid.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Geogrid.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGeogridInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<GeogridDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));
        return services;
    }
}
