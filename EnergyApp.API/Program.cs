using EnergyApp.API.Data;
using EnergyApp.API.Hubs;
using EnergyApp.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(",") ?? ["http://localhost:5173,http://192.168.118.106:5173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVueApp", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .SetIsOriginAllowed(_ => true) // Allow all origins for testing
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ISystemErrorLogStore, InMemorySystemErrorLogStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seeder = new VehicleRecordSeeder(dbContext);
    var csvPath = @"C:\Users\beeman\Downloads\บันทึกข้อมูลรถยนต์ - CarData.csv";
    if (File.Exists(csvPath))
    {
        seeder.SeedFromCsv(csvPath);
    }
    else
    {
        Console.WriteLine($"[WARN] CSV file not found: {csvPath}. Skipping vehicle record seeding.");
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowVueApp");

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
    db.Database.Migrate();

    // Ensure SarabanStats table exists (manual migration fallback)
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""SarabanStats"" (
            ""Id"" uuid NOT NULL,
            ""DepartmentId"" uuid NULL,
            ""BookType"" text NOT NULL DEFAULT '',
            ""BookName"" text NOT NULL DEFAULT '',
            ""RecordMonth"" timestamptz NOT NULL DEFAULT now(),
            ""ReceiverName"" text NOT NULL DEFAULT '',
            ""ReceivedCount"" integer NOT NULL DEFAULT 0,
            ""InternalPaperCount"" integer NOT NULL DEFAULT 0,
            ""InternalDigitalCount"" integer NOT NULL DEFAULT 0,
            ""ExternalPaperCount"" integer NOT NULL DEFAULT 0,
            ""ExternalDigitalCount"" integer NOT NULL DEFAULT 0,
            ""ForwardedCount"" integer NOT NULL DEFAULT 0,
            ""RecordedBy"" text NOT NULL DEFAULT '',
            ""CreatedAt"" timestamptz NOT NULL DEFAULT now(),
            CONSTRAINT ""PK_SarabanStats"" PRIMARY KEY (""Id"")
        )
    ");
}

app.Run("http://0.0.0.0:5008");
