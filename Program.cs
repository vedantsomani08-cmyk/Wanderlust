
using System.Security.Cryptography.X509Certificates;
using Wanderlust.Services;
using Wanderlust.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCors(options => options.AddPolicy("GitHubPages", policy =>
    policy.WithOrigins("https://vedantsomani08-cmyk.github.io")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddHttpClient<ClaudeService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});

// Hotelbeds mTLS client
builder.Services.AddHttpClient<HotelbedsService>(client =>
{
    client.BaseAddress = new Uri("https://api-mtls.test.hotelbeds.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var path = "/etc/secrets/hotelbeds-client.pfx.b64";

    var encoded = File.ReadAllText(path);
    var pfxBytes = Convert.FromBase64String(encoded);

    var password = builder.Configuration["HOTELBEDS_CERTIFICATE_PASSWORD"]
        ?? throw new InvalidOperationException("Hotelbeds certificate password missing.");

    var certificate = X509CertificateLoader.LoadPkcs12(
        pfxBytes, password, X509KeyStorageFlags.EphemeralKeySet);

    var handler = new HttpClientHandler();
    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
    handler.ClientCertificates.Add(certificate);

    return handler;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseCors("GitHubPages");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    app = "Wanderlust",
    serverTime = DateTime.Now
}));

app.MapPost("/api/claude", async (HttpRequest request, ClaudeService claude) =>
{
    using var reader = new StreamReader(request.Body);
    string body = await reader.ReadToEndAsync();

    var (statusCode, json) = await claude.SendMessageAsync(body);
    return Results.Content(json, "application/json", statusCode: statusCode);
});

app.MapGet("/api/hotelbeds/status", async (HotelbedsService hotelbeds) =>
{
    var (statusCode, body) = await hotelbeds.CheckStatusAsync();
    return Results.Content(body, "application/json", statusCode: statusCode);
});

app.MapGet("/error", () => Results.Problem("Something went wrong on the server."));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
