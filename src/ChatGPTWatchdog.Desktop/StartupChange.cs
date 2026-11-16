using Microsoft.Win32;
namespace ChatGPTWatchdog;
internal sealed class StartupChange : IDisposable
{
 const string Run=@"Software\Microsoft\Windows\CurrentVersion\Run";
 static string EntryName(string directory){string normal=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPT Watchdog");return Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar).Equals(normal,StringComparison.OrdinalIgnoreCase)?"ChatGPT Watchdog":"ChatGPT Watchdog."+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant())))[..16];}
 readonly string name;
 readonly object? old;
 readonly RegistryValueKind kind;
 readonly string? next;
 bool committed;
 StartupChange(string name,object? old,RegistryValueKind kind,string? next){this.name=name;this.old=old;this.kind=kind;this.next=next;}
 internal static StartupChange Apply(bool enabled,string directory)
 {
  if(ReviewEntry.Safe)throw new InvalidOperationException("Startup is disabled in this review.");
  string name=EntryName(directory);
  using var key=Registry.CurrentUser.CreateSubKey(Run);var old=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);var kind=old==null?RegistryValueKind.String:key.GetValueKind(name);
  if(old!=null&&old is not string)throw new IOException("The startup entry has an unexpected format.");
  string? next=enabled?$"\"{Environment.ProcessPath}\" --data-directory \"{directory}\" --startup":null;
  var change=new StartupChange(name,old,kind,next);try{if(next==null)key.DeleteValue(name,false);else key.SetValue(name,next,RegistryValueKind.String);return change;}catch{change.Dispose();throw;}
 }
 internal static void RemoveOwned(string directory){string name=EntryName(directory);using var key=Registry.CurrentUser.OpenSubKey(Run,true);if(key?.GetValue(name) is string command && command.StartsWith("\""+Environment.ProcessPath+"\"",StringComparison.OrdinalIgnoreCase))key.DeleteValue(name,false);}
 internal void Commit()=>committed=true;
 public void Dispose(){if(committed)return;using var key=Registry.CurrentUser.OpenSubKey(Run,true);if(key==null||!Equals(key.GetValue(name),next))return;if(old==null)key.DeleteValue(name,false);else key.SetValue(name,old,kind);}
}
