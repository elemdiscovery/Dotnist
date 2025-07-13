using Dotnist.Grpc.Services;
using Dotnist;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Dotnist.Tests;

// TODO: Now that I'm actually reading the test code, this is very wrongly setup.
public class TestStartup
{
    private readonly IConfiguration _configuration;

    public TestStartup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Add gRPC services
        services.AddGrpc();

        // Configure NSRL database
        var databasePath = _configuration["Database:Path"];

        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new InvalidOperationException(
                "Database path not configured. Set Database:Path in configuration. " +
                "Example: Database:Path=./rds/minimal_patched_2025.06.01.db");
        }

        // Resolve relative paths to absolute paths
        if (!Path.IsPathRooted(databasePath))
        {
            // Try to find the database relative to the solution root
            var solutionRoot = FindSolutionRoot();
            var possiblePaths = new[]
            {
                Path.Combine(solutionRoot, databasePath),
                Path.GetFullPath(databasePath)
            };

            databasePath = possiblePaths.FirstOrDefault(File.Exists) ?? databasePath;
        }

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException(
                $"Database file not found: {databasePath}. " +
                "Please ensure the database file exists and the path is correct.");
        }

        services.AddSingleton<NsrlDatabase>(provider =>
        {
            var logger = provider.GetService<ILogger<NsrlDatabase>>();
            logger?.LogInformation("Using database for tests: {DatabasePath}", databasePath);

            return new NsrlDatabase(databasePath);
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<DotnistGrpcService>();
        });
    }

    private static string FindSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        // Look for the solution file (.sln) in parent directories
        while (dir != null && !dir.GetFiles("*.sln").Any())
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? currentDir;
    }
}