param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.local\dotnet-home'
$project = Join-Path $projectRoot 'src\ChatGPTWatchdog\ChatGPTWatchdog.csproj'
$tests = Join-Path $projectRoot 'tests\ChatGPTWatchdog.Tests\ChatGPTWatchdog.Tests.csproj'
& $Dotnet build $project -c Release --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Application build failed' }
& $Dotnet build $tests -c Release --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
& $Dotnet (Join-Path $projectRoot 'tests\ChatGPTWatchdog.Tests\bin\Release\net10.0\ChatGPTWatchdog.Tests.dll')
if ($LASTEXITCODE -ne 0) { throw 'Focused tests failed' }
# A local runtime-dependent build. Never copy recursively from the checkout.
$build = Join-Path $projectRoot 'src\ChatGPTWatchdog\bin\Release\net10.0-windows'
$runtime = Join-Path $projectRoot 'artifacts\ChatGPT-Watchdog'
$allowed = @('ChatGPT-Watchdog.exe','ChatGPT-Watchdog.dll','ChatGPT-Watchdog.deps.json','ChatGPT-Watchdog.runtimeconfig.json','LICENSE.txt')
if (Get-Process -Name 'ChatGPT-Watchdog' -ErrorAction SilentlyContinue) { throw 'Exit Watchdog before replacing its runtime files.' }
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
$unexpected = @(Get-ChildItem -LiteralPath $runtime -Force | Where-Object { $_.PSIsContainer -or $_.Name -notin $allowed })
if ($unexpected.Count) { throw 'Unexpected runtime contents; review them manually before building again.' }
foreach ($name in $allowed) { Copy-Item -LiteralPath (Join-Path $build $name) -Destination (Join-Path $runtime $name) -Force }
if ((Get-FileHash -LiteralPath (Join-Path $runtime 'LICENSE.txt')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'LICENSE')).Hash) { throw 'License copy verification failed' }
# Copy only the executable, assemblies, runtime configuration and license.
if (@(Compare-Object ($allowed | Sort-Object) @(Get-ChildItem -LiteralPath $runtime -Name | Sort-Object)).Count) { throw 'Runtime allowlist verification failed' }
Write-Output ('Local runnable build: ' + (Join-Path $runtime 'ChatGPT-Watchdog.exe'))
