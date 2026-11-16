# Changelog

## 1.0.3 - 2026-11-15

- Introduce the WPF interface with shared setup, Settings, Help, About and confirmation layouts, plus installer and portable ZIP packages.
- Keep monitoring timings on the main page with Save timing, Reset timing and confirmation before discarding edits.
- Show monitoring state with an eye icon beside Pin and provide pause/resume from the notification-area menu.
- Add consistent window-size locking, tray and startup preferences. Preserve configuration and custom warning sounds in backups.
- Organize Help into readable sections. The app can still force-close ChatGPT while work is in progress; save important work before enabling monitoring.
- The separately recorded phone-control work below remains outside this release preparation.

## Unreleased - 0.6.1

- Prepare 0.6.1 for an independently hosted PHP relay and phone interface, with no ChatGPT sign-in dependency.
- Add project-local portable data and logs with startup registration disabled, and prompt relay delivery on warning transitions.
- Package the phone connection support in the self-contained Windows executable; retain private connection settings separately during updates.

- Add optional phone control through an outbound HTTPS relay and a separately paired P-256 signing key.
- Support signed Postpone and Allow shutdown decisions with warning-specific expiry, replay protection and bounded extensions.
- Add an independent phone page, optional ntfy alerts and isolated phone-settings previews.
- Keep remote control disabled by default. Relay hosting, phone delivery and live Windows operation require setup and verification.

## 0.5.5 - October 20, 2026

- Publish the first portable Windows x64 download with its .NET runtime, license notices and SHA256 checksum.
- Enable Help > Report an issue with the project's GitHub issue form.
- Add the project-page link to the README and repository Website field.

## 0.5.4 - Initial public source

- Monitor computer inactivity for the ChatGPT Windows desktop app, with configurable warning and postponement durations.
- Force-close the verified application when the countdown expires; show prominent unsaved-work warnings.
- Provide pause/resume, per-user sign-in startup, local settings/logs and single-instance handling.
- Support Windows or custom WAV warning sounds, foreground requests, always-on-top and System/Light/Dark appearance.
- Include Help, About, matching command icons and Settings > Open data folder.
- Use Exit in the File menu and Exit watchdog in the tray menu.
- Include the unchanged PolyForm Noncommercial 1.0.0 license with the build.
