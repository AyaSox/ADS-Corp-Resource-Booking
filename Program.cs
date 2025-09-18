using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;
using ResourceBooking.Services;

var builder = WebApplication.CreateBuilder(args);

// Resolve connection string with environment overrides
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var envOverride = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrWhiteSpace(envOverride)) connectionString = envOverride;
else if (!string.IsNullOrWhiteSpace(databaseUrl) && databaseUrl.Contains("@"))
{
    connectionString = ConvertDatabaseUrl(databaseUrl);
}
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Database connection string not found.");

bool isPostgres = IsPostgres(connectionString);

if (isPostgres)
{
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(connectionString));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connectionString));
}

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICalculationService, CalculationService>();
builder.Services.AddScoped<IRecurringBookingService, RecurringBookingService>();
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddScoped<IdentityDataSeeder>();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var ctx = sp.GetRequiredService<ApplicationDbContext>();
    logger.LogInformation("Starting DB init. Provider={Provider} Postgres={IsPg}", ctx.Database.ProviderName, isPostgres);

    try
    {
        if (isPostgres)
        {
            // Try migrations (preferred if permissions allow)
            try
            {
                await ctx.Database.MigrateAsync();
                logger.LogInformation("Migrations applied (PostgreSQL)");
            }
            catch (Exception migEx)
            {
                logger.LogWarning(migEx, "PostgreSQL migrations failed. Attempting EnsureCreated as fallback (may lack Identity tables later).");
                try
                {
                    var created = await ctx.Database.EnsureCreatedAsync();
                    logger.LogInformation(created ? "Database created via EnsureCreated (PostgreSQL)" : "Database already exists (EnsureCreated check)");
                }
                catch (Exception ensureEx)
                {
                    logger.LogError(ensureEx, "EnsureCreated also failed. Database cannot be initialized.");
                    throw; // hard fail – can't proceed
                }
            }
        }
        else
        {
            await ctx.Database.MigrateAsync();
            logger.LogInformation("Migrations applied (SQL Server)");
        }

        // Only attempt seeding if core tables exist (quick existence probe)
        if (await CoreTablesExistAsync(ctx, logger))
        {
            var identitySeeder = sp.GetRequiredService<IdentityDataSeeder>();
            await identitySeeder.SeedAsync();
            var dataSeeder = sp.GetRequiredService<DataSeeder>();
            await dataSeeder.SeedAsync();
        }
        else
        {
            logger.LogWarning("Skipping seeding. Core tables missing (likely insufficient privileges). Setup DB manually.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DB initialization failed – application start aborted.");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.Run();

static bool IsPostgres(string conn) => conn.Contains("Host=") || conn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) || conn.Contains("Username=");
static string ConvertDatabaseUrl(string url)
{
    try
    {
        var uri = new Uri(url);
        var parts = uri.UserInfo.Split(':');
        var user = Uri.UnescapeDataString(parts[0]);
        var pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
        var db = uri.AbsolutePath.Trim('/');
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        return $"Host={host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
    }
    catch { return url; }
}
static async Task<bool> CoreTablesExistAsync(ApplicationDbContext ctx, ILogger logger)
{
    try
    {
        var canQuery = await ctx.Database.CanConnectAsync();
        if (!canQuery) return false;
        // Probe AspNetUsers (identity) and Resources
        var usersTable = await ctx.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"AspNetUsers\" LIMIT 1");
        var resourcesTable = await ctx.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"Resources\" LIMIT 1");
        return true;
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Core table probe failed.");
        return false;
    }
}