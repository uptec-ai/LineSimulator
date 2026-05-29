param(
    [Parameter(Mandatory = $false)]
    [string]$Title = "Untitled harness task"
)

. "$PSScriptRoot\Harness.Common.ps1"

$projectRoot = Get-HarnessProjectRoot
$harnessDir = Get-HarnessDirectory -ProjectRoot $projectRoot
$tasksDir = Join-Path $harnessDir "tasks"
New-Item -ItemType Directory -Force -Path $tasksDir | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$slug = ($Title.ToLowerInvariant() -replace "[^a-z0-9]+", "-" -replace "^-|-$", "")
if ([string]::IsNullOrWhiteSpace($slug)) {
    $slug = "task"
}
if ($slug.Length -gt 48) {
    $slug = $slug.Substring(0, 48).TrimEnd("-")
}

$taskId = "$timestamp-$slug"
$taskDir = Join-Path $tasksDir $taskId
New-Item -ItemType Directory -Force -Path $taskDir | Out-Null

$planEn = Join-Path $taskDir "plan.en.md"
$planKo = Join-Path $taskDir "plan.ko.md"
$logEn = Join-Path $taskDir "log.en.md"
$logKo = Join-Path $taskDir "log.ko.md"
$createdAt = Get-HarnessTimestamp

@"
# Plan: $Title

Agent-facing canonical file. Korean reader version: plan.ko.md.

## Goal

TODO: Describe the user-visible outcome.

## Scope

TODO: List included work.

## Out Of Scope

TODO: List excluded work, especially refactoring unless requested.

## Impacted Files

TODO: List expected files or areas.

## Test Strategy

TODO: List planned gates, including unit, static analysis, build, and E2E/manual validation.

## Rollback

TODO: Describe how to safely revert the change.
"@ | Set-Content -LiteralPath $planEn -Encoding UTF8

$planKoTemplateBase64 = "IyDqs4Ttmo06IHt7VGl0bGV9fQoK7JeQ7J207KCE7Yq46rCAIOyngeygkSDsnb3ripQg6riw7KSAIOusuOyEnOuKlCBgcGxhbi5lbi5tZGDsnoXri4jri6QuIOydtCDtjIzsnbzsnYAg7IKs7Jqp7J6Q6rCAIOydveq4sCDsnITtlZwg7ZWc6rWt7Ja0IOuyhOyghOyeheuLiOuLpC4KCiMjIOuqqe2RnAoKVE9ETzog7IKs7Jqp7J6Q7JeQ6rKMIOuztOydtOuKlCDqsrDqs7zrpbwg7KCB7Iq164uI64ukLgoKIyMg67KU7JyECgpUT0RPOiDtj6ztlajtlaAg7J6R7JeF7J2EIOyggeyKteuLiOuLpC4KCiMjIOygnOyZuCDrspTsnIQKClRPRE86IOygnOyZuO2VoCDsnpHsl4XsnYQg7KCB7Iq164uI64ukLiDtirntnogg7JqU7LKt67Cb7KeAIOyViuydgCDrpqztjKnthqDrp4HsnYAg7KCc7Jm47ZWp64uI64ukLgoKIyMg7JiB7ZalIO2MjOydvAoKVE9ETzog7JiI7IOB65CY64qUIO2MjOydvCDrmJDripQg7JiB7Jet7J2EIOyggeyKteuLiOuLpC4KCiMjIO2FjOyKpO2KuCDsoITrnrUKClRPRE86IHVuaXQsIHN0YXRpYyBhbmFseXNpcywgYnVpbGQsIEUyRS9tYW51YWwgdmFsaWRhdGlvbiDqs4Ttmo3snYQg7KCB7Iq164uI64ukLgoKIyMg66Gk67CxCgpUT0RPOiDslYjsoITtlZjqsowg65CY64+M66as64qUIOuwqeuyleydhCDsoIHsirXri4jri6QuCg=="
$planKoContent = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($planKoTemplateBase64)).Replace("{{Title}}", $Title)
$planKoContent | Set-Content -LiteralPath $planKo -Encoding UTF8

@"
# Log: $Title

Agent-facing canonical file. Korean reader version: log.ko.md.

- [$createdAt] Task created.
"@ | Set-Content -LiteralPath $logEn -Encoding UTF8

$logKoTemplateBase64 = "IyDroZzqt7g6IHt7VGl0bGV9fQoK7JeQ7J207KCE7Yq46rCAIOyngeygkSDsnb3ripQg6riw7KSAIOusuOyEnOuKlCBgbG9nLmVuLm1kYOyeheuLiOuLpC4g7J20IO2MjOydvOydgCDsgqzsmqnsnpDqsIAg7J296riwIOychO2VnCDtlZzqta3slrQg67KE7KCE7J6F64uI64ukLgoKLSBbe3tDcmVhdGVkQXR9fV0g7J6R7JeF7J20IOyDneyEseuQmOyXiOyKteuLiOuLpC4K"
$logKoContent = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($logKoTemplateBase64)).Replace("{{Title}}", $Title).Replace("{{CreatedAt}}", $createdAt)
$logKoContent | Set-Content -LiteralPath $logKo -Encoding UTF8

$activeTask = [ordered]@{
    TaskId = $taskId
    Title = $Title
    ProjectRoot = $projectRoot
    TaskDir = $taskDir
    PlanEnglishPath = $planEn
    PlanKoreanPath = $planKo
    LogEnglishPath = $logEn
    LogKoreanPath = $logKo
    CreatedAt = $createdAt
}

$activePath = Get-HarnessActiveTaskPath -ProjectRoot $projectRoot
$activeTask | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $activePath -Encoding UTF8

Write-Host "Harness task started."
Write-Host "TaskId: $taskId"
Write-Host "PlanEnglishPath: $planEn"
Write-Host "PlanKoreanPath: $planKo"
Write-Host "LogEnglishPath: $logEn"
Write-Host "LogKoreanPath: $logKo"
