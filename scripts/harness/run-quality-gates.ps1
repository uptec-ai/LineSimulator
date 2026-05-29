. "$PSScriptRoot\Harness.Common.ps1"

Invoke-HarnessStep -Name "Unit tests" -ScriptPath (Join-Path $PSScriptRoot "run-unit-tests.ps1")
Invoke-HarnessStep -Name "Static analysis" -ScriptPath (Join-Path $PSScriptRoot "run-static-analysis.ps1")
Invoke-HarnessStep -Name "Build" -ScriptPath (Join-Path $PSScriptRoot "run-build.ps1")
Invoke-HarnessStep -Name "E2E" -ScriptPath (Join-Path $PSScriptRoot "run-e2e.ps1")

$projectRoot = Get-HarnessProjectRoot
$task = Read-HarnessActiveTask -ProjectRoot $projectRoot
Add-HarnessLogEntry -Task $task -Message "Quality gates passed."
Write-Host "All quality gates passed."
