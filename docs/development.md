# Development and verification

## Requirements

- Windows 11 x64.
- .NET 10 SDK for building and tests.
- .NET 10 Desktop Runtime for the framework-dependent application output.

There are no third-party package dependencies. NuGet.Config clears external package feeds. Install the SDK and required targeting packs through normal supported setup before building.

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

Preserve the official LICENSE text exactly. A future self-contained package must include the licenses/notices for the actual bundled runtime. Selecting and publishing that package is separate from building the present five-file output.
