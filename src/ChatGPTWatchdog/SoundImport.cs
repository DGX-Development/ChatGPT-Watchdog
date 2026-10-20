using System.Security.Cryptography;

namespace ChatGPTWatchdog;

// Imports only on Save; owns only files it creates, and never overwrites a source or existing file.
internal sealed class SoundImport : IDisposable
{
    internal NotificationOptions Options { get; private set; }
    string? created;
    byte[]? contentHash;
    bool kept;
    SoundImport(NotificationOptions options) { Options = options; }
    internal static SoundImport Prepare(string directory, NotificationOptions options)
    {
        var import = new SoundImport(options);
        try
        {
            if (options.SoundPath is not string source) return import;
            string folder = Path.GetFullPath(Path.Combine(directory, "sounds"));
            source = Path.GetFullPath(source);
            // A missing previously saved sound is retained; playback falls back to Windows.
            if (string.Equals(Path.GetDirectoryName(source), folder, StringComparison.OrdinalIgnoreCase)) return import;
            if (!options.SoundEnabled) return import;
            WarningSound.ValidateWave(source);
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
            byte[] hash = SHA256.HashData(input); input.Position = 0;
            string name = Path.GetFileNameWithoutExtension(source);
            name = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).Take(60).ToArray());
            if (string.IsNullOrWhiteSpace(name)) name = "Warning sound";
            Directory.CreateDirectory(folder);
            string destination = Path.Combine(folder, name + "-" + Convert.ToHexString(hash)[..12].ToLowerInvariant() + ".wav");
            if (File.Exists(destination))
            {
                using var existing = File.OpenRead(destination);
                if (!SHA256.HashData(existing).AsSpan().SequenceEqual(hash))
                    throw new IOException("The saved sound name is already used by a different file. Choose another filename.");
            }
            else
            {
                using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                import.created = destination; import.contentHash = hash;
                input.CopyTo(output); output.Flush(true);
            }
            import.Options = options with { SoundPath = destination };
            return import;
        }
        catch { import.Dispose(); throw; }
    }
    internal void Keep() => kept = true;
    public void Dispose()
    {
        if (kept || created is null) return;
        try
        {
            using (var stream = File.OpenRead(created))
                if (!SHA256.HashData(stream).AsSpan().SequenceEqual(contentHash)) return;
            File.Delete(created);
        }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
