namespace EndlessNight.Services.Feedback;

public sealed class FeedbackSettings
{
    /// <summary>Master toggle for all micro-interactions (sound + visuals).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Sound toggle (beeps).</summary>
    public bool SoundEnabled { get; set; } = true;

    /// <summary>Visual micro-animations toggle.</summary>
    public bool VisualEnabled { get; set; } = true;

    /// <summary>If true, uses fewer/shorter animations (accessibility).</summary>
    public bool ReducedMotion { get; set; } = false;

    /// <summary>Global multiplier applied to durations (higher = slower).</summary>
    public float DurationScale { get; set; } = 1.0f;
}

