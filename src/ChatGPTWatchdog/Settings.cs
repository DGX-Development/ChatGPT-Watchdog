using System.Text.Json;

namespace ChatGPTWatchdog;

internal sealed record Settings(int IdleMinutes = 45, int WarningSeconds = 120,
    int PostponeMinutes = 60, string Appearance = "System")
{
    public bool StartWithWindows { get; init; } = true;
    public NotificationOptions Notifications { get; init; } = new();
    public void Validate()
    {
        if (IdleMinutes is < 1 or > 1440 || WarningSeconds is < 10 or > 600 ||
            PostponeMinutes is < 1 or > 1440 || Appearance is not ("System" or "Light" or "Dark"))
            throw new InvalidDataException("Choose valid timing values and an appearance.");
        if (Notifications is null) throw new InvalidDataException("Notification options are empty.");
        if (Notifications.SoundPath is not null) WarningSound.ValidatePath(Notifications.SoundPath);
    }
}

internal sealed class SettingsStore(string directory)
{
    readonly string path = Path.Combine(directory, "settings.json");
    byte[]? original;
    public bool Exists => File.Exists(path);
    public Settings Load()
    {
        if (!File.Exists(path)) return new();
        original = File.ReadAllBytes(path);
        var result = JsonSerializer.Deserialize<Settings>(original)
            ?? throw new InvalidDataException("Settings are empty.");
        result.Validate();
        return result;
    }
    public Settings Save(Settings value)
    {
        value.Validate();
        var current = File.Exists(path) ? File.ReadAllBytes(path) : null;
        if ((original is null) != (current is null) ||
            (original is not null && !original.AsSpan().SequenceEqual(current)))
            throw new IOException("Settings changed outside this window. Reopen the watchdog before saving.");
        using var sound = SoundImport.Prepare(directory, value.Notifications);
        value = value with { Notifications = sound.Options };
        Directory.CreateDirectory(directory);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { WriteIndented = true });
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            File.Move(temporary, path, true);
            original = bytes; sound.Keep();
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return value;
    }
}


