# ChatGPT Watchdog user guide

Version 1.0.3. Prepared locally; publication pending.

## Getting started

Watchdog watches Windows inactivity and warns before closing the ChatGPT Windows desktop app.

### Warning
It can force-close ChatGPT during active work. Unsaved work may be lost. Save important work before enabling monitoring.

### Set your timing
1. Enter Inactivity, Warning and Postpone countdown on the main page.
2. Choose Save timing. Reset timing restores the default fields without saving.
3. Click the eye icon beside Pin to start monitoring. Manual launches start paused.

### Pause or close
- Use the eye icon or the tray menu to pause or resume monitoring.
- Closing with unsaved timing offers Save changes, Discard changes or Keep editing, including when X hides to the tray.
- Minimizing keeps your draft.
- File > Exit or Ctrl+Q fully exits.

Settings holds appearance, window behavior and notifications. Help and logs stay in the main Help menu.

## Setup wizard

Help > Run setup again opens the optional four-step guide. Step two is Appearance and window behavior; step three sets monitoring timing.

Finish saves your choices. Skip setup leaves draft choices unchanged.

Restoring a configuration applies it immediately. Monitoring pauses while setup is open and stays paused afterward until you start or resume it.

## Monitoring and countdown

Inactivity is the number of minutes without Windows input before a warning. Warning is the number of seconds to cancel or postpone.

Postpone adds the chosen number of minutes. Valid ranges: inactivity 1–1440 minutes, warning 10–600 seconds, postponement 1–1440 minutes.

New input cancels a warning. Sleep does not consume a warning countdown.

Unknown input or process state prevents closure. A failed close is not repeatedly attempted without new activity.

## Window and appearance

Settings > Appearance previews System, Light or Dark immediately. Save retains the choice; Cancel restores it.

Settings > Window controls close/minimize to tray, Start minimized in the system tray and size locking. Timing is on the main page.

Choose Save timing to apply changes, or Reset timing to restore the default fields. Reset window sizes clears the lock and restores the default size when saved.

Pin keeps the main window above ordinary windows; another topmost app may still be in front. Enable Start when I sign in to Windows to start monitoring automatically at sign-in.

Turn it off and Save to remove the registration.

## Warning sound

Choose No sound, Windows default or Custom WAV. Browse starts beside your selected sound, otherwise in Windows Media.

Use a genuine RIFF/WAVE file; renaming an MP3 does not convert it. Save copies the sound into this profile.

Preview plays the selected sound. If the saved sound becomes unavailable, normal warning playback falls back to the Windows sound.

## Back up and restore

File > Back up configuration saves timing, notification options, window preferences and available custom WAV bytes in a ZIP. It does not back up ChatGPT conversations or Windows startup registration.

Restore validates the package before changing files and saves a recovery ZIP first. Restored sound is imported into this profile.

You may also import legacy Watchdog settings.json; a missing external sound cannot be recovered from JSON alone. Restoring pauses monitoring.

Start it again from the main page when ready.

## Help and updates

Help > Check for updates compares the current version to the public GitHub stable release. A newer local version is reported separately and is not a reason to downgrade.

Report an issue asks before opening GitHub in your browser. A GitHub account is needed to submit.

The app does not automatically send personal settings or logs.

## Install and portable use

Installed settings are stored in LocalAppData\ChatGPT Watchdog. A portable ZIP keeps its profile folder beside the executable when portable.mode is present.

Extract the complete ZIP. Back up your configuration before replacing application files.

Before moving or deleting a portable copy, turn off Windows startup and Save. Uninstall removes the Windows startup entry belonging to this installation and offers Keep settings or Remove settings. Keep retains your profile for reinstall; Remove deletes its timing, window settings, saved sound, logs and recovery copies.

Silent uninstall keeps settings. If the profile contains a linked file or folder, Remove settings leaves the profile in place and explains why. External files and separately saved backup ZIPs are left alone.

Start minimized in the system tray hides the main window on launch after setup; use the tray menu to reopen it. This does not change whether monitoring is active.

Monitoring starts paused on manual launch; Start monitoring or sign-in startup enables it.

## Historical development material

The following retained phone-control notes describe separate paused work. Phone control is not included in 1.0.3.

## Phone control (0.6.1)

Phone control uses your own PHP hosting without a ChatGPT sign-in. Open the private phone access link in your regular browser, pair it once and subscribe in the free ntfy app. Bookmark the phone page or add it to the home screen for later access. A new website address requires new browser pairing. Keep the access link private. Phone controls do not require a paid OpenAI API or ChatGPT Sites subscription.

Phone control is optional and disabled by default for a new configuration. It requires a trusted HTTPS relay and a paired phone browser. Open **Settings > Phone control** to inspect the connection. See [relay setup](development.md#phone-control-relay). Keep the private **remote-control.json** beside the executable when updating; do not share it or include it in a public package.

The local candidate includes **Start Watchdog.cmd** for portable monitoring. Exit the old Watchdog from its tray menu before opening it. The new instance stores its profile and logs inside its candidate folder and does not register Windows startup. It uses the same inactivity/force-close protection and can close ChatGPT; this is ordinary use, not a preview. Restarting Windows still follows the existing startup entry unless that entry is separately changed. **Phone Demo.cmd** is the optional fake-target check and cannot close ChatGPT.

When a warning arrives, open the phone page and choose **Postpone** or **Allow shutdown**. Postpone grants the duration configured in Watchdog. Allow shutdown leaves the original countdown running; it does not close ChatGPT early. Wait for Watchdog's reported outcome: sending a response does not mean the PC accepted it.

Watchdog rejects expired, forged, old-session and repeated responses. Default remote limits allow at most two postponements within a 120-minute window beginning at the first accepted postponement. A full configured postponement must fit inside the remaining window; otherwise the countdown continues. Windows input changes reset this budget. Windows cannot establish that input came from a physical person.

The open phone page refreshes status while in the foreground. Background warnings need the optional ntfy Android app and relay notification setup. Delivery can be delayed or blocked by connectivity, service limits or Android power management. Missing delivery or a missing valid response leaves the PC countdown running.

The phone's private key stays in its browser storage; only the public key is copied to the PC and relay. Clearing browser storage requires pairing again. Protect the phone, browser and relay account. Trusted relay hosting is essential because it serves the signing page. This remains a user-mode utility: software already controlling the Windows account can tamper with Watchdog or stop it. Signing does not prevent that.

This candidate does not wake a sleeping computer, relaunch ChatGPT, restrict Codex to its existing task, or close all related workers. It does not inject messages into existing Codex chats or create ChatGPT Tasks.
