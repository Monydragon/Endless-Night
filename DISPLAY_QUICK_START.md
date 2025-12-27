# Display Configuration Quick Start

## For 1920x1080 Display (Default - No Changes Needed!)
The game comes pre-configured for 1920x1080 displays. Just run it!

## To Change Display Size

1. Open `appsettings.json` in the game directory

2. Find the Display section:
```json
"Display": {
  "Width": 240,
  "Height": 60
}
```

3. Change Width and Height based on your monitor:

| Monitor | Width | Height |
|---------|-------|--------|
| 1920x1080 | 240 | 60 |
| 1600x900 | 200 | 50 |
| 1280x720 | 160 | 40 |
| 2560x1440 | 320 | 80 |

4. Save the file

5. Run the game - it will automatically use the new settings!

## What You'll See
✅ HUD fits perfectly on screen  
✅ Player stats table displays correctly  
✅ Room information shows completely  
✅ Menu options are clearly visible  
✅ All text is readable and well-spaced  

## If Text Gets Cut Off
Reduce Width and Height by 10-20 each (e.g., 240→220, 60→50)

## If Window Is Too Small
Increase Width and Height by 10-20 each (e.g., 240→260, 60→70)

## Full Configuration Guide
See `CONSOLE_CONFIGURATION.md` for advanced options and detailed information.

