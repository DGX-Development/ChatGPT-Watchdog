ChatGPT Watchdog 1.0.3

Extract the entire portable ZIP and run ChatGPT-Watchdog.exe.

Getting started

Watchdog watches Windows inactivity and warns before closing the ChatGPT Windows desktop app.

## Warning
It can force-close ChatGPT during active work. Unsaved work may be lost. Save important work before enabling monitoring.

## Set your timing
1. Enter Inactivity, Warning and Postpone countdown on the main page.
2. Choose Save timing. Reset timing restores the default fields without saving.
3. Click the eye icon beside Pin to start monitoring. Manual launches start paused.

## Pause or close
- Use the eye icon or the tray menu to pause or resume monitoring.
- Closing with unsaved timing offers Save changes, Discard changes or Keep editing, including when X hides to the tray.
- Minimizing keeps your draft.
- File > Exit or Ctrl+Q fully exits.

Settings holds appearance, window behavior and notifications. Help and logs stay in the main Help menu.

Setup wizard

Help > Run setup again opens the optional four-step guide. Step two is Appearance and window behavior; step three sets monitoring timing.

Finish saves your choices. Skip setup leaves draft choices unchanged.

Restoring a configuration applies it immediately. Monitoring pauses while setup is open and stays paused afterward until you start or resume it.

Monitoring and countdown

Inactivity is the number of minutes without Windows input before a warning. Warning is the number of seconds to cancel or postpone.

Postpone adds the chosen number of minutes. Valid ranges: inactivity 1–1440 minutes, warning 10–600 seconds, postponement 1–1440 minutes.

New input cancels a warning. Sleep does not consume a warning countdown.

Unknown input or process state prevents closure. A failed close is not repeatedly attempted without new activity.

Window and appearance

Settings > Appearance previews System, Light or Dark immediately. Save retains the choice; Cancel restores it.

Settings > Window controls close/minimize to tray, Start minimized in the system tray and size locking. Timing is on the main page.

Choose Save timing to apply changes, or Reset timing to restore the default fields. Reset window sizes clears the lock and restores the default size when saved.

Pin keeps the main window above ordinary windows; another topmost app may still be in front. Enable Start when I sign in to Windows to start monitoring automatically at sign-in.

Turn it off and Save to remove the registration.

Warning sound

Choose No sound, Windows default or Custom WAV. Browse starts beside your selected sound, otherwise in Windows Media.

Use a genuine RIFF/WAVE file; renaming an MP3 does not convert it. Save copies the sound into this profile.

Preview plays the selected sound. If the saved sound becomes unavailable, normal warning playback falls back to the Windows sound.

Back up and restore

File > Back up configuration saves timing, notification options, window preferences and available custom WAV bytes in a ZIP. It does not back up ChatGPT conversations or Windows startup registration.

Restore validates the package before changing files and saves a recovery ZIP first. Restored sound is imported into this profile.

You may also import legacy Watchdog settings.json; a missing external sound cannot be recovered from JSON alone. Restoring pauses monitoring.

Start it again from the main page when ready.

Help and updates

Help > Check for updates compares the current version to the public GitHub stable release. A newer local version is reported separately and is not a reason to downgrade.

Report an issue asks before opening GitHub in your browser. A GitHub account is needed to submit.

The app does not automatically send personal settings or logs.

Install and portable use

Installed settings are stored in LocalAppData\ChatGPT Watchdog. A portable ZIP keeps its profile folder beside the executable when portable.mode is present.

Extract the complete ZIP. Back up your configuration before replacing application files.

Before moving or deleting a portable copy, turn off Windows startup and Save. Uninstall removes the Windows startup entry belonging to this installation and offers Keep settings or Remove settings. Keep retains your profile for reinstall; Remove deletes its timing, window settings, saved sound, logs and recovery copies.

Silent uninstall keeps settings. If the profile contains a linked file or folder, Remove settings leaves the profile in place and explains why. External files and separately saved backup ZIPs are left alone.

Start minimized in the system tray hides the main window on launch after setup; use the tray menu to reopen it. This does not change whether monitoring is active.

Monitoring starts paused on manual launch; Start monitoring or sign-in startup enables it.

