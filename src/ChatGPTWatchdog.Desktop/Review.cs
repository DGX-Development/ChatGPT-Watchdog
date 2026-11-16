using W=System.Windows;
using C=System.Windows.Controls;
using M=System.Windows.Media;
using F=System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
namespace ChatGPTWatchdog;

internal static class ReviewEntry
{
 #if SAFE_REVIEW
 internal static bool Safe=>true;
#else
 internal static bool Safe=>Verifying;
#endif
 internal static bool Verifying;
 internal static string Resource(string name){using var s=typeof(ReviewEntry).Assembly.GetManifestResourceStream(name)??throw new IOException(name);using var r=new StreamReader(s);return r.ReadToEnd();}
 static string ReviewStyles(){
  var xml=System.Xml.Linq.XDocument.Parse(Resource("styles.xaml"));
  System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
  foreach(var style in xml.Root!.Elements(ns+"Style").Where(x=>new[]{"Button","ComboBox","CheckBox"}.Contains((string?)x.Attribute("TargetType")))){
   var focus=style.Elements(ns+"Setter").FirstOrDefault(x=>(string?)x.Attribute("Property")=="FocusVisualStyle");
   var border=focus?.Descendants(ns+"Border").FirstOrDefault();if(border!=null){border.RemoveAttributes();border.SetAttributeValue("Margin","4");border.SetAttributeValue("BorderThickness","1");border.SetAttributeValue("CornerRadius","4");border.SetAttributeValue("BorderBrush","{DynamicResource Theme969696}");}
  }
  var combo=xml.Root.Elements(ns+"Style").Single(x=>(string?)x.Attribute("TargetType")=="ComboBox");
  var popup=combo.Descendants(ns+"Popup").Single();popup.SetAttributeValue("PlacementTarget","{Binding RelativeSource={RelativeSource TemplatedParent}}");
  var viewerStyle=xml.Root.Elements(ns+"Style").Single(x=>(string?)x.Attribute("TargetType")=="ScrollViewer");
  var popupTemplate=new System.Xml.Linq.XElement(viewerStyle.Descendants(ns+"ControlTemplate").First());
  popupTemplate.Element(ns+"ControlTemplate.Triggers")?.Remove();
  var viewer=combo.Descendants(ns+"ScrollViewer").Single();
  viewer.Element(ns+"ScrollViewer.Template")?.Remove();
  viewer.AddFirst(new System.Xml.Linq.XElement(ns+"ScrollViewer.Template",popupTemplate));
  var surface=combo.Descendants(ns+"Border").Single(x=>(string?)x.Attribute(System.Xml.Linq.XName.Get("Name","http://schemas.microsoft.com/winfx/2006/xaml"))=="DropdownSurface");surface.SetAttributeValue("Padding","5");
  var option=xml.Root.Elements(ns+"Style").Single(x=>(string?)x.Attribute("TargetType")=="ComboBoxItem");option.Elements(ns+"Setter").Single(x=>(string?)x.Attribute("Property")=="Padding").SetAttributeValue("Value","17,9");
  var menuStyle=xml.Root.Elements(ns+"Style").Single(x=>(string?)x.Attribute("TargetType")=="MenuItem");
  var topItem=menuStyle.Descendants(ns+"Trigger").Single(x=>(string?)x.Attribute("Property")=="Role"&&(string?)x.Attribute("Value")=="TopLevelItem");topItem.Add(new System.Xml.Linq.XElement(ns+"Setter",new System.Xml.Linq.XAttribute("Property","Padding"),new System.Xml.Linq.XAttribute("Value","12,6")));
  return xml.ToString();
 }

 [STAThread] static int Main(string[] args)
 {
  Verifying=args.Contains("--verify");
  string directory=Safe?(File.Exists(Path.Combine(AppContext.BaseDirectory,"portable.mode"))?Path.Combine(AppContext.BaseDirectory,"profile"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPT Watchdog Review")):File.Exists(Path.Combine(AppContext.BaseDirectory,"portable.mode"))?Path.Combine(AppContext.BaseDirectory,"profile"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ChatGPT Watchdog");
  try{
   int p=Array.IndexOf(args,"--data-directory");if(p>=0)directory=Path.GetFullPath(args[p+1]);
   if(!args.Contains("--remove-integrations"))Directory.CreateDirectory(directory);
   if(Safe&&args.Any(x=>x is "--monitor" or "--startup"))throw new ArgumentException("Monitoring and Windows startup are disabled in this review build.");
   string identity="Local\\DGX.Watchdog.WpfReview."+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(directory.ToUpperInvariant())))[..20];
   if(!Safe)identity=$"Local\\DGX.ChatGPTWatchdog.{Process.GetCurrentProcess().SessionId}";using var mutex=new Mutex(false,identity);bool owns;try{owns=mutex.WaitOne(0);}catch(AbandonedMutexException){owns=true;}
   using var activate=new EventWaitHandle(false,EventResetMode.AutoReset,identity+".activate");if(!owns){if(args.Contains("--remove-integrations"))throw new IOException("Close the running app before uninstalling.");activate.Set();return 0;}
   if(args.Contains("--remove-integrations")){StartupChange.RemoveOwned(directory);mutex.ReleaseMutex();return 0;}
   var app=new W.Application{ShutdownMode=W.ShutdownMode.OnMainWindowClose};DgxUi.Install();
   app.Resources.MergedDictionaries.Add((W.ResourceDictionary)XamlReader.Parse(ReviewStyles()));
   var shell=new ReviewWindow(directory);if(!Safe&&!args.Contains("--paused")&&args.Contains("--startup")&&shell.value.StartWithWindows)shell.Window.ContentRendered+=(_,_)=>shell.StartMonitoring();
   if(!Safe&&shell.preferences.StartMinimized&&File.Exists(Path.Combine(directory,"setup.json")))shell.Window.ContentRendered+=(_,_)=>shell.Hide();
   var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};timer.Tick+=(_,_)=>{if(activate.WaitOne(0))shell.Reopen();};timer.Start();
   if(args.Contains("--verify"))shell.Window.ContentRendered+=(_,_)=>{try{Verification.Run(shell,directory);}catch(Exception e){File.WriteAllText(Path.Combine(directory,"verification-error.txt"),e.ToString());Environment.ExitCode=1;}finally{shell.Exit();}};
   app.Run(shell.Window);timer.Stop();mutex.ReleaseMutex();return Environment.ExitCode;
  }catch(Exception e){if(!args.Contains("--remove-integrations")){Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"launch-error.txt"),e.ToString());}return 1;}
 }
}
internal sealed record WindowPreferences(bool CloseToTray=true,bool MinimizeToTray=false,bool LockWindowSizes=false,double Width=640,double Height=860,bool StartMinimized=false);
internal sealed partial class ReviewWindow
{
 internal W.Window Window;
 internal Settings value;
 internal readonly string directory;
 internal SettingsStore store;
 internal WindowPreferences preferences;
 bool dark=true,exiting;
 string theme="System";
 F.NotifyIcon? tray;
 readonly C.TextBlock status=new();
 readonly C.TextBlock timings=new();
 C.Button monitoringToggle=null!;
 C.TextBox mainIdle=null!,mainWarning=null!,mainPostpone=null!;
 internal WatchEngine engine;
 W.Window? warning;
 readonly System.Windows.Threading.DispatcherTimer demoTimer=new(){Interval=TimeSpan.FromMilliseconds(250)};
 long demoNow;
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
 static M.Brush Brush(string hex)=>(M.Brush)new M.BrushConverter().ConvertFromString(hex)!;
 static void Resource(W.FrameworkElement e,W.DependencyProperty p,string key)=>e.SetResourceReference(p,key);
 static C.TextBlock Text(string value,double size=14)=>new(){Text=value,FontSize=size,TextWrapping=W.TextWrapping.Wrap};
 C.Button Button(string text,Action action){var b=new C.Button{Content=text,Margin=new W.Thickness(0)};b.Click+=(_,_)=>Guard(action);return b;}
 void Guard(Action action){try{action();}catch(Exception e){Notice("Could not complete this action",e.Message);}}
 static void Open(string target)=>Process.Start(new ProcessStartInfo(target){UseShellExecute=true});
 internal ReviewWindow(string path)
 {
  directory=path;store=new(directory);value=store.Load();if(!store.Exists)value=value with{StartWithWindows=false};
  preferences=File.Exists(PrefsPath)?JsonSerializer.Deserialize<WindowPreferences>(File.ReadAllText(PrefsPath))??new():new();
  engine=new(value,0);engine.Pause(true,0);ApplyTheme(value.Appearance);
  Window=(W.Window)XamlReader.Parse(ReviewEntry.Resource("shell.xaml"));Window.Title=ReviewEntry.Safe?"ChatGPT Watchdog - Safe review":"ChatGPT Watchdog";
  Window.Width=Math.Clamp(preferences.Width,580,W.SystemParameters.WorkArea.Width);Window.Height=Math.Clamp(preferences.Height,560,W.SystemParameters.WorkArea.Height);Window.MinWidth=580;Window.MinHeight=560;Window.WindowStartupLocation=W.WindowStartupLocation.CenterScreen;
  Window.Icon=BrandSource();
  var root=(C.DockPanel)Window.Content;root.Children.RemoveAt(1);var menu=(C.Menu)Window.FindName("AppMenu");menu.Items.Clear();
  var file=new C.MenuItem{Header="_File"};menu.Items.Add(file);Command(file,"Back up configuration…","config-backup",BackUp);Command(file,"Restore configuration…","config-restore",Restore);file.Items.Add(new C.Separator());Command(file,"Open data folder","folder",()=>Open(directory));file.Items.Add(new C.Separator());Command(file,"Exit","exit",Exit,"Ctrl+Q");
  var settings=new C.MenuItem{Header="_Settings",Height=28,Padding=new W.Thickness(11,2,11,4),Margin=new W.Thickness(2,0,2,0),VerticalAlignment=W.VerticalAlignment.Center};menu.Items.Add(settings);settings.Click+=(_,e)=>{e.Handled=true;SettingsWindow().ShowDialog();};
  var help=new C.MenuItem{Header="_Help"};menu.Items.Add(help);Command(help,"Help & user guide","help",Help,"F1");Command(help,"Run setup again…","setup",Setup);Command(help,"Check for updates","updates",CheckUpdates);Command(help,"Report an issue…","report",()=>Notice("Report an issue","Open ChatGPT Watchdog's GitHub issue page in your browser. A GitHub account is required to submit an issue. ChatGPT Watchdog will stay open.",()=>Open("https://github.com/DGX-Development/ChatGPT-Watchdog/issues/new")));Command(help,"Open logs folder","folder",OpenLogs);help.Items.Add(new C.Separator());Command(help,"About ChatGPT Watchdog","info",About);
  var body=new C.DockPanel{Margin=new W.Thickness(24,12,0,24)};root.Children.Add(body);
  var actions=new C.WrapPanel{HorizontalAlignment=W.HorizontalAlignment.Right,Margin=new W.Thickness(0,20,24,0)};C.DockPanel.SetDock(actions,C.Dock.Bottom);body.Children.Add(actions);actions.Children.Add(Standard(Button("Close for now",()=>Window.Close())));actions.Children.Add(Standard(Button("Settings",()=>SettingsWindow().ShowDialog())));
  var stack=new C.StackPanel{Margin=new W.Thickness(0,0,7,0)};body.Children.Add(new C.ScrollViewer{Content=stack,VerticalScrollBarVisibility=C.ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=C.ScrollBarVisibility.Disabled});
  var heading=new C.DockPanel{Margin=new W.Thickness(0,0,0,24)};stack.Children.Add(heading);
  var pin=Button("",()=>{Window.Topmost=!Window.Topmost;});pin.VerticalAlignment=W.VerticalAlignment.Top;pin.Width=pin.Height=36;pin.Padding=new W.Thickness(0);pin.ToolTip="Always on top";var pinIcon=Glyph("pin",24);pin.Content=pinIcon;pin.Click+=(_,_)=>{pin.ToolTip=Window.Topmost?"Always on top is on":"Always on top is off";pinIcon.Tag=Window.Topmost?"pin-active":"pin";Resource(pinIcon,System.Windows.Documents.TextElement.ForegroundProperty,Window.Topmost?"DashboardAccent":"ThemeF2F2F2");};C.DockPanel.SetDock(pin,C.Dock.Right);heading.Children.Add(pin);
  monitoringToggle=Button("",ToggleMonitoring);monitoringToggle.Width=monitoringToggle.Height=36;monitoringToggle.Padding=new W.Thickness(0);monitoringToggle.Margin=new W.Thickness(0,0,8,0);monitoringToggle.VerticalAlignment=W.VerticalAlignment.Top;C.DockPanel.SetDock(monitoringToggle,C.Dock.Right);heading.Children.Add(monitoringToggle);
  var logo=new C.Image{Source=BrandSource(),Width=48,Height=48,Margin=new W.Thickness(0,0,14,0)};C.DockPanel.SetDock(logo,C.Dock.Left);heading.Children.Add(logo);var titles=new C.StackPanel();titles.Children.Add(new C.TextBlock{Text="ChatGPT Watchdog",FontSize=DgxUi.MainTitleSize,FontWeight=W.FontWeights.SemiBold});titles.Children.Add(WorkText("For the ChatGPT Windows desktop app."));heading.Children.Add(titles);
  stack.Children.Add(ReviewEntry.Safe?Card("Monitoring is disabled in this review","You can edit an isolated profile and try a simulated warning. This copy cannot discover or close ChatGPT or Codex, and cannot register Windows startup."):DangerCard("This app force-closes ChatGPT","After the inactivity time you set, ChatGPT will be force-closed when the countdown ends, even if it is working on a task. You may lose unsaved work. Cancel or postpone the countdown, or pause monitoring, to keep it open."));
  var panel=new C.StackPanel();panel.Children.Add(new C.TextBlock{Text="Monitoring preferences",FontSize=DgxUi.SectionSize,FontWeight=W.FontWeights.SemiBold});status.Margin=new W.Thickness(0,12,0,12);panel.Children.Add(status);
  mainIdle=Number(panel,"Inactivity (minutes)",value.IdleMinutes,1,1440);mainWarning=Number(panel,"Warning (seconds)",value.WarningSeconds,10,600);mainPostpone=Number(panel,"Postpone countdown (minutes)",value.PostponeMinutes,1,1440);
  var saveTiming=Standard(Button("Save timing",SaveTiming));saveTiming.HorizontalAlignment=W.HorizontalAlignment.Left;saveTiming.Margin=new W.Thickness(0,8,0,0);var timingActions=new C.WrapPanel();timingActions.Children.Add(saveTiming);var resetTiming=Standard(Button("Reset timing",()=>{var defaults=new Settings();mainIdle.Text=defaults.IdleMinutes.ToString();mainWarning.Text=defaults.WarningSeconds.ToString();mainPostpone.Text=defaults.PostponeMinutes.ToString();}));resetTiming.Margin=new W.Thickness(8,8,0,0);resetTiming.ToolTip="Restore default timing values. Choose Save timing to apply.";timingActions.Children.Add(resetTiming);panel.Children.Add(timingActions);stack.Children.Add(Surface(panel));
  Window.SourceInitialized+=(_,_)=>{Caption(Window);_ = new WindowBrand(Window);};Window.Closing+=(_,e)=>{if(TimingChanged()){if(!DgxUnsavedChanges.Confirm(Window,content=>Dialog("Unsaved changes",560,220,content),SaveTiming)){e.Cancel=true;exiting=false;return;}SyncTiming();}if(!exiting&&preferences.CloseToTray){e.Cancel=true;Hide();return;}SaveBounds();};Window.StateChanged+=(_,_)=>{if(Window.WindowState==W.WindowState.Minimized&&preferences.MinimizeToTray)Hide();};Window.Closed+=(_,_)=>{demoTimer.Stop();runtime?.Dispose();DisposeTray();};Window.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.F1){e.Handled=true;Help();}if(e.Key==System.Windows.Input.Key.Q&&System.Windows.Input.Keyboard.Modifiers==System.Windows.Input.ModifierKeys.Control){e.Handled=true;Exit();}};
  if(!ReviewEntry.Safe&&!File.Exists(Path.Combine(directory,"setup.json"))){Window.ContentRendered+=FirstSetup;void FirstSetup(object? sender,EventArgs e){Window.ContentRendered-=FirstSetup;Setup();File.WriteAllText(Path.Combine(directory,"setup.json"),"{}");}}
  ApplyLock();Refresh();demoTimer.Tick+=(_,_)=>DemoTick();
 }
 void Command(C.MenuItem parent,string label,string glyph,Action action,string shortcut=""){var item=new C.MenuItem{Header=label,Icon=Glyph(glyph,24),InputGestureText=shortcut};item.Click+=(_,_)=>Guard(action);parent.Items.Add(item);}
 C.Border Surface(W.UIElement content){var b=new C.Border{CornerRadius=new W.CornerRadius(9),Padding=new W.Thickness(18),Margin=new W.Thickness(0,0,0,16),Child=content};Resource(b,C.Border.BackgroundProperty,"Surface");return b;}
 C.Border Card(string title,string detail){var p=new C.StackPanel();p.Children.Add(new C.TextBlock{Text=title,FontWeight=W.FontWeights.SemiBold,TextWrapping=W.TextWrapping.Wrap});var t=WorkText(detail);t.Margin=new W.Thickness(0,8,0,0);p.Children.Add(t);return Surface(p);}
 W.UIElement MonitoringIcon(bool active){var icon=Glyph(active?"eye":"eye_off",24);Resource(icon,System.Windows.Documents.TextElement.ForegroundProperty,active?"DashboardAccent":"ThemeF2F2F2");return icon;}
 C.Border DangerCard(string title,string detail){
  var row=new C.DockPanel();var icon=new C.TextBlock{Text="⚠",FontSize=24,Margin=new W.Thickness(0,0,14,0)};Resource(icon,C.TextBlock.ForegroundProperty,"DangerAccent");C.DockPanel.SetDock(icon,C.Dock.Left);row.Children.Add(icon);
  var text=new C.StackPanel();var heading=Text(title);heading.FontWeight=W.FontWeights.SemiBold;Resource(heading,C.TextBlock.ForegroundProperty,"DangerAccent");text.Children.Add(heading);var body=Text(detail);body.Margin=new W.Thickness(0,8,0,0);Resource(body,C.TextBlock.ForegroundProperty,"DangerText");text.Children.Add(body);row.Children.Add(text);
  var card=Surface(row);card.BorderThickness=new W.Thickness(1);Resource(card,C.Border.BackgroundProperty,"DangerBackground");Resource(card,C.Border.BorderBrushProperty,"DangerBorder");return card;
 }
 void SyncTiming(){mainIdle.Text=value.IdleMinutes.ToString();mainWarning.Text=value.WarningSeconds.ToString();mainPostpone.Text=value.PostponeMinutes.ToString();Refresh();}
 bool TimingChanged()=>!int.TryParse(mainIdle.Text,out var idle)||idle!=value.IdleMinutes||!int.TryParse(mainWarning.Text,out var seconds)||seconds!=value.WarningSeconds||!int.TryParse(mainPostpone.Text,out var postpone)||postpone!=value.PostponeMinutes;
 void SaveTiming(){var next=value with{IdleMinutes=int.Parse(mainIdle.Text),WarningSeconds=int.Parse(mainWarning.Text),PostponeMinutes=int.Parse(mainPostpone.Text)};next.Validate();value=store.Save(next);runtime?.Configure(value);Refresh();}
 void ToggleMonitoring(){if(ReviewEntry.Safe)return;if(runtime==null||runtime.Paused){SaveTiming();StartMonitoring();}else runtime.Pause();Refresh();UpdateTray();}
 void Refresh(){if(monitoringToggle!=null){bool active=runtime!=null&&!runtime.Paused;monitoringToggle.Content=MonitoringIcon(active);string action=runtime==null?"Start monitoring":runtime.Paused?"Resume monitoring":"Pause monitoring";System.Windows.Automation.AutomationProperties.SetName(monitoringToggle,action);monitoringToggle.IsEnabled=!ReviewEntry.Safe;monitoringToggle.ToolTip=(active?"Monitoring on. ":"Monitoring off. ")+action;}status.Text=ReviewEntry.Safe?"Safe review · monitoring off":runtime?.StateText??"Monitoring paused";timings.Text=$"After {value.IdleMinutes} minutes inactive, warn for {value.WarningSeconds} seconds.\nPostpone keeps ChatGPT open for {value.PostponeMinutes} minutes.";}
 string PrefsPath=>Path.Combine(directory,"window-preferences.json");
 internal void SavePreferences(WindowPreferences p){File.WriteAllText(PrefsPath+".tmp",JsonSerializer.Serialize(p));File.Move(PrefsPath+".tmp",PrefsPath,true);preferences=p;ApplyLock();}
 void SaveBounds(){var b=Window.WindowState==W.WindowState.Normal?new W.Rect(Window.Left,Window.Top,Window.Width,Window.Height):Window.RestoreBounds;SavePreferences(preferences with{Width=b.Width,Height=b.Height});}
 void ApplyLock(){Window.ResizeMode=preferences.LockWindowSizes?W.ResizeMode.CanMinimize:W.ResizeMode.CanResize;}
 W.Window Dialog(string title,double width,double height,W.UIElement content){var d=new W.Window{Title=title,Owner=Window,Width=Math.Min(width,W.SystemParameters.WorkArea.Width),Height=Math.Min(height,W.SystemParameters.WorkArea.Height),MinWidth=Math.Min(width,520),MinHeight=Math.Min(height,380),WindowStartupLocation=W.WindowStartupLocation.CenterOwner,ShowInTaskbar=false,UseLayoutRounding=true,FontFamily=Window.FontFamily,FontSize=DgxUi.BodySize,Icon=Window.Icon,Content=new C.Border{Child=content}};Resource((C.Border)d.Content,C.Border.BackgroundProperty,"Background");Resource(d,W.Window.BackgroundProperty,"Background");Resource(d,W.Window.ForegroundProperty,"ThemeF2F2F2");DgxUi.ApplyWindow(d);M.TextOptions.SetTextFormattingMode(d,M.TextFormattingMode.Display);M.RenderOptions.SetBitmapScalingMode(d,M.BitmapScalingMode.HighQuality);d.SnapsToDevicePixels=true;d.SourceInitialized+=(_,_)=>{Caption(d);_ = new WindowBrand(d);};return d;}
 internal void Reopen(){Window.Show();Window.WindowState=W.WindowState.Normal;(Window.OwnedWindows.Cast<W.Window>().LastOrDefault(x=>x.IsVisible)??Window).Activate();}
 internal void Exit(){exiting=true;Window.Close();}
 internal void Hide(){if(Window.OwnedWindows.Cast<W.Window>().Any(x=>x.IsVisible))return;EnsureTray();SaveBounds();Window.Hide();}
}