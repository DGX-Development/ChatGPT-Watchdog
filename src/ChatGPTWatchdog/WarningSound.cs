namespace ChatGPTWatchdog;

// WAV validation and fallback adapted from Backup Reminder.
internal static class WarningSound
{
    internal static void ValidatePath(string? path)
    {
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||!string.Equals(Path.GetExtension(path),".wav",StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a .wav sound file with a full path, or select Windows sound.");
    }
    internal static string? Problem(string? path)
    {
        try
        {
            ValidatePath(path);
            using var file=File.OpenRead(path!);Span<byte> header=stackalloc byte[12];
            int count=file.ReadAtLeast(header,12,throwOnEndOfStream:false);
            if(count==12&&header[..4].SequenceEqual("RIFF"u8)&&header[8..].SequenceEqual("WAVE"u8))return null;
            string? format=count>=4&&header[..4].SequenceEqual("OggS"u8)?"Ogg":
                count>=4&&header[..4].SequenceEqual("fLaC"u8)?"FLAC":
                count>=3&&header[..3].SequenceEqual("ID3"u8)?"MP3":null;
            return format is not null?$"This file contains {format} audio despite its .wav name. Convert it to PCM WAV; renaming the file does not convert its audio.":
                "This file is not a standard RIFF/WAVE sound. Export or convert it as PCM WAV; changing its extension is not enough.";
        }
        catch(FileNotFoundException){return "Sound file not found. Choose an existing .wav file.";}
        catch(DirectoryNotFoundException){return "Sound folder not found. Choose an existing .wav file.";}
        catch(UnauthorizedAccessException){return "The sound file cannot be read. Choose a file you can access.";}
        catch(IOException){return "The sound file could not be read. Check that it is available and not locked.";}
        catch(ArgumentException){return "Choose a .wav sound file using Browse or enter its full path.";}
        catch(InvalidDataException error){return error.Message;}
    }
    internal static bool IsWave(string? path)=>Problem(path) is null;
    internal static void ValidateWave(string path)
    {
        if(Problem(path) is string problem)throw new InvalidDataException(problem);
    }
    // Failed custom playback must fall back once, without blocking or canceling a reminder.
    internal static bool Play(string? path,Func<string,bool> wave,Action windows)
    {
        try{if(IsWave(path)&&wave(path!))return true;}catch{ }
        try{windows();}catch{ }
        return false;
    }
}
