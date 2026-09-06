# Architecture

## High-level pipeline

```text
Android device
   │ USB
   ▼
Transport / Device Agent
   │
   ├── screen frames
   ├── device metrics
   └── input commands
   ▼
Windows Controller
   │
   ├── keyboard + mouse hooks
   ├── coordinate calibration
   ├── HUD detector
   ├── mapping engine
   └── validator
   ▼
Game Profile
```

## Design rules

1. Keep the coordinate system normalized so profiles survive resolution changes.
2. Keep joystick and mouse-look as first-class input primitives instead of ordinary button mappings.
3. Separate detection from execution: a detected point is not automatically trusted until its confidence passes validation.
4. Every automated mapping must be inspectable and manually correctable.
5. Device/game-specific data belongs in profiles, not in the core controller.
6. The first release should use deterministic image/geometry detection before adding heavier ML dependencies.

## Validation states

- `PASS` — verified by a live device test.
- `WARNING` — detected but below the preferred confidence threshold.
- `FAIL` — missing, inconsistent, or failed during live input validation.

## Security boundary

Android normally prevents a regular app from injecting arbitrary touches into another application. The agent/transport implementation must therefore use an explicitly supported mechanism and prove it works on the target Android version before the feature is marked stable.
