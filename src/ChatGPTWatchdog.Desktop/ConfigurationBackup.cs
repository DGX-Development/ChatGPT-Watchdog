using System.IO.Compression;
using System.Text.Json;
namespace ChatGPTWatchdog;
internal sealed record ConfigurationData(Settings Settings,WindowPreferences Preferences,byte[]? Sound);
internal static class ConfigurationBackup
{
 internal static bool Export(string directory,Settings settings,WindowPreferences preferences,string destination)
 {
  string fullDestination=Path.GetFullPath(destination);
  var protectedPaths=new[]{Path.Combine(directory,"settings.json"),Path.Combine(directory,"window-preferences.json"),settings.Notifications.SoundPath};
  if(protectedPaths.Any(path=>path!=null&&string.Equals(Path.GetFullPath(path),fullDestination,StringComparison.OrdinalIgnoreCase)))throw new IOException("Choose a backup filename outside your configuration files.");
  byte[]? sound=null;bool missing=false;if(settings.Notifications.SoundPath is string p){if(File.Exists(p)){WarningSound.ValidateWave(p);sound=File.ReadAllBytes(p);if(sound.Length>32*1024*1024)throw new InvalidDataException("The custom sound exceeds 32 MB.");}else missing=true;}
  string temp=destination+".pending";try{using(var zip=ZipFile.Open(temp,ZipArchiveMode.Create)){Write(zip,"settings.json",JsonSerializer.SerializeToUtf8Bytes(settings with{Notifications=settings.Notifications with{SoundPath=null}}));Write(zip,"window-preferences.json",JsonSerializer.SerializeToUtf8Bytes(preferences));if(sound!=null)Write(zip,"warning.wav",sound);}File.Move(temp,destination,true);}finally{if(File.Exists(temp))File.Delete(temp);}return missing;
 }
 static void Write(ZipArchive zip,string name,byte[] bytes){using var s=zip.CreateEntry(name).Open();s.Write(bytes);}
 internal static ConfigurationData Read(string file)
 {
  if(Path.GetExtension(file).Equals(".json",StringComparison.OrdinalIgnoreCase)){var settings=JsonSerializer.Deserialize<Settings>(File.ReadAllBytes(file))??throw new InvalidDataException("Settings are empty.");settings.Validate();return new(settings,new(),settings.Notifications.SoundPath is string p&&File.Exists(p)?File.ReadAllBytes(p):null);}
  using var zip=ZipFile.OpenRead(file);var allowed=new HashSet<string>(StringComparer.Ordinal){"settings.json","window-preferences.json","warning.wav"};var entries=new Dictionary<string,byte[]>(StringComparer.Ordinal);long total=0;foreach(var e in zip.Entries){if(!allowed.Contains(e.FullName)||entries.ContainsKey(e.FullName))throw new InvalidDataException("Unexpected or duplicate backup entry.");total+=e.Length;if(total>34*1024*1024)throw new InvalidDataException("The backup is too large.");using var s=e.Open();using var m=new MemoryStream();s.CopyTo(m);entries.Add(e.FullName,m.ToArray());}
  if(!entries.ContainsKey("settings.json")||!entries.ContainsKey("window-preferences.json"))throw new InvalidDataException("The backup is incomplete.");var value=JsonSerializer.Deserialize<Settings>(entries["settings.json"])??throw new InvalidDataException("Settings are empty.");value.Validate();var prefs=JsonSerializer.Deserialize<WindowPreferences>(entries["window-preferences.json"])??throw new InvalidDataException("Preferences are empty.");if(!double.IsFinite(prefs.Width)||!double.IsFinite(prefs.Height)||prefs.Width<300||prefs.Height<300)throw new InvalidDataException("Invalid window size.");entries.TryGetValue("warning.wav",out var wav);if(wav!=null&&(wav.Length<12||!wav.AsSpan(0,4).SequenceEqual("RIFF"u8)||!wav.AsSpan(8,4).SequenceEqual("WAVE"u8)))throw new InvalidDataException("Invalid WAV in backup.");return new(value,prefs,wav);
 }
 internal static void Restore(string directory,ConfigurationData data,Settings current,WindowPreferences prefs)
 {
  data.Settings.Validate();string recovery=Path.Combine(directory,"recovery");Directory.CreateDirectory(recovery);Export(directory,current,prefs,Path.Combine(recovery,"before-restore.zip"));
  string settingsPath=Path.Combine(directory,"settings.json"),prefsPath=Path.Combine(directory,"window-preferences.json");byte[]? beforeSettings=File.Exists(settingsPath)?File.ReadAllBytes(settingsPath):null,beforePrefs=File.Exists(prefsPath)?File.ReadAllBytes(prefsPath):null;string? newSound=null;
  try{var next=data.Settings;if(data.Sound!=null){string folder=Path.Combine(directory,"sounds");Directory.CreateDirectory(folder);string path=Path.Combine(folder,"Warning-"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data.Sound))[..12]+".wav");if(!File.Exists(path)){File.WriteAllBytes(path,data.Sound);newSound=path;}else if(!File.ReadAllBytes(path).AsSpan().SequenceEqual(data.Sound))throw new IOException("A sound filename is already occupied.");next=next with{Notifications=next.Notifications with{SoundPath=path}};}
   var store=new SettingsStore(directory);store.Load();store.Save(next);File.WriteAllText(prefsPath+".tmp",JsonSerializer.Serialize(data.Preferences));File.Move(prefsPath+".tmp",prefsPath,true);
  }catch{if(beforeSettings!=null)File.WriteAllBytes(settingsPath,beforeSettings);else File.Delete(settingsPath);if(beforePrefs!=null)File.WriteAllBytes(prefsPath,beforePrefs);else File.Delete(prefsPath);if(newSound!=null)File.Delete(newSound);throw;}finally{File.Delete(prefsPath+".tmp");}
 }
}
