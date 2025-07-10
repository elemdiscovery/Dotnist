using Dotnist.Grpc.Services;
using Dotnist;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();

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
app.MapGrpcService<NsrlGrpcService>();

// Health check endpoint
app.MapGet("/health", async (NsrlDatabase db) =>
{
    try
    {
        var versionInfo = await db.GetVersionInfoAsync();
        if (versionInfo == null)
        {
            return Results.Json(new { healthy = false, status = "ERROR", error = "No version information found in database" }, statusCode: 500);
        }

        return Results.Ok(new
        {
            healthy = true,
            status = "OK",
            database_version = versionInfo.Version,
            build_set = versionInfo.BuildSet,
            build_date = versionInfo.BuildDate.ToString("yyyy-MM-dd HH:mm:ss"),
            release_date = versionInfo.ReleaseDate.ToString("yyyy-MM-dd HH:mm:ss"),
            description = versionInfo.Description
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { healthy = false, status = "ERROR", error = ex.Message }, statusCode: 500);
    }
});

app.MapGet("/", () => "NSRL gRPC Service - Communication with gRPC endpoints must be made through a gRPC client.");

app.Run();
