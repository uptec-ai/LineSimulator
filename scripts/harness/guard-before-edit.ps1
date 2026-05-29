. "$PSScriptRoot\Harness.Common.ps1"

$projectRoot = Get-HarnessProjectRoot
$currentPath = (Get-Location).Path

if (!(Test-HarnessInsideRoot -ProjectRoot $projectRoot -CurrentPath $currentPath)) {
    throw "Current path is outside the project root. Current: $currentPath Root: $projectRoot"
}

$requiredFiles = @(
    (Join-Path $projectRoot "AGENTS.md"),
    (Join-Path $projectRoot "docs\harness\workflow.md")
)

foreach ($file in $requiredFiles) {
    if (!(Test-Path -LiteralPath $file)) {
        throw "Required harness file is missing: $file"
    }
}

$task = Read-HarnessActiveTask -ProjectRoot $projectRoot

if (!(Test-Path -LiteralPath $task.PlanEnglishPath)) {
    throw "English plan file is missing: $($task.PlanEnglishPath)"
}

$plan = Get-Content -LiteralPath $task.PlanEnglishPath -Raw
$requiredSections = @("## Goal", "## Scope", "## Out Of Scope", "## Impacted Files", "## Test Strategy", "## Rollback")
foreach ($section in $requiredSections) {
    if (!$plan.Contains($section)) {
        throw "Plan is missing required section: $section"
    }
}

if ($plan.Contains("TODO:")) {
    throw "Plan still contains TODO markers. Fill plan.en.md before editing application code."
}

$statusPath = Join-Path $task.TaskDir "pre-edit-status.txt"
if (!(Test-Path -LiteralPath $statusPath)) {
    Push-Location $projectRoot
    try {
        $gitStatus = git status --short 2>$null
        if ($LASTEXITCODE -eq 0) {
            $gitStatus | Set-Content -LiteralPath $statusPath -Encoding UTF8
        }
        else {
            "Git status unavailable." | Set-Content -LiteralPath $statusPath -Encoding UTF8
        }
    }
    finally {
        Pop-Location
    }
}

Add-HarnessLogEntry -Task $task -Message "Guard passed before edit."
Write-Host "Harness guard passed."
