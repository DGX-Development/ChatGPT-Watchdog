param([switch]$Locked)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\ChatGPTWatchdog\bin\Release\net10.0-windows\ChatGPT-Watchdog.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Build the application first using scripts/Build.ps1.' }
$kind = if ($Locked) { 'locked' } else { 'unlocked' }
$output = Join-Path $root ('.local\shutdown-tests\' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $kind + '-' + [guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $output | Out-Null
$mode = if ($Locked) { '--shutdown-locked-test' } else { '--shutdown-self-test' }
$test = Start-Process -FilePath $exe -ArgumentList @($mode, ('"' + $output + '"')) -WindowStyle Hidden -PassThru
Write-Output ('Disposable helper tests only. Log directory: ' + $output)
if ($Locked) {
    Write-Output 'Press Win+L when ready, within five minutes. Testing starts when Windows locks. Keep it locked and awake for 90 seconds, then unlock and read result.txt and the JSONL logs.'
} else {
    if (-not $test.WaitForExit(60000)) { throw 'Test still running; no process was terminated by this script.' }
    Get-Content -LiteralPath (Join-Path $output 'result.txt')
    if ($test.ExitCode -ne 0) { throw 'Disposable helper diagnostics failed. Review the logged Windows errors.' }
}
