# PUBG AutoMapper

A lightweight Windows + Android toolkit for controlling PUBG Mobile on a real Android phone with keyboard and mouse, while providing coordinate calibration and automatic HUD mapping.

## Current development path

- Real Android device; no emulator.
- USB/ADB device detection.
- Normalized coordinate profiles for PUBG Mobile.
- Automatic HUD calibration from a gameplay screenshot.
- JSON export with confidence scores and a visual preview.
- Lightweight Python runtime for the mapper.
- Planned low-latency continuous joystick and mouse-look transport.

## Automatic HUD calibration

The new calibrator lives in `tools/hud_auto_calibrator.py` and uses OpenCV circle detection plus semantic screen zones. It is designed to estimate centers for Joystick, Fire, ADS, Jump, Crouch, Prone, Reload, Map, Weapon 1/2, First-Person View, and Inventory.

From a screenshot:

```bat
cd tools
python -m pip install -r requirements-hud.txt
python hud_auto_calibrator.py "C:\path\to\pubg_screenshot.jpg" -o hud_profile_auto.json --preview hud_preview.png
```

`hud_profile_auto.json` contains normalized coordinates and confidence. `hud_preview.png` marks the detections on the source image. Low-confidence detections are flagged for review instead of being silently treated as perfect.

A ready reference profile generated from the current S10 Lite gameplay screenshot is in `profiles/hud_profile_auto_reference.json`.

## Important Android limitation

A normal Android application cannot freely inject arbitrary touch events into another app because of Android security boundaries. The project therefore treats input injection as a dedicated transport/agent problem and will validate the selected mechanism on the target device before calling the system production-ready.

## Status

The repository now has a working screenshot-based HUD calibration prototype. The remaining engineering work is to connect the generated profile to a robust low-latency Android input transport and validate continuous joystick/mouse-look plus multitouch on the target device.
