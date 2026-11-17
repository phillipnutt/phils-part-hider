# Runs PartHiderSelfTest.cs in a KSP install: builds the mod and the test, puts
# them in, starts KSP (the test makes its own throwaway sandbox save, checks the
# VAB, then quits KSP), collects the results, and puts the install back as it was.
# Use a test install, never one with a career you care about.
#
#   powershell -ExecutionPolicy Bypass -File Tests\InGame\Run-SelfTest.ps1 -KspRoot <KSP folder> [-Out <folder>] [-BuildOnly] [-KeepBuild]
#
# -BuildOnly  build both DLLs and stop (no game started, nothing copied)
# -KeepBuild  leave the new mod build installed afterwards (the test itself is always removed)

param(
    [Parameter(Mandatory = $true)][string]$KspRoot,
    [string]$Out = (Join-Path $env:TEMP 'PartHiderSelfTest'),
    [switch]$BuildOnly,
    [switch]$KeepBuild,
    [int]$TimeoutMinutes = 10
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$managed = Join-Path $KspRoot 'KSP_x64_Data\Managed'
if (-not (Test-Path (Join-Path $managed 'Assembly-CSharp.dll'))) { throw "Not a KSP folder: $KspRoot" }
New-Item -ItemType Directory -Force $Out | Out-Null

# 1. Build the mod (against this install, not copied into it) and the test.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
$csc = Join-Path (Split-Path $msbuild) 'Roslyn\csc.exe'
& $msbuild (Join-Path $repo 'PhilsPartHider.csproj') /p:InstallToGame=false "/p:KspDir=$KspRoot" /nologo /v:minimal
if ($LASTEXITCODE) { throw 'The mod did not build.' }
$modDll = Join-Path $repo 'bin\Release\PhilsPartHider.dll'
$testDll = Join-Path $Out 'PartHiderSelfTest.dll'
$refs = 'mscorlib', 'System', 'System.Core', 'Assembly-CSharp', 'UnityEngine', 'UnityEngine.CoreModule', 'UnityEngine.UI',
        'UnityEngine.UIModule', 'UnityEngine.InputLegacyModule', 'UnityEngine.TextRenderingModule' |
    ForEach-Object { '/r:' + (Join-Path $managed "$_.dll") }
& $csc /nologo /noconfig /nostdlib+ /target:library /optimize+ /langversion:latest "/out:$testDll" @refs "/r:$modDll" (Join-Path $PSScriptRoot 'PartHiderSelfTest.cs')
if ($LASTEXITCODE) { throw 'The test did not build.' }
"Built $modDll and $testDll"
if ($BuildOnly) { return }

if (Get-Process -Name KSP_x64 -ErrorAction SilentlyContinue) { throw 'KSP is already running; not touching it.' }

# 2. Back up what the run changes, then put the build and the test in.
$mod = Join-Path $KspRoot 'GameData\PhilsPartHider'
$list = Join-Path $mod 'PluginData\HiddenParts.cfg'
$backup = Join-Path $Out 'backup'
New-Item -ItemType Directory -Force $backup | Out-Null
Copy-Item (Join-Path $mod 'Plugins\PhilsPartHider.dll') (Join-Path $backup 'PhilsPartHider.dll') -Force
$hadList = Test-Path $list
if ($hadList) { Copy-Item $list (Join-Path $backup 'HiddenParts.cfg') -Force }
$testMod = Join-Path $KspRoot 'GameData\zz_PartHiderSelfTest'
$testDir = Join-Path $KspRoot 'PartHiderSelfTest'
$save = Join-Path $KspRoot 'saves\zz_PartHiderSelfTest'
try {
    Copy-Item $modDll (Join-Path $mod 'Plugins\PhilsPartHider.dll') -Force
    New-Item -ItemType Directory -Force (Join-Path $testMod 'Plugins'), $testDir | Out-Null
    Copy-Item $testDll (Join-Path $testMod 'Plugins') -Force
    Set-Content (Join-Path $testDir 'run') 'run'

    # 3. Run: the test quits KSP when it's done.
    $started = Get-Date
    $p = Start-Process -FilePath (Join-Path $KspRoot 'KSP_x64.exe') -WorkingDirectory $KspRoot -PassThru
    if (-not $p.WaitForExit($TimeoutMinutes * 60000)) {
        "KSP still running after $TimeoutMinutes minutes; closing the copy this script started."
        $p.CloseMainWindow() | Out-Null
        if (-not $p.WaitForExit(30000)) { $p.Kill() }
    }
    "KSP ran for {0:N0} s" -f ((Get-Date) - $started).TotalSeconds
    Copy-Item (Join-Path $testDir 'results.log') $Out -Force -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $KspRoot 'KSP.log') $Out -Force
}
finally {
    # 4. Put the install back.
    Remove-Item $testMod, $testDir, $save -Recurse -Force -Confirm:$false -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $KspRoot 'thumbs\zz_PartHiderSelfTest_*') -Force -Confirm:$false -ErrorAction SilentlyContinue
    if (-not $KeepBuild) { Copy-Item (Join-Path $backup 'PhilsPartHider.dll') (Join-Path $mod 'Plugins\PhilsPartHider.dll') -Force }
    if ($hadList) { Copy-Item (Join-Path $backup 'HiddenParts.cfg') $list -Force }
}
$results = Join-Path $Out 'results.log'
if (Test-Path $results) { Get-Content $results } else { 'No results.log: the test never ran or crashed early. See KSP.log in ' + $Out }
