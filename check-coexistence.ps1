param(
    [string]$DotNetHost = (Join-Path $env:LOCALAPPDATA 'Temp\sts2-mod-inspect-tools\dotnet9\dotnet.exe'),
    [string]$MainDll,
    [string]$MainPack
)
$ErrorActionPreference = 'Stop'
$jiuCheckRoot = $PSScriptRoot
$jiuCheckGame = [System.IO.Path]::GetFullPath((Join-Path $jiuCheckRoot '..\..'))
if (-not (Test-Path -LiteralPath $DotNetHost)) { throw 'Pass -DotNetHost pointing to a dotnet host with the .NET 9 runtime. Game Harmony does not support .NET 10.' }
dotnet build (Join-Path $jiuCheckRoot 'tests\Coexistence.csproj') --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'Compatibility test compilation failed.' }
$jiuCheckDll = Join-Path $jiuCheckRoot 'tests\bin\Debug\net9.0\Coexistence.dll'
$jiuCheckArgs = @($jiuCheckGame)
if ($MainDll) { $jiuCheckArgs += "--main-dll=$MainDll" }
& $DotNetHost $jiuCheckDll @jiuCheckArgs
if ($LASTEXITCODE -ne 0) { throw 'Main-first compatibility test failed.' }
& $DotNetHost $jiuCheckDll @jiuCheckArgs reverse
if ($LASTEXITCODE -ne 0) { throw 'Peer-first compatibility test failed.' }
$jiuPackCheck = @((Join-Path $jiuCheckRoot 'tools\check_pack_coexistence.py'))
if ($MainPack) { $jiuPackCheck += $MainPack }
python @jiuPackCheck
if ($LASTEXITCODE -ne 0) { throw 'Pack/localization compatibility test failed.' }
