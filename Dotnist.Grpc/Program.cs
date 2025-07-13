using Dotnist.Grpc.Services;
using Dotnist;
using Grpc.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Configure URLs at runtime (after Cloud Run sets PORT)
var port = Environment.GetEnvironmentVariable("PORT") ?? "3380";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddGrpc();
// Smoking health check
builder.Services.AddGrpcHealthChecks()
    .AddCheck("", () => HealthCheckResult.Healthy())
    .AddCheck("dotnist", () => HealthCheckResult.Healthy("Dotnist is healthy"));

// Configure NSRL database - requires explicit configuration
var databasePath = Environment.GetEnvironmentVariable("DATABASE_PATH") ??
                   builder.Configuration["Database:Path"];

if (string.IsNullOrWhiteSpace(databasePath))
{
    throw new InvalidOperationException(
        "Database path not configured. Set DATABASE_PATH environment variable or configure Database:Path in appsettings.json. " +
        "Example: DATABASE_PATH=./rds/minimal_patched_2025.06.01.db");
}

// Resolve relative paths to absolute paths
if (!Path.IsPathRooted(databasePath))
{
    // Try to find the database relative to the application directory
    var appDir = AppContext.BaseDirectory;
    var possiblePaths = new[]
    {
        Path.Combine(appDir, databasePath),
        Path.Combine(Directory.GetCurrentDirectory(), databasePath),
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

builder.Services.AddSingleton<NsrlDatabase>(provider =>
{
    var logger = provider.GetService<ILogger<NsrlDatabase>>();
    logger?.LogInformation("Using database: {DatabasePath}", databasePath);

    return new NsrlDatabase(databasePath);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<DotnistGrpcService>();

// Add health check service
app.MapGrpcHealthChecksService();

app.MapGet("/", () => "Dotnist gRPC Service - Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();

public partial class Program { }
