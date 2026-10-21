param(
    [string]$Godot = (Join-Path $env:LOCALAPPDATA 'Temp\sts2-mod-inspect-tools\godot\Godot_v4.5.1-stable_win64_console.exe'),
    [string]$DotNetHost = (Join-Path $env:LOCALAPPDATA 'Temp\sts2-mod-inspect-tools\dotnet9\dotnet.exe'),
    [switch]$NoInstall
)
$ErrorActionPreference = 'Stop'
$jiuProject = $PSScriptRoot
$jiuGame = [System.IO.Path]::GetFullPath((Join-Path $jiuProject '..\..'))
$jiuOutput = Join-Path $jiuGame 'mods\PhigrosJiu'
$jiuGodotProject = Join-Path $jiuProject 'build\godot'
if (-not (Test-Path -LiteralPath $Godot)) { throw 'Pass -Godot with the path to Godot 4.5.1.' }
python (Join-Path $jiuProject 'tools\prepare_content.py')
if ($LASTEXITCODE -ne 0) { throw 'Content preparation failed.' }
python (Join-Path $jiuProject 'tools\audit_economy.py')
if ($LASTEXITCODE -ne 0) { throw 'Song identity/resource economy audit failed.' }
python (Join-Path $jiuProject 'tools\audit_artwork.py')
if ($LASTEXITCODE -ne 0) { throw 'Artwork identity/rarity/visual audit failed.' }
dotnet build (Join-Path $jiuProject 'src\PhigrosJiu.csproj') -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
& $Godot --headless --path $jiuGodotProject --editor --import --log-file (Join-Path $jiuProject 'build\import.log') *> (Join-Path $jiuProject 'build\import-stdout.log')
if ($LASTEXITCODE -ne 0) { throw 'Texture import failed.' }
$jiuStaging = Join-Path $jiuProject 'build\staging'
New-Item -ItemType Directory -Force -Path $jiuStaging | Out-Null
$jiuPack = Join-Path $jiuStaging 'PhigrosJiu.pck'
& $Godot --headless --path $jiuGodotProject --script 'res://pack.gd' -- $jiuPack
if ($LASTEXITCODE -ne 0) { throw 'Resource packaging failed.' }
& $Godot --headless --path $jiuGodotProject --script (Join-Path $jiuProject 'tools\validate_design_resources.gd') -- (Join-Path $jiuGame 'SlayTheSpire2.pck') $jiuPack
if ($LASTEXITCODE -ne 0) { throw 'Resource validation failed.' }
if (Test-Path -LiteralPath (Join-Path $jiuGame 'mods\jiu_2\jiu_2.dll')) {
    & (Join-Path $jiuProject 'check-coexistence.ps1') -DotNetHost $DotNetHost -MainDll (Join-Path $jiuProject 'src\bin\Release\net9.0\PhigrosJiu.dll') -MainPack $jiuPack
}
if ($NoInstall) { Write-Output "Built and validated staging package: $jiuStaging"; return }
New-Item -ItemType Directory -Force -Path $jiuOutput | Out-Null
# Check all destinations before replacing any file; a running game may lock its DLL.
foreach ($jiuName in 'PhigrosJiu.dll', 'PhigrosJiu.pck', 'PhigrosJiu.json') {
    $jiuTarget = Join-Path $jiuOutput $jiuName
    if (Test-Path -LiteralPath $jiuTarget) {
        $jiuLockCheck = [IO.File]::Open($jiuTarget, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $jiuLockCheck.Dispose()
    }
}
Copy-Item -LiteralPath $jiuPack -Destination $jiuOutput -Force
Copy-Item -LiteralPath (Join-Path $jiuProject 'src\bin\Release\net9.0\PhigrosJiu.dll') -Destination $jiuOutput -Force
Copy-Item -LiteralPath (Join-Path $jiuProject 'content\PhigrosJiu.json') -Destination $jiuOutput -Force
foreach ($jiuDoc in 'README.md', 'CREDITS.md', 'COEXISTENCE.md', 'DESIGN.md', 'VALIDATION.md', 'ECONOMY.md', 'ARTWORK.md') {
    $jiuDocSource = Join-Path $jiuProject $jiuDoc
    if (Test-Path -LiteralPath $jiuDocSource) { Copy-Item -LiteralPath $jiuDocSource -Destination $jiuOutput -Force }
}
Write-Output "Built and installed: $jiuOutput"
