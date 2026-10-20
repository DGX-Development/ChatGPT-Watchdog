param(
    [string]$Dotnet = 'dotnet',
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$project = Join-Path $projectRoot 'src\ChatGPTWatchdog\ChatGPTWatchdog.csproj'
[xml]$metadata = Get-Content -Raw -LiteralPath $project
$version = [string]$metadata.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid package version' }
$artifacts = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectRoot 'artifacts\releases' }
$packageName = 'ChatGPT-Watchdog-' + $version + '-' + $Runtime
$packageRoot = Join-Path $artifacts $packageName
$zip = Join-Path $artifacts ($packageName + '.zip')
foreach ($destination in @($packageRoot,$zip,($zip + '.sha256'))) {
    if (Test-Path -LiteralPath $destination) { throw ('Output already exists: ' + $destination) }
}
# Publish into a fresh directory. Package only explicitly selected output files.
$publish = Join-Path $artifacts ('build-' + [guid]::NewGuid().ToString('N'))
& $Dotnet publish $project -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -warnaserror -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed' }
$executable = Join-Path $publish 'ChatGPT-Watchdog.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Published executable missing' }
$assets = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'src\ChatGPTWatchdog\obj\project.assets.json') | ConvertFrom-Json
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)
$notices = @(
    @{Package='Microsoft.NETCore.App.Runtime.win-x64';Source='LICENSE.TXT';Destination='DOTNET-LICENSE.txt'},
    @{Package='Microsoft.NETCore.App.Runtime.win-x64';Source='THIRD-PARTY-NOTICES.TXT';Destination='DOTNET-THIRD-PARTY-NOTICES.txt'},
    @{Package='Microsoft.WindowsDesktop.App.Runtime.win-x64';Source='LICENSE';Destination='WINDOWS-DESKTOP-LICENSE.txt'}
)
$resolvedNotices = @()
foreach ($notice in $notices) {
    $downloads = @($assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object name -eq $notice.Package)
    if ($downloads.Count -ne 1) { throw ('Resolved runtime package missing or ambiguous: ' + $notice.Package) }
    $runtimeVersion = $downloads[0].version.Trim('[',']').Split(',')[0].Trim()
    $source = $null
    foreach ($folder in $packageFolders) {
        $candidate = Join-Path $folder ($notice.Package.ToLowerInvariant() + '\' + $runtimeVersion + '\' + $notice.Source)
        if (Test-Path -LiteralPath $candidate) { $source=$candidate; break }
    }
    if (-not $source) { throw ('Required runtime notice missing: ' + $notice.Source) }
    $resolvedNotices += @{Source=$source;Destination=$notice.Destination}
    Write-Output ('Runtime notice: ' + $notice.Package + ' ' + $runtimeVersion)
}
New-Item -ItemType Directory -Path $packageRoot | Out-Null
Copy-Item -LiteralPath $executable -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\Portable-Readme.txt') -Destination (Join-Path $packageRoot 'README.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $packageRoot 'LICENSE.txt')
foreach ($notice in $resolvedNotices) { Copy-Item -LiteralPath $notice.Source -Destination (Join-Path $packageRoot $notice.Destination) }
$allowed = @('ChatGPT-Watchdog.exe','README.txt','LICENSE.txt','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt','WINDOWS-DESKTOP-LICENSE.txt')
$actual = @(Get-ChildItem -LiteralPath $packageRoot -Force)
if (@($actual | Where-Object PSIsContainer).Count -or @(Compare-Object ($allowed | Sort-Object) @($actual.Name | Sort-Object)).Count) { throw 'Unexpected package contents' }
if ((Get-FileHash -LiteralPath (Join-Path $packageRoot 'LICENSE.txt')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $projectRoot 'LICENSE')).Hash) { throw 'License copy verification failed' }
Compress-Archive -LiteralPath @($allowed | ForEach-Object { Join-Path $packageRoot $_ }) -DestinationPath $zip
$checksum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip + '.sha256'),($checksum + '  ' + [IO.Path]::GetFileName($zip) + "`n"))
Write-Output ('Executable: ' + (Join-Path $packageRoot 'ChatGPT-Watchdog.exe'))
Write-Output ('ZIP: ' + $zip)
