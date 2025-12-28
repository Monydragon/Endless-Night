using System;
using EndlessNight.Domain;
using Spectre.Console;

namespace EndlessNight.Services.Feedback;

/// <summary>
/// Centralized micro-interaction layer (audio + tiny visual cues).
/// Designed to be safe in IDE consoles and easy to disable.
/// </summary>
public sealed class FeedbackService
{
    private readonly FeedbackSettings _settings;
    private readonly IFeedbackSoundDevice _sound;

    public FeedbackService(FeedbackSettings settings, IFeedbackSoundDevice soundDevice)
    {
        _settings = settings ?? new FeedbackSettings();
        _sound = soundDevice;
    }

    public void MenuMove(RunState? run)
    {
        if (!IsEnabled) return;
        PlayBeep(run, kind: FeedbackSoundKind.MenuMove);
    }

    public void MenuSelect(RunState? run)
    {
        if (!IsEnabled) return;
        PlayBeep(run, kind: FeedbackSoundKind.MenuSelect);
        PulseStatusLine(run, accentColor: "cyan");
    }

    public void MenuBack(RunState? run)
    {
        if (!IsEnabled) return;
        PlayBeep(run, kind: FeedbackSoundKind.MenuBack);
    }

    public void StatusDelta(RunState run, int deltaHealth, int deltaSanity)
    {
        if (!IsEnabled) return;

        // Sound: prioritize critical decreases.
        if (deltaHealth < 0)
            PlayBeep(run, FeedbackSoundKind.Damage);
        else if (deltaHealth > 0)
            PlayBeep(run, FeedbackSoundKind.Heal);

        if (deltaSanity < 0)
            PlayBeep(run, FeedbackSoundKind.SanityDown);
        else if (deltaSanity > 0)
            PlayBeep(run, FeedbackSoundKind.SanityUp);

        // Visual: brief inline pulse message (doesn't require clearing HUD).
        if (_settings.VisualEnabled && !_settings.ReducedMotion)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (deltaHealth != 0)
                parts.Add(deltaHealth > 0 ? $"[green]+{deltaHealth} HP[/]" : $"[red]{deltaHealth} HP[/]");
            if (deltaSanity != 0)
                parts.Add(deltaSanity > 0 ? $"[cyan]+{deltaSanity} SAN[/]" : $"[magenta]{deltaSanity} SAN[/]");

            if (parts.Count > 0)
            {
                // Keep it subtle: one line, short lifecycle.
                AnsiConsole.MarkupLine($"[dim]({string.Join(" ", parts)})[/]");
            }
        }
    }

    private bool IsEnabled => _settings.Enabled && (_settings.SoundEnabled || _settings.VisualEnabled);

    private void PulseStatusLine(RunState? run, string accentColor)
    {
        if (!_settings.VisualEnabled) return;

        // Minimal "micro" animation: a short-lived status hint.
        // Avoid cursor repositioning (fragile across terminals).
        var ms = Scale(_settings.ReducedMotion ? 80 : 140);
        try
        {
            AnsiConsole.MarkupLine($"[dim {accentColor}]»[/] [dim]…[/]");
            Thread.Sleep(ms);
        }
        catch
        {
            // ignore
        }
    }

    private void PlayBeep(RunState? run, FeedbackSoundKind kind)
    {
        if (!_settings.SoundEnabled) return;

        var (f1, d1, f2, d2) = GetBeepPattern(run, kind);
        _sound.Beep(f1, Scale(d1));
        if (f2 > 0 && d2 > 0)
            _sound.Beep(f2, Scale(d2));
    }

    private int Scale(int ms)
    {
        var scaled = (int)Math.Round(ms * Math.Clamp(_settings.DurationScale, 0.25f, 4.0f));
        return Math.Clamp(scaled, 10, 1500);
    }

    internal static (int f1, int d1, int f2, int d2) GetBeepPattern(RunState? run, FeedbackSoundKind kind)
    {
        var (healthBand, sanityBand) = GetBands(run);

        // Base pitch shifts with sanity (higher sanity = higher pitch).
        var basePitch = sanityBand switch
        {
            SanityBand.Stable => 880,
            SanityBand.Unsteady => 740,
            SanityBand.Fraying => 620,
            SanityBand.Broken => 520,
            _ => 740
        };

        // Add urgency with low health.
        var urgencyOffset = healthBand switch
        {
            HealthBand.Safe => 0,
            HealthBand.Wounded => -60,
            HealthBand.Critical => -140,
            _ => 0
        };

        var pitch = Math.Clamp(basePitch + urgencyOffset, 200, 2000);

        return kind switch
        {
            FeedbackSoundKind.MenuMove => (pitch, 20, 0, 0),
            FeedbackSoundKind.MenuSelect => (pitch + 160, 35, pitch + 240, 35),
            FeedbackSoundKind.MenuBack => (pitch - 120, 25, 0, 0),

            FeedbackSoundKind.Heal => (pitch + 200, 55, pitch + 320, 65),
            FeedbackSoundKind.Damage => (pitch - 220, 70, pitch - 340, 80),

            FeedbackSoundKind.SanityUp => (pitch + 120, 40, pitch + 220, 50),
            FeedbackSoundKind.SanityDown => (pitch - 140, 50, pitch - 240, 60),

            _ => (pitch, 20, 0, 0)
        };
    }

    private static (HealthBand health, SanityBand sanity) GetBands(RunState? run)
    {
        if (run is null)
            return (HealthBand.Safe, SanityBand.Unsteady);

        var health = run.Health switch
        {
            >= 70 => HealthBand.Safe,
            >= 30 => HealthBand.Wounded,
            _ => HealthBand.Critical
        };

        var sanity = run.Sanity switch
        {
            >= 70 => SanityBand.Stable,
            >= 40 => SanityBand.Unsteady,
            >= 15 => SanityBand.Fraying,
            _ => SanityBand.Broken
        };

        return (health, sanity);
    }

    internal enum FeedbackSoundKind
    {
        MenuMove,
        MenuSelect,
        MenuBack,
        Heal,
        Damage,
        SanityUp,
        SanityDown
    }

    private enum HealthBand { Safe, Wounded, Critical }
    private enum SanityBand { Stable, Unsteady, Fraying, Broken }
}

