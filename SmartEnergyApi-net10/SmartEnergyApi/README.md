# Smart Consuming Energy — ASP.NET Core Backend

This replaces the Node.js/Express prototype (`backend_full.zip`) with the
ASP.NET Core Web API + SQL Server stack described in the project report
(Chapter 1 §1.1 overview, Chapter 3 §3.4.2 system requirements). The Flutter
app in `smart_plug_app` needs **zero changes** — every route, JSON field
name (camelCase), status code and the port (`4000`) are unchanged. New,
additive endpoints were added for the real ESP32 hardware side and for the
data-analysis work the report calls for.

## Stack

- **ASP.NET Core 8** Web API (controllers)
- **Entity Framework Core** — SQLite by default (zero-config), one config
  flag away from **SQL Server**
- **JWT bearer auth** (`Microsoft.AspNetCore.Authentication.JwtBearer`)
- **BCrypt** password hashing (same algorithm the Node backend used)
- **Swagger/OpenAPI** at `/swagger` in Development

## Running it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
cd SmartEnergyApi
dotnet restore
dotnet run
```

The API listens on `http://localhost:4000` (see `Properties/launchSettings.json`),
matching `ApiClient.baseUrl = 'http://10.0.2.2:4000/api'` in the Flutter app
unchanged. On first run it creates `smart_energy.db` (SQLite) next to the
executable and seeds the default tariff tiers.

Swagger UI: `http://localhost:4000/swagger` — every endpoint, with a "Try it
out" button and a bearer-token field for the protected ones.

## Switching to SQL Server (to match the report exactly)

1. In `appsettings.json`, set:
   ```json
   "Database": { "Provider": "SqlServer" },
   "ConnectionStrings": { "SqlServer": "Server=localhost;Database=SmartEnergyDb;Trusted_Connection=True;TrustServerCertificate=True;" }
   ```
2. Adjust the connection string for your SQL Server instance.
3. For production, replace the `Database.EnsureCreated()` call in `Program.cs`
   with proper EF Core migrations:
   ```bash
   dotnet tool install --global dotnet-ef
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

## Data model (matches the report's ERD, Figure 3.4)

| Table | Purpose |
|---|---|
| `Users` | accounts (`FR1`, `FR2`, `FR14`) |
| `Devices` | one row per ESP32 sensing node; also holds the latest live snapshot for fast dashboard reads |
| `Readings` | **real time-series sensor data** (voltage/current/power/energy/frequency/PF) pushed by the ESP32 — `FR4`–`FR6` |
| `Thresholds` | per-device power limit — `FR9` |
| `Alerts` | generated when a threshold is crossed, or when a device is toggled — `FR10` |
| `TariffTiers` | stepped kWh pricing used for cost estimation — `FR11`, `FR13` |
| `Schedules` | on/off schedules (kept from the prototype; scheduled auto-control itself is out of scope per §1.5) |
| `PasswordResets` | forgot/reset-password flow |

## Data analysis: what changed vs. the prototype

The Node prototype's `/api/statistics` endpoint returned **seeded random
numbers** — there was no real readings table to summarize. This version adds
a `Readings` table fed by the ESP32 (`POST /api/devices/{id}/readings`) and a
`StatisticsService` that computes the dashboard from real data:

- **Energy per time bucket** — trapezoidal integration of instantaneous
  power `P(t)` over each reading interval (`kWh = avg(P) × Δt / 1000`),
  rather than a flat average. Gaps longer than 6 hours (e.g. a Wi-Fi outage,
  per the report's general constraints §1.6) are skipped instead of
  fabricating energy for them.
- **Day-over-day change** — real percentage difference between the last 24h
  and the previous 24h of readings.
- **Cost estimate** — `TariffService` applies Egypt's *stepped* tariff
  correctly: the first N kWh at the cheapest tier, the next band at the next
  tier, etc. (not a flat multiply), matching `FR11`/`FR13` and the tiered
  structure mentioned in Chapter 3 §3.4.3 (data domain).
  Tiers are editable via `/api/tariff-tiers` (admin role).
- **Appliance/device breakdown** — real per-device share of total energy in
  the requested window, replacing the hardcoded `AC/Heater/Lighting/Others`
  percentages in the prototype.
- A device with no readings yet (freshly registered, not wired up) falls
  back to its last-known snapshot values so the UI never breaks.

## Hardware-facing endpoints (new)

These are what the ESP32 firmware talks to; they're authenticated with a
per-device key (`Device.DeviceKey`, generated on creation) sent as the
`X-Device-Key` header — separate from the user's JWT, since the device isn't
a logged-in user.

- `POST /api/devices/{id}/readings` — push one PZEM-004T V3 measurement
  (`voltage`, `current`, `power`, `energy`, `frequency`, `powerFactor`).
  Automatically updates the device's live snapshot, accumulates `todayKwh`,
  and raises a `power` alert if an active threshold is exceeded.
- `GET /api/devices/{id}/command` — the firmware polls this; returns
  `{ "command": true | false | null }` for a pending relay on/off request
  queued by `POST /api/devices/{id}/toggle` from the app (`FR16`/`FR17`).
  The command is cleared once fetched (one-shot).
- `GET /api/devices/{id}/readings?take=200` — historical readings for a
  device (used by the app for detailed charts beyond the summary in
  `/api/statistics`).
- `GET/PUT /api/devices/{id}/threshold` — read/set the per-device power
  threshold (`FR9`).
- `GET/POST/PATCH/DELETE /api/tariff-tiers` — tariff-tier management
  (`FR13`); reads are open to any logged-in user, writes require
  `User.Role == "admin"`.

## Endpoint parity with the old Node backend

Every route below is byte-for-byte identical in path, method, request body,
response shape and status codes to `backend_full/backend/src/routes/*.js` —
only the implementation moved from Express+SQLite(node:sqlite) to
ASP.NET Core+EF Core:

```
GET    /api/health
POST   /api/auth/signup
POST   /api/auth/login
POST   /api/auth/change-password
POST   /api/auth/forgot-password
POST   /api/auth/reset-password
GET    /api/devices
GET    /api/devices/scan
GET    /api/devices/{id}
POST   /api/devices
PATCH  /api/devices/{id}
DELETE /api/devices/{id}
POST   /api/devices/{id}/toggle
GET    /api/devices/{deviceId}/schedules
POST   /api/devices/{deviceId}/schedules
PATCH  /api/schedules/{id}
DELETE /api/schedules/{id}
PATCH  /api/schedules/{id}/toggle
GET    /api/alerts
PATCH  /api/alerts/{id}/read
PATCH  /api/alerts/read-all
DELETE /api/alerts/{id}
GET    /api/statistics
GET    /api/profile
PATCH  /api/profile
```

## Notes / next steps

- `Jwt:Secret` in `appsettings.json` is a placeholder — set a real secret via
  the `Jwt__Secret` environment variable before deploying anywhere public.
- `forgot-password` still returns `devCode` in the response for local testing,
  exactly like the prototype — wire up a real email/SMS provider and remove
  it before shipping.
- Consider adding rate-limiting on `POST /api/devices/{id}/toggle` per the
  report's safety non-functional requirement (Table 3.3: "relay commands are
  authenticated and rate-limited").
