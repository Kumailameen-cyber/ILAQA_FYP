using System.Security.Claims;
using System.Threading.RateLimiting;
using ILAQA.Common;
using ILAQA.Data;
using ILAQA.Features.Auth;
using ILAQA.Features.Runner;
using ILAQA.Features.Runs;
using ILAQA.Models;
using ILAQA.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Database (PostgreSQL + PostGIS) ----------

var connectionString = config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Database connection string not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite()));
builder.Services.AddDataProtection();

// ---------- Identity (users and password rules) ----------

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---------- JWT sign-in (one active session per runner) ----------

var jwtKey = config["Jwt:Key"];
var jwtIssuer = config["Jwt:Issuer"];
var jwtAudience = config["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtKey) || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("JWT settings are missing.");

var signingKey = Convert.FromBase64String(jwtKey);
if (signingKey.Length < 32)
    throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ClockSkew = TimeSpan.Zero
        };

        // A token is only valid for the runner's latest login session:
        // logging in on another phone signs out the first one.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionId = context.Principal?.FindFirstValue("sessionId");

                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(sessionId, out var tokenSessionId))
                {
                    context.Fail("Invalid session.");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var activeSessionId = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.ActiveSessionId)
                    .SingleOrDefaultAsync();

                if (activeSessionId != tokenSessionId)
                    context.Fail("Session expired or replaced.");
            }
        };
    });
builder.Services.AddAuthorization();

// ---------- Feature services (used by the thin controllers) ----------

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RunnerService>();
builder.Services.AddScoped<RunService>();

// ---------- Shared services ----------

builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<GpsDistanceService>();
builder.Services.AddScoped<TerritoryDetectionService>();
builder.Services.AddScoped<TerritoryClaimService>();
builder.Services.AddHttpClient<SupabaseStorageService>();
builder.Services.AddHttpClient<GoogleRoadsService>(client => client.Timeout = TimeSpan.FromSeconds(5))
    .RemoveAllLoggers(); // The request URL contains the Google key: never log it.

// Hourly background jobs.
builder.Services.AddHostedService<PendingRegistrationCleanupService>();
builder.Services.AddHostedService<TerritoryInactivityService>();

// ---------- Global error handling ----------

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ---------- Rate limits ----------

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Sign-in endpoints, per IP address.
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Environment.IsDevelopment() ? 60 : 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    // GPS uploads, per signed-in runner (NFR-05).
    var gpsUploadsPerMinute = config.GetValue(
        "RateLimiting:GpsUploadsPerMinute", GpsUploadRateLimit.DefaultUploadsPerMinute);
    options.AddPolicy(GpsUploadRateLimit.PolicyName, context =>
        GpsUploadRateLimit.Partition(context, gpsUploadsPerMinute));

    // Tell the app when to try again.
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        return ValueTask.CompletedTask;
    };
});

// ---------- Controllers, CORS and Swagger ----------

builder.Services.AddCors(options =>
    options.AddPolicy("DevelopmentCors", policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var bearer = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT access token.",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    options.AddSecurityDefinition("Bearer", bearer);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { bearer, [] } });
});

// ---------- HTTP pipeline ----------

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("DevelopmentCors"); // Allows local Android testing over HTTP.
}
else
{
    app.UseHttpsRedirection();
}

// Authentication runs before the rate limiter, so the GPS upload limit
// can see which runner is calling.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.Run();
