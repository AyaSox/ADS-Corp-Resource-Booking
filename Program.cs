using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ResourceBooking.Data;
using ResourceBooking.Models;
using ResourceBooking.Services;
using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDefaultIdentity<ApplicationUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// Performance: Response compression for CSV/ICS/JSON/HTML
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<GzipCompressionProvider>();
});

// Configure Settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<CompanySettings>(builder.Configuration.GetSection("CompanySettings"));

// Register Services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICalculationService, CalculationService>();
builder.Services.AddScoped<IRecurringBookingService, RecurringBookingService>();

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseResponseCompression();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");
app.MapRazorPages();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var emailService = services.GetRequiredService<IEmailService>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        logger.LogInformation("Starting application data seeding...");
        
        // Identity data seeding first
        logger.LogInformation("Running identity data seeder...");
        var identitySeederLogger = services.GetRequiredService<ILogger<IdentityDataSeeder>>();
        var identitySeeder = new IdentityDataSeeder(context, userManager, identitySeederLogger);
        await identitySeeder.SeedAsync();
        logger.LogInformation("Identity data seeding completed.");
        
        // Enhanced data seeding (resources and bookings)
        logger.LogInformation("Running enhanced data seeder...");
        var dataSeederLogger = services.GetRequiredService<ILogger<DataSeeder>>();
        var dataSeeder = new DataSeeder(context, dataSeederLogger);
        await dataSeeder.SeedAsync();
        logger.LogInformation("Enhanced data seeding completed.");
        
        // Send welcome emails to newly created users
        logger.LogInformation("Checking for users who need welcome emails...");
        var usersNeedingWelcome = await userManager.Users
            .Where(u => u.EmailConfirmed) // Only send to confirmed users
            .ToListAsync();
            
        foreach (var user in usersNeedingWelcome)
        {
            try
            {
                await emailService.SendWelcomeEmailAsync(user);
                logger.LogInformation("Welcome email sent to {Email}", user.Email);
            }
            catch (Exception emailEx)
            {
                logger.LogWarning(emailEx, "Failed to send welcome email to {Email}", user.Email);
            }
        }
        
        logger.LogInformation("Application startup seeding completed successfully!");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();
