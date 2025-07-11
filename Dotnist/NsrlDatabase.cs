using System;
using System.Collections.Generic;
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
public class NsrlDatabase : IDisposable
{
    private readonly string _databasePath;
    private SqliteConnection? _connection;
    private readonly object _connectionLock = new object();

    /// <summary>
    /// Initializes a new instance of the NSRL database wrapper
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file</param>
    public NsrlDatabase(string databasePath)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException($"NSRL database not found at: {databasePath}");
        }
    }

    /// <summary>
    /// Gets the database connection, creating it if necessary
    /// </summary>
    private SqliteConnection GetConnection()
    {
        if (_connection == null)
        {
            lock (_connectionLock)
            {
                if (_connection == null)
                {
                    var connectionString = $"Data Source={_databasePath};Mode=ReadOnly;";
                    _connection = new SqliteConnection(connectionString);
                    _connection.Open();
                }
            }
        }
        return _connection;
    }

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

        var foundFiles = await GetConnection().QueryAsync<NsrlFileInfo>(sql, new { hashes = normalizedHashes });
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
        var result = await GetConnection().QueryFirstOrDefaultAsync<NsrlVersionInfo>(
            "SELECT version as Version, build_set as BuildSet, build_date as BuildDate, release_date as ReleaseDate, description as Description FROM VERSION");
        return result;
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
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
