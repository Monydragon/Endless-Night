namespace EndlessNight.Services.Feedback;

/// <summary>
/// Cross-platform safe sound device: does nothing.
/// Use this when you want to avoid any OS-specific APIs.
/// </summary>
public sealed class NullSoundDevice : IFeedbackSoundDevice
{
    public void Beep(int frequencyHz, int durationMs)
    {
        // Intentionally no-op.
    }
}

