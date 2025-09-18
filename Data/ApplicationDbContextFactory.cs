using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace ResourceBooking.Data
{
    // Enables design-time EF Core commands (migrations) for both SQL Server (local) and PostgreSQL (Render/Neon)
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // Build configuration (looks for appsettings.json in project directory)
            var basePath = Directory.GetCurrentDirectory();
            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            // Priority: explicit env var > ConnectionStrings:DefaultConnection > DATABASE_URL
            var conn = configuration.GetConnectionString("DefaultConnection");
            var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

            if (!string.IsNullOrWhiteSpace(envConn))
            {
                conn = envConn;
            }
            else if (!string.IsNullOrWhiteSpace(databaseUrl) && databaseUrl.Contains("@"))
            {
                // Render style DATABASE_URL: postgres://user:pass@host:port/db?sslmode=require
                conn = ConvertDatabaseUrlToNpgsql(databaseUrl);
            }

            if (string.IsNullOrWhiteSpace(conn))
            {
                throw new InvalidOperationException("No database connection string could be resolved for design-time context.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            if (IsPostgres(conn))
            {
                optionsBuilder.UseNpgsql(conn);
            }
            else
            {
                optionsBuilder.UseSqlServer(conn);
            }

            return new ApplicationDbContext(optionsBuilder.Options);
        }

        private bool IsPostgres(string conn) => conn.Contains("Host=") || conn.StartsWith("postgres", StringComparison.OrdinalIgnoreCase) || conn.Contains("Username=");

        private string ConvertDatabaseUrlToNpgsql(string databaseUrl)
        {
            // Example: postgres://user:pass@host:5432/dbname
            // Convert to: Host=host;Port=5432;Database=dbname;Username=user;Password=pass;SSL Mode=Require;Trust Server Certificate=true
            try
            {
                var uri = new Uri(databaseUrl);
                var userInfo = uri.UserInfo.Split(':');
                var user = Uri.UnescapeDataString(userInfo[0]);
                var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
                var db = uri.AbsolutePath.Trim('/');
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
            }
            catch
            {
                return databaseUrl; // Fallback – maybe already in Npgsql format
            }
        }
    }
}
