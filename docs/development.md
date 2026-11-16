# Development and verification

The 1.0.3 desktop release builds from `src/ChatGPTWatchdog.Desktop/ChatGPTWatchdog.Desktop.csproj`. It uses the shared templates in `src/DgxDesktop`. Every compile/resource reference is repository-relative; another application's checkout is not required.

## Build the installer and portable archive

Install the .NET 10 SDK and Inno Setup 7.1.1 on Windows x64. From this repository root:

```powershell
./scripts/Build-DesktopRelease.ps1 -Compiler 'C:/Program Files (x86)/Inno Setup 7/ISCC.exe'
```

Use `-DotNet` or `-Compiler` to select installed tool executables, and `-OutputDirectory` to select an empty output location. Defaults produce `artifacts/releases/Portable`, a portable ZIP, a Setup EXE and SHA256SUMS.txt. Restore requires the Windows runtime packs, either cached or available from NuGet. End users need no separate .NET runtime.

The portable archive contains only the executable, offline instructions, original license, runtime notices and portable.mode. It contains no user profile, test fixtures, private notes or machine-specific configuration. The installer omits portable.mode and uses the installed profile location. Building does not enable Windows startup or monitoring and does not publish anything.

## Isolated functional verification

Exit any running copy before testing. Run the packaged executable with an explicit disposable directory:

```powershell
./artifacts/releases/Portable/ChatGPT-Watchdog.exe --data-directory "$PWD/artifacts/verification" --verify
```

Inspect the generated JSON/text reports and process exit status. These checks use isolated profiles; they do not prove a real sign-in or a real destructive action. Never use personal backup files as disposable test input. Installer verification must preserve any existing installation and personal profile.

## Shared UI template updates

The three shared files are DgxAboutView.cs, ForgePresentation.xaml and Shell.xaml. Keep the files byte-identical across the three applications. `template-manifest.json` records their SHA-256 hashes and canonical source identity. Product-specific content stays in its own page adapters. Do not make per-app copies of common layout logic or tune each page separately.

After changing a template, update all consumers and hashes, rebuild each package, inspect light/dark states, and repeat the relevant behavior tests. The shared factories cover About, notices, setup, Help articles, settings rows, unsaved-change prompts and sample content. Date-picker button and calendar styling live in ForgePresentation.xaml.

## Release preparation

Confirm version metadata, Help and offline/online guides, installer wording, package allowlists, hashes, backup/restore round trips and install/reinstall/uninstall behavior. In an isolated Windows account, verify that the installed copy has a startup entry pointing to its executable. Leave startup enabled, uninstall, and confirm the entry is gone and the profile follows the selected Keep or Remove option. Record an untested option separately. Use generic optional samples for screenshots. Keep monitoring paused during presentation captures. Publishing and changing repository metadata are separate steps.

## Historical development procedure

The following describes the retained older and paused phone-control sources, not the current desktop release build. Do not substitute these commands for the release procedure above.

# Development and verification

## Requirements

- Windows 11 x64.
- .NET 10 SDK for building and tests.
- .NET 10 Desktop Runtime for the framework-dependent application output.

There are no third-party package dependencies. NuGet.Config clears external package feeds. Install the SDK and required targeting packs through normal supported setup before building.

## Phone control relay

The 0.6.1 candidate uses the independently hosted PHP relay. `ApiStyle: "Php"` selects `api.php?route=pc`; `SiteAccessToken` is empty. There is no active Sites service. Tokens remain in ignored local configuration; the website stores only their SHA-256 hashes. The authoritative website sources and publication inventory live in the separate website project. Do not upload `.dev`, local connection files, runtime state or client credentials.

The phone stores a non-extractable P-256 signing key in its browser. Only the public key is pinned on the PC and relay. Responses bind the process instance, warning nonce and decision using ECDSA/SHA-256 with a 64-byte IEEE P1363 signature. The PC validates monotonic expiry, replay and configured extension limits. No remote command can run a shell, change timings, pause indefinitely or accelerate shutdown. Existing Windows-user and hosting/browser compromise limitations still apply.

Use `--portable <data-directory>` for real monitoring with settings, imported sounds and logs inside that directory. Startup registration is disabled. The existing session mutex prevents simultaneous monitoring instances; exit the installed copy manually first. This mode can force-close ChatGPT. Use `--phone-demo <output-directory>` for a fake target, or `--phone-check <output-directory>` to start that fake warning immediately. Neither fake mode constructs the real monitoring controller or accesses personal settings.

The development candidate has launchers for portable use and the fake-target demo. Its five framework-dependent runtime files need the .NET 10 Desktop Runtime. Normal use should run the self-contained executable produced by `Publish-Windows.ps1`, which includes its runtime and needs no launch script. Preserve the private `remote-control.json` beside the installed executable during updates; it is excluded from packages and source publication. Candidate connection JSON and profile data also remain excluded. Installed replacement and startup changes need task-specific approval.

For a fresh candidate build, run `scripts/Test-PhoneControl.ps1` with an existing SDK path. It builds, runs the fake-target checks and optionally renders the phone settings. It refuses to overwrite an existing candidate. Packaging does not launch monitoring or enable startup. Android receipt and installed monitoring cannot be inferred from compilation alone.

Background notifications use the free ntfy service; subscription topics are bearer addresses. Messages contain only a generic warning and the phone-page URL. Delivery is not guaranteed. Missing delivery or a valid response leaves the PC countdown running. Wake/relaunch and task isolation are not implemented.

## Build

From the repository root:

```powershell
.\scripts\Build.ps1
```

If needed, pass `-Dotnet` with the path to your SDK executable. The script compiles the application and focused tests with warnings as errors, runs the focused tests, then copies exactly five files to `artifacts/ChatGPT-Watchdog`:

- ChatGPT-Watchdog.exe
- ChatGPT-Watchdog.dll
- ChatGPT-Watchdog.deps.json
- ChatGPT-Watchdog.runtimeconfig.json
- LICENSE.txt

Exit a running Watchdog before replacing its files. Keep the output together. The script does not launch monitoring, register startup, create a release, or download packages. This is not a self-contained distribution and no installer or updater is provided.

## Safe visual previews

```powershell
.\artifacts\ChatGPT-Watchdog\ChatGPT-Watchdog.exe --preview .local/preview
```

Preview mode creates generic forms and menus without loading personal settings, constructing the monitoring controller or discovering/closing ChatGPT. Sound checks use fake callbacks. It renders both themes and performs isolated UI checks. Native preview window frames can differ from normal Windows desktop frames.

## Tests

The focused test project covers state transitions, settings persistence, shutdown guards, failure handling and sound/notification behavior using fake targets. It does not call Windows target APIs. The build script runs it automatically.

Optional disposable-process diagnostics use only helpers they create:

```powershell
.\scripts\Test-Shutdown.ps1
```

The locked variant waits for an actual Windows lock and needs deliberate interactive participation:

```powershell
.\scripts\Test-Shutdown.ps1 -Locked
```

Use its printed output directory to inspect result.txt and JSONL records after unlocking. Leave Windows locked for at least 90 seconds and keep it awake; locking is different from sleeping. An unknown lock state or interruption invalidates the test. Do not infer successful full-duration monitoring from this short diagnostic.

Do not use important active ChatGPT work for a live force-close test. Active-task shutdown, full-duration locked monitoring and actual sign-in startup remain separate verification tasks. There is no name-based batch termination, process-tree kill, privilege elevation or automatic wake behavior.

## Artwork and packaging

Production icon resources are assets/watchdog.ico and assets/watchdog.png. The approved vector source is assets/concepts/watchdog-refined-v2.svg. Repository banners, social artwork and screenshots are not part of the runtime output.

Preserve the official LICENSE text exactly. To prepare a self-contained portable Windows x64 package:

```powershell
.\scripts\Publish-Windows.ps1
```

Pass `-Dotnet` if the SDK is not on PATH, and optionally `-OutputDirectory` for a fresh destination. Runtime packs must already be cached; NuGet.Config does not enable network feeds. If needed, restore the project with `-r win-x64 -p:SelfContained=true -p:PublishSingleFile=true --source https://api.nuget.org/v3/index.json` using the same SDK and package-cache environment before publishing.

The script refuses to overwrite an existing package. It publishes without debug symbols, includes notices from the exact resolved Microsoft runtime packs, and packages only the executable, offline README, app license and three runtime notice files. It writes a ZIP and SHA256 checksum under artifacts/releases. It does not replace the installed local build, launch monitoring, register startup, tag, commit or publish to GitHub.
