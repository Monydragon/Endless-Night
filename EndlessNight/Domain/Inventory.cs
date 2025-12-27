namespace EndlessNight.Domain;

public sealed class Inventory
{
    public List<ItemStack> Items { get; set; } = new();

    public bool IsEmpty => Items.Count == 0;

    public void Add(string key, int quantity = 1)
    {
        var existing = Items.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            Items.Add(new ItemStack { Key = key, Quantity = quantity });
        else
            existing.Quantity += quantity;
    }

    public bool Remove(string key, int quantity = 1)
    {
        var existing = Items.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (existing is null || existing.Quantity < quantity)
            return false;

        existing.Quantity -= quantity;
        if (existing.Quantity <= 0)
            Items.Remove(existing);

        return true;
    }
}

