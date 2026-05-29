. "$PSScriptRoot\Harness.Common.ps1"

$projectRoot = Get-HarnessProjectRoot
Push-Location $projectRoot
try {
    $testProjects = Get-ChildItem -Path $projectRoot -Recurse -Filter "*.csproj" |
        Where-Object {
            $content = Get-Content -LiteralPath $_.FullName -Raw
            $content -match "Microsoft\.NET\.Test\.Sdk|xunit|NUnit|MSTest"
        }

    if ($testProjects.Count -eq 0) {
        Write-Host "No dedicated test project found. Unit test gate is a discovery pass."
        exit 0
    }

    dotnet test ".\TestMcAlgorithm.sln" -c Debug --no-build
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
