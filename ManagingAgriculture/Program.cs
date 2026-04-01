using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ManagingAgriculture.Data;
using ManagingAgriculture.Models;
using ManagingAgriculture.Services;


var builder = WebApplication.CreateBuilder(args);
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (connectionString?.StartsWith("postgres") == true && connectionString.Contains("://"))
    {
        var databaseUri = new Uri(connectionString);
        var userInfo = databaseUri.UserInfo.Split(':');
        var port = databaseUri.Port > 0 ? databaseUri.Port : 5432;
        connectionString = $"Host={databaseUri.Host};Port={port};Database={databaseUri.AbsolutePath[1..]};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Prefer;Trust Server Certificate=true;";
        options.UseNpgsql(connectionString);
    }
    else
    {
        // Use SQL Server for local development
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<ArduinoService>();

var app = builder.Build();

// Seed admin user
// Seed Roles and Users
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try 
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        if (context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            // For Render (PostgreSQL): Ignore SQL Server migrations and just create the tables seamlessly
            await context.Database.EnsureCreatedAsync();
        }
        else
        {
            // For Local (SQL Server): Apply migrations normally
            await context.Database.MigrateAsync();
        }

        await DbInitializer.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Add status code handling
app.UseStatusCodePagesWithReExecute("/Error/{0}");

// app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
