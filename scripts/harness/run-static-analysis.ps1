. "$PSScriptRoot\Harness.Common.ps1"

$projectRoot = Get-HarnessProjectRoot
$knownConfig = @(
    ".editorconfig",
    "Directory.Build.props",
    "Directory.Packages.props",
    "global.json"
) | ForEach-Object { Join-Path $projectRoot $_ } | Where-Object { Test-Path -LiteralPath $_ }

if ($knownConfig.Count -eq 0) {
    Write-Host "No project static analysis command or analyzer config found. Static analysis gate is a discovery pass."
    exit 0
}

Write-Host "Analyzer-related config detected:"
$knownConfig | ForEach-Object { Write-Host " - $_" }
Write-Host "No dedicated static analysis command is configured yet. Static analysis gate passed as documentation-only."
exit 0
