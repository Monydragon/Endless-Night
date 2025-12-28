namespace EndlessNight.Services.Ui;

public readonly record struct MenuPollResult(int Result, bool Dirty)
{
    public static readonly MenuPollResult ContinueClean = new(int.MinValue, false);
    public static readonly MenuPollResult ContinueDirty = new(int.MinValue, true);

    public static MenuPollResult Exit(int result) => new(result, false);
}

