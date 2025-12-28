using System;
using Microsoft.Extensions.Configuration;

namespace EndlessNight;

/// <summary>
/// Console configuration helpers.
/// This project intentionally avoids OS-specific console window APIs.
/// </summary>
public static class ConsoleConfig
{
    private static int _configuredWidth = 240;
    private static int _configuredHeight = 60;

    /// <summary>
    /// Load display settings (if present). This does not attempt to resize the terminal.
    /// </summary>
    public static void ConfigureConsoleWindow(IConfiguration? configuration = null)
    {
        if (configuration == null)
            return;

        var displayConfig = configuration.GetSection("Display");
        if (!displayConfig.Exists())
            return;

        int.TryParse(displayConfig["Width"], out var width);
        int.TryParse(displayConfig["Height"], out var height);

        if (width > 0) _configuredWidth = width;
        if (height > 0) _configuredHeight = height;
    }

    /// <summary>
    /// Get the current console width for responsive layout.
    /// Falls back to configured width if the host doesn't support reading window size.
    /// </summary>
    public static int GetConsoleWidth()
    {
        try
        {
            return Math.Max(10, Console.WindowWidth);
        }
        catch
        {
            return Math.Max(10, _configuredWidth > 0 ? _configuredWidth : 80);
        }
    }

    /// <summary>
    /// Get the current console height for responsive layout.
    /// Falls back to configured height if the host doesn't support reading window size.
    /// </summary>
    public static int GetConsoleHeight()
    {
        try
        {
            return Math.Max(5, Console.WindowHeight);
        }
        catch
        {
            return Math.Max(5, _configuredHeight > 0 ? _configuredHeight : 24);
        }
    }
}
