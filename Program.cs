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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
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

// Enhanced database initialization for production
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        logger.LogInformation("Starting database initialization...");
        
        // Check if we're using PostgreSQL (production)
        bool isPostgreSQL = context.Database.ProviderName?.Contains("Npgsql") ?? false;
        
        if (isPostgreSQL)
        {
            logger.LogInformation("PostgreSQL detected - using EnsureCreated for fresh database setup");
            
            // For PostgreSQL (production), use EnsureCreated to avoid migration conflicts
            var created = await context.Database.EnsureCreatedAsync();
            if (created)
            {
                logger.LogInformation("? Database created successfully using EnsureCreated");
            }
            else
            {
                logger.LogInformation("Database already exists");
            }
        }
        else
        {
            logger.LogInformation("SQL Server detected - using migrations");
            
            // For SQL Server (development), use migrations
            await context.Database.MigrateAsync();
            logger.LogInformation("? Database migrations applied successfully");
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
        logger.LogError(ex, "? An error occurred during database initialization");
        
        // In production, log the error but continue - let the app start
        if (app.Environment.IsDevelopment())
        {
            throw;
        }
        else
        {
            logger.LogWarning("?? Continuing application startup despite database initialization error");
        }
    }
}

app.Run();