using EndlessNight.Domain;

namespace EndlessNight.Services;

public sealed record WorldGenerationResult(
    IReadOnlyList<RoomInstance> Rooms,
    IReadOnlyList<WorldObjectInstance> Objects
);

