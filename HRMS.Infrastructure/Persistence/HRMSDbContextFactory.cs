using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HRMS.Infrastructure.Persistence;

/// <summary>Provides a non-hosted context for migration tooling and clean-database setup.</summary>
public sealed class HRMSDbContextFactory : IDesignTimeDbContextFactory<HRMSDbContext>
{
    public HRMSDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var applicationDirectory = Directory.Exists(Path.Combine(currentDirectory, "HRMS"))
            ? Path.Combine(currentDirectory, "HRMS")
            : Path.GetFullPath(Path.Combine(currentDirectory, "..", "HRMS"));
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";
        var configurationBuilder = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(applicationDirectory, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(applicationDirectory, $"appsettings.{environment}.json"), optional: true);
        if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            configurationBuilder.AddUserSecrets("hrms-local-development");
        var configuration = configurationBuilder.AddEnvironmentVariables().Build();

        var connectionString = configuration.GetConnectionString("HRMS");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings:HRMS or ConnectionStrings__HRMS before running EF migration tools.");
        }

        var options = new DbContextOptionsBuilder<HRMSDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new HRMSDbContext(options);
    }
}
