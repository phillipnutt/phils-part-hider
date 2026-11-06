# Builds a release zip, PhilsPartHider-<version>.zip, holding GameData/PhilsPartHider
# (the DLL, the toolbar icon, and the README, LICENSE and CHANGELOG). The player's
# own hidden list (PluginData) is never included.
# Build first (MSBuild; /p:InstallToGame=false is fine), then:
#   powershell -ExecutionPolicy Bypass -File Tools\Make-Release.ps1 -Version 1.0.0 -Out <folder>

param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Out
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$dll = Join-Path $root 'bin\Release\PhilsPartHider.dll'
if (-not (Test-Path $dll)) { throw 'Build first: bin\Release\PhilsPartHider.dll is missing.' }
New-Item -ItemType Directory -Force $Out | Out-Null

$path = Join-Path $Out "PhilsPartHider-$Version.zip"
if (Test-Path $path) { Remove-Item $path }
$zip = [IO.Compression.ZipFile]::Open($path, 'Create')
# Zip entry names always use forward slashes (Windows PowerShell 5.1's
# Compress-Archive writes backslashes, which other tools misread).
function AddFile($src, $entry) {
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $src, $entry, 'Optimal')
}
$gd = Join-Path $root 'GameData'
Get-ChildItem (Join-Path $gd 'PhilsPartHider') -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\(Plugins|PluginData)\\' } |
    ForEach-Object { AddFile $_.FullName ('GameData/' + $_.FullName.Substring($gd.Length + 1).Replace('\', '/')) }
AddFile $dll 'GameData/PhilsPartHider/Plugins/PhilsPartHider.dll'
foreach ($d in 'LICENSE', 'README.md', 'CHANGELOG.md') { AddFile (Join-Path $root $d) "GameData/PhilsPartHider/$d" }
$zip.Dispose()

$check = [IO.Compression.ZipFile]::OpenRead($path)
$check.Entries | ForEach-Object { "  $($_.FullName)  ($($_.Length) bytes)" }
$check.Dispose()
Write-Host "Wrote $path"
