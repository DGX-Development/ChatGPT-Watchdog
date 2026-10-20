namespace ChatGPTWatchdog;

internal static class ApplicationLogs
{
    static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatGPT Watchdog");
    internal static void OpenDataFolder() => OpenDirectory(DataDirectory);
    static void OpenDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(directory) { UseShellExecute = true });
    }
}
