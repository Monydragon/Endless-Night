using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using System.Runtime.Versioning;

namespace EndlessNight;

/// <summary>
/// Configures console window size and properties for optimal display
/// </summary>
public static class ConsoleConfig
{
    private static int _configuredWidth = 240;
    private static int _configuredHeight = 60;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const int WM_SETTINGCHANGE = 0x001A;

    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// Configure console window from configuration file
    /// </summary>
    public static void ConfigureConsoleWindow(IConfiguration? configuration = null)
    {
        // Load settings from config if available
        if (configuration != null)
        {
            var displayConfig = configuration.GetSection("Display");
            if (displayConfig.Exists())
            {
                int.TryParse(displayConfig["Width"], out var width);
                int.TryParse(displayConfig["Height"], out var height);
                
                if (width > 0) _configuredWidth = width;
                if (height > 0) _configuredHeight = height;
            }
        }

        ConfigureConsoleWindowInternal();
    }

    /// <summary>
    /// Configure console window for optimal gameplay
    /// Targets ~240 columns x ~60 rows (approximately 1920x1080 at typical console font sizes)
    /// </summary>
    private static void ConfigureConsoleWindowInternal()
    {
        try
        {
            if (IsWindows)
            {
                var consoleWindow = GetConsoleWindow();
                if (consoleWindow == IntPtr.Zero)
                {
                    // Not a console window (e.g., running in IDE), use console properties instead
                    ConfigureConsoleSize();
                    return;
                }

                // On Windows, set console window size
                TrySetConsoleSize();

                // Try to maximize and position the window
                try
                {
                    MoveWindow(consoleWindow, 0, 0, 1920, 1080, true);
                }
                catch
                {
                    // Window sizing failed, but console buffer is set
                }
                return;
            }

            // Non-Windows: just set buffer/window sizes using Console APIs
            ConfigureConsoleSize();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Console configuration error: {ex.Message}");
            // Non-fatal error, game can continue with default console settings
        }
    }

    private static void ConfigureConsoleSize()
    {
        try
        {
            TrySetConsoleSize();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Console size configuration error: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private static void TrySetConsoleSizeWindows()
    {
        try
        {
            Console.BufferWidth = _configuredWidth;
            Console.BufferHeight = _configuredHeight;
            Console.WindowWidth = _configuredWidth;
            Console.WindowHeight = _configuredHeight;
        }
        catch (ArgumentOutOfRangeException)
        {
            // System can't support configured size, try something smaller
            try
            {
                Console.BufferWidth = 200;
                Console.BufferHeight = 50;
                Console.WindowWidth = 200;
                Console.WindowHeight = 50;
            }
            catch
            {
                // Final fallback: leave defaults
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static void TrySetConsoleSize()
    {
        // Console size mutators are only supported on Windows terminals.
        if (!IsWindows)
            return;

        TrySetConsoleSizeWindows();
    }

    /// <summary>
    /// Get the current console width for responsive layout
    /// </summary>
    public static int GetConsoleWidth()
    {
        try
        {
            // Prefer the visible window width, fall back to buffer width
            return Math.Max(10, Math.Max(Console.WindowWidth, Console.BufferWidth));
        }
        catch
        {
            return 80; // Fallback to standard width
        }
    }

    /// <summary>
    /// Get the current console height for responsive layout
    /// </summary>
    public static int GetConsoleHeight()
    {
        try
        {
            return Math.Max(5, Math.Max(Console.WindowHeight, Console.BufferHeight));
        }
        catch
        {
            return 24; // Fallback to standard height
        }
    }
}
