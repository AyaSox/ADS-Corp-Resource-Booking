using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;
using ResourceBooking.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection; // Added for data protection
using System.Reflection; // Added for migrations

var builder = WebApplication.CreateBuilder(args);

// Helper to read connection string from multiple env keys
static string? GetEnv(params string[] keys)
{
    foreach (var k in keys)
    {
        var v = Environment.GetEnvironmentVariable(k);
        if (!string.IsNullOrWhiteSpace(v)) return v;
    }
    return null;
}

// Database configuration - prefer Neon/PostgreSQL when provided
var configConn = builder.Configuration.GetConnectionString("DefaultConnection");
var envNeonConn = GetEnv("DefaultConnection", "ConnectionStrings__DefaultConnection");
var databaseUrl = GetEnv("DATABASE_URL"); // optional postgres URL

// Log which connection we're using (for debugging)
Console.WriteLine("Checking database connection options...");
if (!string.IsNullOrEmpty(envNeonConn))
{
    Console.WriteLine("Using environment variable connection string");
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(envNeonConn));
}
else if (!string.IsNullOrEmpty(databaseUrl))
{
    Console.WriteLine("Using DATABASE_URL connection string");
    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var pgConn = $"Host={uri.Host};Port={uri.Port};Database={uri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(pgConn));
}
else if (!string.IsNullOrEmpty(configConn) && configConn.Contains("Host=", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Using config connection string with Host");
    builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(configConn));
}
else
{
    // In production, throw an error if no PostgreSQL connection is found
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("PostgreSQL connection string is required in production. Please set DefaultConnection environment variable.");
    }
    else
    {
        // Fallback: SQL Server for local development only
        Console.WriteLine("Falling back to SQL Server for local development");
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(
            configConn ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));
    }
}

// Add data protection services
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/tmp/keys")) // Use /tmp directory on Render
    .SetApplicationName("ResourceBookingApp");

builder.Services.AddDefaultIdentity<ApplicationUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// Configure email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Register services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICalculationService, CalculationService>();
builder.Services.AddScoped<IRecurringBookingService, RecurringBookingService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Respect proxy headers on Render to avoid redirect loops and get correct scheme/remote IP
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor,
    KnownNetworks = { },
    KnownProxies = { }
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // Always migrate in production, regardless of database type
        if (app.Environment.IsProduction())
        {
            await context.Database.MigrateAsync();
        }
        else if (context.Database.IsNpgsql())
        {
            // For development with PostgreSQL, use EnsureCreated if no migrations exist
            await context.Database.EnsureCreatedAsync();
        }
        else
        {
            // For development with SQL Server, use migrations
            await context.Database.MigrateAsync();
        }

        // Seed identity users first
        var identityLogger = services.GetRequiredService<ILogger<IdentityDataSeeder>>();
        var identitySeeder = new IdentityDataSeeder(context, userManager, identityLogger);
        await identitySeeder.SeedAsync();

        // Seed application data
        var dataLogger = services.GetRequiredService<ILogger<DataSeeder>>();
        var dataSeeder = new DataSeeder(context, dataLogger);
        await dataSeeder.SeedAsync();
        
        logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing the database.");
    }
}

app.Run();
