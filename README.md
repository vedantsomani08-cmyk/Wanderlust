# Wanderlust – ASP.NET Core (C#) Web App

A travel-planner website served by a C# ASP.NET Core backend.

## Project structure

```
Wanderlust/
├── Program.cs                 ← C# startup: Blazor app + API endpoints
├── Components/                ← Interactive Blazor pages and routing
├── Services/ClaudeService.cs  ← C# class that calls the Claude AI API (keeps the key secret)
├── appsettings.json           ← settings (put your Anthropic API key here)
├── wwwroot/app.css            ← Blazor app styles
├── wwwroot/index.html         ← preserved original experience
├── Properties/launchSettings.json
├── .vscode/launch.json        ← makes F5 work
├── .vscode/tasks.json         ← build / publish tasks
├── publish-to-iis.ps1         ← one-click deploy to local IIS
└── Wanderlust.csproj
```

### How it works

| URL | What happens |
|---|---|
| `/` | Original travel-planner experience, unchanged |
| `/blazor` | Blazor travel discovery draft |
| `GET /api/health` | Returns `{ "status": "ok" }` – quick test that the server is alive |
| `POST /api/claude` | The page sends AI requests here; C# adds the API key and forwards to Anthropic |

The original HTML called `https://api.anthropic.com` directly from the browser, which only works inside Claude.ai. Those two calls now go to `/api/claude`, so the C# server handles them safely.

---

## 1. Install once

1. **.NET 10 SDK** – https://dotnet.microsoft.com/download (check: `dotnet --version`)
   *Only have .NET 8?* Change `net10.0` → `net8.0` in `Wanderlust.csproj` **and** `.vscode/launch.json`.
2. **VS Code** + extension **C# Dev Kit** (VS Code will suggest it when you open the folder).

## 2. Add your API key (for the AI features)

Open `appsettings.json` and paste your key from https://console.anthropic.com:

```json
"Anthropic": { "ApiKey": "sk-ant-...", "Model": "claude-sonnet-5-5" }
```

Without a key the site still loads and browses places; only the AI-generated parts will show an error.
Never upload your real key to GitHub.

## 3. Run with F5

1. VS Code → **File › Open Folder** → select `Wanderlust`.
2. Press **F5**. It builds, starts the server and opens **http://localhost:5080**.
3. Press **Shift+F5** to stop. You can put breakpoints in `Program.cs` or `ClaudeService.cs`.

(Terminal alternative: `dotnet run`)

## 4. Deploy to local IIS

### a) Turn on IIS (one time)
Start menu → **Turn Windows features on or off** → tick **Internet Information Services** (with *Web Management Tools* and *World Wide Web Services*) → OK.

> Windows 11 **Home** also has IIS in this list.

### b) Install the ASP.NET Core Hosting Bundle (one time)
Download **Hosting Bundle** for the same .NET version (10 or 8) from https://dotnet.microsoft.com/download/dotnet → install → restart the PC (or run `iisreset` as admin).

### c) Publish
Open **PowerShell as Administrator**, go to the project folder and run:

```powershell
cd C:\Travel\Wanderlust
Set-ExecutionPolicy -Scope Process Bypass
.\publish-to-iis.ps1
```

Open **http://localhost:8080** 🎉

The script publishes to `C:\inetpub\wwwroot\Wanderlust`, creates app pool **WanderlustPool** (No Managed Code) and site **Wanderlust** on port 8080. Run it again whenever you change the code.

### Manual way (if you prefer clicking)
1. `dotnet publish -c Release -o C:\inetpub\wwwroot\Wanderlust`
2. IIS Manager → **Application Pools** → Add → name `WanderlustPool`, .NET CLR version **No Managed Code**.
3. **Sites** → Add Website → name `Wanderlust`, path `C:\inetpub\wwwroot\Wanderlust`, port `8080`, app pool `WanderlustPool`.

## 5. Deploy to Azure App Service (Free)

The existing Azure app is `Wanderlust` in resource group `Wanderlust` on Windows F1 with .NET 10.

### Deploy from PowerShell

Install Azure CLI once, then sign in and run the deployment script after code changes:

```powershell
winget install --exact --id Microsoft.AzureCLI
az login
az account set --subscription "Azure for Students"
.\deploy-to-azure.ps1
```

The script publishes a Release build, packages it, deploys to the existing Web App, checks `/api/health`, and restores SCM publishing credentials to their previous state. It removes any local API key from the deployment package and excludes `appsettings.Development.json`.

Use `.\deploy-to-azure.ps1 -BuildOnly` to create and retain a ZIP package without requiring Azure CLI login.

### GitHub source, Pages frontend, and Azure backend

The repository is public at `https://github.com/vedantsomani08-cmyk/Wanderlust`. GitHub Pages serves the original static frontend from `wwwroot`; Azure continues hosting the ASP.NET Core API. The frontend calls the Azure API cross-origin, and `Program.cs` allows only this GitHub Pages origin.

The `Deploy frontend to GitHub Pages` workflow publishes `wwwroot` on pushes to `main`. In repository **Settings → Pages**, set the build and deployment source to **GitHub Actions**. The site URL will be `https://vedantsomani08-cmyk.github.io/Wanderlust/` after the workflow succeeds.

The `Deploy to Azure App Service` workflow builds and deploys the backend on pushes to `main`. Azure OIDC is configured for this repository and branch. Its repository **Actions variables** are:

   | Variable | Value |
   |---|---|
   | `AZURE_CLIENT_ID` | Entra app registration's application (client) ID |
   | `AZURE_TENANT_ID` | Azure tenant ID |
   | `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |

The Azure workflow uses OIDC, so no publish profile or long-lived Azure password is stored in GitHub. Push to `main` or run either workflow manually from the **Actions** tab.

Add `Anthropic__ApiKey` in the App Service **Environment variables** settings for AI features. Never put the key in GitHub. The Free F1 plan has limited CPU and memory, no SLA, and may unload the app while idle; Anthropic usage is billed separately.

## Troubleshooting

| Problem | Fix |
|---|---|
| F5 says "program not found" | Run `dotnet build` once; check `net10.0` matches in csproj and launch.json |
| IIS **HTTP 500.19** | Hosting Bundle not installed → install it, then `iisreset` |
| IIS **HTTP 500.30 / 502.5** | Wrong .NET version on the server → install matching Hosting Bundle |
| Port 8080 in use | Change `$Port` in `publish-to-iis.ps1` |
| AI parts show error | API key missing/invalid in `appsettings.json` (for IIS: edit `C:\inetpub\wwwroot\Wanderlust\appsettings.json`) |
| Check server is alive | Open `/api/health` |
