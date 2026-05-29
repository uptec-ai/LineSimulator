$ErrorActionPreference = "Stop"

function Get-HarnessProjectRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

function Get-HarnessDirectory {
    param([string]$ProjectRoot)
    return (Join-Path $ProjectRoot ".harness")
}

function Get-HarnessActiveTaskPath {
    param([string]$ProjectRoot)
    return (Join-Path (Get-HarnessDirectory -ProjectRoot $ProjectRoot) "active-task.json")
}

function Read-HarnessActiveTask {
    param([string]$ProjectRoot)

    $activePath = Get-HarnessActiveTaskPath -ProjectRoot $ProjectRoot
    if (!(Test-Path -LiteralPath $activePath)) {
        throw "No active harness task. Run scripts/harness/start-task.ps1 first."
    }

    return (Get-Content -LiteralPath $activePath -Raw | ConvertFrom-Json)
}

function Get-HarnessTimestamp {
    return (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}

function Add-HarnessLogEntry {
    param(
        [object]$Task,
        [string]$Message
    )

    $timestamp = Get-HarnessTimestamp
    Add-Content -LiteralPath $Task.LogEnglishPath -Value "- [$timestamp] $Message"
    Add-Content -LiteralPath $Task.LogKoreanPath -Value "- [$timestamp] $Message"
}

function Invoke-HarnessStep {
    param(
        [string]$Name,
        [string]$ScriptPath
    )

    Write-Host "==> $Name"
    & $ScriptPath
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

function Test-HarnessInsideRoot {
    param(
        [string]$ProjectRoot,
        [string]$CurrentPath
    )

    $root = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd('\')
    $current = [System.IO.Path]::GetFullPath($CurrentPath).TrimEnd('\')
    return ($current -eq $root -or $current.StartsWith($root + "\", [System.StringComparison]::OrdinalIgnoreCase))
}
