using System.Text.Json;
namespace ChatGPTWatchdog;
internal sealed class ShutdownLog(string directory)
{
    public bool Write(string name, string detail)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "shutdown-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".jsonl");
            File.AppendAllText(path, JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, name, detail }) + Environment.NewLine);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }
}
