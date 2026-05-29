param(
    [switch]$KeepActive
)

. "$PSScriptRoot\Harness.Common.ps1"

$projectRoot = Get-HarnessProjectRoot
$task = Read-HarnessActiveTask -ProjectRoot $projectRoot
$completedAt = Get-HarnessTimestamp
$summaryEn = Join-Path $task.TaskDir "summary.en.md"
$summaryKo = Join-Path $task.TaskDir "summary.ko.md"

Push-Location $projectRoot
try {
    $status = git status --short 2>$null
    if ($LASTEXITCODE -ne 0) {
        $status = @("Git status unavailable.")
    }
}
finally {
    Pop-Location
}

@"
# Summary: $($task.Title)

Agent-facing canonical file. Korean reader version: summary.ko.md.

Completed at: $completedAt

## Git Status

````text
$($status -join "`n")
````

## Notes

- Review log.en.md for decisions and validation notes.
"@ | Set-Content -LiteralPath $summaryEn -Encoding UTF8

$summaryKoTemplateBase64 = "IyDsmpTslb06IHt7VGl0bGV9fQoK7JeQ7J207KCE7Yq46rCAIOyngeygkSDsnb3ripQg6riw7KSAIOusuOyEnOuKlCBgc3VtbWFyeS5lbi5tZGDsnoXri4jri6QuIOydtCDtjIzsnbzsnYAg7IKs7Jqp7J6Q6rCAIOydveq4sCDsnITtlZwg7ZWc6rWt7Ja0IOuyhOyghOyeheuLiOuLpC4KCuyZhOujjCDsi5zqsIE6IHt7Q29tcGxldGVkQXR9fQoKIyMgR2l0IOyDge2DnAoKYGBgYHRleHQKe3tTdGF0dXN9fQpgYGBgCgojIyDssLjqs6AKCi0g6rKw7KCV6rO8IOqygOymnSDquLDroZ3snYAgYGxvZy5lbi5tZGDrpbwg7ZmV7J247ZWp64uI64ukLgo="
$summaryKoContent = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($summaryKoTemplateBase64)).
    Replace("{{Title}}", $task.Title).
    Replace("{{CompletedAt}}", $completedAt).
    Replace("{{Status}}", ($status -join "`n"))
$summaryKoContent | Set-Content -LiteralPath $summaryKo -Encoding UTF8

Add-HarnessLogEntry -Task $task -Message "Task completed."

if (!$KeepActive) {
    $activePath = Get-HarnessActiveTaskPath -ProjectRoot $projectRoot
    Remove-Item -LiteralPath $activePath -Force
}

Write-Host "Harness task completed."
Write-Host "SummaryEnglishPath: $summaryEn"
Write-Host "SummaryKoreanPath: $summaryKo"
