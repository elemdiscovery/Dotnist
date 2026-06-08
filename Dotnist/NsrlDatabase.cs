using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Dapper;

namespace Dotnist;

/// <summary>
/// Wrapper for the NSRL RDS (Reference Data Set) minimal database
/// Provides efficient hash lookups and file information retrieval
/// </summary>
public class NsrlDatabase
{
    private readonly Func<DbConnection> _connectionFactory;

    /// <summary>
    /// Initializes a new instance backed by a read-only SQLite database file
    /// (the bundled RDS database).
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file</param>
    public NsrlDatabase(string databasePath)
    {
        if (databasePath is null)
        {
            throw new ArgumentNullException(nameof(databasePath));
        }

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException($"NSRL database not found at: {databasePath}");
        }

        var connectionString = $"Data Source={databasePath};Mode=ReadOnly;";
        _connectionFactory = () => new SqliteConnection(connectionString);
    }

    /// <summary>
    /// Initializes a new instance backed by an arbitrary ADO.NET database, allowing
    /// the library to run against providers other than SQLite (SQL Server, PostgreSQL, etc.).
    /// The factory is invoked once per operation to create a fresh connection, so it must
    /// return a new, unopened connection each time.
    /// </summary>
    /// <param name="connectionFactory">Factory that creates a new database connection.</param>
    public NsrlDatabase(Func<DbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Creates a new connection for each operation using the configured factory
    /// </summary>
    private DbConnection CreateConnection() => _connectionFactory();

    /// <summary>
    /// Checks multiple hashes and returns file information for found hashes
    /// </summary>
    /// <param name="sha256Hashes">List of SHA-256 hashes to check</param>
    /// <returns>Result containing found files and not found hashes</returns>
    public async Task<NsrlHashCheckResult> CheckHashesAsync(IEnumerable<string> sha256Hashes)
    {
        if (sha256Hashes == null)
            return new NsrlHashCheckResult();

        var hashList = sha256Hashes.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        if (hashList.Count == 0)
            return new NsrlHashCheckResult();

        // Normalize all hashes to uppercase
        var normalizedHashes = hashList.Select(h => h.ToUpperInvariant()).ToList();
        var hashSet = normalizedHashes.ToHashSet();

        var sql = @"
            SELECT 
                mf.sha256 as Sha256,
                mf.package_id as PackageId,
                p.name as PackageName,
                p.application_type as ApplicationType,
                os.name as OsName,
                mfg.name as ManufacturerName
            FROM ""FILE"" mf
            JOIN PKG p ON mf.package_id = p.package_id
            JOIN OS os ON p.operating_system_id = os.operating_system_id
            JOIN MFG mfg ON p.manufacturer_id = mfg.manufacturer_id
            WHERE mf.sha256 IN @hashes
            ORDER BY mf.sha256, mf.package_id, p.name, os.name, mfg.name, p.application_type";

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        var foundFiles = await connection.QueryAsync<NsrlFileInfo>(sql, new { hashes = normalizedHashes });
        var foundHashes = foundFiles.Select(f => f.Sha256).ToHashSet();
        var notFoundHashes = hashSet.Except(foundHashes).ToList();

        return new NsrlHashCheckResult
        {
            FoundFiles = foundFiles.ToList(),
            NotFoundHashes = notFoundHashes
        };
    }

    /// <summary>
    /// Gets version information from the NSRL database
    /// </summary>
    /// <returns>Version information</returns>
    public async Task<NsrlVersionInfo?> GetVersionInfoAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        var result = await connection.QueryFirstOrDefaultAsync<NsrlVersionInfo>(
            "SELECT version as Version, build_set as BuildSet, build_date as BuildDate, release_date as ReleaseDate, description as Description FROM VERSION");
        return result;
    }

}

/// <summary>
/// Represents file information from the NSRL minimal database
/// </summary>
public class NsrlFileInfo
{
    public string Sha256 { get; set; } = string.Empty;
    public long PackageId { get; set; }
    public string? PackageName { get; set; }
    public string? ApplicationType { get; set; }
    public string? OsName { get; set; }
    public string? ManufacturerName { get; set; }
}

/// <summary>
/// Result of checking multiple hashes
/// </summary>
public class NsrlHashCheckResult
{
    public List<NsrlFileInfo> FoundFiles { get; set; } = new List<NsrlFileInfo>();
    public List<string> NotFoundHashes { get; set; } = new List<string>();
}

/// <summary>
/// Represents version information from the NSRL database
/// </summary>
public class NsrlVersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string BuildSet { get; set; } = string.Empty;
    public string BuildDate { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
