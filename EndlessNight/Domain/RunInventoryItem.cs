using EndlessNight.Domain.Abstractions;

namespace EndlessNight.Domain;

/// <summary>
/// Inventory item owned by a specific run (persisted).
/// </summary>
public sealed class RunInventoryItem : IRunScoped
{
    public Guid Id { get; set; }

    public required Guid RunId { get; set; }

    public required string ItemKey { get; set; }

    public int Quantity { get; set; } = 1;
}
