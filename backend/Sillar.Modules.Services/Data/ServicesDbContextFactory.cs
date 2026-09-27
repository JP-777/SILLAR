using Microsoft.EntityFrameworkCore.Design;
using Sillar.Shared.Data.Modularity;

namespace Sillar.Modules.Services.Data;
public sealed class ServicesDbContextFactory : IDesignTimeDbContextFactory<ServicesDbContext>
{
    public ServicesDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=sillar_dev;Username=sillar;Password=sillar_dev";
        return new ServicesDbContext(PersistenciaDeModulo.Opciones<ServicesDbContext>(connection,
            ServicesDbContext.Schema, ServicesDbContext.MigrationsHistoryTable));
    }
}
