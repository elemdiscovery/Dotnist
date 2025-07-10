using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dotnist.Tests;

/// <summary>
/// Helper class for discovering and managing test databases
/// </summary>
public static class TestDatabaseHelper
{
    public const string MinimalDbPath = "rds/RDS_2025.06.1_modern_minimal_patched.db";
    public const string MinimalDbPathFlattened = "rds/RDS_2025.06.1_modern_minimal_patched_flattened.db";

    /// <summary>
    /// Gets the solution root directory by walking up the directory tree
    /// </summary>
    /// <returns>Path to the solution root directory</returns>
    public static string GetSolutionRoot()
    {
        var currentDir = new DirectoryInfo(Directory.GetCurrentDirectory());

        // Walk up the directory tree to find the solution root (where .sln file is)
        while (currentDir != null)
        {
            if (currentDir.GetFiles("*.sln").Any())
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }

        // Fallback to current directory if we can't find the solution
        return Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Discovers available test databases using environment variables or hardcoded paths
    /// Environment variables MINIMAL_DB_PATH and MINIMAL_DB_PATH_FLATTENED are overrides
    /// for the default paths MinimalDbPath and MinimalDbPathFlattened respectively.
    /// </summary>
    /// <returns>List of available database paths</returns>
    public static IEnumerable<string> GetAvailableTestDatabases()
    {
        var availableDatabases = new List<string>();

        // Check for environment variable overrides first
        var minimalDbPath = Environment.GetEnvironmentVariable("MINIMAL_DB_PATH");
        var minimalDbPathFlattened = Environment.GetEnvironmentVariable("MINIMAL_DB_PATH_FLATTENED");

        if (!string.IsNullOrEmpty(minimalDbPath) && File.Exists(minimalDbPath))
        {
            availableDatabases.Add(minimalDbPath);
        }

        if (!string.IsNullOrEmpty(minimalDbPathFlattened) && File.Exists(minimalDbPathFlattened))
        {
            availableDatabases.Add(minimalDbPathFlattened);
        }

        // If environment variables provided databases, return them
        if (availableDatabases.Count > 0)
        {
            return availableDatabases;
        }

        // Fallback to hardcoded paths
        var solutionRoot = GetSolutionRoot();

        // Check minimal database path
        var fullMinimalPath = Path.Combine(solutionRoot, MinimalDbPath);
        if (File.Exists(fullMinimalPath))
        {
            availableDatabases.Add(fullMinimalPath);
        }

        // Check flattened database path
        var fullFlattenedPath = Path.Combine(solutionRoot, MinimalDbPathFlattened);
        if (File.Exists(fullFlattenedPath))
        {
            availableDatabases.Add(fullFlattenedPath);
        }

        return availableDatabases;
    }

    /// <summary>
    /// Gets test data for Theory tests that need database paths
    /// </summary>
    /// <returns>Test data with database paths</returns>
    public static IEnumerable<object[]> GetDatabaseTestData()
    {
        var availableDatabases = GetAvailableTestDatabases();

        if (!availableDatabases.Any())
        {
            // Return a dummy test case if no databases are available
            yield return new object[] { "nonexistent.db" };
            yield break;
        }

        foreach (var dbPath in availableDatabases)
        {
            yield return new object[] { dbPath };
        }
    }

    /// <summary>
    /// Validates that at least one test database is available
    /// </summary>
    /// <param name="testOutputHelper">Optional test output helper for logging</param>
    /// <returns>True if at least one database is available</returns>
    public static bool ValidateTestDatabasesAvailable(ITestOutputHelper? testOutputHelper = null)
    {
        var availableDatabases = GetAvailableTestDatabases().ToList();

        var hasDatabases = availableDatabases.Count > 0;

        if (!hasDatabases)
        {
            testOutputHelper?.WriteLine($"No test databases found. Expected at least one of: {MinimalDbPath}, {MinimalDbPathFlattened} or environment variables MINIMAL_DB_PATH, MINIMAL_DB_PATH_FLATTENED");
        }
        else
        {
            testOutputHelper?.WriteLine($"Found {availableDatabases.Count} test databases:");
            foreach (var db in availableDatabases)
            {
                testOutputHelper?.WriteLine($"  - {db}");
            }
        }

        return hasDatabases;
    }
}