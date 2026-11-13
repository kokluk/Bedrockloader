param(
    [string]$SetupPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'installer-output\BedrockLoader-Setup-v0.17.12-x64.exe')
)

$ErrorActionPreference = 'Stop'
$workspacePath = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$testRoot = Join-Path $workspacePath ('installer-test-' + [Guid]::NewGuid().ToString('N'))
$targetPath = [IO.Path]::GetFullPath((Join-Path $testRoot 'app'))
if (-not $targetPath.StartsWith($workspacePath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Test path is outside the workspace.' }

$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{7B8B8A23-6749-4C7D-9D23-420A29B0F11B}_is1'
if (Test-Path -LiteralPath $uninstallKey) { throw 'BedrockLoader is already installed; the test will not modify that installation.' }

New-Item -ItemType Directory -Path $testRoot | Out-Null
$verified = 0
$installed = $false
try {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/LANG=turkish',
        ('/DIR="' + $targetPath + '"'), ('/LOG="' + (Join-Path $testRoot 'install.log') + '"'))
    $setupProcess = Start-Process -FilePath $SetupPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($setupProcess.ExitCode -ne 0) { throw "Install failed: $($setupProcess.ExitCode)" }
    $installed = $true

    $payloadPath = (Resolve-Path -LiteralPath (Join-Path $workspacePath 'installer-payload-v0.17.12')).Path
    foreach ($source in Get-ChildItem -LiteralPath $payloadPath -File -Recurse) {
        if ($source.Extension -eq '.pdb') { continue }
        $relative = $source.FullName.Substring($payloadPath.Length).TrimStart('\')
        $destination = Join-Path $targetPath $relative
        if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) { throw "Missing installed file: $relative" }
        if ((Get-FileHash -LiteralPath $source.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) { throw "Installed file differs: $relative" }
        $verified++
    }
    $runtime = Get-Content -LiteralPath (Join-Path $targetPath 'BedrockLoader.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (-not ($runtime.runtimeOptions.includedFrameworks | Where-Object name -eq 'Microsoft.WindowsDesktop.App')) { throw 'Embedded desktop runtime missing.' }
    if (-not (Test-Path -LiteralPath $uninstallKey)) { throw 'Windows uninstall entry missing.' }
}
finally {
    if ($installed) {
        $uninstallerPath = Join-Path $targetPath 'unins000.exe'
        $uninstallProcess = Start-Process -FilePath $uninstallerPath -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', ('/LOG="' + (Join-Path $testRoot 'uninstall.log') + '"')) -WindowStyle Hidden -Wait -PassThru
        if ($uninstallProcess.ExitCode -ne 0) { throw "Uninstall failed: $($uninstallProcess.ExitCode)" }
    }
}
if (Test-Path -LiteralPath (Join-Path $targetPath 'BedrockLoader.exe')) { throw 'Application remained after uninstall.' }
if (Test-Path -LiteralPath $uninstallKey) { throw 'Windows uninstall entry remained after uninstall.' }

$result = [pscustomobject]@{ Install = 'Passed'; VerifiedFiles = $verified; EmbeddedRuntime = 'Passed'; Uninstall = 'Passed'; TestDirectory = $testRoot }
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testRoot 'result.json') -Encoding UTF8
$result
