using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartEnergyApi.Data;
using SmartEnergyApi.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Database Context based on Provider in appsettings.json
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite"));
    }
});

// 2. Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "CHANGE_ME_smart-energy-dev-secret-please-rotate";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// 3. Add Services & Controllers
builder.Services.AddControllers();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<TariffService>();
builder.Services.AddScoped<StatisticsService>();
builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. Automatic Database Creation & Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();

        // EnsureCreated() does not alter an existing database, so add the RatedWatts column if it is missing.
        try
        {
            if (context.Database.IsSqlite())
                context.Database.ExecuteSqlRaw("ALTER TABLE \"Devices\" ADD COLUMN \"RatedWatts\" REAL NULL");
            else
                context.Database.ExecuteSqlRaw("IF COL_LENGTH('Devices','RatedWatts') IS NULL ALTER TABLE [Devices] ADD [RatedWatts] float NULL");
        }
        catch { /* column already exists */ }

        SeedData.SeedDefaultTariffTiers(context);

        // Appliance catalogue (types/brands/models). Its schema patch is SQLite-specific;
        // on SQL Server EnsureCreated() already builds the new tables for a fresh database.
        if (context.Database.IsSqlite())
            CatalogSeeder.Apply(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while creating or migrating the database.");
    }
}

// 5. Configure Middleware Pipeline
// Swagger stays on in every environment so the hosted API can be demoed/tested.
app.UseSwagger();
app.UseSwaggerUI();

// HTTPS is terminated by the hosting platform (Render), so no HTTPS redirection here.
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("ok"));
app.MapControllers();

app.Run();