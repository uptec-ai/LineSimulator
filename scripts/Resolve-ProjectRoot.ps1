# Portable project-root resolver.
# Walks up from -StartPath to the first directory containing the -Anchor (default: the .sln).
# Use in build/run commands so no absolute machine path is ever hardcoded:
#   $root = & "$PSScriptRoot\Resolve-ProjectRoot.ps1"
[CmdletBinding()]
param([string]$StartPath = $PSScriptRoot, [string]$Anchor = '*.sln')

if ([string]::IsNullOrWhiteSpace($StartPath)) { $StartPath = (Get-Location).Path }
$dir = Get-Item -LiteralPath $StartPath
if (-not $dir.PSIsContainer) { $dir = $dir.Directory }
while ($null -ne $dir) {
    if (Get-ChildItem -LiteralPath $dir.FullName -Filter $Anchor -File -ErrorAction SilentlyContinue | Select-Object -First 1) {
        return $dir.FullName
    }
    $dir = $dir.Parent
}
throw "Anchor '$Anchor' not found at or above '$StartPath'."
