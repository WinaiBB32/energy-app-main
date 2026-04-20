using EnergyApp.API.Data;
using EnergyApp.API.Hubs;
using EnergyApp.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

// ✅ แก้ปัญหา Npgsql ไม่ยอมรับ DateTime.Kind=Local
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// --- เชื่อมต่อ Database PostgreSQL ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSignalR();
// --- JWT Authentication ---
var jwtSecret = builder.Configuration["JwtSettings:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/maintenance") ||
                     path.StartsWithSegments("/api/v1/hubs/maintenance")))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// --- CORS ---
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(",", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.MapType<IFormFile>(() => new Microsoft.OpenApi.Models.OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});
builder.Services.AddSingleton<ISystemErrorLogStore, InMemorySystemErrorLogStore>();

// --- Rate Limiting ---
builder.Services.AddRateLimiter(options =>
{
    // Global: 300 req/min per IP สำหรับทุก endpoint
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    // "auth" policy: จำกัด 10 req/min per IP สำหรับ /login และ /register
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "คำขอมากเกินไป กรุณาลองใหม่ในอีกสักครู่ (Too Many Requests)"
        }, token);
    };
});

var app = builder.Build();

var vehicleCsvPath = app.Configuration["SeedSettings:VehicleCsvPath"];
if (!string.IsNullOrWhiteSpace(vehicleCsvPath) && File.Exists(vehicleCsvPath))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    new VehicleRecordSeeder(dbContext).SeedFromCsv(vehicleCsvPath);
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowVueApp");
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    var errorStore = context.RequestServices.GetRequiredService<ISystemErrorLogStore>();
    var alreadyLogged = false;

    try
    {
        await next();
    }
    catch (Exception ex)
    {
        alreadyLogged = true;
        errorStore.Add(new SystemErrorEntry
        {
            OccurredAtUtc = DateTime.UtcNow,
            Method = context.Request.Method,
            Path = context.Request.Path,
            StatusCode = 500,
            Message = ex.Message,
            ExceptionType = ex.GetType().Name,
            TraceId = context.TraceIdentifier
        });

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            message = "Internal server error",
            traceId = context.TraceIdentifier
        });
    }
    finally
    {
        if (!alreadyLogged && context.Response.StatusCode >= 500)
        {
            errorStore.Add(new SystemErrorEntry
            {
                OccurredAtUtc = DateTime.UtcNow,
                Method = context.Request.Method,
                Path = context.Request.Path,
                StatusCode = context.Response.StatusCode,
                Message = $"HTTP {context.Response.StatusCode}",
                ExceptionType = null,
                TraceId = context.TraceIdentifier
            });
        }
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<MaintenanceHub>("/hubs/maintenance");
app.MapHub<MaintenanceHub>("/api/v1/hubs/maintenance");
app.MapControllers();

// Auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Fix migrations out-of-sync: insert missing records into __EFMigrationsHistory
    var connStr = db.Database.GetConnectionString()!;
    using (var conn = new Npgsql.NpgsqlConnection(connStr))
    {
        conn.Open();

        // สร้าง __EFMigrationsHistory ถ้ายังไม่มี
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                    ""MigrationId"" character varying(150) NOT NULL,
                    ""ProductVersion"" character varying(32) NOT NULL,
                    CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
                )";
            cmd.ExecuteNonQuery();
        }

        var staleMigrations = new[] {
            ("20260415100000_AddVehicleMasterTables", "VehicleDepartments"),
            ("20260416000000_AddSarabanStatTable", "SarabanStats"),
        };

        foreach (var (migId, table) in staleMigrations)
        {
            // เช็คว่าตารางมีอยู่ใน DB
            bool tableExists;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM pg_tables WHERE schemaname='public' AND tablename=@t";
                cmd.Parameters.AddWithValue("t", table);
                tableExists = (long)(cmd.ExecuteScalar() ?? 0L) > 0;
            }

            if (tableExists)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                    SELECT @id, '8.0.0'
                    WHERE NOT EXISTS (
                        SELECT 1 FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" = @id
                    )";
                cmd.Parameters.AddWithValue("id", migId);
                var rows = cmd.ExecuteNonQuery();
                Console.WriteLine($"[MigrationFix] {migId}: inserted={rows > 0}");
            }
        }
    }

    // แสดง pending migrations ก่อนรัน
    var pending = db.Database.GetPendingMigrations().ToList();
    Console.WriteLine($"[Pending migrations: {pending.Count}]");
    foreach (var m in pending) Console.WriteLine($"  - {m}");

    db.Database.Migrate();
}

app.Run("http://0.0.0.0:5008");
