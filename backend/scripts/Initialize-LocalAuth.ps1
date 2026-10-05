# Creates an ignored development signing key. Existing keys are preserved.
$ErrorActionPreference = 'Stop'
$localConfigPath = Join-Path $PSScriptRoot '../src/SkillBridge.Api/appsettings.Development.local.json'
if (-not (Test-Path -LiteralPath $localConfigPath)) {
    $keyBytes = New-Object byte[] 64
    $keyGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $keyGenerator.GetBytes($keyBytes) } finally { $keyGenerator.Dispose() }
    $localConfig = @{ Jwt = @{ Key = [Convert]::ToBase64String($keyBytes) } } | ConvertTo-Json
    [System.IO.File]::WriteAllText($localConfigPath, $localConfig, [System.Text.UTF8Encoding]::new($false))
}
Write-Output 'Development JWT configuration is ready. The signing key is not tracked by Git.'
