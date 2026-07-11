using System.Reflection;
using System.Text;
using hacman_app_back.config;
using hacman_app_back.Modules.GalleryCatalog.Application;
using hacman_app_back.Modules.GalleryCatalog.Infrastructure;
using hacman_app_back.Modules.Identity.Api;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Infrastructure;
using hacman_app_back.Modules.LevelTemplate.Application;
using hacman_app_back.Modules.LevelTemplate.Infrastructure;
using hacman_app_back.Modules.PlayerProgress.Application;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;
using hacman_app_back.Modules.Scoring.Application;
using hacman_app_back.Modules.Scoring.Infrastructure;
using hacman_app_back.SharedKernel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NHibernate;
using Npgsql;
using Prometheus;

// Configure Npgsql for JSON serialization
// This codebase uses raw ADO.NET connections, so we keep GlobalTypeMapper despite its deprecation in Npgsql 8.0.
// The recommended path is NpgsqlDataSourceBuilder, but that requires significant architectural changes.
// Suppressing the CS0618 warning is acceptable here.
// See: https://www.npgsql.org/doc/release-notes/8.0.html
#pragma warning disable CS0618
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
#pragma warning restore CS0618

LoadDotEnv(new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".env")),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env")),
    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env"))
});

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
                       ?? throw new InvalidOperationException(
                           "Missing ConnectionStrings:Default (configure it in appsettings/User Secrets/environment variable).");

var schema = builder.Configuration["NHibernate:Schema"]
             ?? "hacman";

var sessionFactory = NHibernateHelper.CreateSessionFactory(connectionString, schema);
builder.Services.AddSingleton(sessionFactory);
builder.Services.AddScoped(sp => sp.GetRequiredService<ISessionFactory>().OpenSession());

builder.Services.AddScoped<IAuthClaimsParser, AuthClaimsParser>();
builder.Services.AddScoped<Func<IUnitOfWork>>(_ => () => new UnitOfWork(connectionString));

builder.Services.AddScoped<IGalleryRepository>(sp =>
    new GalleryRepository(connectionString, sp.GetRequiredService<ILogger<GalleryRepository>>()));
builder.Services.AddScoped<IGalleryTypeRepository>(sp =>
    new GalleryTypeRepository(connectionString, sp.GetRequiredService<ILogger<GalleryTypeRepository>>()));
builder.Services.AddScoped<IGallerySubtypeRepository>(sp =>
    new GallerySubtypeRepository(connectionString, sp.GetRequiredService<ILogger<GallerySubtypeRepository>>()));
builder.Services.AddScoped<IGalleryThemeRepository>(sp =>
    new GalleryThemeRepository(connectionString, sp.GetRequiredService<ILogger<GalleryThemeRepository>>()));
builder.Services.AddScoped<IUserRepository>(sp =>
    new UserRepository(connectionString, sp.GetRequiredService<ILogger<UserRepository>>()));
builder.Services.AddScoped<IUserGalleryRepository>(sp =>
    new UserGalleryRepository(connectionString, sp.GetRequiredService<ILogger<UserGalleryRepository>>()));
builder.Services.AddScoped<ILevelScoreRepository>(sp =>
    new LevelScoreRepository(connectionString, sp.GetRequiredService<ILogger<LevelScoreRepository>>()));
builder.Services.AddScoped<IMazeLevelRepository>(sp =>
    new MazeLevelRepository(connectionString, sp.GetRequiredService<ILogger<MazeLevelRepository>>()));

builder.Services.AddScoped<IGalleryTypeService, GalleryTypeService>();
builder.Services.AddScoped<IGallerySubtypeService, GallerySubtypeService>();
builder.Services.AddScoped<IGalleryThemeService, GalleryThemeService>();
builder.Services.AddScoped<IUserProvider, UserProvider>();
builder.Services.AddScoped<IUserUpdater>(provider =>
    new UserUpdater(
        provider.GetRequiredService<IUserRepository>(),
        provider.GetRequiredService<IGalleryRepository>(),
        provider.GetRequiredService<IUserGalleryRepository>(),
        provider.GetRequiredService<Func<IUnitOfWork>>()));
builder.Services.AddScoped<IUserGalleryProvider, UserGalleryProvider>();
builder.Services.AddScoped<ILevelScoreService, LevelScoreService>();
builder.Services.AddScoped<IMazeLevelProvider, MazeLevelProvider>();

builder.Services.AddHttpLogging(logging => { logging.LoggingFields = HttpLoggingFields.All; });
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "hacman-app-back API", Version = "v1" });
});


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false, // Do not validate the issuer
            ValidateAudience = false, // Audience not specified in the token
            ValidateLifetime = true,
            ValidateIssuerSigningKey = false,
            // Signature validation is disabled for Nebula integration.
            SignatureValidator = delegate(string token, TokenValidationParameters parameters)
            {
                var jwt = new JsonWebToken(token);
                return jwt;
            },
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"]
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["accessToken"];
                context.Token = token;
                Console.WriteLine(
                    $"[JWT] Token from cookie 'accessToken' present: {!string.IsNullOrEmpty(token)} (Length: {token?.Length ?? 0})");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"[JWT] Authentication failed: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("[JWT] Token validated successfully");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"[JWT] Challenge: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5146",
                "https://localhost:7196",
                "http://localhost:5196",
                "https://localhost:7001"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Pac-Man favicon for the Swagger UI browser tab (classic yellow; the frontend uses the same
// shape in matrix-green). Embedded as an inline data URI so no static-file serving is needed —
// Swagger UI is the backend's only browser-facing surface, and only in Development.
const string faviconSvg =
    "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\" width=\"100\" height=\"100\">" +
    "<path d=\"M50 50 L89.7 28.9 A45 45 0 1 0 89.7 71.1 Z\" fill=\"#ffce00\"/>" +
    "<circle cx=\"44\" cy=\"27\" r=\"6\" fill=\"#1a1400\"/></svg>";
var faviconDataUri = "data:image/svg+xml;base64," +
                     Convert.ToBase64String(Encoding.UTF8.GetBytes(faviconSvg));
var swaggerFaviconHead = $"<link rel=\"icon\" type=\"image/svg+xml\" href=\"{faviconDataUri}\">";

app.UseHttpLogging();

// Prometheus metrics. UseMetricServer exposes GET /metrics at the container root, registered
// before authentication so the scraper needs no JWT (Prometheus scrapes the container directly on
// its port inside the Docker network). UseHttpMetrics records request count/duration/in-progress
// labelled by method, route, and status code. The /metrics scrape itself is terminal here, so it
// is not counted as an HTTP request metric.
app.UseMetricServer();
app.UseHttpMetrics();

// Eagerly register domain metric collectors at startup so their families are present in /metrics
// from boot (otherwise a static, lazily-initialised counter only appears after its first Inc, leaving
// dashboards/alerts referencing it with "no data" until the first score is ever submitted).
_ = LevelScoreService.ScoresSubmitted;

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwagger(c => { c.RouteTemplate = "api/swagger/{documentName}/swagger.json"; });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "hacman-app-back API V1");
        c.RoutePrefix = "swagger";
        c.HeadContent = swaggerFaviconHead;
    });

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("v1/swagger.json", "hacman-app-back API V1");
        c.RoutePrefix = "api/swagger";
        c.HeadContent = swaggerFaviconHead;
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

var assembly = Assembly.GetExecutingAssembly();
var version = builder.Configuration["App:Version"]
              ?? assembly.GetName().Version?.ToString()
              ?? "unknown";
var informationalVersion = builder.Configuration["App:InformationalVersion"]
                           ?? assembly
                               .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute),
                                   inherit: false)
                               .OfType<AssemblyInformationalVersionAttribute>()
                               .FirstOrDefault()
                               ?.InformationalVersion ?? version;

app.MapGet("/v1/hello", () =>
        Results.Ok(new { status = "ok", message = "hello" }))
    .WithName("HealthHelloV1")
    .WithTags("Diagnostics");

app.MapGet("/api/v1/hello", () =>
        Results.Ok(new { status = "ok", message = "hello" }))
    .WithName("HealthHelloApiV1")
    .WithTags("Diagnostics");

app.MapGet("/v1/version", () =>
        Results.Ok(new { version, informationalVersion }))
    .WithName("VersionV1")
    .WithTags("Diagnostics");

app.MapGet("/api/v1/version", () =>
        Results.Ok(new { version, informationalVersion }))
    .WithName("VersionApiV1")
    .WithTags("Diagnostics");

app.MapControllers();

app.Run();

void LoadDotEnv(IEnumerable<string> candidatePaths)
{
    foreach (var path in candidatePaths.Distinct())
    {
        if (!File.Exists(path))
        {
            continue;
        }

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            {
                line = line[7..].Trim();
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        break;
    }
}