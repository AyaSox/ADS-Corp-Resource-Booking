using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;
using ResourceBooking.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Configure Entity Framework with database provider detection
if (connectionString.Contains("Host=") && connectionString.Contains("postgres"))
{
    // PostgreSQL (Neon) configuration for production
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}
else
{
    // SQL Server configuration for local development
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));
}

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configure Identity
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

// Register application services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICalculationService, CalculationService>();
builder.Services.AddScoped<IRecurringBookingService, RecurringBookingService>();
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddScoped<IdentityDataSeeder>();

// Configure email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

// Add MVC services
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// CRITICAL: Initialize database BEFORE configuring the pipeline
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        logger.LogInformation("?? Starting database initialization...");
        
        // Check if we're using PostgreSQL
        bool isPostgreSQL = context.Database.ProviderName?.Contains("Npgsql") ?? false;
        
        if (isPostgreSQL)
        {
            logger.LogInformation("?? PostgreSQL detected - creating database structure...");
            
            // For PostgreSQL, try migrations first, fall back to EnsureCreated
            try
            {
                await context.Database.MigrateAsync();
                logger.LogInformation("? Migrations applied successfully");
            }
            catch (Exception migrationEx)
            {
                logger.LogWarning(migrationEx, "?? Migrations failed, trying EnsureCreated...");
                
                // If migrations fail, use EnsureCreated for PostgreSQL
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("? Database created using EnsureCreated");
            }
        }
        else
        {
            logger.LogInformation("?? SQL Server detected - applying migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("? Migrations applied successfully");
        }

        // Seed identity data (users and roles)
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        
        var identitySeeder = services.GetRequiredService<IdentityDataSeeder>();
        await identitySeeder.SeedAsync();
        logger.LogInformation("? Identity data seeded successfully");

        // Seed application data (resources, sample bookings)
        var dataSeeder = services.GetRequiredService<DataSeeder>();
        await dataSeeder.SeedAsync();
        logger.LogInformation("? Application data seeded successfully");
        
        logger.LogInformation("?? Database initialization completed successfully!");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "? CRITICAL: Database initialization failed!");
        logger.LogError("?? Application cannot start without database. Exiting...");
        throw; // Always throw - app cannot work without database
    }
}

// Configure the HTTP request pipeline.
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

// Configure routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();