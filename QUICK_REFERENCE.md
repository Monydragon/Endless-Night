# Quick Reference: New Features

## Item Types

### Key Items (Open Puzzles)
- `rusty-key` - Opens rusted locks
- `sigil` - Opens sigil seals
- `silver-key` - Opens silver gates
- `crystal-shard` - Opens crystal barriers
- `ancient-tome` - Opens sealed doors

### Consumables
- `bandage` - Restores sanity (+2)
- `health-potion` - Restores health (+20)
- `note` - Reduces sanity (-2)

### Tools
- `lantern` - Light source
- `torch` - Light source
- `rope` - Utility

## World Objects

### Chests
- **Locked Chest** - Requires rusty-key or silver-key
- **Unlocked Chest** - No key required
- Contains 1-4 items

### Traps
| Type | Trigger | Hidden |
|------|---------|--------|
| Tripwire | OnSearchRoom | 65% |
| Pressure Plate | OnEnterRoom | 65% |
| Dart Trap | OnInteract | 65% |
| Gas Vent | OnEnterRoom | 65% |

Damage: 3-10 health, 0-4 sanity

### Puzzle Gates
| Gate | Required Item | Blocks |
|------|---------------|--------|
| Rusted Lock | rusty-key | Exit direction |
| Sigil Seal | sigil | Exit direction |
| Silver Gate | silver-key | Exit direction |
| Crystal Barrier | crystal-shard | Exit direction |
| Sealed Door | ancient-tome | Exit direction |

## Game Commands

### Main Actions
- **Move** - Navigate to adjacent rooms
- **Search room** - Reveal hidden items and traps
- **Interact** - Use objects in the room
- **Inventory** - View collected items
- **Use item** - Consume or use an item
- **Quit** - Exit game

### Interact Options
- **Pick up** - Collect ground items
- **Chest** - Open chest (may require key)
- **Trap** - Examine trap
- **Gate** - Solve puzzle (requires key item)

## World Generation

### Room Count
- 7-10 rooms per world
- Connected with main path + 1-2 branches

### Object Distribution
- Ground Items: 2-4
- Key Items: 1 (always accessible)
- Chests: 2-4 (40% locked)
- Traps: 1-3 (in danger rooms)
- Puzzle Gates: 0-1 (if solvable)

## Testing Commands

```powershell
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ChestAndItemTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Debug Menu

In main menu, select:
- **Inspect Saves (Debug)** - View save details
- **Reset DB (Debug)** - Delete database

## Seeded Worlds

Try these seeds:
- `42` - Balanced layout
- `12345` - Complex branching
- `999` - High danger

## Code Structure

```
Domain/
  ├── ItemDefinition.cs - Item templates
  ├── PuzzleDefinition.cs - Puzzle gate types
  ├── WorldObjectInstance.cs - In-game objects
  ├── WorldObjectKind.cs - Object types enum
  └── TrapTrigger.cs - Trap trigger types

Services/
  ├── ProceduralLevel1Generator.cs - World generation
  ├── PuzzleSolvabilityValidator.cs - Solvability check
  ├── Seeder.cs - Initial data
  └── RunService.cs - Game logic

Tests/
  ├── PuzzleSolvabilityValidatorTests.cs
  ├── ChestAndItemTests.cs
  ├── PuzzleGateTests.cs
  ├── WorldObjectTests.cs
  └── InventoryTests.cs
```

## Quick Tips

### Finding Items
- Search every room
- Hidden items appear after search
- Check danger rooms carefully

### Solving Puzzles
1. Find blocked direction
2. Search for required key item
3. Pick up key item
4. Return and interact with gate

### Avoiding Traps
- Search rooms before exploring thoroughly
- Some traps trigger on entry (unavoidable)
- Watch for hidden traps (65% chance)

### Managing Chests
- Try opening first (may be unlocked)
- If locked, find the required key
- Keys: rusty-key, silver-key

## Item Tags

- `key` - Opens locks/gates
- `artifact` - Special mystical items
- `tool` - Utility items
- `consumable` - Single-use items
- `health` - Affects health
- `sanity` - Affects sanity
- `clue` - Story/puzzle hints
- `light` - Light sources

