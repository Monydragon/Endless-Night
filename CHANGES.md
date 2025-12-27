# Endless Night - Chests, Items, Traps, and Puzzles Enhancement

## Summary of Changes

This update adds comprehensive support for chests, items, environmental traps, and puzzles that require key items.

## 1. Test Framework Migration: XUnit → NUnit

### Changed Files:
- `EndlessNight.Tests.csproj`
- `PuzzleSolvabilityValidatorTests.cs`

### Changes:
- Replaced XUnit packages with NUnit (v4.3.1) and NUnit3TestAdapter (v4.6.0)
- Updated test attributes: `[Fact]` → `[Test]`, added `[TestFixture]`
- Updated assertions: `Assert.True(...)` → `Assert.That(..., Is.True)`
- Added `GenerateAssemblyInfo=false` to avoid duplicate attribute errors

## 2. Expanded Item System

### Added Items (in Seeder.cs):
- **sigil** - Chalk Sigil artifact for magical puzzles
- **silver-key** - Ornate silver key for special locks
- **ancient-tome** - Knowledge artifact for sealed doors
- **crystal-shard** - Crystalline artifact for barriers
- **torch** - Light source tool
- **rope** - Utility tool

### Tags:
- `key` - Items that open locks
- `artifact` - Special mystical items
- `tool` - Utility items
- `consumable` - Single-use items
- `health`, `sanity` - Effect types
- `clue` - Story/puzzle hints

## 3. Enhanced Puzzle System

### Updated PuzzleDefinition.cs:
Now includes 5 different puzzle gate types, each requiring specific key items:

1. **gate.rusty-key** - Rusted Lock (requires: rusty-key)
2. **gate.sigil** - Sigil Seal (requires: sigil)
3. **gate.silver-key** - Silver Gate (requires: silver-key)
4. **gate.crystal-shard** - Crystal Barrier (requires: crystal-shard)
5. **gate.tome** - Sealed Door (requires: ancient-tome)

Each gate includes:
- Unique key identifier
- Descriptive name
- Atmospheric description
- Required item key for solving

## 4. Enhanced World Generation (ProceduralLevel1Generator.cs)

### Ground Items:
- Expanded pool: `bandage`, `note`, `health-potion`, `torch`, `rope`
- Generate 2-4 items per world
- 40% chance to be hidden (discovered via search)
- Distributed randomly across rooms

### Chests:
- Generate 2-4 chests per world
- **40% chance** to require a key (rusty-key or silver-key)
- Contain 1-4 items from expanded loot pool
- **20% chance** to be hidden initially
- Dynamic descriptions based on lock type
- Loot pool: `bandage`, `health-potion`, `note`, `lantern`, `torch`, `rope`, `ancient-tome`

### Traps:
- **4 different trap types** with varied mechanics:
  1. **Tripwire** - Hidden floor hazard (OnSearchRoom trigger)
  2. **Pressure Plate** - Floor tile trap (OnEnterRoom trigger)
  3. **Dart Trap** - Wall mechanism (OnInteract trigger)
  4. **Gas Vent** - Poison gas (OnEnterRoom trigger)
- Generate 1-3 traps in rooms with danger rating ≥ 2
- **65% chance** to be hidden
- Health damage: 3-10 points
- Sanity damage: 0-4 points

### Puzzle Gates:
- Automatically matched to the key item placed in the world
- Use PuzzleDefinition for consistent gate definitions
- Block a random exit direction
- **Solvability validation** - gates are removed if unsolvable
- Ensures player can reach the key before needing it

### Key Item Placement:
- **5 possible key items**: rusty-key, sigil, silver-key, crystal-shard, ancient-tome
- One key item always placed in the first half of the level
- Never hidden (always immediately discoverable)
- Matched to the puzzle gate for guaranteed solvability

## 5. New Test Coverage

### PuzzleSolvabilityValidatorTests.cs (Updated):
- Converted to NUnit
- Tests gate solvability across multiple seeds
- Ensures no unreachable puzzles

### ChestAndItemTests.cs (New):
- Tests chest generation
- Tests trap generation
- Tests key item placement
- Validates locked chests have valid keys
- Validates traps have proper triggers and damage
- Tests ground item generation

### PuzzleGateTests.cs (New):
- Tests puzzle gates match key items
- Validates required items exist in world
- Tests multiple gate types
- Ensures gate definitions are complete

### WorldObjectTests.cs (New):
- Tests chest properties and loot
- Tests trap properties (triggers, damage, hidden state)
- Tests puzzle gate requirements
- Tests ground item properties

### InventoryTests.cs (New):
- Tests adding items (new stacks vs. existing)
- Tests removing items
- Tests quantity management
- Tests stack removal when empty
- Tests inventory empty state

## 6. Bug Fixes

### Program.cs:
- Fixed `InteractMenuAsync` method
- Resolved generic type inference error in `AnsiConsole.Prompt<T>`
- Changed from object-based selection to string-based selection with lookup
- Properly handles "Back" option without creating invalid WorldObjectInstance

### Project Files:
- Added `GenerateAssemblyInfo=false` to prevent duplicate attribute errors
- Both main project and test project now consistent

## 7. Game Mechanics

### Chest Interaction:
- Locked chests require specific keys in inventory
- Opening reveals all loot items at once
- State tracked: `IsOpened`

### Trap Mechanics:
- Hidden traps revealed when triggered or via search
- Different trigger conditions:
  - **OnEnterRoom** - Activates when player enters room
  - **OnSearchRoom** - Activates during room search
  - **OnInteract** - Activates when player interacts with it
- Can be disarmed (state: `IsDisarmed`)
- Applies health and sanity damage when triggered

### Puzzle Gate Mechanics:
- Blocks specific exit directions
- Requires matching key item in inventory
- State tracked: `IsSolved`
- Once solved, passage is permanently open

### Solvability Validation:
- BFS algorithm checks reachability with inventory simulation
- Accounts for:
  - Initially reachable rooms
  - Collectable items in those rooms
  - Chests that can be opened with available keys
  - Gates that can be solved with available items
- Iterates until no progress (saturation)
- Removes all puzzle gates if any are unsolvable

## 8. WorldObjectInstance Properties

### Common Properties:
- `Id`, `RunId`, `RoomId` - Identity and ownership
- `Kind` - Type of object (GroundItem, Chest, Trap, PuzzleGate)
- `Key`, `Name`, `Description` - Identification and display
- `IsHidden` - Discovery state

### State Flags:
- `IsConsumed` - For ground items (picked up)
- `IsOpened` - For chests (looted)
- `IsDisarmed` - For traps (neutralized)
- `IsTriggered` - For traps (activated)
- `IsSolved` - For puzzle gates (unlocked)

### Item/Loot Properties:
- `ItemKey` - Single item key (ground items)
- `Quantity` - Stack size
- `LootItemKeys` - Multiple items (chests)

### Requirements:
- `RequiredItemKey` - Key needed to interact (chests, gates)

### Trap Properties:
- `TrapTrigger` - When trap activates
- `HealthDelta` - HP change (negative for damage)
- `SanityDelta` - Sanity change (negative for loss)

### Puzzle Properties:
- `BlocksDirection` - Exit direction blocked by gate

## Files Modified:
1. ✅ EndlessNight.Tests.csproj
2. ✅ EndlessNight.csproj
3. ✅ PuzzleSolvabilityValidatorTests.cs
4. ✅ Seeder.cs
5. ✅ PuzzleDefinition.cs
6. ✅ ProceduralLevel1Generator.cs
7. ✅ Program.cs

## Files Created:
1. ✅ ChestAndItemTests.cs
2. ✅ PuzzleGateTests.cs
3. ✅ WorldObjectTests.cs
4. ✅ InventoryTests.cs
5. ✅ CHANGES.md (this file)

## Testing:
- Run tests: `dotnet test`
- All tests use NUnit framework
- Tests validate world generation consistency
- Tests ensure puzzle solvability
- Tests verify game mechanics

## Next Steps:
1. Implement trap disarm mechanics in RunService
2. Add UI feedback for trap triggers
3. Expand puzzle gate visuals in Program.cs
4. Add more trap variety and effects
5. Create item combination mechanics
6. Add puzzle hints system

