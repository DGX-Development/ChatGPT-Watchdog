using W=System.Windows;
using C=System.Windows.Controls;
using System.IO.Compression;
namespace ChatGPTWatchdog;
internal static class Verification
{
 static void Render(W.Window window,string path){window.UpdateLayout();var content=(W.FrameworkElement)window.Content;var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(content.ActualWidth)),Math.Max(1,(int)Math.Ceiling(content.ActualHeight)),96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(content);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);}
internal static void Run(ReviewWindow shell,string directory)
 {
  var results=new List<string>();void Check(bool valid,string label){if(!valid)throw new Exception(label);results.Add("PASS: "+label);}
  Check(ReviewEntry.Safe,"Verification requires the isolated verification mode");
  Check(shell.engine.State==WatchState.Paused,"Review starts paused without a real monitoring controller");
  bool blocked=false;try{using var controller=new MonitoringController(shell.value,directory,_=>{},_=>{});}catch(InvalidOperationException){blocked=true;}Check(blocked,"Review blocks construction of real monitoring before target access");
  blocked=false;try{using var startup=StartupChange.Apply(true,directory);}catch(InvalidOperationException){blocked=true;}Check(blocked,"Review blocks startup registration before registry access");
  shell.Window.UpdateLayout();Check(WindowBrand.HasCorrectNativeFrames(shell.Window),"Native caption/taskbar use separate DPI-sized approved icon frames");shell.EnsureTray();Check(shell.ReviewTray != null && shell.ReviewTray.Items.Count == 6,"Tray has status, three essential commands and two separators");var trayCommands=shell.ReviewTray!.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().ToArray();Check(trayCommands.Skip(1).Select(x=>x.Text).SequenceEqual(new[]{"Open ChatGPT Watchdog","Resume monitoring","Exit"}),"Tray command order excludes Help and logs");Check(!trayCommands[0].Enabled&&!trayCommands[2].Enabled,"Status is non-clickable and safe review cannot resume monitoring");trayCommands[2].PerformClick();Check(shell.engine.State==WatchState.Paused,"Disabled tray Resume cannot activate monitoring");Check(shell.Window.Width==640,"Default standalone width is 640 DIP");
  foreach(string theme in new[]{"Dark","Light"}){
   shell.value=shell.value with{Appearance=theme};shell.ApplyTheme(theme);for(int page=0;page<3;page++){var dialog=shell.SettingsWindow(page);dialog.Show();dialog.UpdateLayout();Check(WindowBrand.HasCorrectNativeFrames(dialog),$"{theme} dialog {page} has correct native icon frames");Render(dialog,Path.Combine(directory,$"settings-{theme}-{page}.png"));Check(dialog.Width==780&&dialog.Height==700,$"{theme} settings page {page} retains shared dimensions");Check(dialog.ActualWidth>0&&dialog.ActualHeight>0,$"{theme} settings page {page} lays out");dialog.Close();}
  }
  foreach(string theme in new[]{"Dark","Light"}){
    shell.ApplyTheme(theme);
    foreach(var dialog in new[]{shell.HelpWindow(),shell.AboutWindow()}){
      dialog.Show();dialog.UpdateLayout();Check(dialog.FontFamily.Source==shell.Window.FontFamily.Source,"Shared font: "+theme+" "+dialog.Title);Check(WindowBrand.HasCorrectNativeFrames(dialog),"Native artwork: "+theme+" "+dialog.Title);Render(dialog,Path.Combine(directory,theme+"-"+(dialog.Title.StartsWith("About")?"about":"help")+".png"));dialog.Close();
    }
  }
  foreach(var sample in new[]{("1.0.3",true,"Update available"),("1.0.2",false,"You're up to date"),("0.6.0",false,"You're running a newer version")}){var result=ReviewWindow.ReleaseStatus("{\"tag_name\":\"v"+sample.Item1+"\"}",new Version(1,0,2));Check(result.Available==sample.Item2&&result.Heading==sample.Item3,"Release comparison "+sample.Item1);}
  shell.SavePreferences(shell.preferences with{LockWindowSizes=true});Check(shell.Window.ResizeMode==W.ResizeMode.CanMinimize,"Window lock disables resize and maximize");shell.SavePreferences(shell.preferences with{LockWindowSizes=false});Check(shell.Window.ResizeMode==W.ResizeMode.CanResize,"Unlock restores resizing");
  var setting=new Settings(1,10,2){StartWithWindows=false};
  for(int cycle=0;cycle<3;cycle++){
   var engine=new WatchEngine(setting,0);engine.Tick(0,1,0,"fake");Check(engine.Tick(60000,1,60000,"fake")==WatchState.Warning,$"Cycle {cycle}: inactivity starts warning");engine.Postpone(61000);Check(engine.State==WatchState.Postponed,$"Cycle {cycle}: postpone holds target");engine.Pause(true,62000);Check(engine.Tick(999999,1,999999,"fake")==WatchState.Paused,$"Cycle {cycle}: pause prevents closing");engine.Pause(false,0);engine.Tick(0,1,0,"fake");engine.Tick(60000,1,60000,"fake");Check(engine.Tick(70000,2,0,"fake")==WatchState.Monitoring,$"Cycle {cycle}: input cancels warning");engine.Tick(130000,2,60000,"fake");Check(engine.Tick(140000,2,70000,"fake")==WatchState.Closing,$"Cycle {cycle}: unchanged input reaches close state");engine.CloseFinished(false);Check(engine.Tick(180000,2,110000,"fake")==WatchState.Failed,$"Cycle {cycle}: failed close does not retry");
  }
  for(int scenario=0;scenario<5;scenario++){
 var fake=new FakeMonitorPlatform{Known=scenario!=1,HasInput=scenario!=2,Fail=scenario==3,ActivityDuringCapture=scenario==4};int? shown=null;
 using var controller=new MonitoringController(setting,directory,v=>shown=v,_=>{},fake,()=>fake.Now);
 controller.Tick();Check(fake.Reads==0,"Controller starts paused without discovering targets: "+scenario);controller.Start();
 for(int second=0;second<=80;second++){fake.Now=second*1000;controller.Tick();}
 Check(fake.ForceCount==(scenario==0||scenario==3?1:0),"Controller close authorization scenario "+scenario);
 controller.Pause();int reads=fake.Reads;fake.Now+=100000;controller.Tick();Check(fake.Reads==reads,"Paused controller stops discovery: "+scenario);
}
string test=Path.Combine(directory,"backup-test");Directory.CreateDirectory(test);var current=new SettingsStore(test);current.Load();current.Save(setting);string wav=Path.Combine(test,"tone.wav");File.WriteAllBytes(wav,"RIFF\u0024\0\0\0WAVEfmt \u0010\0\0\0\u0001\0\u0001\0\u0044\u00ac\0\0\u0088\u0058\u0001\0\u0002\0\u0010\0data\0\0\0\0".Select(c=>(byte)c).ToArray());setting=setting with{Notifications=new(SoundPath:wav)};string zip=Path.Combine(test,"backup.zip");ConfigurationBackup.Export(test,setting,new(),zip);var data=ConfigurationBackup.Read(zip);File.Delete(wav);ConfigurationBackup.Restore(test,data,new Settings(),new());var loaded=new SettingsStore(test).Load();Check(loaded.IdleMinutes==1&&File.Exists(loaded.Notifications.SoundPath),"Backup restores timing and sound after original is deleted");Check(File.Exists(Path.Combine(test,"recovery","before-restore.zip")),"Restore preserves recovery backup");
  foreach(var protectedName in new[]{"settings.json","window-preferences.json"}){var protectedPath=Path.Combine(test,protectedName);var before=File.ReadAllBytes(protectedPath);bool prevented=false;try{ConfigurationBackup.Export(test,loaded,new(),protectedPath);}catch(IOException){prevented=true;}Check(prevented&&before.SequenceEqual(File.ReadAllBytes(protectedPath)),"Backup cannot overwrite "+protectedName);}
  var savedSound=File.ReadAllBytes(loaded.Notifications.SoundPath!);var roundTrip=Path.Combine(test,"round-trip.zip");ConfigurationBackup.Export(test,loaded,new(),roundTrip);ConfigurationBackup.Restore(test,ConfigurationBackup.Read(roundTrip),loaded,new());var twice=new SettingsStore(test).Load();Check(savedSound.SequenceEqual(File.ReadAllBytes(twice.Notifications.SoundPath!)),"Repeated backup/restore preserves exact sound bytes");
  string bad=Path.Combine(test,"bad.zip");using(var z=ZipFile.Open(bad,ZipArchiveMode.Create))z.CreateEntry("../outside.txt");bool rejected=false;try{ConfigurationBackup.Read(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"Traversal backup rejected before restore");
  var stale=new SettingsStore(test);var existing=stale.Load();File.AppendAllText(Path.Combine(test,"settings.json")," ");rejected=false;try{stale.Save(existing);}catch(IOException){rejected=true;}Check(rejected,"External profile edits are not overwritten");
  Render(shell.Window,Path.Combine(directory,"main.png"));File.WriteAllLines(Path.Combine(directory,"verification.txt"),results);Directory.Delete(test,true);
 }
}


