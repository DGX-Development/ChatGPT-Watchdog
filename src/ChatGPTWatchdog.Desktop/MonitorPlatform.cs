namespace ChatGPTWatchdog;
internal interface IMonitorPlatform
{
 InputSample? Input();
 TargetSnapshot Read();
 IShutdownTarget Capture(TargetSnapshot snapshot,Func<bool> allowed);
 bool Locked();
}
internal sealed class NativeMonitorPlatform : IMonitorPlatform
{
 internal NativeMonitorPlatform(){if(ReviewEntry.Safe)throw new InvalidOperationException("Native target access is disabled in review builds.");}
 public InputSample? Input()=>WindowsTarget.Input();
 public TargetSnapshot Read()=>WindowsTarget.Read();
 public IShutdownTarget Capture(TargetSnapshot snapshot,Func<bool> allowed)=>WindowsTarget.Capture(snapshot,allowed);
 public bool Locked()=>WindowsTarget.Locked();
}
internal sealed class FakeMonitorPlatform : IMonitorPlatform,IShutdownTarget
{
 internal long Now;
 internal uint Token=1;
 internal bool Known=true,HasInput=true,Exited,Fail,ActivityDuringCapture;
 internal int ForceCount,Reads;
 public string Identity=>"disposable-fake-target";
 public bool Locked()=>false;
 public InputSample? Input()=>HasInput?new(Token,Now):null;
 public TargetSnapshot Read(){Reads++;return new(Known,Exited?null:"fake",Array.Empty<TargetWindow>(),Exited?Array.Empty<int>():new[]{123});}
 public IShutdownTarget Capture(TargetSnapshot snapshot,Func<bool> allowed){if(ActivityDuringCapture)Token++;if(!allowed())throw new IOException("Activity changed");return this;}
 public ProcessObservation Observe()=>Exited?ProcessObservation.Exited:ProcessObservation.Running;
 public ShutdownResult Force(){ForceCount++;if(!Fail)Exited=true;return new(!Fail,Fail?"Simulated refusal":"Simulated exit");}
 public void Dispose(){}
}
