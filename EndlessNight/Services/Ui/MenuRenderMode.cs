namespace EndlessNight.Services.Ui;

public enum MenuRenderMode
{
    /// <summary>
    /// Uses Spectre.Console Live rendering (cursor movement). Best for real terminals.
    /// </summary>
    Live,

    /// <summary>
    /// Clears and redraws the full page each frame. Best for IDE/limited ANSI hosts.
    /// </summary>
    ClearRedraw
}

