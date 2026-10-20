using System.IO.Pipes;
using System.Security.Principal;

namespace ChatGPTWatchdog;

internal sealed class SingleInstanceChannel : IDisposable
{
    readonly CancellationTokenSource stop = new();
    readonly Task listener;
    static string PipeName
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return $"DGX.ChatGPTWatchdog.{identity.User?.Value}.{System.Diagnostics.Process.GetCurrentProcess().SessionId}";
        }
    }
    internal SingleInstanceChannel(Action showSettings)
    {
        listener = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(stop.Token);
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    request.CancelAfter(TimeSpan.FromSeconds(3));
                    byte[] command = new byte[1];
                    if (await pipe.ReadAsync(command, request.Token) == 1 && command[0] == 1) showSettings();
                }
                catch (OperationCanceledException) { if (stop.IsCancellationRequested) break; }
                catch (IOException) { if (stop.IsCancellationRequested) break; await Task.Delay(250); }
                catch (UnauthorizedAccessException) { break; }
            }
        });
    }
    internal static bool TryShowSettings()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(2000); pipe.WriteByte(1); pipe.Flush(); return true;
        }
        catch (Exception error) when (error is IOException or TimeoutException or UnauthorizedAccessException) { return false; }
    }
    public void Dispose()
    {
        stop.Cancel();
        // Dispose the token source only after the asynchronous listener has observed cancellation.
        _ = listener.ContinueWith(_ => stop.Dispose(), TaskScheduler.Default);
    }
}
