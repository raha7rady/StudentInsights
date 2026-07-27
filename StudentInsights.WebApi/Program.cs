using System.Text;
using FluentValidation;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudentInsights.Application.Common.Behaviors;
using StudentInsights.Application.Common.Interfaces;
using StudentInsights.Application.Features.Auth.Commands.Register;
using StudentInsights.Infrastructure.BackgroundJobs;
using StudentInsights.Infrastructure.Email;
using StudentInsights.Infrastructure.Persistence;
using StudentInsights.Infrastructure.Security;
using StudentInsights.WebApi.Middleware;
using StudentInsights.WebApi.Serialization;
using StudentInsights.WebApi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // See UtcDateTimeConverter for the full rationale: normalizes every
        // DateTime crossing the API boundary (request and response) to
        // DateTimeKind.Utc, matching this project's *Utc naming convention.
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "StudentInsights API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: eyJhbGciOiJIUzI1NiIs..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS  required because the React frontend (per the architecture doc,
// 9) is a separately hosted SPA, never served from this API's origin.
// Named policy, origins/headers/methods pulled from configuration rather
// than hard-coded, so the allowed origin(s) can differ between
// Development (Vite's localhost port) and Production (the deployed
// frontend URL) without a code change.
const string FrontendCorsPolicy = "FrontendCorsPolicy";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
        // No AllowCredentials(): the API is Bearer-token authenticated,
        // not cookie-authenticated, so browsers never need to send
        // credentials cross-origin for this API.
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Hangfire  recurring background jobs (currently just Notification
// generation, see BackgroundJobs/NotificationGenerationJob.cs). Reuses
// the same DefaultConnection SQL Server database as ApplicationDbContext
// rather than a second connection string: Hangfire creates its own
// "HangFire" schema inside the existing database, so no new database is
// introduced for this module.
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));

builder.Services.AddHangfireServer();

// MediatR  scans the assembly containing RegisterCommand for all handlers.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// FluentValidation  scans the same assembly for every IValidator<T>
// (e.g. CreateCourseCommandValidator), picked up automatically by
// ValidationBehavior above.
builder.Services.AddValidatorsFromAssembly(typeof(RegisterCommand).Assembly);

// Auth-related settings & services  validated at startup instead of failing lazily.
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(s => !string.IsNullOrWhiteSpace(s.Secret) && s.Secret.Length >= 32,
        "Jwt:Secret must be set and at least 32 characters (256 bits) via User Secrets or environment variables.")
    .ValidateOnStart();

builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
    .Validate(s => !string.IsNullOrWhiteSpace(s.SmtpHost), "Email:SmtpHost must be configured.")
    .ValidateOnStart();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Hangfire dashboard, gated by HangfireDashboardAuthorizationFilter (see
// that class for the full rationale) — never left open in Production.
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter(app.Environment, app.Configuration) }
});

// Registering a recurring job is itself idempotent — re-running this on
// every app start just re-registers the same schedule under the same
// job id, it does not create duplicates — so no separate seed/migration
// step is needed for it.
RecurringJob.AddOrUpdate<NotificationGenerationJob>(
    "notification-generation",
    job => job.RunAsync(CancellationToken.None),
    Cron.Hourly);

app.Run();