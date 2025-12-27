using EndlessNight.Domain;

namespace EndlessNight.Services;

/// <summary>
/// Validates that puzzle gates are solvable given the set of placed items and chest loot.
/// This is intentionally conservative: if we can't prove solvable, the generator should skip puzzles.
/// </summary>
public static class PuzzleSolvabilityValidator
{
    public static bool AreAllGatesSolvable(
        IReadOnlyList<RoomInstance> rooms,
        IReadOnlyList<WorldObjectInstance> objects,
        Guid startRoomId)
    {
        var roomById = rooms.ToDictionary(r => r.RoomId, r => r);
        var objectsByRoom = objects
            .Where(o => !o.IsHidden)
            .GroupBy(o => o.RoomId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // We iterate, expanding reachable rooms and collectable items until no progress. Any gate requiring
        // an item must be passable with the inventory we can collect.
        var reachable = new HashSet<Guid> { startRoomId };
        var inventory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool progressed;
        do
        {
            progressed = false;

            // Collect items/loot in any reachable room.
            foreach (var roomId in reachable.ToList())
            {
                if (!objectsByRoom.TryGetValue(roomId, out var objs))
                    continue;

                foreach (var obj in objs)
                {
                    switch (obj.Kind)
                    {
                        case WorldObjectKind.GroundItem:
                            if (!obj.IsConsumed && !string.IsNullOrWhiteSpace(obj.ItemKey))
                            {
                                if (inventory.Add(obj.ItemKey))
                                    progressed = true;
                            }
                            break;
                        case WorldObjectKind.Chest:
                            // Opening a chest might require a key; if we have it, we can obtain loot.
                            if (!obj.IsOpened)
                            {
                                if (string.IsNullOrWhiteSpace(obj.RequiredItemKey) || inventory.Contains(obj.RequiredItemKey))
                                {
                                    foreach (var loot in obj.LootItemKeys)
                                    {
                                        if (inventory.Add(loot))
                                            progressed = true;
                                    }
                                }
                            }
                            break;
                    }
                }
            }

            // Expand reachable rooms via exits; puzzle gates block an exit direction unless solved via required item.
            foreach (var roomId in reachable.ToList())
            {
                if (!roomById.TryGetValue(roomId, out var room))
                    continue;

                foreach (var exit in room.Exits)
                {
                    var dir = exit.Key;
                    var nextRoom = exit.Value;

                    if (IsDirectionBlockedByGate(roomId, dir, objectsByRoom, inventory))
                        continue;

                    if (reachable.Add(nextRoom))
                        progressed = true;
                }
            }
        } while (progressed);

        // After saturation, ensure that any gate in any reachable room is solvable (i.e., required item is in inventory).
        foreach (var gate in objects.Where(o => o.Kind == WorldObjectKind.PuzzleGate && !o.IsSolved))
        {
            if (gate.BlocksDirection is null)
                continue;

            // If the room is never reachable, it's fine (shouldn't happen in generation), but don't fail validator.
            if (!reachable.Contains(gate.RoomId))
                continue;

            if (!string.IsNullOrWhiteSpace(gate.RequiredItemKey) && !inventory.Contains(gate.RequiredItemKey))
                return false;
        }

        return true;
    }

    private static bool IsDirectionBlockedByGate(
        Guid roomId,
        Direction direction,
        Dictionary<Guid, List<WorldObjectInstance>> objectsByRoom,
        HashSet<string> inventory)
    {
        if (!objectsByRoom.TryGetValue(roomId, out var objs))
            return false;

        foreach (var gate in objs.Where(o => o.Kind == WorldObjectKind.PuzzleGate && !o.IsSolved))
        {
            if (gate.BlocksDirection != direction)
                continue;

            if (string.IsNullOrWhiteSpace(gate.RequiredItemKey))
                return true; // blocked and can't be solved

            // Gate is solvable if player has required item.
            return !inventory.Contains(gate.RequiredItemKey);
        }

        return false;
    }
}

