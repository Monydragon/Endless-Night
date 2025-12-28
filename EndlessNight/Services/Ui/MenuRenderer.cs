using Spectre.Console;
using Spectre.Console.Rendering;

namespace EndlessNight.Services.Ui;

public static class MenuRenderer
{
    /// <summary>
    /// Heuristic: JetBrains/IDE consoles often don't support ANSI cursor movement well enough for Live.
    /// </summary>
    private static bool IsJetBrainsHost()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JETBRAINS_IDE"))
           || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IDEA_INITIAL_DIRECTORY"));

    public static MenuRenderMode DetectDefaultMode()
    {
        // User override:
        //   ENDLESSNIGHT_MENU_MODE=live  -> force Spectre Live rendering
        //   ENDLESSNIGHT_MENU_MODE=clear -> force clear/redraw mode
        var forced = Environment.GetEnvironmentVariable("ENDLESSNIGHT_MENU_MODE");
        if (!string.IsNullOrWhiteSpace(forced))
        {
            if (string.Equals(forced, "live", StringComparison.OrdinalIgnoreCase))
                return MenuRenderMode.Live;
            if (string.Equals(forced, "clear", StringComparison.OrdinalIgnoreCase))
                return MenuRenderMode.ClearRedraw;
        }

        // If output is redirected, Live won't work.
        if (Console.IsOutputRedirected)
            return MenuRenderMode.ClearRedraw;

        // Some IDE hosts set these env vars.
        if (IsJetBrainsHost())
            return MenuRenderMode.ClearRedraw;

        // Conservative fallback: if TERM is missing or "dumb".
        var term = Environment.GetEnvironmentVariable("TERM");
        if (string.IsNullOrWhiteSpace(term) || string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase))
            return MenuRenderMode.ClearRedraw;

        return MenuRenderMode.Live;
    }

    public static int RunMenuLoop(
        MenuRenderMode mode,
        Func<IRenderable> build,
        Func<MenuPollResult> pollStep)
    {
        return mode switch
        {
            MenuRenderMode.Live => RunLive(build, pollStep),
            _ => RunClearRedraw(build, pollStep)
        };
    }

    private static int RunLive(Func<IRenderable> build, Func<MenuPollResult> pollStep)
    {
        return AnsiConsole.Live(build())
            .AutoClear(true)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .Start(ctx =>
            {
                // Always render first frame.
                ctx.UpdateTarget(build());
                while (true)
                {
                    var res = pollStep();
                    if (res.Result != int.MinValue)
                        return res.Result;

                    if (res.Dirty)
                        ctx.UpdateTarget(build());
                }
            });
    }

    private static int RunClearRedraw(Func<IRenderable> build, Func<MenuPollResult> pollStep)
    {
        // Always render first frame.
        RenderPage(build);

        while (true)
        {
            var res = pollStep();
            if (res.Result != int.MinValue)
                return res.Result;

            if (res.Dirty)
                RenderPage(build);
        }
    }

    private static void RenderPage(Func<IRenderable> build)
    {
        if (IsJetBrainsHost())
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[grey]PAGE[/]")
                .RuleStyle("grey")
                .LeftJustified());
        }
        else
        {
            try
            {
                Console.Clear();
            }
            catch
            {
                AnsiConsole.Clear();
            }
        }

        AnsiConsole.Write(build());
    }
}
