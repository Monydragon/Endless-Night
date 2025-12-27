# Endless Night - Enhanced UI & Game Experience

## Summary of Latest Changes

### 1. Enhanced Room Banner with Colorful Theme

The room display now features:
- **Danger-Based Room Colors**: Room names change color based on danger rating:
  - Red (danger 3+): High threat areas
  - Yellow (danger 2): Moderate threat
  - Blue (danger 1): Low threat
  - Green (danger 0): Safe areas
  
- **Emoji & Visual Hierarchy**: Clear iconography for stats:
  - ❤ Health
  - ⚡ Sanity
  - ⚖ Morality
  - 🔄 Turn
  - ⚠ Danger

- **Color-Coded Stats** (reordered as requested):
  1. **Health** (first) - Green (75+), Yellow (50+), Orange (25+), Red (<25)
  2. **Sanity** (second) - Green (75+), Cyan (50+), Magenta (25+), Red (<25)
  3. **Morality** (third) - Green (>0), Red (<0), Grey (0) with directional symbols:
     - ↑ for positive morality
     - ↓ for negative morality
     - → for neutral morality
  4. **Turn** (last) - Clean white counter

- **Atmospheric Exposition** (colored by sanity):
  - Green (80+ sanity): "The walls behave. Mostly."
  - Yellow (60+): "Something watches with polite hunger."
  - Cyan (40+): "Angles disagree about being angles."
  - Magenta (20+): "You keep seeing doors that won't admit to being doors."
  - Bold Red (<20): "Reality is threadbare here. Your breath draws patterns that don't hold."

- **Dark Theme Border**: Room panel uses dark gray border for subtle atmosphere

### 2. Intro Dialogue & Dynamic Goal Generation

Every new game now begins with an introduction screen featuring:

- **Seed-Based Goal Variety** (6 different goal themes):
  1. Cryptic memory about the House's heart
  2. Fragmented memories seeking answers
  3. Ritual and binding themes
  4. House memory/escape angle
  5. Time and conscious core concept
  6. Dream-based narrative

- **Dynamic Context Lines** (4 thematic variations):
  1. Artifact gathering and puzzle-solving
  2. Trap awareness and room history
  3. Campfire sanctuary and patience
  4. Sanity, morality, and transformation

- **Dark Atmospheric Presentation**:
  - Magenta rule separator
  - Seeded randomization (same seed = same intro)
  - "The darkness beckons..." closing line
  - Clear visual separation from gameplay

### 3. Gameplay Flow

**Before starting:**
- Main menu appears
- Player selects: Continue, New Game, New Game (Seeded), or other debug options
- **New:** Upon creating a new game, intro dialogue displays
- **New:** Goal and context are seeded with the run's seed for consistency

**During gameplay:**
- Room banner displays at top of each turn
- Stats update with each action
- Colored text highlights danger and player condition
- No "Reality" stat (renamed to Morality display)

### 4. Test Coverage

All existing tests remain isolated and passing:
- 10 NUnit tests in `Tests\EndlessNight.Tests`
- Coverage includes:
  - World generation (rooms, objects, campfires, coordinates, names)
  - Run service operations (initialization, movement, search, campfire rest)
  - Puzzle/chest mechanics (locked chests, puzzle gates)
  - Solvability validation across multiple seeds

**Test Status:** ✅ 10 passed, 0 failed

### 5. Visual Design Philosophy

- **Dark Undertones**: Dark gray borders, red for danger, magenta for mystery
- **Highlights for Surprises**: Bright colors for special events (red warnings, cyan mysteries)
- **Consistent Theme**: All text fits gothic horror atmosphere
- **Clean Information Hierarchy**: Most important (Health) listed first, visual weight matches importance
- **Seed-Based Variation**: Every run feels unique through intro dialogue seeding

### 6. Menu Options

Main menu now includes:
- **Continue**: Resume last saved run
- **New Game**: Create a new run with random seed (triggers intro)
- **New Game (Seeded)**: Create with specific seed (triggers intro)
- **Inspect Saves (Debug)**: View run statistics
- **Reset DB (Debug)**: Clear database completely
- **Recreate DB (Fix tables)**: Recreate with all data and seeding
- **Quit**: Exit game

## How to Experience

### Run the Game
```powershell
cd "C:\Projects\Github\Console\Endless-Night"
dotnet run --project EndlessNight
```

### Run Tests
```powershell
cd "C:\Projects\Github\Console\Endless-Night\Tests\EndlessNight.Tests"
dotnet test
```

## Color Reference

### Room Danger Colors
| Danger | Color | Meaning |
|--------|-------|---------|
| 0 | Green | Safe |
| 1 | Blue | Caution |
| 2 | Yellow | Hazardous |
| 3+ | Red | Deadly |

### Health Colors
- Green (≥75): Excellent condition
- Yellow (50-74): Healthy
- Orange (25-49): Damaged
- Red (<25): Critical

### Sanity Colors
- Green (≥75): Stable mind
- Cyan (50-74): Unsettled
- Magenta (25-49): Fractured
- Red (<25): Unraveling

### Morality Indicators
- ↑ Green: Good choices accumulating
- → Grey: Neutral, balanced
- ↓ Red: Dark choices accumulating

## Notes

- Minor warnings remain in Program.cs related to nullable string handling (CS8604) - these don't affect gameplay
- Intro dialogue is seeded for consistency across sessions with same seed
- All stats are real-time updated each turn
- Room coordinates displayed with location (X, Y)
- No "Reality" stat; "Morality" replaces it with clearer thematic intent

## Next Steps (Optional)

- Add more intro dialogue variations
- Implement mid-game story pop-ups with similar styling
- Add "surprise" event indicators in certain rooms
- Create victory/defeat end screens with similar theming
- Add difficulty tiers that affect intro dialogue tone

---

**Status**: ✅ Complete - UI enhanced, intro added, tests passing, dark theme applied throughout

