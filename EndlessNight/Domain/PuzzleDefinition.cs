namespace EndlessNight.Domain;

public static class PuzzleDefinition
{
    // Deterministic puzzle gate definitions that require specific key items
    public static readonly (string key, string name, string description, string requiredItemKey)[] KeyItemGates =
    {
        (
            key: "gate.rusty-key",
            name: "Rusted Lock",
            description: "A rusted lock clamps the way forward. It looks hungry for a key.",
            requiredItemKey: "rusty-key"
        ),
        (
            key: "gate.sigil",
            name: "Sigil Seal",
            description: "A chalk sigil occludes the passage. It reacts to something you might carry.",
            requiredItemKey: "sigil"
        ),
        (
            key: "gate.silver-key",
            name: "Silver Gate",
            description: "An ornate gate sealed with ancient runes. Only silver will open it.",
            requiredItemKey: "silver-key"
        ),
        (
            key: "gate.crystal-shard",
            name: "Crystal Barrier",
            description: "A shimmering barrier blocks the way. It resonates with crystalline energy.",
            requiredItemKey: "crystal-shard"
        ),
        (
            key: "gate.tome",
            name: "Sealed Door",
            description: "A door covered in inscriptions. Knowledge is the key to passage.",
            requiredItemKey: "ancient-tome"
        )
    };
}

