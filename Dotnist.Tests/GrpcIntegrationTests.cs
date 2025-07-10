using Xunit;
using Grpc.Core;
using Grpc.Net.Client;
using Dotnist.Grpc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Dotnist.Tests;

public class GrpcIntegrationTests
{
    public static IEnumerable<object[]> GetDatabaseTestData() => TestDatabaseHelper.GetDatabaseTestData();

    private GrpcChannel CreateChannel(string databasePath)
    {
        var builder = new WebHostBuilder()
            .UseStartup<TestStartup>()
            .UseTestServer()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Path"] = databasePath
                });
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            });

        var webHost = builder.Build();
        webHost.Start();

        var testServer = webHost.GetTestServer();
        var handler = testServer.CreateHandler();

        return GrpcChannel.ForAddress("http://localhost", new GrpcChannelOptions
        {
            HttpHandler = handler
        });
    }

    private void LogMessage(string message)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(message);
    }

    [Fact]
    public void TestDatabaseFiles_ShouldExist()
    {
        Assert.True(TestDatabaseHelper.ValidateTestDatabasesAvailable(TestContext.Current.TestOutputHelper));
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashes_SingleValidHash_ReturnsFoundFiles(string dbPath)
    {
        // Arrange
        using var channel = CreateChannel(dbPath);
        var client = new NsrlService.NsrlServiceClient(channel);
        var hash = "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA"; // Known hash from database

        LogMessage($"Testing single hash check with database: {dbPath}");

        // Act
        var response = await client.CheckHashesAsync(new HashCheckRequest { Sha256Hashes = { hash } }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.FoundFiles.Count > 0, "Should find files for known hash");
        Assert.Equal(hash, response.FoundFiles[0].Sha256);
        Assert.Equal("", response.ErrorMessage);

        LogMessage($"Single hash check result: {response.FoundFiles.Count} files found");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashes_SingleInvalidHash_ReturnsNoFiles(string dbPath)
    {
        // Arrange
        using var channel = CreateChannel(dbPath);
        var client = new NsrlService.NsrlServiceClient(channel);
        var hash = "0000000000000000000000000000000000000000000000000000000000000000"; // Non-existent hash

        LogMessage($"Testing invalid hash check with database: {dbPath}");

        // Act
        var response = await client.CheckHashesAsync(new HashCheckRequest { Sha256Hashes = { hash } }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Empty(response.FoundFiles);
        Assert.Equal("", response.ErrorMessage);

        LogMessage($"Invalid hash check result: {response.FoundFiles.Count} files found");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashes_MultipleHashes_ReturnsFoundFiles(string dbPath)
    {
        // Arrange
        using var channel = CreateChannel(dbPath);
        var client = new NsrlService.NsrlServiceClient(channel);
        var hashes = new[]
        {
            "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA", // Known hash from database
            "002F58AD1C6BEA9B560081FA2A5434D782A5CDE21058FBAC8A9FCFC6EB070DA5", // Another known hash
            "0000000000000000000000000000000000000000000000000000000000000000"  // Non-existent hash
        };

        LogMessage($"Testing multiple hash check with database: {dbPath}");

        // Act
        var response = await client.CheckHashesAsync(new HashCheckRequest { Sha256Hashes = { hashes } }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.FoundFiles.Count > 0, "Should find files for known hashes");
        Assert.Equal("", response.ErrorMessage);

        // Should find files for the first two hashes (known hashes)
        var foundHashes = response.FoundFiles.Select(f => f.Sha256).ToHashSet();
        Assert.Contains(hashes[0], foundHashes);
        Assert.Contains(hashes[1], foundHashes);
        Assert.DoesNotContain(hashes[2], foundHashes); // Non-existent hash should not be found

        LogMessage($"Multiple hash check result: {response.FoundFiles.Count} files found");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashes_EmptyRequest_ReturnsEmptyResponse(string dbPath)
    {
        // Arrange
        using var channel = CreateChannel(dbPath);
        var client = new NsrlService.NsrlServiceClient(channel);

        LogMessage($"Testing empty hash check with database: {dbPath}");

        // Act
        var response = await client.CheckHashesAsync(new HashCheckRequest { Sha256Hashes = { } }, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.Empty(response.FoundFiles);
        Assert.Equal("", response.ErrorMessage);

        LogMessage($"Empty hash check result: {response.FoundFiles.Count} files found");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task HealthCheck_ValidDatabase_ReturnsHealthy(string dbPath)
    {
        // Arrange
        using var channel = CreateChannel(dbPath);
        var client = new NsrlService.NsrlServiceClient(channel);

        LogMessage($"Testing health check with database: {dbPath}");

        // Act
        var response = await client.HealthCheckAsync(new HealthRequest(), cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Healthy);
        Assert.NotNull(response.VersionInfo);
        Assert.NotNull(response.VersionInfo.Version);
        Assert.NotNull(response.VersionInfo.BuildSet);
        Assert.NotNull(response.VersionInfo.BuildDate);
        Assert.NotNull(response.VersionInfo.ReleaseDate);
        Assert.NotNull(response.VersionInfo.Description);
        Assert.Equal("", response.ErrorMessage);

        LogMessage($"Health check result: Healthy={response.Healthy}, Version={response.VersionInfo.Version}");
    }
}