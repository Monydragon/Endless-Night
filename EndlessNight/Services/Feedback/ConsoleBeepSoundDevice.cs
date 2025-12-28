using System;

namespace EndlessNight.Services.Feedback;

public sealed class ConsoleBeepSoundDevice : IFeedbackSoundDevice
{
    public void Beep(int frequencyHz, int durationMs)
    {
        try
        {
            // Console.Beep can throw on some platforms/hosts (and in some IDE consoles).
            Console.Beep(Math.Clamp(frequencyHz, 37, 32767), Math.Max(1, durationMs));
        }
        catch
        {
            // Best-effort only.
        }
    }
}

