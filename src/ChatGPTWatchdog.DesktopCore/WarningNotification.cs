namespace ChatGPTWatchdog;

internal sealed record NotificationOptions(bool SoundEnabled = true, string? SoundPath = null,
    bool BringToForeground = true, bool AlwaysOnTop = true);

internal interface IWarningAttention
{
    bool PlaySound(string? path);
    bool TryForeground();
    void FlashTaskbar();
}

// Presentation failure never changes the countdown or shutdown authorization.
internal static class WarningNotification
{
    internal static void Request(NotificationOptions options, bool locked, IWarningAttention attention)
    {
        if (locked) return;
        if (options.SoundEnabled) { try { attention.PlaySound(options.SoundPath); } catch { } }
        bool activated = false;
        if (options.BringToForeground) { try { activated = attention.TryForeground(); } catch { } }
        if (!activated) { try { attention.FlashTaskbar(); } catch { } }
    }
}
