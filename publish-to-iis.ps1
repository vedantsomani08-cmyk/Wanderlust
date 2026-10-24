# ============================================================
#  Deploy Wanderlust to local IIS
#  Run in PowerShell AS ADMINISTRATOR from the project folder:
#     .\publish-to-iis.ps1
#  Before running: install IIS + the ASP.NET Core Hosting Bundle (see README.md)
# ============================================================

$SiteName  = "Wanderlust"
$PoolName  = "WanderlustPool"
$Port      = 8080
$SitePath  = "C:\inetpub\wwwroot\Wanderlust"

Import-Module WebAdministration

# 1. Stop the site if it already exists (so files are not locked)
if (Test-Path "IIS:\Sites\$SiteName") {
    Write-Host "Stopping existing site..."
    Stop-Website -Name $SiteName
    Stop-WebAppPool -Name $PoolName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# 2. Publish the app (Release build) into the IIS folder
Write-Host "Publishing app to $SitePath ..."
dotnet publish "$PSScriptRoot\Wanderlust.csproj" -c Release -o $SitePath
if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed"; exit 1 }

# 3. Create the App Pool (No Managed Code - .NET Core runs on its own)
if (-not (Test-Path "IIS:\AppPools\$PoolName")) {
    Write-Host "Creating app pool $PoolName ..."
    New-WebAppPool -Name $PoolName | Out-Null
    Set-ItemProperty "IIS:\AppPools\$PoolName" -Name managedRuntimeVersion -Value ""
}

# 4. Create the website
if (-not (Test-Path "IIS:\Sites\$SiteName")) {
    Write-Host "Creating website $SiteName on port $Port ..."
    New-Website -Name $SiteName -Port $Port -PhysicalPath $SitePath -ApplicationPool $PoolName | Out-Null
}

# 5. Start everything
Start-WebAppPool -Name $PoolName -ErrorAction SilentlyContinue
Start-Website -Name $SiteName

Write-Host ""
Write-Host "Done! Open http://localhost:$Port in your browser." -ForegroundColor Green
