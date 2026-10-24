[CmdletBinding()]
param(
    [string]$ResourceGroupName = "Wanderlust",
    [string]$AppName = "Wanderlust",
    [string]$SubscriptionId,
    [switch]$BuildOnly
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "Wanderlust.csproj"
if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Could not find the project file: $projectPath"
}

$workDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ("Wanderlust-deploy-" + [guid]::NewGuid().ToString("N"))
$publishDirectory = Join-Path $workDirectory "publish"
$packagePath = Join-Path $workDirectory "Wanderlust.zip"
$keepArtifacts = $false
$restoreScmPolicy = $false
$scmPolicyUrl = $null

try {
    New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

    Write-Host "Publishing Wanderlust in Release configuration..."
    & dotnet publish $projectPath --configuration Release --output $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    $productionSettingsPath = Join-Path $publishDirectory "appsettings.json"
    if (Test-Path -LiteralPath $productionSettingsPath) {
        $settings = Get-Content -LiteralPath $productionSettingsPath -Raw | ConvertFrom-Json
        $anthropicProperty = $settings.PSObject.Properties["Anthropic"]
        if ($anthropicProperty -and $anthropicProperty.Value) {
            $apiKeyProperty = $anthropicProperty.Value.PSObject.Properties["ApiKey"]
            if ($apiKeyProperty) {
                $apiKeyProperty.Value = ""
                $settings | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $productionSettingsPath -Encoding UTF8
            }
        }
    }

    $developmentSettingsPath = Join-Path $publishDirectory "appsettings.Development.json"
    if (Test-Path -LiteralPath $developmentSettingsPath) {
        Remove-Item -LiteralPath $developmentSettingsPath -Force
    }

    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $packagePath -CompressionLevel Optimal

    if ($BuildOnly) {
        $keepArtifacts = $true
        Write-Output $packagePath
        return
    }

    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI is required. Install it with 'winget install --exact --id Microsoft.AzureCLI', then reopen PowerShell."
    }

    $activeSubscription = & az account show --query id --output tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($activeSubscription)) {
        throw "Azure CLI is not signed in. Run 'az login' and rerun this script."
    }

    if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
        & az account set --subscription $SubscriptionId
        if ($LASTEXITCODE -ne 0) {
            throw "Could not select Azure subscription '$SubscriptionId'."
        }
        $activeSubscription = $SubscriptionId
    }

    $siteJson = & az webapp show --resource-group $ResourceGroupName --name $AppName --query "{host:defaultHostName,isLinux:reserved}" --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Could not find Azure Web App '$AppName' in resource group '$ResourceGroupName'."
    }
    $site = ConvertFrom-Json -InputObject ($siteJson -join [Environment]::NewLine)
    if ($site.isLinux) {
        throw "'$AppName' is a Linux App Service. This deployment script is configured for the existing Windows App Service."
    }

    $scmPolicyUrl = "https://management.azure.com/subscriptions/$activeSubscription/resourceGroups/$ResourceGroupName/providers/Microsoft.Web/sites/$AppName/basicPublishingCredentialsPolicies/scm?api-version=2022-03-01"
    $policyJson = & az rest --method get --url $scmPolicyUrl --output json
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read the App Service SCM publishing policy. Check your Azure permissions."
    }
    $scmWasEnabled = [bool](ConvertFrom-Json -InputObject ($policyJson -join [Environment]::NewLine)).properties.allow

    if (-not $scmWasEnabled) {
        $restoreScmPolicy = $true
        & az rest --method put --url $scmPolicyUrl --body '{"properties":{"allow":true}}' --output none
        if ($LASTEXITCODE -ne 0) {
            throw "Could not temporarily enable SCM publishing credentials."
        }
    }

    Write-Host "Deploying package to Azure Web App '$AppName'..."
    & az webapp deploy --resource-group $ResourceGroupName --name $AppName --src-path $packagePath --type zip
    if ($LASTEXITCODE -ne 0) {
        throw "Azure Zip Deploy failed with exit code $LASTEXITCODE."
    }

    $healthUrl = "https://$($site.host)/api/health"
    $health = Invoke-RestMethod -Method Get -Uri $healthUrl -TimeoutSec 45
    if ($health.status -ne "ok") {
        throw "The deployment completed, but the health check did not return status 'ok': $healthUrl"
    }

    $hasApiKey = & az webapp config appsettings list --resource-group $ResourceGroupName --name $AppName --query "contains([].name, 'Anthropic__ApiKey')" --output tsv 2>$null
    if ($LASTEXITCODE -eq 0 -and "$hasApiKey" -ne "true") {
        Write-Warning "The site is live, but AI features need the Anthropic__ApiKey app setting in Azure."
    }

    Write-Host "Deployment verified: $healthUrl" -ForegroundColor Green
}
finally {
    if ($restoreScmPolicy -and $scmPolicyUrl) {
        & az rest --method put --url $scmPolicyUrl --body '{"properties":{"allow":false}}' --output none
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not disable SCM publishing credentials automatically. Disable them in the App Service Configuration settings."
        }
        else {
            Write-Host "SCM publishing credentials restored to disabled."
        }
    }

    if (-not $keepArtifacts -and (Test-Path -LiteralPath $workDirectory)) {
        Remove-Item -LiteralPath $workDirectory -Recurse -Force
    }
}