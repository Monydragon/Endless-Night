# Testing Guide - Endless Night

## Running Tests

### Using dotnet CLI
```powershell
# Run all tests
dotnet test

# Run tests from a specific test class
dotnet test --filter "FullyQualifiedName~ChestAndItemTests"
dotnet test --filter "FullyQualifiedName~PuzzleGateTests"
dotnet test --filter "FullyQualifiedName~PuzzleSolvabilityValidatorTests"

# Run tests with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Using JetBrains Rider
1. Right-click on the test project → Run Unit Tests
2. Or right-click on individual test classes/methods → Run
3. View results in the Unit Tests window

## Test Coverage

### ChestAndItemTests
- ✅ Verifies chests are generated
- ✅ Verifies traps are generated  
- ✅ Verifies key items are placed
- ✅ Validates locked chest requirements
- ✅ Validates trap triggers and damage
- ✅ Tests ground item generation

### PuzzleGateTests
- ✅ Tests puzzle gate generation
- ✅ Validates gate-item matching
- ✅ Ensures required keys exist in world
- ✅ Tests PuzzleDefinition completeness

### PuzzleSolvabilityValidatorTests
- ✅ Tests world solvability across multiple seeds
- ✅ Ensures all generated puzzles are solvable

## IDE Configuration

### If NUnit symbols aren't resolved in Rider:
1. **Invalidate Caches**: File → Invalidate Caches / Restart
2. **Restore NuGet**: Right-click solution → Restore NuGet Packages
3. **Rebuild**: Build → Rebuild All
4. **Restart Rider**: Sometimes a full restart is needed

### The tests WILL compile and run via dotnet CLI even if the IDE shows errors.

## Testing New Features

### To test chests in-game:
1. Start a new game with a specific seed (e.g., `42`)
2. Explore rooms and look for "Chest" or "Locked Chest"
3. Try the "Interact" action to open chests
4. Locked chests will require keys (rusty-key or silver-key)

### To test traps:
1. Enter rooms with danger rating >= 2
2. Traps may trigger when:
   - Entering a room (Pressure Plate, Gas Vent)
   - Searching a room (Tripwire)
   - Interacting with objects (Dart Trap)
3. Watch your Health and Sanity stats

### To test puzzle gates:
1. Navigate through the world
2. Look for blocked directions (gates)
3. Find the required key item (shows in room descriptions)
4. Pick up the key item via "Interact" → "Pick up"
5. Try moving through the previously blocked direction

## Debugging Tips

### View world generation details:
```csharp
// Add to ProceduralLevel1Generator.cs for debugging
Console.WriteLine($"Generated {objects.Count} objects:");
foreach (var obj in objects)
{
    Console.WriteLine($"  {obj.Kind}: {obj.Key} in room {obj.RoomId}");
}
```

### Check database contents:
Use a SQLite browser to inspect `endless-night.db`:
- Tables: RoomInstances, WorldObjects, RunInventoryItems
- Look for objects with Kind = 1 (Chest), 2 (Trap), 3 (PuzzleGate)

### Test specific seeds:
Seeds that are known to generate interesting layouts:
- `42` - Good balance of all features
- `12345` - Complex layout with multiple branches
- `999` - Challenging world with high danger rooms

## Common Issues

### Issue: NUnit symbols not recognized in IDE
**Solution**: This is a Rider caching issue. The tests will still compile and run via `dotnet test`. Try invalidating caches or restarting Rider.

### Issue: Tests fail with "Cannot resolve symbol 'GenerateWorld'"
**Solution**: The method exists but IDE hasn't refreshed. Run `dotnet restore` and rebuild.

### Issue: Database errors when running the game
**Solution**: Delete `endless-night.db` or use the "Reset DB (Debug)" option in the main menu.

## Continuous Integration

If setting up CI/CD, use:
```yaml
- name: Run tests
  run: dotnet test --configuration Release --logger trx --results-directory ./TestResults
```

## Next Steps

After verifying tests pass:
1. Test gameplay with various seeds
2. Verify save/load works with new objects
3. Check that interactions work as expected
4. Validate that puzzles are solvable in practice

