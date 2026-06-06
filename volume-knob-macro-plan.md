# Volume Knob Macro Wheel (GTA 5 Style)

## Goal
Build a macro application where turning the volume knob triggers a GTA 5 style radial menu overlay built in C# (Frontend) and controlled by AutoHotkey (Backend).

## Tasks
- [ ] Task 1: Initialize C# WPF project for a transparent, always-on-top overlay window → Verify: App builds and shows a transparent window.
- [ ] Task 2: Design the radial UI wheel in C# to mimic the GTA 5 weapon switch → Verify: UI displays a circular menu with selectable slots.
- [ ] Task 3: Implement Inter-Process Communication (e.g., Named Pipes) in C# to listen for AHK signals → Verify: C# app can receive test messages from a generic script.
- [ ] Task 4: Write AHK script to intercept Volume Up/Volume Down events → Verify: Turning the volume knob triggers an AHK tooltip instead of changing system volume.
- [ ] Task 5: Link AHK script to send navigation signals (Next/Prev slot) to the C# app via Named Pipes → Verify: Turning the volume knob visually navigates the C# UI wheel.
- [ ] Task 6: Implement selection logic (e.g., timeout after X milliseconds of no turn) to trigger the selected macro → Verify: Desired macro action executes upon selection and the wheel hides.

## Done When
- [ ] Turning the volume knob opens/shows the C# radial UI.
- [ ] Continuing to turn the knob rotates through the available macro options.
- [ ] Stopping the turn for a brief moment selects an option and executes the corresponding macro via AHK.
- [ ] Only the volume knob is used (no other keys involved).

## Notes
- Since we only use the volume knob, we'll need a timeout-based selection mechanism (e.g., 500ms after the last volume tick) to confirm the selection and hide the UI.
- AHK must block the native volume change behavior while the macro mode is active, or entirely replace it.
