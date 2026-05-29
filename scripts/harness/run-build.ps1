param(
    [string]$Configuration = "Debug"
)

. "$PSScriptRoot\Harness.Common.ps1"

$projectRoot = Get-HarnessProjectRoot
Push-Location $projectRoot
try {
    dotnet build ".\TestMcAlgorithm.sln" -c $Configuration
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
