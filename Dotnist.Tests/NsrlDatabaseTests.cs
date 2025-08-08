using Xunit;
using Dotnist;
using System.Threading.Tasks;
using System.Linq;

namespace Dotnist.Tests;

public class NsrlDatabaseTests
{
    public static IEnumerable<object[]> GetDatabaseTestData() => TestDatabaseHelper.GetDatabaseTestData();

    [Fact]
    public void Constructor_WithInValidPath_ShouldThrow()
    {
        Assert.Throws<FileNotFoundException>(() => new NsrlDatabase("nonexistent.db"));
    }

    [Fact]
    public void Constructor_WithNullPath_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new NsrlDatabase(null!));
    }

    [Fact]
    public void Constructor_WithEmptyPath_ShouldThrowArgumentNullException()
    {
        Assert.Throws<FileNotFoundException>(() => new NsrlDatabase(""));
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task GetVersionInfoAsync_WithValidDatabase_ShouldReturnVersionInfo(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);
        var versionInfo = await database.GetVersionInfoAsync();

        Assert.NotNull(versionInfo);
        Assert.NotEmpty(versionInfo.Version);
        Assert.NotEmpty(versionInfo.BuildSet);
        Assert.NotEqual(default(string), versionInfo.BuildDate);
        Assert.NotEqual(default(string), versionInfo.ReleaseDate);

        TestContext.Current.TestOutputHelper?.WriteLine($"Database {dbPath}: Version {versionInfo.Version}, Build {versionInfo.BuildSet}");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithKnownHashes_ShouldReturnFileInfo(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        // Test with known hashes that should exist in the database
        var knownHashes = new[]
        {
            "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA",
            "002F58AD1C6BEA9B560081FA2A5434D782A5CDE21058FBAC8A9FCFC6EB070DA5"
        };

        TestContext.Current.TestOutputHelper?.WriteLine($"Testing known hashes in database {dbPath}:");

        foreach (var hash in knownHashes)
        {
            var result = await database.CheckHashesAsync(new[] { hash });
            Assert.True(result.FoundFiles.Count > 0, $"Hash {hash} should exist in database {dbPath}");
            Assert.Empty(result.NotFoundHashes);
            TestContext.Current.TestOutputHelper?.WriteLine($"Hash {hash} found in {dbPath}: {result.FoundFiles.Count} entries");
        }
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithSingleHash_ShouldWork(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        var expectedHash = "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA";
        var hashes = new[] { expectedHash };
        var result = await database.CheckHashesAsync(hashes);

        Assert.True(result.FoundFiles.Count > 0, $"Hash {expectedHash} should be found in database {dbPath}");
        Assert.Empty(result.NotFoundHashes);

        TestContext.Current.TestOutputHelper?.WriteLine($"Single hash check in {dbPath}: {result.FoundFiles.Count} files found");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithMultipleHashes_ShouldWork(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        // Test with multiple specific hashes - some existing, some not
        var expectedHash1 = "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA";
        var expectedHash2 = "002F58AD1C6BEA9B560081FA2A5434D782A5CDE21058FBAC8A9FCFC6EB070DA5";
        var nonExistentHash = "0000000000000000000000000000000000000000000000000000000000000000";

        var hashes = new[]
        {
            expectedHash1,
            expectedHash2,
            nonExistentHash // Non-existent hash
        };

        var result = await database.CheckHashesAsync(hashes);

        Assert.True(result.FoundFiles.Count > 0, "Should find files for known hashes");
        Assert.True(result.NotFoundHashes.Count > 0, "Should have not found hashes");

        // Should find files for the first two hashes (known hashes)
        var foundHashes = result.FoundFiles.Select(f => f.Sha256).ToHashSet();
        Assert.Contains(expectedHash1, foundHashes);
        Assert.Contains(expectedHash2, foundHashes);
        Assert.DoesNotContain(nonExistentHash, foundHashes); // Non-existent hash should not be found

        // The count can vary due to duplicate hashes in some databases
        Assert.True(result.FoundFiles.Count >= 2, $"Should find at least 2 files for known hashes, but found {result.FoundFiles.Count}");
        Assert.Single(result.NotFoundHashes); // Should have 1 not found hash

        TestContext.Current.TestOutputHelper?.WriteLine($"Batch check in {dbPath}: {result.FoundFiles.Count} found, {result.NotFoundHashes.Count} not found");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithEmptyList_ShouldReturnEmptyResult(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        var result = await database.CheckHashesAsync(new string[0]);

        Assert.Empty(result.FoundFiles);
        Assert.Empty(result.NotFoundHashes);

        TestContext.Current.TestOutputHelper?.WriteLine($"Empty hash list check in {dbPath}: returned empty result");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithNullInput_ShouldReturnEmptyResult(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        var result = await database.CheckHashesAsync(null!);

        Assert.Empty(result.FoundFiles);
        Assert.Empty(result.NotFoundHashes);

        TestContext.Current.TestOutputHelper?.WriteLine($"Null hash list check in {dbPath}: returned empty result");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithInvalidHashes_ShouldFilterThem(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        var expectedHash1 = "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA";
        var expectedHash2 = "002F58AD1C6BEA9B560081FA2A5434D782A5CDE21058FBAC8A9FCFC6EB070DA5";

        var hashes = new[]
        {
            expectedHash1,
            "", // Empty hash
            "   ", // Whitespace hash
            null!, // Null hash
            expectedHash2
        };

        var result = await database.CheckHashesAsync(hashes);

        // Should only process the valid hashes (2 valid ones)
        Assert.True(result.FoundFiles.Count > 0, "Should find files for valid hashes");
        Assert.Empty(result.NotFoundHashes); // Both valid hashes should be found

        // Verify that both expected hashes were found
        var foundHashes = result.FoundFiles.Select(f => f.Sha256).ToHashSet();
        Assert.Contains(expectedHash1, foundHashes);
        Assert.Contains(expectedHash2, foundHashes);

        TestContext.Current.TestOutputHelper?.WriteLine($"Invalid hash filtering in {dbPath}: {result.FoundFiles.Count} files found from valid hashes");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithSpecificHash_ShouldReturnOrderedResults(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        var hash = "002F59A4DA5A22BB3A4C4B552AFF5DE4EF9FE9404C2720528F76C77818DD0D8E";
        var result = await database.CheckHashesAsync(new[] { hash });

        TestContext.Current.TestOutputHelper?.WriteLine($"Testing ordering for hash {hash} in database {dbPath}");

        // Verify that the expected hash was found
        Assert.True(result.FoundFiles.Count > 0, $"Hash {hash} should be found in database {dbPath}");
        Assert.Empty(result.NotFoundHashes);

        // Verify that all returned files have the same SHA256 hash
        Assert.All(result.FoundFiles, file => Assert.Equal(hash, file.Sha256));

        // Verify ordering: sha256, package_id, name, os_name, manufacturer_name, application_type
        for (int i = 0; i < result.FoundFiles.Count - 1; i++)
        {
            var current = result.FoundFiles[i];
            var next = result.FoundFiles[i + 1];

            TestContext.Current.TestOutputHelper?.WriteLine($"Entry {i}: PackageId={current.PackageId}, Name='{current.PackageName}', OS='{current.OsName}', Manufacturer='{current.ManufacturerName}', AppType='{current.ApplicationType}'");

            // SHA256 should be equal (already verified above)
            Assert.Equal(current.Sha256, next.Sha256);

            // If package_id is different, next should be greater
            if (current.PackageId != next.PackageId)
            {
                Assert.True(current.PackageId < next.PackageId,
                    $"PackageId should be ordered: {current.PackageId} < {next.PackageId}");
                continue;
            }

            // If package_id is same, check package name ordering
            if (current.PackageName != next.PackageName)
            {
                var currentName = current.PackageName ?? "";
                var nextName = next.PackageName ?? "";
                Assert.True(string.Compare(currentName, nextName, StringComparison.Ordinal) <= 0,
                    $"PackageName should be ordered: '{currentName}' <= '{nextName}'");
                continue;
            }

            // If package name is same, check OS name ordering
            if (current.OsName != next.OsName)
            {
                var currentOs = current.OsName ?? "";
                var nextOs = next.OsName ?? "";
                Assert.True(string.Compare(currentOs, nextOs, StringComparison.Ordinal) <= 0,
                    $"OsName should be ordered: '{currentOs}' <= '{nextOs}'");
                continue;
            }

            // If OS name is same, check manufacturer name ordering
            if (current.ManufacturerName != next.ManufacturerName)
            {
                var currentMfg = current.ManufacturerName ?? "";
                var nextMfg = next.ManufacturerName ?? "";
                Assert.True(string.Compare(currentMfg, nextMfg, StringComparison.Ordinal) <= 0,
                    $"ManufacturerName should be ordered: '{currentMfg}' <= '{nextMfg}'");
                continue;
            }

            // If manufacturer name is same, check application type ordering
            if (current.ApplicationType != next.ApplicationType)
            {
                var currentAppType = current.ApplicationType ?? "";
                var nextAppType = next.ApplicationType ?? "";
                Assert.True(string.Compare(currentAppType, nextAppType, StringComparison.Ordinal) <= 0,
                    $"ApplicationType should be ordered: '{currentAppType}' <= '{nextAppType}'");
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine($"Ordering verification completed for {result.FoundFiles.Count} entries");
    }

    [Fact]
    public void TestDatabaseFiles_ShouldExist()
    {
        Assert.True(TestDatabaseHelper.ValidateTestDatabasesAvailable(TestContext.Current.TestOutputHelper));
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_ConcurrentRequests_ShouldNotThrow(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        // Known-good hashes present in the test dataset(s)
        var knownHashes = new[]
        {
            "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA",
            "002F58AD1C6BEA9B560081FA2A5434D782A5CDE21058FBAC8A9FCFC6EB070DA5"
        };

        var degreeOfParallelism = Environment.ProcessorCount * 2;
        TestContext.Current.TestOutputHelper?.WriteLine($"Running {degreeOfParallelism} concurrent CheckHashesAsync calls against {dbPath}");

        var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async i =>
        {
            // Alternate between single-hash and two-hash queries
            if ((i % 2) == 0)
            {
                var single = new[] { knownHashes[i % knownHashes.Length] };
                var res = await database.CheckHashesAsync(single);
                Assert.True(res.FoundFiles.Count > 0);
                Assert.Empty(res.NotFoundHashes);
            }
            else
            {
                var res = await database.CheckHashesAsync(knownHashes);
                Assert.True(res.FoundFiles.Count > 0);
                Assert.Empty(res.NotFoundHashes);
            }
        });

        await Task.WhenAll(tasks);
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task GetVersionInfoAsync_ConcurrentRequests_ShouldNotThrow(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        var degreeOfParallelism = Math.Max(8, Environment.ProcessorCount * 2);
        TestContext.Current.TestOutputHelper?.WriteLine($"Running {degreeOfParallelism} concurrent GetVersionInfoAsync calls against {dbPath}");

        var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
        {
            var info = await database.GetVersionInfoAsync();
            Assert.NotNull(info);
            Assert.False(string.IsNullOrWhiteSpace(info!.Version));
        });

        await Task.WhenAll(tasks);
    }
}

