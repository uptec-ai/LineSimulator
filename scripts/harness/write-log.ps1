param(
    [Parameter(Mandatory = $true)]
    [string]$Message
)

. "$PSScriptRoot\Harness.Common.ps1"

$projectRoot = Get-HarnessProjectRoot
$task = Read-HarnessActiveTask -ProjectRoot $projectRoot
Add-HarnessLogEntry -Task $task -Message $Message
Write-Host "Harness log updated."
