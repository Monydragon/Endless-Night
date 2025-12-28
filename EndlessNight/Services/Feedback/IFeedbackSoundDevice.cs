namespace EndlessNight.Services.Feedback;

public interface IFeedbackSoundDevice
{
    void Beep(int frequencyHz, int durationMs);
}

