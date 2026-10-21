param([int]$TimeoutSeconds = 120, [switch]$Staged, [switch]$EditorFeatures)
$ErrorActionPreference = 'Stop'
$jiuRoot = $PSScriptRoot
$jiuGame = [IO.Path]::GetFullPath((Join-Path $jiuRoot '../..'))
$jiuHost = [IO.Path]::GetFullPath((Join-Path $jiuRoot 'build/test-host'))
if (-not $jiuHost.StartsWith($jiuRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid test host path' }
$jiuExe = Join-Path $jiuHost 'SlayTheSpire2.exe'
if (Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue | Where-Object Path -eq $jiuExe) { throw 'Validation host already running' }
New-Item -ItemType Directory -Force -Path $jiuHost | Out-Null
foreach ($jiuFile in Get-ChildItem -LiteralPath $jiuGame -File | Where-Object { $_.Extension -in '.exe', '.dll', '.pck' -or $_.Name -eq 'release_info.json' }) {
    $jiuTarget = Join-Path $jiuHost $jiuFile.Name
    if (-not (Test-Path -LiteralPath $jiuTarget)) { New-Item -ItemType HardLink -Path $jiuTarget -Target $jiuFile.FullName | Out-Null }
}
$jiuData = Join-Path $jiuHost 'data_sts2_windows_x86_64'
if (-not (Test-Path -LiteralPath $jiuData)) { New-Item -ItemType Junction -Path $jiuData -Target (Join-Path $jiuGame 'data_sts2_windows_x86_64') | Out-Null }
@'
[application]
config/use_custom_user_dir=true
config/custom_user_dir_name="PhigrosJiuValidation"
'@ | Set-Content -LiteralPath (Join-Path $jiuHost 'override.cfg') -Encoding utf8
@'
extends SceneTree
func _initialize():
    print("JIU_TEST_USER_DIR=", OS.get_user_data_dir())
    quit()
'@ | Set-Content -LiteralPath (Join-Path $jiuHost 'query_dir.gd') -Encoding utf8

function Invoke-JiuHost([string[]]$JiuArguments) {
    $jiuStart = [Diagnostics.ProcessStartInfo]::new($jiuExe)
    $jiuStart.UseShellExecute = $false
    $jiuStart.CreateNoWindow = $true
    $jiuStart.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $jiuStart.WorkingDirectory = $jiuHost
    foreach ($jiuArgument in $JiuArguments) { $jiuStart.ArgumentList.Add($jiuArgument) }
    $jiuProcess = [Diagnostics.Process]::Start($jiuStart)
    $jiuTimer = [Diagnostics.Stopwatch]::StartNew()
    while (-not $jiuProcess.WaitForExit(1000)) {
        if ($jiuTimer.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
            throw "Validation host timed out (PID $($jiuProcess.Id)); no normal game process was stopped."
        }
    }
    if ($jiuProcess.ExitCode -ne 0) { throw "Validation host failed with exit code $($jiuProcess.ExitCode)" }
}
$jiuQueryLog = Join-Path $jiuRoot 'build/query-dir.log'
Invoke-JiuHost @('--headless', '--force-steam=off', '--script', 'res://query_dir.gd', '--log-file', $jiuQueryLog)
$jiuUserRoot = Join-Path $env:APPDATA 'PhigrosJiuValidation'
if (-not ((Get-Content -LiteralPath $jiuQueryLog -Raw).Contains("JIU_TEST_USER_DIR=$($jiuUserRoot.Replace('\','/'))"))) {
    throw 'Godot did not confirm the isolated user directory; refusing to run tests'
}
$jiuSettingsDir = Join-Path $jiuUserRoot 'default/1'
New-Item -ItemType Directory -Force -Path $jiuSettingsDir | Out-Null
$jiuSettingsFile = Join-Path $jiuSettingsDir 'settings.save'
$jiuSettings = if (Test-Path -LiteralPath $jiuSettingsFile) { Get-Content -LiteralPath $jiuSettingsFile -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
$jiuSettings['mod_settings'] = @{ mods_enabled = $true; mod_list = @() }
$jiuSettings['skip_intro_logo'] = $true
$jiuSettings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $jiuSettingsFile -Encoding utf8
foreach ($jiuMod in 'PhigrosJiu', 'jiu_2') {
    $jiuModTarget = Join-Path $jiuHost "mods/$jiuMod"
    New-Item -ItemType Directory -Force -Path $jiuModTarget | Out-Null
    foreach ($jiuExtension in '.dll', '.pck', '.json') {
        $jiuInput = Join-Path $jiuGame "mods/$jiuMod/$jiuMod$jiuExtension"
        if ($Staged -and $jiuMod -eq 'PhigrosJiu' -and $jiuExtension -eq '.pck') { $jiuInput = Join-Path $jiuRoot 'build/staging/PhigrosJiu.pck' }
        if ($Staged -and $jiuMod -eq 'PhigrosJiu' -and $jiuExtension -eq '.json') { $jiuInput = Join-Path $jiuRoot 'content/PhigrosJiu.json' }
        Copy-Item -LiteralPath $jiuInput -Destination $jiuModTarget -Force
    }
}
dotnet build (Join-Path $jiuRoot 'src/PhigrosJiu.csproj') -c Release '-p:DefineConstants=JIU_VALIDATION' '-p:OutputPath=../build/validation-dll/' --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Validation DLL compilation failed' }
Copy-Item -LiteralPath (Join-Path $jiuRoot 'build/validation-dll/PhigrosJiu.dll') -Destination (Join-Path $jiuHost 'mods/PhigrosJiu/PhigrosJiu.dll') -Force
$jiuReportFile = Join-Path $jiuRoot 'build/native-validation.json'
$jiuLogFile = Join-Path $jiuRoot 'build/native-validation.log'
$jiuRunStart = [DateTime]::UtcNow
$jiuRunArguments = @('--headless', '--force-steam=off', '--jiu-validation', "--jiu-report=$jiuReportFile", '--max-fps', '60', '--quit-after', '18000', '--log-file', $jiuLogFile)
if ($EditorFeatures) { $jiuRunArguments += '--jiu-editor-validation' }
Invoke-JiuHost $jiuRunArguments
if ((Get-Item -LiteralPath $jiuReportFile).LastWriteTimeUtc -lt $jiuRunStart) { throw 'Validation did not write a fresh report' }
$jiuReport = Get-Content -LiteralPath $jiuReportFile -Raw | ConvertFrom-Json
if ($jiuReport.result -ne 'PASS') { throw "Native validation failed: $($jiuReport.error)" }
@{
    timestamp_utc = [DateTime]::UtcNow.ToString('o')
    design_sha256 = (Get-FileHash -LiteralPath (Join-Path $jiuRoot 'content/cards.json')).Hash
    installed_dll_sha256 = (Get-FileHash -LiteralPath (Join-Path $jiuGame 'mods/PhigrosJiu/PhigrosJiu.dll')).Hash
    installed_pack_sha256 = (Get-FileHash -LiteralPath (Join-Path $jiuGame 'mods/PhigrosJiu/PhigrosJiu.pck')).Hash
    tested_pack_sha256 = (Get-FileHash -LiteralPath (Join-Path $jiuHost 'mods/PhigrosJiu/PhigrosJiu.pck')).Hash
    peer_tested_dll_sha256 = (Get-FileHash -LiteralPath (Join-Path $jiuHost 'mods/jiu_2/jiu_2.dll')).Hash
    peer_tested_pack_sha256 = (Get-FileHash -LiteralPath (Join-Path $jiuHost 'mods/jiu_2/jiu_2.pck')).Hash
    staged = [bool]$Staged
    validation_mode = $(if ($EditorFeatures) { 'editor-features' } else { 'full' })
    cases = $jiuReport.cases; checks = $jiuReport.checks; result = 'PASS'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $jiuRoot 'build/native-validation-build.json') -Encoding utf8
$jiuReport | Select-Object result, cases, checks
$jiuReport.benchmarks | Format-Table
