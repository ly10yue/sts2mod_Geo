# CommandLineToArgvW quoting, also supported by Windows PowerShell/.NET Framework.
function ConvertTo-JiuNativeArgument([AllowEmptyString()][string]$JiuValue) {
    $jiuEscaped = [regex]::Replace($JiuValue, '(\\*)"', {
        param($jiuMatch)
        ('\' * (2 * $jiuMatch.Groups[1].Length + 1)) + '"'
    })
    $jiuEscaped = [regex]::Replace($jiuEscaped, '\\+$', {
        param($jiuMatch)
        $jiuMatch.Value + $jiuMatch.Value
    })
    return '"' + $jiuEscaped + '"'
}
