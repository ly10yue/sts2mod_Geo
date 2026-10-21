param([switch]$NoBrowser, [ValidateRange(1024,65535)][int]$Port = 18769)
$ErrorActionPreference = 'Stop'
$jiuEditorRoot = $PSScriptRoot
$jiuEditorUrl = "http://127.0.0.1:$Port/"
try {
    $jiuResponse = Invoke-WebRequest -Uri $jiuEditorUrl -TimeoutSec 2 -UseBasicParsing
    if ($jiuResponse.Content -notmatch '鸠 · 卡牌工坊') { throw 'Port is occupied by another program' }
} catch {
    $jiuPythons = @(
        (Join-Path $env:LOCALAPPDATA 'Programs/Python/Python312/python.exe'),
        (Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
    )
    $jiuPython = $null
    foreach ($jiuCandidate in $jiuPythons) {
        if (Test-Path -LiteralPath $jiuCandidate) { $jiuPython = $jiuCandidate; break }
    }
    if (-not $jiuPython) { throw '未找到 Python 3，请安装 Python 后运行 tools/card_editor.py。' }
    $jiuEditorLog = Join-Path $jiuEditorRoot "build/card-editor-$Port"
    New-Item -ItemType Directory -Force -Path $jiuEditorLog | Out-Null
    $jiuArguments = '"' + (Join-Path $jiuEditorRoot 'tools/card_editor.py') + '" --no-browser --port ' + $Port
    $jiuProcess = Start-Process -FilePath $jiuPython -ArgumentList $jiuArguments -WorkingDirectory $jiuEditorRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $jiuEditorLog 'server.log') -RedirectStandardError (Join-Path $jiuEditorLog 'error.log')
    $jiuReady = $false
    for ($jiuAttempt = 0; $jiuAttempt -lt 30; $jiuAttempt++) {
        Start-Sleep -Milliseconds 200
        if ($jiuProcess.HasExited) { throw '编辑器启动失败，请查看 build/card-editor/error.log。' }
        try {
            $jiuResponse = Invoke-WebRequest -Uri $jiuEditorUrl -TimeoutSec 1 -UseBasicParsing
            if ($jiuResponse.Content -match '鸠 · 卡牌工坊') { $jiuReady = $true; break }
        } catch { }
    }
    if (-not $jiuReady) { throw "编辑器未能启动；请检查 $Port 端口是否被占用。" }
    $jiuProcess.Id | Set-Content -LiteralPath (Join-Path $jiuEditorLog 'server.pid')
}
if (-not $NoBrowser) { Start-Process $jiuEditorUrl -WindowStyle Hidden }
Write-Output "卡牌编辑器已打开：$jiuEditorUrl"
