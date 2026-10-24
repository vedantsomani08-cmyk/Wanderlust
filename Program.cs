// ============================================================
//  Wanderlust - ASP.NET Core web app (C#)
//  1. Serves the website from the wwwroot folder (index.html)
//  2. Provides a small C# Web API:
//       GET  /api/health  -> checks the server is running
//       POST /api/claude  -> forwards AI requests to Anthropic,
//                            adding the secret API key on the server
// ============================================================

using Wanderlust.Services;
using Wanderlust.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register our ClaudeService so it can be injected into endpoints.
// AddHttpClient gives the service a ready-to-use HttpClient.
builder.Services.AddHttpClient<ClaudeService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3); // AI answers can take a while
});

var app = builder.Build();

// Show detailed errors only while developing
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAntiforgery();

// ---------- API endpoints ----------

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    app = "Wanderlust",
    serverTime = DateTime.Now
}));

app.MapPost("/api/claude", async (HttpRequest request, ClaudeService claude) =>
{
    // Read the JSON the browser sent
    using var reader = new StreamReader(request.Body);
    string body = await reader.ReadToEndAsync();

    // Let the service call Anthropic and give us the result
    var (statusCode, json) = await claude.SendMessageAsync(body);
    return Results.Content(json, "application/json", statusCode: statusCode);
});

app.MapGet("/error", () => Results.Problem("Something went wrong on the server."));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
