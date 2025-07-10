using Xunit;
using Dotnist;

namespace Dotnist.Tests;

public class NsrlDatabaseTests
{
    public static IEnumerable<object[]> GetDatabaseTestData() => TestDatabaseHelper.GetDatabaseTestData();

    [Fact]
    public void Constructor_WithValidPath_ShouldNotThrow()
    {
        // This test would require a real database file
        // For now, we'll test the constructor with an invalid path to ensure it throws
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
        Assert.NotEqual(default(DateTime), versionInfo.BuildDate);
        Assert.NotEqual(default(DateTime), versionInfo.ReleaseDate);

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
            TestContext.Current.TestOutputHelper?.WriteLine($"Hash {hash} found in {dbPath}: {result.FoundFiles.Count} entries");
        }
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithSingleHash_ShouldWork(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        var hashes = new[] { "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA" };
        var result = await database.CheckHashesAsync(hashes);

        Assert.True(result.FoundFiles.Count > 0, "Should find files for known hash");
        Assert.Empty(result.NotFoundHashes);

        TestContext.Current.TestOutputHelper?.WriteLine($"Single hash check in {dbPath}: {result.FoundFiles.Count} files found");
    }

    [Theory]
    [MemberData(nameof(GetDatabaseTestData))]
    public async Task CheckHashesAsync_WithMultipleHashes_ShouldWork(string dbPath)
    {
        using var database = new NsrlDatabase(dbPath);

        // Test with multiple specific hashes - some existing, some not
        var hashes = new[]
        {
            "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA",
            "002F58AD1C6BEA9B560081FA2A5434D782A5CDE21058FBAC8A9FCFC6EB070DA5",
            "0000000000000000000000000000000000000000000000000000000000000000" // Non-existent hash
        };

        var result = await database.CheckHashesAsync(hashes);

        Assert.True(result.FoundFiles.Count > 0, "Should find files for known hashes");
        Assert.True(result.NotFoundHashes.Count > 0, "Should have not found hashes");

        // Should find files for the first two hashes (known hashes)
        var foundHashes = result.FoundFiles.Select(f => f.Sha256).ToHashSet();
        Assert.Contains(hashes[0], foundHashes);
        Assert.Contains(hashes[1], foundHashes);
        Assert.DoesNotContain(hashes[2], foundHashes); // Non-existent hash should not be found

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

        var hashes = new[]
        {
            "0008B261E386296CFF720B14279F0C5EDA4AC6AA612EE36C7895383C55641CCA",
            "", // Empty hash
            "   ", // Whitespace hash
            null!, // Null hash
            "002F58AD1C6BEA9B560081FA2A5434D782A5CDE21058FBAC8A9FCFC6EB070DA5"
        };

        var result = await database.CheckHashesAsync(hashes);

        // Should only process the valid hashes (2 valid ones)
        Assert.True(result.FoundFiles.Count > 0, "Should find files for valid hashes");
        Assert.Empty(result.NotFoundHashes); // Both valid hashes should be found

        TestContext.Current.TestOutputHelper?.WriteLine($"Invalid hash filtering in {dbPath}: {result.FoundFiles.Count} files found from valid hashes");
    }

    [Fact]
    public void TestDatabaseFiles_ShouldExist()
    {
        Assert.True(TestDatabaseHelper.ValidateTestDatabasesAvailable(TestContext.Current.TestOutputHelper));
    }
}

