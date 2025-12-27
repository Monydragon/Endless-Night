# Console Display Configuration Guide

## Overview
The Endless Night game now supports configurable console window sizing to work optimally with 1920x1080 displays and other resolutions.

## Default Configuration
By default, the game attempts to configure the console to:
- **Width**: 240 columns
- **Height**: 60 rows
- This approximately maps to 1920x1080 pixel display (at standard console font sizes of ~8x16 pixels)

## Customizing Console Size

### Method 1: Edit appsettings.json
The easiest way to customize console display is by editing `appsettings.json` in the game directory:

```json
{
  "Display": {
    "Width": 240,
    "Height": 60,
    "Fullscreen": false,
    "WindowPositionX": 0,
    "WindowPositionY": 0,
    "WindowWidth": 1920,
    "WindowHeight": 1080
  }
}
```

### Configuration Options

#### Width
- **Default**: 240 columns
- **Range**: 80-240+ (depending on system capability)
- **Effect**: Controls the number of character columns displayed
- **Recommendation**: Use 240 for 1920px width displays, 160 for 1280px, 200 for 1600px

#### Height  
- **Default**: 60 rows
- **Range**: 24-80+ (depending on system capability)
- **Effect**: Controls the number of character rows displayed
- **Recommendation**: Use 60 for 1080px height displays, 40 for 720px, 50 for 900px

#### Fullscreen
- **Default**: false
- **Effect**: Toggles fullscreen mode (currently reserved for future use)

#### WindowPositionX & WindowPositionY
- **Default**: 0, 0 (top-left corner)
- **Effect**: Initial window position on screen (Windows only)
- **Note**: Useful for multi-monitor setups

#### WindowWidth & WindowHeight
- **Default**: 1920, 1080
- **Effect**: Physical window size in pixels (Windows only)
- **Note**: Actual size depends on font size and DPI settings

## Common Resolutions

### For 1920x1080 (Full HD)
```json
{
  "Display": {
    "Width": 240,
    "Height": 60
  }
}
```

### For 1280x720 (HD)
```json
{
  "Display": {
    "Width": 160,
    "Height": 40
  }
}
```

### For 1600x900
```json
{
  "Display": {
    "Width": 200,
    "Height": 50
  }
}
```

### For 2560x1440 (2K)
```json
{
  "Display": {
    "Width": 320,
    "Height": 80
  }
}
```

## Responsive HUD
The HUD automatically adapts to the console width:
- **Narrow displays (<120 columns)**: Vertical menu layouts
- **Medium displays (120-160 columns)**: Compact layouts
- **Wide displays (160+ columns)**: Horizontal layouts with better spacing

## Font Size Impact
Console font size significantly affects the actual display resolution:
- **Small font (8x12)**: Can fit more content
- **Standard font (8x16)**: Default configuration uses this
- **Large font (10x20)**: Fewer columns/rows displayed

To get the best experience:
1. Use a standard or small console font size
2. Configure Width and Height for your display resolution
3. Let the game auto-detect and adjust

## Troubleshooting

### Text Gets Cut Off
- Reduce Width and Height values
- Try Width: 200, Height: 50

### Window Too Small
- Increase Width and Height values
- Try Width: 280, Height: 70

### System Doesn't Support Width/Height
- The game will automatically fall back to supported sizes
- Check console error messages for actual dimensions used

### Font Size Not Matching
- Windows: Right-click console title bar → Properties → Font
- Change font size to smaller value (8pt or 10pt recommended)

## Performance Notes
- Larger console sizes (240+ columns) may use more memory
- Rendering performance is not impacted on modern systems
- If experiencing slowness, reduce Width and Height slightly

## Platform Differences

### Windows
- Full support for console sizing
- Window positioning works
- Font size customizable

### macOS/Linux
- Console sizing limited by terminal capabilities
- Game will adapt to available terminal size
- Window positioning not supported

## Resetting to Defaults
Delete your appsettings.json file and the game will regenerate it with default values.

## HUD Layout Optimization

The HUD automatically optimizes for your console width:

| Width | Layout Style | Best For |
|-------|------|----------|
| < 100 | Vertical (Narrow) | Small terminals |
| 100-160 | Compact | Laptops, Small displays |
| 160-220 | Balanced | Standard monitors |
| 220+ | Wide/Expansive | Large displays, 1920x1080+ |

The stats table, room info, and menus all expand/contract to fit available space.

