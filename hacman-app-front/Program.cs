using hacman_app_front.Components;
using hacman_app_front.Services;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Configure forwarded headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Assuming Traefik is on the same machine or in a trusted network.
    // If not, you might need to specify known proxies.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Read important URLs from configuration (environment variables, appsettings, etc.).
// If a value is missing we keep a sensible default; missing-config warnings are logged
// via app.Logger right after the app is built (no ILogger exists yet at this point).
var appBasePath = builder.Configuration["AppBasePath"] ?? "/";

var hacmanApiBaseUrl = builder.Configuration["HacmanApiBaseUrl"] ?? "https://milkyway.test/hacman/api";

if (!hacmanApiBaseUrl.EndsWith('/'))
{
    hacmanApiBaseUrl += "/";
}

// For server-to-server calls (e.g. inside Docker) prefer the internal service URL so we bypass
// the external reverse proxy (Traefik) which may not be reachable from within the container.
var hacmanApiInternalBaseUrl = builder.Configuration["HacmanApiInternalUrl"] ?? hacmanApiBaseUrl;
if (!hacmanApiInternalBaseUrl.EndsWith('/')) hacmanApiInternalBaseUrl += "/";
var hacmanApiServerSideUri = new Uri(hacmanApiInternalBaseUrl);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // Default is 32 KB which is too small when gallery data is passed through the circuit.
        // Set to 512 KB to safely handle large payloads without killing the circuit.
        options.MaximumReceiveMessageSize = 512 * 1024;
    });
// Enable detailed Blazor circuit errors in development so we can see stacktraces for circuit exceptions
builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
{
    options.DetailedErrors = true;
});
builder.Services.AddControllers(); // Enable controller endpoints (AuthController)
builder.Services.AddHttpLogging(logging => { logging.LoggingFields = HttpLoggingFields.All; });
builder.Services.AddHttpClient(); // For server-to-server token exchange
builder.Services.AddAntiforgery(); // Keep if antiforgery is used

// Self-signed / private-CA certificates (e.g. the homelab's milkyway.test domain behind
// Traefik) are only trusted in Development, or when a deployment explicitly opts in via
// TrustSelfSignedCertificates=true. Production with proper certificates keeps full validation.
var trustAnyServerCertificate = builder.Environment.IsDevelopment()
                                || builder.Configuration.GetValue<bool>("TrustSelfSignedCertificates");

builder.Services.AddHttpClient("NebulaClient")
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        if (trustAnyServerCertificate)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return handler;
    });

builder.Services.AddScoped<ILocalStorageService, LocalStorageService>();
builder.Services.AddScoped<UserSessionState>();
builder.Services.AddHttpContextAccessor();

// Development-only setting to trust self-signed certificates.
builder.Services.AddScoped(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("NebulaClient");
    // Nebula API base address should come from configuration (NebulaApiBaseUrl).
    var nebulaApiBaseUrl = builder.Configuration["NebulaApiBaseUrl"] ??
                           builder.Configuration["NEBULA_TOKEN_ENDPOINT"] ?? "https://localhost:7196";
    if (string.IsNullOrEmpty(builder.Configuration["NebulaApiBaseUrl"]) &&
        string.IsNullOrEmpty(builder.Configuration["NEBULA_TOKEN_ENDPOINT"]))
    {
        sp.GetRequiredService<ILogger<Program>>().LogWarning(
            "NebulaApiBaseUrl and NEBULA_TOKEN_ENDPOINT not set; defaulting Nebula client to https://localhost:7196 (development only).");
    }

    httpClient.BaseAddress = new Uri(nebulaApiBaseUrl);
    return httpClient;
});

// All three Hacman API services share one client factory: token resolution
// (HttpContext cookie → UserSessionState fallback) and HttpClient construction
// live in HacmanApiClientFactory instead of being copy-pasted per registration.
builder.Services.AddScoped(sp => new HacmanApiClientFactory(
    sp.GetRequiredService<IHttpContextAccessor>(),
    sp.GetRequiredService<UserSessionState>(),
    sp.GetRequiredService<ILogger<HacmanApiClientFactory>>(),
    hacmanApiServerSideUri,
    trustAnyServerCertificate));

builder.Services.AddScoped<IUserApiService>(sp => new UserApiService(
    sp.GetRequiredService<HacmanApiClientFactory>().CreateClient(),
    sp.GetRequiredService<ILogger<UserApiService>>()));

builder.Services.AddScoped<IScoresApiService>(sp => new ScoresApiService(
    sp.GetRequiredService<HacmanApiClientFactory>().CreateClient(),
    sp.GetRequiredService<ILogger<ScoresApiService>>(),
    sp.GetRequiredService<UserSessionState>()));

builder.Services.AddScoped<IAchievementsApiService>(sp => new AchievementsApiService(
    sp.GetRequiredService<HacmanApiClientFactory>().CreateClient(),
    sp.GetRequiredService<ILogger<AchievementsApiService>>()));

builder.Services.AddScoped<IUserGalleryApiService>(sp => new UserGalleryApiService(
    sp.GetRequiredService<HacmanApiClientFactory>().CreateClient(),
    sp.GetRequiredService<ILogger<UserGalleryApiService>>()));


// Scoped, not singleton: this holds per-user gallery/unlock state, and in Blazor Server a
// singleton would be shared across every connected user's circuit (cross-user data leak).
builder.Services.AddScoped<UserGalleriesState>();

// Orchestrates the three startup bootstrap paths (localStorage cache / server fetch / JS callback)
// on behalf of MainLayout. Scoped so it shares the per-circuit state services above.
builder.Services.AddScoped<UserBootstrapService>();

var app = builder.Build();

if (string.IsNullOrEmpty(builder.Configuration["AppBasePath"]))
{
    app.Logger.LogWarning(
        "AppBasePath not set in configuration; defaulting to '/'. Set the AppBasePath env var if you need a different mount prefix.");
}

if (string.IsNullOrEmpty(builder.Configuration["HacmanApiBaseUrl"]))
{
    app.Logger.LogWarning(
        "HacmanApiBaseUrl not set in configuration; defaulting to {DefaultUrl}. Set the HacmanApiBaseUrl env var to override.",
        hacmanApiBaseUrl);
}

app.Logger.LogInformation(
    "Hacman API base URL (public): {PublicUrl}; server-side URI: {ServerSideUri}",
    hacmanApiBaseUrl, hacmanApiServerSideUri);

app.UseForwardedHeaders();

// NOTE: the former [HEADER_DEBUG]/[COOKIE_DEBUG] middleware was removed — it logged every
// request's full header values (including the accessToken cookie, i.e. JWTs ended up in
// logs). Request logging is handled by UseHttpLogging below, which redacts unlisted
// header values by default.

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UsePathBase(appBasePath);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpLogging();
app.UseHttpsRedirection();

// Configure static files with proper MIME type handling
var staticFileOptions = new StaticFileOptions();
var contentTypeProvider = staticFileOptions.ContentTypeProvider as FileExtensionContentTypeProvider
                          ?? new FileExtensionContentTypeProvider();
if (!contentTypeProvider.Mappings.ContainsKey(".css"))
{
    contentTypeProvider.Mappings[".css"] = "text/css";
}

staticFileOptions.ContentTypeProvider = contentTypeProvider;
app.UseStaticFiles(staticFileOptions);

app.UseRouting();

app.UseAntiforgery();

app.MapControllers(); // make /auth/callback available
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();


app.Run();

// Marker making the top-level-statements entry point visible to WebApplicationFactory<Program>
// in the test project.
public partial class Program
{
}