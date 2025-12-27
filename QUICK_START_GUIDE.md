# 🎮 Quick Start - Controller & HUD Complete

## Build and Run

```powershell
cd C:\Projects\Github\Console\Endless-Night
dotnet build
dotnet run --project EndlessNight\EndlessNight.csproj
```

## What You'll See

### At Startup
```
Initializing controller support...
Checking all XInput controller slots...
🎮 Controller detected and ready!
You can use D-Pad or Left Stick to navigate, A to select, B to go back.
```

### Main Menu
Navigate with **D-Pad ↑↓** or **Left Stick ↑↓**
Press **A Button** to select

```
╔══════════════════════════════════════════════════════════════╗
║                      Main Menu                               ║
╚══════════════════════════════════════════════════════════════╝

  ► Continue
    New Game
    New Game (Seeded)
    Inspect Saves (Debug)
    Reset DB (Debug)
    Recreate DB (Fix tables)
    Quit
```

### During Gameplay
You'll see the full HUD with:

**Player Stats:**
- ❤ Health bar (colored)
- ⚡ Sanity bar (colored)
- ⚖ Morality indicator
- 🔄 Turn counter

**Room Information:**
- Room name
- Coordinates
- Danger level
- Description

**Room Contents:**
- Available objects (chests, traps, campfires)
- Available exits with arrows

**Action Menu:**
```
AVAILABLE ACTIONS
  ► Move
    Search Room
    Interact
    Inventory
    Rest (Campfire)
    Toggle Debug
    Quit
```

Navigate and select normally with controller.

## Controller Buttons

| Button | Action |
|--------|--------|
| D-Pad ↑↓ | Navigate menu |
| Left Stick ↑↓ | Navigate menu (alternative) |
| A Button | Select / Confirm |
| B Button | Go Back |
| Enter | Always works as backup |
| Arrow Keys | Always work as backup |

## What's New

✅ **Continue prompts** - Press A button instead of Enter
✅ **Yes/No questions** - Navigate with D-Pad, select with A
✅ **Full HUD** - See stats and room details always
✅ **Colored bars** - Health and Sanity visuals
✅ **Room details** - Name, danger, coordinates, description
✅ **Vibration** - Feel feedback on every action

## Example Gameplay Flow

1. **Start game** → See main menu with HUD preview
2. **Select "New Game"** → See intro dialogue, press A to continue
3. **Enter first room** → See full HUD with your stats
4. **View the HUD:**
   - Health: 100/100 (green bar)
   - Sanity: 100/100 (green bar)
   - Morality: → 0 (neutral)
   - Turn: 1
   - Room: "Entrance" (yellow - moderate danger)
   - Exits: ↑ North, → East
5. **Action menu appears** → Navigate with D-Pad, press A to select
6. **Choose action** → e.g., "Move"
7. **Choose direction** → e.g., "North"
8. **Enter new room** → HUD updates with new room info
9. **Repeat** → Navigate, interact, manage inventory

## Tips

- **Watch the HUD** - Colors tell you your status
  - Green = Good
  - Yellow = Caution
  - Red = Critical
  
- **Danger levels** - Rooms show danger rating
  - ⚠ = Low danger (1-2)
  - ⚠⚠⚠ = High danger (3-5)

- **Sanity affects gameplay** - As sanity drops:
  - Walls become unreliable
  - Perception changes
  - HUD text becomes more disturbing

- **No controller?** - Keyboard works perfectly:
  - Arrow Keys = Navigate
  - Enter = Select
  - Escape = Back

## Troubleshooting

**Controller not detected?**
- Make sure it's plugged in/connected
- Controller works in other games?
- Check `joy.cpl` - should appear in list

**Menus not responding to controller?**
- Try pressing D-Pad Up/Down
- Try moving Left Stick Up/Down
- Use keyboard as backup (always works)

**No vibration feedback?**
- Some controllers don't support it
- Doesn't affect gameplay
- Still get visual feedback

## Save Locations

By default, saves are stored in the game directory:
```
endless-night.db
```

You can have multiple saves per player. Use "Continue" to load your most recent save.

## Keyboard Alternative

If you don't have a controller or prefer keyboard:

```
↑↓ = Navigate
Enter = Select
Escape = Back
```

Keyboard works everywhere controller does!

---

**Status: ✅ Ready to Play**

Enjoy Endless Night! 🎮🌙

