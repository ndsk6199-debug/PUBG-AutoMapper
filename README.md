# PUBG AutoMapper

A from-scratch Windows + Android toolkit for controlling PUBG Mobile on a real Android phone with keyboard and mouse, while providing coordinate calibration and automatic HUD mapping.

## Project goals

- Real Android device; no Android emulator.
- USB-first low-latency transport.
- WASD movement through the real on-screen joystick.
- Mouse capture and continuous camera control.
- Keyboard/mouse actions mapped to calibrated HUD coordinates.
- Coordinate inspector: pixels <-> normalized coordinates.
- Automatic HUD detection with confidence scores.
- Export/import of a stable project profile format.
- Automated build validation with GitHub Actions.

## Important Android limitation

A normal Android application cannot freely inject arbitrary touch events into another app because of Android security boundaries. The project therefore treats input injection as a dedicated transport/agent problem and will validate the selected mechanism on the target device before calling the system production-ready.

## Planned components

- `desktop/` — Windows controller, calibration UI, input hooks, profile engine.
- `android/` — Android companion/agent.
- `profiles/` — device/game profiles.
- `docs/` — architecture and protocol documentation.

## Development order

1. Device detection and connection health.
2. Screen capture and coordinate calibration.
3. Coordinate inspector and manual point calibration.
4. Android touch test.
5. Joystick control.
6. Mouse-look control.
7. Keyboard/mouse mapping engine.
8. HUD detection and confidence scoring.
9. Automatic profile generation.
10. End-to-end PUBG validation.

## Status

**Bootstrap phase:** repository created and initial project specification committed. The first implementation milestone is the calibration + device test harness.
