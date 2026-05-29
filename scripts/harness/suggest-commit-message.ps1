. "$PSScriptRoot\Harness.Common.ps1"

$projectRoot = Get-HarnessProjectRoot
Push-Location $projectRoot
try {
    $changed = git status --short 2>$null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "chore: update project harness"
        exit 0
    }

    $text = ($changed -join "`n")
    if ($text -match "scripts/harness|docs/harness|AGENTS") {
        Write-Host "chore: add project harness workflow"
    }
    elseif ($text -match "\.xaml|ViewModels|Views") {
        Write-Host "fix: update WPF UI behavior"
    }
    elseif ($text -match "Services|Models") {
        Write-Host "fix: update simulator logic"
    }
    else {
        Write-Host "chore: update project files"
    }
}
finally {
    Pop-Location
}
