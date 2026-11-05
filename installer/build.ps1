param(
    [Parameter(Mandatory = $true)]
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'
$workspacePath = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $workspacePath 'BedrockLoader'
if (-not (Test-Path -LiteralPath (Join-Path $projectDir 'KoklukBedrockModloader.csproj'))) { $projectDir = $workspacePath }
$projectPath = Join-Path $projectDir 'KoklukBedrockModloader.csproj'
$payloadPath = Join-Path $workspacePath 'installer-payload-v0.16.10'
$setupSource = Join-Path $PSScriptRoot 'BedrockLoader.iss'

if (-not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw "Inno Setup compiler not found: $IsccPath"
}

& dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $payloadPath -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }

$manifest = Join-Path $payloadPath 'BedrockLoader.deps.json'
if (-not (Select-String -LiteralPath $manifest -SimpleMatch '"coreclr.dll"' -Quiet)) {
    throw 'The published dependency manifest is missing CoreCLR. Installer compilation aborted.'
}

& $IsccPath "/DPayloadDir=$payloadPath" "/DProjectDir=$projectDir" $setupSource
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }

Write-Output (Join-Path $workspacePath 'installer-output\BedrockLoader-Setup-v0.16.10-x64.exe')
