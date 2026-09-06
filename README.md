# PUBG AutoMapper

A from-scratch Windows + Android toolkit for controlling PUBG Mobile on a real Android phone with keyboard and mouse, while providing coordinate calibration and automatic HUD mapping.

## Project goals

- Real Android device; no Android emulator.
- USB-first low-latency transport.
- WASD movement through the real on-screen joystick.
- Mouse capture and camera-control prototype.
- Keyboard/mouse actions mapped to calibrated HUD coordinates.
- Coordinate inspector: pixels <-> normalized coordinates.
- Automatic HUD detection with confidence scores.
- Export/import of a stable project profile format.
- Automated build validation with GitHub Actions.

## Current implementation

- Windows WPF analyzer with ADB detection and screenshot capture.
- HUD candidate detection and normalized coordinate overlay.
- JSON profile generation.
- Android AccessibilityService gesture agent.
- USB ADB reverse TCP bridge with `PING`, `TAP`, and `SWIPE`.
- Serialized bridge transport for concurrent input events.
- Windows low-level keyboard/mouse hooks with WASD, mouse look, fire, aim, jump, crouch, prone, reload, inventory, map, weapon selection, and sprint mappings.

## Important Android limitation

A normal Android application cannot freely inject arbitrary touch events into another app because of Android security boundaries. AccessibilityService gestures are used here as the current experimental transport and must be validated on the target device. Continuous multi-touch behavior and final PUBG latency are not claimed production-ready until tested on the S10 Lite.

## Development order

1. Device detection and connection health. ✓
2. Screen capture and coordinate calibration. ✓
3. Coordinate inspector and manual point calibration. ✓
4. Android touch test. ✓
5. Joystick control prototype. ✓
6. Mouse-look control prototype. ✓
7. Keyboard/mouse mapping bridge prototype. ✓
8. HUD detection and confidence scoring. ✓
9. Automatic profile generation. ✓
10. End-to-end PUBG validation. **Target-device test required.**

## Status

**Phase 5:** desktop real-time input bridge is implemented. The remaining validation step is running the Windows build and Android agent on the Samsung Galaxy S10 Lite and measuring real PUBG behavior.
