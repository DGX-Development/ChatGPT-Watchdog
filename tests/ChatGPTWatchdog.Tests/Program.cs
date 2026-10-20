using ChatGPTWatchdog;

int passed = 0;
void Test(string name, Action action)
{
    try { action(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception error) { Console.Error.WriteLine("FAIL " + name + ": " + error.Message); Environment.Exit(1); }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Throws<T>(Action action) where T : Exception
{ try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
const long Idle = 45 * 60_000L, Warning = 120_000, Postpone = 60 * 60_000L;
WatchEngine Ready()
{
    var engine = new WatchEngine(new(), 0);
    engine.Tick(0, 100, 0, "official-main");
    return engine;
}
WatchEngine Warn()
{
    var engine = Ready();
    Equal(WatchState.Warning, engine.Tick(Idle, 100, Idle, "official-main"));
    return engine;
}
Test("approved defaults and validation", () =>
{
    var value = new Settings(); Equal(45, value.IdleMinutes); Equal(120, value.WarningSeconds); Equal(60, value.PostponeMinutes);
    Throws<InvalidDataException>(() => new Settings(IdleMinutes: 0).Validate());
    Throws<InvalidDataException>(() => new Settings(WarningSeconds: 0).Validate());
    Throws<InvalidDataException>(() => new Settings(Appearance: "Unknown").Validate());
});
Test("threshold does not fire early", () => Equal(WatchState.Monitoring, Ready().Tick(Idle - 1, 100, Idle - 1, "official-main")));
Test("45 minute threshold begins full 120 second warning", () => Equal(Idle + Warning, Warn().WarningEnds));
Test("warning never closes early", () => Equal(WatchState.Warning, Warn().Tick(Idle + Warning - 1, 100, Idle + Warning - 1, "official-main")));
Test("ignored warning permits one close attempt", () =>
{
    var engine = Warn(); Equal(WatchState.Closing, engine.Tick(Idle + Warning, 100, Idle + Warning, "official-main"));
    Equal(WatchState.Closing, engine.Tick(Idle + Warning + 1, 100, Idle + Warning + 1, "official-main"));
});
Test("activity anywhere cancels warning", () => Equal(WatchState.Monitoring, Warn().Tick(Idle + 1000, 101, 0, "official-main")));
Test("cancel requires a fresh complete inactivity period", () =>
{
    var engine = Warn(); engine.Cancel(Idle);
    Equal(WatchState.Monitoring, engine.Tick(2 * Idle - 1, 100, 2 * Idle, "official-main"));
    Equal(WatchState.Warning, engine.Tick(2 * Idle, 100, 2 * Idle, "official-main"));
});
Test("postpone suppresses warnings for 60 minutes", () =>
{
    var engine = Warn(); engine.Postpone(Idle);
    Equal(WatchState.Postponed, engine.Tick(Idle + Postpone - 1, 100, Idle + Postpone, "official-main"));
    Equal(WatchState.Warning, engine.Tick(Idle + Postpone, 100, Idle + Postpone, "official-main"));
});
Test("recent activity still protects after postponement expires", () =>
{
    var engine = Warn(); engine.Postpone(Idle);
    engine.Tick(Idle + Postpone - 1, 101, 0, "official-main");
    Equal(WatchState.Monitoring, engine.Tick(Idle + Postpone, 101, 1, "official-main"));
});
Test("pause cancels and resume rearms without immediate closure", () =>
{
    var engine = Warn(); engine.Pause(true, Idle);
    Equal(WatchState.Paused, engine.Tick(10 * Idle, 100, 10 * Idle, "official-main"));
    engine.Pause(false, 10 * Idle);
    Equal(WatchState.Monitoring, engine.Tick(10 * Idle, 100, 10 * Idle, "official-main"));
});
Test("cancel and settings changes preserve paused state immediately", () =>
{
    var engine = Warn(); engine.Pause(true, Idle); engine.Cancel(Idle + 1);
    Equal(WatchState.Paused, engine.State);
    engine.Configure(new Settings(IdleMinutes: 50), Idle + 2); Equal(WatchState.Paused, engine.State);
    Equal(WatchState.Paused, engine.Tick(10 * Idle, 100, 10 * Idle, "official-main"));
    engine.Pause(false, 10 * Idle); Equal(WatchState.Monitoring, engine.State);
    Equal(WatchState.Monitoring, engine.Tick(10 * Idle, 100, 10 * Idle, "official-main"));
});
Test("missing target dismisses warning", () => Equal(WatchState.Monitoring, Warn().Tick(Idle + 1, 100, Idle + 1, null)));
Test("reopened or replaced target gets full inactivity period", () =>
{
    var engine = Warn(); Equal(WatchState.Monitoring, engine.Tick(Idle + Warning, 100, Idle + Warning, "new-instance"));
});
Test("unknown identity cannot permit closure", () => Equal(WatchState.Unavailable, Warn().Tick(Idle + Warning, 100, Idle + Warning, "official-main", false)));
Test("failed input reading cannot permit closure", () => Equal(WatchState.Unavailable, Warn().Tick(Idle + Warning, 100, -1, "official-main")));
Test("recovery from unknown requires full idle period", () =>
{
    var engine = Warn(); engine.Tick(Idle + 1, 100, Idle, "official-main", false);
    Equal(WatchState.Monitoring, engine.Tick(Idle + Warning, 100, Idle + Warning, "official-main"));
});
Test("failed closure is not retried while unattended", () =>
{
    var engine = Warn(); engine.Tick(Idle + Warning, 100, Idle + Warning, "official-main"); engine.CloseFinished(false);
    Equal(WatchState.Failed, engine.Tick(10 * Idle, 100, 10 * Idle, "official-main"));
    Equal(WatchState.Monitoring, engine.Tick(10 * Idle + 1, 101, 0, "official-main"));
});
Test("sleep cannot expire an existing warning", () =>
{
    var engine = Warn(); engine.Resume(10 * Idle);
    Equal(10 * Idle + Warning, engine.WarningEnds);
    Equal(WatchState.Warning, engine.Tick(10 * Idle, 100, 10 * Idle, "official-main"));
});
Test("restart does not inherit old idle time", () =>
{
    var engine = new WatchEngine(new(), 10 * Idle);
    Equal(WatchState.Monitoring, engine.Tick(10 * Idle, 100, 10 * Idle, "official-main"));
});
Test("warning button interaction has time to finish", () =>
{
    var engine = Warn(); Equal(WatchState.Warning, engine.Tick(Idle + 1, 101, 0, "official-main", warningInteraction: true));
    engine.Postpone(Idle + 2);
    Equal(WatchState.Postponed, engine.Tick(Idle + 3, 101, 2, "official-main"));
});
Test("input temporarily deferred is not forgotten", () =>
{
    var engine = Warn(); engine.Tick(Idle + 1, 101, 0, "official-main", warningInteraction: true);
    Equal(WatchState.Monitoring, engine.Tick(Idle + 1000, 101, 999, "official-main"));
});
Test("settings changes restart timing", () =>
{
    var engine = Warn(); engine.Configure(new(IdleMinutes: 15), Idle);
    Equal(WatchState.Monitoring, engine.Tick(Idle, 100, Idle, "official-main"));
});
Test("settings save/load, outside edits and malformed files", () =>
{
    string directory = Path.Combine(Path.GetTempPath(), "ChatGPT-Watchdog-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        var store = new SettingsStore(directory); Equal(new Settings(), store.Load());
        store.Save(new(Appearance: "Dark"));
        Equal("Dark", new SettingsStore(directory).Load().Appearance);
        string path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, "external edit");
        Throws<IOException>(() => store.Save(new())); Equal("external edit", File.ReadAllText(path));
        Throws<System.Text.Json.JsonException>(() => new SettingsStore(directory).Load());
    }
    finally { Directory.Delete(directory, true); }
});
Test("resume cannot issue a second close request", () =>
{
    var engine = Warn(); engine.Tick(Idle + Warning, 100, Idle + Warning, "official-main");
    engine.Resume(10 * Idle);
    Equal(WatchState.Failed, engine.Tick(10 * Idle, 100, 10 * Idle, "official-main"));
});
Test("settings failure does not change approved engine settings", () =>
{
    var engine = Warn(); Throws<InvalidDataException>(() => engine.Configure(new(IdleMinutes: 0), Idle));
    Equal(45, engine.Settings.IdleMinutes);
});
Test("postpone survives target disappearance", () =>
{
    var engine = Warn(); engine.Postpone(Idle);
    engine.Tick(Idle + 1, 100, Idle + 1, null);
    Equal(WatchState.Postponed, engine.Tick(Idle + 2, 100, Idle + 2, "reopened"));
});
Test("background helper churn cannot retry a failed closure", () =>
{
    var engine = Warn(); engine.Tick(Idle + Warning, 100, Idle + Warning, "official-main");
    engine.CloseFinished(false);
    Equal(WatchState.Failed, engine.Tick(10 * Idle, 100, 10 * Idle, "background-helper-set"));
    engine.Tick(10 * Idle + 1, 100, 10 * Idle, null);
    Equal(WatchState.Monitoring, engine.Tick(10 * Idle + 2, 100, 10 * Idle, "reopened"));
});


Test("legacy settings retain timings and discard obsolete force options", () => {
    var value=System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"IdleMinutes\":47,\"WarningSeconds\":100,\"PostponeMinutes\":70,\"Appearance\":\"Dark\",\"ForceTermination\":false,\"GraceSeconds\":60}")!;
    value.Validate(); Equal(47,value.IdleMinutes); Equal(100,value.WarningSeconds); Equal(70,value.PostponeMinutes); Equal("Dark",value.Appearance);
    string saved=System.Text.Json.JsonSerializer.Serialize(value);Equal(false,saved.Contains("ForceTermination"));Equal(false,saved.Contains("GraceSeconds"));
});
Test("countdown completion requests immediate direct force close", () => {
    long now=0;var target=new FakeShutdown();using var attempt=new ShutdownAttempt(target,()=>now,()=>true,(_,_)=>true);
    Equal(1,target.Forces); Equal(false,attempt.Complete); Equal(false,attempt.Success);
    target.State=ProcessObservation.Exited;attempt.Tick();Equal(true,attempt.Success);attempt.Tick();Equal(1,target.Forces);
});
Test("force request alone is not reported as exit success", () => {
    long now=0;var target=new FakeShutdown();using var attempt=new ShutdownAttempt(target,()=>now,()=>true,(_,_)=>true);
    now=4999;attempt.Tick();Equal(false,attempt.Complete);now=5000;attempt.Tick();Equal(true,attempt.Complete);Equal(false,attempt.Success);Equal(1,target.Forces);
});
Test("already exited process is left alone", () => {
    var target=new FakeShutdown{State=ProcessObservation.Exited};using var attempt=new ShutdownAttempt(target,()=>0,()=>true,(_,_)=>true);Equal(true,attempt.Success);Equal(0,target.Forces);
});
Test("unknown process state prevents termination", () => {
    var target=new FakeShutdown{State=ProcessObservation.Unknown};using var attempt=new ShutdownAttempt(target,()=>0,()=>true,(_,_)=>true);Equal(false,attempt.Success);Equal(true,attempt.Complete);Equal(0,target.Forces);
});
Test("new activity prevents direct force close", () => {
    var target=new FakeShutdown();using var attempt=new ShutdownAttempt(target,()=>0,()=>false,(_,_)=>true);Equal(0,target.Forces);Equal(true,attempt.Complete);
});
Test("activity after the log write is checked again", () => {
    var target=new FakeShutdown();bool allowed=true;using var attempt=new ShutdownAttempt(target,()=>0,()=>allowed,(name,_)=>{if(name=="ForcedTerminationAttempted")allowed=false;return true;});Equal(0,target.Forces);Equal(false,attempt.Success);
});
Test("log failure prevents force close", () => {
    var target=new FakeShutdown();using var attempt=new ShutdownAttempt(target,()=>0,()=>true,(name,_)=>name!="ForcedTerminationAttempted");Equal(0,target.Forces);Equal(false,attempt.Success);
});
Test("permission denied is logged without retry", () => {
    var target=new FakeShutdown{Result=new(false,"Windows error 5: Access denied")};var events=new List<string>();using var attempt=new ShutdownAttempt(target,()=>0,()=>true,(name,_)=>{events.Add(name);return true;});attempt.Tick();Equal(1,target.Forces);Equal(false,attempt.Success);Equal("ForcedTerminationFailed",events.Last());Equal(true,attempt.Detail.Contains("Access denied"));
});
Test("unknown exit observation is not success or permission to retry", () => {
    var target=new FakeShutdown();using var attempt=new ShutdownAttempt(target,()=>0,()=>true,(_,_)=>true);target.State=ProcessObservation.Unknown;attempt.Tick();attempt.Tick();Equal(false,attempt.Success);Equal(true,attempt.Complete);Equal(1,target.Forces);
});
Test("suspension cancels verification without repeating force", () => {
    long now=0;var target=new FakeShutdown();using var attempt=new ShutdownAttempt(target,()=>now,()=>true,(_,_)=>true);attempt.Cancel("Suspended");now=60000;attempt.Tick();Equal(1,target.Forces);Equal(false,attempt.Success);
});

void WithAudioFiles(Action<string, string> action)
{
    string temporaryRoot = Path.GetFullPath(Path.GetTempPath());
    string folder = Path.Combine(temporaryRoot, "ChatGPT-Watchdog-audio-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try
    {
        string source = Path.Combine(folder, "My chime.wav");
        using (var file = File.Create(source))
        using (var writer = new BinaryWriter(file))
        {
            writer.Write("RIFF"u8); writer.Write(196); writer.Write("WAVEfmt "u8); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000);
            writer.Write((short)2); writer.Write((short)16); writer.Write("data"u8); writer.Write(160); writer.Write(new byte[160]);
        }
        action(folder, source);
    }
    finally
    {
        if (!string.Equals(Path.GetDirectoryName(folder), temporaryRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(folder).StartsWith("ChatGPT-Watchdog-audio-tests-")) throw new Exception("Invalid test cleanup path");
        Directory.Delete(folder, true);
    }
}
Test("legacy JSON enables sound, foreground and on-top defaults", () => {
    var value=System.Text.Json.JsonSerializer.Deserialize<Settings>("{\"IdleMinutes\":1,\"WarningSeconds\":10,\"PostponeMinutes\":1,\"Appearance\":\"Dark\"}")!;
    Equal(new NotificationOptions(),value.Notifications);Equal(1,value.IdleMinutes);value.Validate();
    Throws<InvalidDataException>(()=>(value with {Notifications=null!}).Validate());
});
Test("sound off preserves visual attention",()=>{
    var fake=new FakeAttention(); WarningNotification.Request(new(SoundEnabled:false),false,fake);
    Equal(0,fake.Sounds);Equal(1,fake.Foreground);Equal(1,fake.Flashes);
});
Test("foreground success avoids flashing",()=>{
    var fake=new FakeAttention{Activate=true};WarningNotification.Request(new(),false,fake);
    Equal(1,fake.Sounds);Equal(1,fake.Foreground);Equal(0,fake.Flashes);
});
Test("foreground disabled does not request focus and still flashes",()=>{
    var fake=new FakeAttention();WarningNotification.Request(new(BringToForeground:false),false,fake);
    Equal(1,fake.Sounds);Equal(0,fake.Foreground);Equal(1,fake.Flashes);
});
Test("locked desktop requests no attention",()=>{
    var fake=new FakeAttention();WarningNotification.Request(new(),true,fake);
    Equal(0,fake.Sounds+fake.Foreground+fake.Flashes);
});
Test("audio and activation errors do not block fallback or escape",()=>{
    var fake=new FakeAttention{Fail=true};WarningNotification.Request(new(),false,fake);
    Equal(1,fake.Sounds);Equal(1,fake.Foreground);Equal(1,fake.Flashes);
});
Test("custom WAV plays once without Windows fallback",()=>WithAudioFiles((folder,source)=>{
    int custom=0,windows=0;Equal(true,WarningSound.Play(source,_=>{custom++;return true;},()=>windows++));Equal(1,custom);Equal(0,windows);
}));
Test("missing and invalid custom sounds fall back once",()=>WithAudioFiles((folder,source)=>{
    int custom=0,windows=0;File.Delete(source);Equal(false,WarningSound.Play(source,_=>{custom++;return true;},()=>windows++));
    File.WriteAllText(source,"OggS not a WAV");Equal(true,WarningSound.Problem(source)!.Contains("Ogg"));
    Equal(false,WarningSound.Play(source,_=>{custom++;return true;},()=>windows++));Equal(0,custom);Equal(2,windows);
}));
Test("rejected or throwing custom playback falls back once",()=>WithAudioFiles((folder,source)=>{
    int windows=0;Equal(false,WarningSound.Play(source,_=>false,()=>windows++));
    Equal(false,WarningSound.Play(source,_=>throw new IOException("simulated"),()=>windows++));Equal(2,windows);
    Equal(false,WarningSound.Play(source,_=>false,()=>throw new IOException("no audio device")));
}));
Test("Windows sound selection never invokes custom playback",()=>{
    int windows=0;Equal(false,WarningSound.Play(null,_=>throw new Exception("must not run"),()=>windows++));Equal(1,windows);
});
Test("saving copies custom sound and retains timing and source",()=>WithAudioFiles((folder,source)=>{
    string data=Path.Combine(folder,"profile");byte[] original=File.ReadAllBytes(source);
    var store=new SettingsStore(data);store.Load();
    var saved=store.Save(new Settings(1,10,1,"Dark"){Notifications=new(SoundPath:source,BringToForeground:false,AlwaysOnTop:false)});
    Equal(1,saved.IdleMinutes);Equal(false,saved.Notifications.BringToForeground);Equal(false,saved.Notifications.AlwaysOnTop);
    Equal(Path.Combine(data,"sounds"),Path.GetDirectoryName(saved.Notifications.SoundPath));
    Equal(true,original.AsSpan().SequenceEqual(File.ReadAllBytes(source)));
    Equal(true,original.AsSpan().SequenceEqual(File.ReadAllBytes(saved.Notifications.SoundPath!)));
    Equal(saved,new SettingsStore(data).Load());store.Save(saved);Equal(1,Directory.GetFiles(Path.Combine(data,"sounds")).Length);
}));
Test("reselecting the original reuses its saved sound",()=>WithAudioFiles((folder,source)=>{
    var store=new SettingsStore(Path.Combine(folder,"profile"));store.Load();
    var draft=new Settings{Notifications=new(SoundPath:source)};var first=store.Save(draft);var second=store.Save(draft);
    Equal(first.Notifications.SoundPath,second.Notifications.SoundPath);
}));
Test("failed settings replacement rolls back only the new sound",()=>WithAudioFiles((folder,source)=>{
    string data=Path.Combine(folder,"profile");var store=new SettingsStore(data);store.Load();Directory.CreateDirectory(Path.Combine(data,"settings.json"));
    Throws<UnauthorizedAccessException>(()=>store.Save(new Settings{Notifications=new(SoundPath:source)}));
    Equal(0,Directory.GetFiles(Path.Combine(data,"sounds")).Length);Equal(true,File.Exists(source));
}));
Test("external settings edit prevents sound import",()=>WithAudioFiles((folder,source)=>{
    string data=Path.Combine(folder,"profile");var store=new SettingsStore(data);store.Load();Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"settings.json"),"owner edit");
    Throws<IOException>(()=>store.Save(new Settings{Notifications=new(SoundPath:source)}));Equal(false,Directory.Exists(Path.Combine(data,"sounds")));
    Equal("owner edit",File.ReadAllText(Path.Combine(data,"settings.json")));
}));
Test("invalid audio cannot be saved enabled and original remains",()=>WithAudioFiles((folder,source)=>{
    File.WriteAllText(source,"not WAV");var store=new SettingsStore(Path.Combine(folder,"profile"));store.Load();
    Throws<InvalidDataException>(()=>store.Save(new Settings{Notifications=new(SoundPath:source)}));Equal("not WAV",File.ReadAllText(source));
    var saved=store.Save(new Settings{Notifications=new(SoundEnabled:false,SoundPath:source)});Equal(false,saved.Notifications.SoundEnabled);
}));
Test("missing saved audio does not prevent other settings changes",()=>WithAudioFiles((folder,source)=>{
    var store=new SettingsStore(Path.Combine(folder,"profile"));store.Load();var saved=store.Save(new Settings{Notifications=new(SoundPath:source)});
    File.Delete(saved.Notifications.SoundPath!);var updated=store.Save(saved with{Appearance="Dark"});Equal("Dark",updated.Appearance);
    Equal(saved.Notifications.SoundPath,updated.Notifications.SoundPath);
}));
Console.WriteLine($"{passed} focused tests passed. No Windows target APIs were called.");
sealed class FakeShutdown : IShutdownTarget
{
 public string Identity=>"disposable-fake"; public int Forces;
 public ProcessObservation State=ProcessObservation.Running; public ShutdownResult Result=new(true,"requested");
 public ProcessObservation Observe()=>State;
 public ShutdownResult Force(){Forces++;return Result;}
 public void Dispose(){}
}

sealed class FakeAttention : IWarningAttention
{
    public int Sounds, Foreground, Flashes;
    public bool Activate, Fail;
    public bool PlaySound(string? path) { Sounds++; if(Fail)throw new IOException("audio");return true; }
    public bool TryForeground() { Foreground++;if(Fail)throw new IOException("activation");return Activate; }
    public void FlashTaskbar() { Flashes++;if(Fail)throw new IOException("flash"); }
}
