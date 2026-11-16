using W=System.Windows;
using C=System.Windows.Controls;
using Microsoft.Win32;
namespace ChatGPTWatchdog;
internal sealed class MonitoringController : IDisposable
{
 readonly WatchEngine engine; readonly IMonitorPlatform platform; readonly Func<long> clock; readonly bool native;
 readonly System.Windows.Threading.DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)};
 readonly ShutdownLog log;
 readonly Action<int?> warning;
 readonly Action<string> changed; readonly Func<bool> warningInteraction; uint? priorInput; long interactionUntil;
 ShutdownAttempt? attempt;
 long last=Environment.TickCount64;
 bool suspended,locked,disposed;
 internal bool Paused{get;private set;}=true;
 internal string StateText{get;private set;}="Monitoring paused";
 internal MonitoringController(Settings settings,string directory,Action<int?> warning,Action<string> changed,IMonitorPlatform? testPlatform=null,Func<long>? testClock=null,Func<bool>? warningInteraction=null)
 {
  if(ReviewEntry.Safe&&testPlatform==null)throw new InvalidOperationException("Real monitoring is unavailable in the safe review executable.");
  native=testPlatform==null;platform=testPlatform??new NativeMonitorPlatform();clock=testClock??(()=>Environment.TickCount64);last=clock();engine=new(settings,clock());engine.Pause(true,clock());log=new(Path.Combine(directory,"logs"));this.warning=warning;this.changed=changed;this.warningInteraction=warningInteraction??(()=>false);timer.Tick+=(_,_)=>Tick();if(native){SystemEvents.PowerModeChanged+=Power;SystemEvents.SessionSwitch+=Session;locked=platform.Locked();timer.Start();}
 }
 void Dispatch(Action a)=>W.Application.Current.Dispatcher.BeginInvoke(a);
 void Power(object? s,PowerModeChangedEventArgs e)=>Dispatch(()=>{if(e.Mode==PowerModes.Suspend){suspended=true;Stop("Computer suspended");warning(null);}else if(e.Mode==PowerModes.Resume){suspended=false;engine.Resume(clock());}});
 void Session(object? s,SessionSwitchEventArgs e)=>Dispatch(()=>{if(e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect){locked=true;warning(null);}else if(e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect){locked=false;Cancel();}});
 internal void Start(){Paused=false;engine.Pause(false,clock());Status();}
 internal void Pause(){Paused=true;Stop("Monitoring paused");engine.Pause(true,clock());warning(null);Status();}
 internal void Cancel(){Stop("Warning canceled");engine.Cancel(clock());warning(null);Status();}
 internal void Postpone(){Stop("Warning postponed");engine.Postpone(clock());warning(null);Status();}
 internal void Configure(Settings s){Stop("Settings changed");engine.Configure(s,clock());warning(null);Status();}
 void Stop(string why){attempt?.Cancel(why);attempt?.Dispose();attempt=null;}
 void Status(){StateText=engine.State switch{WatchState.Paused=>"Monitoring paused",WatchState.Warning=>"Warning countdown active",WatchState.Closing=>"Closing ChatGPT",WatchState.Failed=>"Closure not confirmed. Check logs.",WatchState.Unavailable=>"Monitoring unavailable. No close request.",WatchState.Postponed=>"Monitoring postponed",_=>engine.Target==null?"Monitoring active · waiting for ChatGPT":"Monitoring active"};changed(StateText);}
 internal void Tick()
 {
  if(disposed||suspended||Paused)return;
  try{
   long now=clock();if(now-last>5000)engine.Resume(now);last=now;
   if(engine.State!=WatchState.Closing&&attempt!=null)Stop("Monitoring state changed");
   if(engine.State==WatchState.Closing){if(attempt==null){engine.CloseFinished(false);Status();return;}attempt.Tick();if(attempt.Complete){bool success=attempt.Success;if(success){var remaining=platform.Read();success=remaining.Known&&remaining.ProcessIds.Count==0;}log.Write(success?"CloseConfirmed":"CloseNotConfirmed",attempt.Detail);engine.CloseFinished(success);attempt.Dispose();attempt=null;}Status();return;}
   var input=platform.Input();var target=platform.Read();if(engine.State==WatchState.Warning&&input.HasValue&&priorInput.HasValue&&input.Value.Token!=priorInput.Value&&warningInteraction())interactionUntil=now+600;priorInput=input?.Token;var state=engine.Tick(now,input?.Token??0,input?.IdleMilliseconds??-1,target.Key,target.Known&&input.HasValue,now<interactionUntil);
   if(state==WatchState.Warning&&!locked)warning((int)Math.Ceiling((engine.WarningEnds-now)/1000.0));else warning(null);
   if(state==WatchState.Closing){var fresh=platform.Input();if(!fresh.HasValue||!input.HasValue||fresh.Value.Token!=input.Value.Token){engine.Cancel(now);Status();return;}IShutdownTarget? captured=null;try{captured=platform.Capture(target,()=>!suspended&&!Paused&&engine.State==WatchState.Closing&&platform.Input()?.Token==fresh.Value.Token);if(platform.Input()?.Token!=fresh.Value.Token){captured.Dispose();engine.Cancel(now);return;}attempt=new ShutdownAttempt(captured,clock,()=>!suspended&&!Paused&&engine.State==WatchState.Closing&&platform.Input()?.Token==fresh.Value.Token,log.Write);}catch(Exception e){captured?.Dispose();engine.CloseFinished(false);log.Write("TargetCaptureFailed",e.ToString());}}
   Status();
  }catch(Exception e){Stop("Monitoring error");engine.CloseFinished(false);warning(null);log.Write("MonitoringError",e.ToString());Status();}
 }
 public void Dispose(){disposed=true;timer.Stop();Stop("Watchdog exited");if(native){SystemEvents.PowerModeChanged-=Power;SystemEvents.SessionSwitch-=Session;}}
}
internal sealed partial class ReviewWindow
{
 MonitoringController? runtime;
 W.Window? liveWarning;
 C.TextBlock? liveCountdown;
 bool dismissing;
 internal void StartMonitoring(){if(ReviewEntry.Safe)throw new InvalidOperationException("Monitoring is disabled in this review.");runtime??=new(value,directory,ShowWarning,s=>{Refresh();UpdateTray();},warningInteraction:()=>liveWarning?.IsMouseOver==true||liveWarning?.IsKeyboardFocusWithin==true);runtime.Start();}
 void ShowWarning(int? seconds)
 {
  if(seconds==null){if(liveWarning!=null){dismissing=true;liveWarning.Close();liveWarning=null;dismissing=false;}return;}
  if(liveWarning==null){var body=new C.StackPanel{Margin=new W.Thickness(28)};Heading(body,"ChatGPT will close soon");body.Children.Add(WorkText("Unsaved work may be lost. Cancel or postpone to keep ChatGPT open."));liveCountdown=Text("",22);body.Children.Add(liveCountdown);var actions=new C.WrapPanel{HorizontalAlignment=W.HorizontalAlignment.Right,Margin=new W.Thickness(0,24,0,0)};body.Children.Add(actions);actions.Children.Add(Standard(Button("Postpone",()=>runtime?.Postpone())));actions.Children.Add(Standard(Button("Cancel",()=>runtime?.Cancel())));liveWarning=Dialog("ChatGPT closure warning",520,300,body);liveWarning.Owner=null;liveWarning.ShowInTaskbar=true;liveWarning.Topmost=value.Notifications.AlwaysOnTop;liveWarning.Closing+=(_,_)=>{if(!dismissing){var old=liveWarning;liveWarning=null;runtime?.Cancel();}};liveWarning.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;runtime?.Cancel();}};liveWarning.Show();WarningNotification.Request(value.Notifications,WindowsTarget.Locked(),new WpfWarningAttention(liveWarning));}
  liveCountdown!.Text=$"Closing in {Math.Max(0,seconds.Value)} seconds";
 }
}




