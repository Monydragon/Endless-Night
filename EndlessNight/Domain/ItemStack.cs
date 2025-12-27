namespace EndlessNight.Domain;

public sealed class ItemStack
{
    public required string Key { get; set; }
    public int Quantity { get; set; } = 1;
}

