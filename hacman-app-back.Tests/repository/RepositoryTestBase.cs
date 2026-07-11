using System;
using System.IO;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace hacman_app_back.Tests.repository;

public abstract class RepositoryTestBase
{
    static RepositoryTestBase()
    {
        // Enable dynamic JSON serialization for Npgsql 8.0+
        // This codebase uses raw ADO.NET connections, so we keep GlobalTypeMapper despite its deprecation.
        // For Npgsql 8.0+, the recommended path is NpgsqlDataSourceBuilder, but that requires 
        // significant architectural changes. Suppressing the CS0618 warning is acceptable here.
        // See: https://www.npgsql.org/doc/release-notes/8.0.html
#pragma warning disable CS0618
        NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
#pragma warning restore CS0618
    }

    protected static string GetConnectionString()
    {
        try
        {
            var projectRoot = Directory.GetCurrentDirectory();
            while (projectRoot != null && !Directory.Exists(Path.Combine(projectRoot, "hacman-app-back")))
                projectRoot = Directory.GetParent(projectRoot)?.FullName;

            if (projectRoot != null)
            {
                var envPath = Path.Combine(projectRoot, "hacman-app-back", ".env");
                if (File.Exists(envPath))
                    Env.Load(envPath);
            }
        }
        catch
        {
            /* ignore */
        }

        var envConn = Environment.GetEnvironmentVariable("TEST_HACMAN_CONNECTIONSTRING")
                      ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default");

        if (!string.IsNullOrWhiteSpace(envConn))
            return envConn;

        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Test.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets(typeof(RepositoryTestBase).Assembly, optional: true)
            .Build();

        return config["TEST_HACMAN_CONNECTIONSTRING"]
               ?? config.GetConnectionString("Test")
               ?? config.GetConnectionString("Default")
               ?? string.Empty;
    }
}