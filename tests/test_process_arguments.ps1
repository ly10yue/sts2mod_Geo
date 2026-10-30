param([Parameter(Mandatory=$true)][string]$Python)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\tools\process_arguments.ps1')
$jiuExpected = @('plain', 'path with spaces', '', '"', 'a\"b', 'C:\path with spaces\', 'C:\ending\\', [string][char]0x9e20)
$jiuArguments = @('-c', 'import json,sys; print(json.dumps(sys.argv[1:],ensure_ascii=True))') + $jiuExpected
$jiuInfo = New-Object Diagnostics.ProcessStartInfo
$jiuInfo.FileName = $Python
$jiuInfo.UseShellExecute = $false
$jiuInfo.CreateNoWindow = $true
$jiuInfo.RedirectStandardOutput = $true
$jiuInfo.Arguments = ($jiuArguments | ForEach-Object { ConvertTo-JiuNativeArgument $_ }) -join ' '
$jiuChild = [Diagnostics.Process]::Start($jiuInfo)
$jiuJson = $jiuChild.StandardOutput.ReadToEnd()
$jiuChild.WaitForExit()
if ($jiuChild.ExitCode -ne 0) { throw 'Argument probe failed' }
$jiuActual = ConvertFrom-Json -InputObject $jiuJson
if ($jiuActual.Count -ne $jiuExpected.Count) { throw ('Argument count changed: '+$jiuJson+'; command='+$jiuInfo.Arguments) }
for ($jiuIndex=0; $jiuIndex -lt $jiuExpected.Count; $jiuIndex++) {
    if ($jiuActual[$jiuIndex] -cne $jiuExpected[$jiuIndex]) { throw "Argument changed at index $jiuIndex" }
}
Write-Output "NATIVE_ARGUMENTS=PASS shell=$($PSVersionTable.PSVersion) cases=$($jiuExpected.Count)"
