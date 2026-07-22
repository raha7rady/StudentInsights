using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudentInsights.Application.Common.Interfaces;
using StudentInsights.Application.Features.Auth.Commands.Register;
using StudentInsights.Infrastructure.Email;
using StudentInsights.Infrastructure.Persistence;
using StudentInsights.Infrastructure.Security;
using StudentInsights.WebApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// MediatR — scans the assembly containing RegisterCommand for all handlers.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly));

// Auth-related settings & services — validated at startup instead of failing lazily.
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(s => !string.IsNullOrWhiteSpace(s.Secret) && s.Secret.Length >= 32,
        "Jwt:Secret must be set and at least 32 characters (256 bits) via User Secrets or environment variables.")
    .ValidateOnStart();

builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
    .Validate(s => !string.IsNullOrWhiteSpace(s.SmtpHost), "Email:SmtpHost must be configured.")
    .ValidateOnStart();

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();