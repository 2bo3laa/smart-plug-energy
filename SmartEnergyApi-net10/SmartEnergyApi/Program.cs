using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartEnergyApi.Data;
using SmartEnergyApi.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Optional private settings file, created once on the server and never committed to GitHub, e.g.
// { "Jwt": { "Secret": "a-long-random-string" }, "Auth": { "ExposeResetCode": false }, "Google": { "ClientIds": "..." } }
// Re-publishing the API does not overwrite it because it is not part of the published files.
builder.Configuration.AddJsonFile("secrets.local.json", optional: true, reloadOnChange: false);

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
builder.Services.AddScoped<CardService>();
builder.Services.AddScoped<GoogleAuthService>();
builder.Services.AddHttpClient();
builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (jwtSecret.StartsWith("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    app.Logger.LogWarning("Jwt:Secret is still the public default: anyone can forge login tokens. Set your own in secrets.local.json.");
}

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

        // New Users columns (prepaid card, tier tracking, linked Google account).
        var userColumns = new (string Name, string Sqlite, string SqlServer)[]
        {
            ("CardBalanceBase", "REAL NOT NULL DEFAULT 0", "float NOT NULL DEFAULT 0"),
            ("CardBaseAt", "TEXT NULL", "datetime2 NULL"),
            ("CardAlertLevel", "INTEGER NOT NULL DEFAULT 0", "int NOT NULL DEFAULT 0"),
            ("TierMonth", "INTEGER NOT NULL DEFAULT 0", "int NOT NULL DEFAULT 0"),
            ("TierIndex", "INTEGER NOT NULL DEFAULT 0", "int NOT NULL DEFAULT 0"),
            ("GoogleId", "TEXT NULL", "nvarchar(64) NULL"),
            ("GoogleEmail", "TEXT NULL", "nvarchar(180) NULL"),
            ("MonthKey", "INTEGER NOT NULL DEFAULT 0", "int NOT NULL DEFAULT 0"),
            ("MonthKwh", "REAL NOT NULL DEFAULT 0", "float NOT NULL DEFAULT 0"),
            ("CardCountersReady", "INTEGER NOT NULL DEFAULT 0", "bit NOT NULL DEFAULT 0"),
        };
        foreach (var col in userColumns)
        {
            try
            {
                if (context.Database.IsSqlite())
                    context.Database.ExecuteSqlRaw($"ALTER TABLE \"Users\" ADD COLUMN \"{col.Name}\" {col.Sqlite}");
                else
                    context.Database.ExecuteSqlRaw($"IF COL_LENGTH('Users','{col.Name}') IS NULL ALTER TABLE [Users] ADD [{col.Name}] {col.SqlServer}");
            }
            catch { /* column already exists */ }
        }

        // New Devices columns (online detection + per-device card counters).
        var deviceColumns = new (string Name, string Sqlite, string SqlServer)[]
        {
            ("LastSeenAt", "TEXT NULL", "datetime2 NULL"),
            ("CardKwh", "REAL NOT NULL DEFAULT 0", "float NOT NULL DEFAULT 0"),
            ("CardCostEgp", "REAL NOT NULL DEFAULT 0", "float NOT NULL DEFAULT 0"),
        };
        foreach (var col in deviceColumns)
        {
            try
            {
                if (context.Database.IsSqlite())
                    context.Database.ExecuteSqlRaw($"ALTER TABLE \"Devices\" ADD COLUMN \"{col.Name}\" {col.Sqlite}");
                else
                    context.Database.ExecuteSqlRaw($"IF COL_LENGTH('Devices','{col.Name}') IS NULL ALTER TABLE [Devices] ADD [{col.Name}] {col.SqlServer}");
            }
            catch { /* column already exists */ }
        }

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

// HTTPS (if any) is terminated by the hosting platform, so no HTTPS redirection here.
app.UseDefaultFiles();
app.UseStaticFiles();   // serves the Flutter web build (wwwroot) for iPhone / browser use
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("ok"));
app.MapControllers();

app.Run();