using Microsoft.Win32;

namespace ChatGPTWatchdog;

// Owns one per-user Run value. No service, task, elevation, or StartupApproved overrides.
internal sealed class StartupRegistration : IDisposable
{
    internal const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "ChatGPT Watchdog";
    readonly string? previous, desired;
    readonly RegistryValueKind previousKind;
    readonly bool changed;
    bool committed;
    StartupRegistration(string? previous, RegistryValueKind kind, string? desired)
    { this.previous = previous; previousKind = kind; this.desired = desired; changed = previous != desired; }
    internal static StartupRegistration Begin(bool enabled, string executable)
    {
        if (enabled && (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
            !string.Equals(Path.GetFileName(executable), "ChatGPT-Watchdog.exe", StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Launch ChatGPT-Watchdog.exe from its permanent folder before enabling startup.");
        string? desired = enabled ? "\"" + executable + "\"" : null;
        using var key = Registry.CurrentUser.CreateSubKey(RunPath, true)
            ?? throw new IOException("Windows startup settings could not be opened.");
        object? old = key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        var kind = old is null ? RegistryValueKind.String : key.GetValueKind(ValueName);
        if (old is not null && old is not string)
            throw new IOException("The Watchdog startup entry has an unexpected format. It was left unchanged.");
        var change = new StartupRegistration((string?)old, kind, desired);
        if (!change.changed) return change;
        try
        {
            if (desired is null) key.DeleteValue(ValueName, false);
            else key.SetValue(ValueName, desired, RegistryValueKind.String);
            if (!Equals(key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames), desired))
                throw new IOException("Windows did not retain the startup setting.");
            return change;
        }
        catch { change.Dispose(); throw; }
    }
    internal void Commit() => committed = true;
    public void Dispose()
    {
        if (committed || !changed) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunPath, true);
            if (key is null || !Equals(key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames), desired)) return;
            // Restore only our unchanged write, preserving a concurrent edit to this value.
            if (previous is null) key.DeleteValue(ValueName, false);
            else key.SetValue(ValueName, previous, previousKind);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }
}
