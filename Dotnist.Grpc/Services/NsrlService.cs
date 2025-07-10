using Grpc.Core;
using Dotnist;
using Dotnist.Grpc;

namespace Dotnist.Grpc.Services;

public class NsrlGrpcService : NsrlService.NsrlServiceBase
{
    private readonly ILogger<NsrlGrpcService> _logger;
    private readonly NsrlDatabase _database;
    private readonly string _version;

    public NsrlGrpcService(ILogger<NsrlGrpcService> logger, NsrlDatabase database)
    {
        _logger = logger;
        _database = database;
        _version = typeof(NsrlGrpcService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }

    public override async Task<HashCheckResponse> CheckHashes(HashCheckRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogDebug("Checking {Count} hashes", request.Sha256Hashes.Count);

            var result = await _database.CheckHashesAsync(request.Sha256Hashes);

            var response = new HashCheckResponse();

            // Add found files
            foreach (var fileInfo in result.FoundFiles)
            {
                response.FoundFiles.Add(new FileInfo
                {
                    Sha256 = fileInfo.Sha256,
                    PackageId = fileInfo.PackageId,
                    PackageName = fileInfo.PackageName ?? "",
                    PackageVersion = fileInfo.PackageVersion ?? "",
                    PackageLanguage = fileInfo.PackageLanguage ?? "",
                    ApplicationType = fileInfo.ApplicationType ?? "",
                    OsName = fileInfo.OsName ?? "",
                    OsVersion = fileInfo.OsVersion ?? "",
                    ManufacturerName = fileInfo.ManufacturerName ?? ""
                });
            }

            _logger.LogDebug("CheckHashes result: {FoundCount} found", result.FoundFiles.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking hashes");
            return new HashCheckResponse
            {
                ErrorMessage = ex.Message
            };
        }
    }

    public override async Task<HealthResponse> HealthCheck(HealthRequest request, ServerCallContext context)
    {
        try
        {
            var versionInfo = await _database.GetVersionInfoAsync();

            if (versionInfo == null)
            {
                return new HealthResponse
                {
                    Healthy = false,
                    Status = "ERROR",
                    Version = _version,
                    DatabasePath = _database.GetType().GetField("_databasePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_database)?.ToString() ?? "Unknown",
                    ErrorMessage = "No version information found in database"
                };
            }

            return new HealthResponse
            {
                Healthy = true,
                Status = "OK",
                Version = _version,
                DatabasePath = _database.GetType().GetField("_databasePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_database)?.ToString() ?? "Unknown",
                DatabaseVersion = versionInfo.Version,
                BuildSet = versionInfo.BuildSet,
                BuildDate = versionInfo.BuildDate.ToString("yyyy-MM-dd HH:mm:ss"),
                ReleaseDate = versionInfo.ReleaseDate.ToString("yyyy-MM-dd HH:mm:ss"),
                Description = versionInfo.Description
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return new HealthResponse
            {
                Healthy = false,
                Status = "ERROR",
                Version = _version,
                DatabasePath = "Unknown",
                ErrorMessage = ex.Message
            };
        }
    }
}