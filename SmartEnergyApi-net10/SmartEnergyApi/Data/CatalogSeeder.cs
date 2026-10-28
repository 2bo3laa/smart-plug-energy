using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Data;

/// <summary>
/// Brings an existing smart_energy.db up to date (new tables/columns) and loads the appliance catalogue.
/// Safe to run on every start.
/// </summary>
public static class CatalogSeeder
{
    private static readonly (string Key, string Name, double Watts)[] Types =
    {
        ("refrigerator",     "Refrigerator",      180),
        ("freezer",          "Freezer",           160),
        ("washing_machine",  "Washing Machine",   500),
        ("air_conditioner",  "Air Conditioner",  1300),
        ("tv",               "TV",                100),
        ("blender",          "Blender",           500),
        ("microwave",        "Microwave",        1200),
        ("water_heater",     "Water Heater",     1800),
        ("dishwasher",       "Dishwasher",       1800),
        ("fan",              "Fan",                60),
        ("iron",             "Iron",             2000),
        ("vacuum",           "Vacuum Cleaner",   1600),
        ("kettle",           "Kettle",           2000),
        ("toaster",          "Toaster",           900),
        ("computer",         "Computer",          150),
        ("other",            "Other",               0),
    };

    public static void Apply(AppDbContext db)
    {
        UpdateSchema(db);
        SeedTypes(db);
        SeedItemsFromCsv(db);
    }

    // ---------- schema patch for databases created by an older version ----------
    private static void UpdateSchema(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"ApplianceTypes\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_ApplianceTypes\" PRIMARY KEY AUTOINCREMENT, " +
            "\"Key\" TEXT NOT NULL, \"Name\" TEXT NOT NULL, \"DefaultWatts\" REAL NOT NULL, \"SortOrder\" INTEGER NOT NULL)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_ApplianceTypes_Key\" ON \"ApplianceTypes\" (\"Key\")");

        db.Database.ExecuteSqlRaw(
            "CREATE TABLE IF NOT EXISTS \"CatalogItems\" (" +
            "\"Id\" INTEGER NOT NULL CONSTRAINT \"PK_CatalogItems\" PRIMARY KEY AUTOINCREMENT, " +
            "\"TypeKey\" TEXT NOT NULL, \"Brand\" TEXT NOT NULL, \"Model\" TEXT NOT NULL, " +
            "\"RatedWatts\" REAL NOT NULL, \"Source\" TEXT NOT NULL)");
        db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_CatalogItems_TypeKey_Brand_Model\" ON \"CatalogItems\" (\"TypeKey\", \"Brand\", \"Model\")");

        foreach (var col in new[] { "Type", "Brand", "Model" })
        {
            if (!ColumnExists(db, "Devices", col))
                db.Database.ExecuteSqlRaw($"ALTER TABLE \"Devices\" ADD COLUMN \"{col}\" TEXT NULL");
        }
    }

    private static bool ColumnExists(AppDbContext db, string table, string column)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (string.Equals(r.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    // ---------- data ----------
    private static void SeedTypes(AppDbContext db)
    {
        var existing = db.ApplianceTypes.ToList().ToDictionary(t => t.Key);
        var order = 0;
        foreach (var (key, name, watts) in Types)
        {
            order++;
            if (existing.TryGetValue(key, out var t))
            {
                t.Name = name; t.DefaultWatts = watts; t.SortOrder = order;
            }
            else
            {
                db.ApplianceTypes.Add(new ApplianceType { Key = key, Name = name, DefaultWatts = watts, SortOrder = order });
            }
        }
        db.SaveChanges();
    }

    private static void SeedItemsFromCsv(AppDbContext db)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "catalog_seed.csv");
        if (!File.Exists(path))
        {
            Console.WriteLine($"[catalog] {path} not found - catalogue not loaded");
            return;
        }

        var validTypes = Types.Select(t => t.Key).ToHashSet();
        var existing = db.CatalogItems.ToList()
            .ToDictionary(c => $"{c.TypeKey}|{c.Brand}|{c.Model}".ToLowerInvariant());

        int added = 0, updated = 0, skipped = 0;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("type,", StringComparison.OrdinalIgnoreCase)) continue;
            var p = line.Split(',');
            if (p.Length < 5 ||
                !double.TryParse(p[3].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var watts) || watts <= 0 ||
                !validTypes.Contains(p[0].Trim()))
            {
                skipped++;
                Console.WriteLine($"[catalog] skipped bad line: {line}");
                continue;
            }
            var type = p[0].Trim(); var brand = p[1].Trim(); var model = p[2].Trim();
            var source = p[4].Trim().ToLowerInvariant() == "manufacturer" ? "manufacturer" : "typical";
            var key = $"{type}|{brand}|{model}".ToLowerInvariant();

            if (existing.TryGetValue(key, out var item))
            {
                if (Math.Abs(item.RatedWatts - watts) > 0.001 || item.Source != source)
                {
                    item.RatedWatts = watts; item.Source = source; updated++;
                }
            }
            else
            {
                var n = new CatalogItem { TypeKey = type, Brand = brand, Model = model, RatedWatts = watts, Source = source };
                db.CatalogItems.Add(n);
                existing[key] = n;
                added++;
            }
        }
        db.SaveChanges();
        Console.WriteLine($"[catalog] added {added}, updated {updated}, skipped {skipped}");
    }
}
