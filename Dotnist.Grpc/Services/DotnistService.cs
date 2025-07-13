using Grpc.Core;
using Dotnist;
using Dotnist.Grpc;

namespace Dotnist.Grpc.Services;

public class DotnistGrpcService : DotnistService.DotnistServiceBase
{
    private readonly ILogger<DotnistGrpcService> _logger;
    private readonly NsrlDatabase _database;
    private readonly string _version;

    public DotnistGrpcService(ILogger<DotnistGrpcService> logger, NsrlDatabase database)
    {
        _logger = logger;
        _database = database;
        _version = typeof(DotnistGrpcService).Assembly.GetName().Version?.ToString() ?? "1.0.0";
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
                response.FoundFiles.Add(new NistFileInfo
                {
                    Sha256 = fileInfo.Sha256,
                    PackageId = fileInfo.PackageId,
                    PackageName = fileInfo.PackageName ?? "",
                    ApplicationType = fileInfo.ApplicationType ?? "",
                    OsName = fileInfo.OsName ?? "",
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

    public override async Task<VersionResponse> Version(VersionRequest request, ServerCallContext context)
    {
        var versionInfo = await _database.GetVersionInfoAsync();

        if (versionInfo == null) { throw new Exception("No version information found in database"); }

        return new VersionResponse
        {
            VersionInfo = new VersionInfo
            {
                Version = _version,
                BuildSet = versionInfo.BuildSet,
                BuildDate = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.Parse(versionInfo.BuildDate, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal)
                ),
                ReleaseDate = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                    DateTime.Parse(versionInfo.ReleaseDate, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal)
                ),
                Description = versionInfo.Description
            }
        };
    }
}