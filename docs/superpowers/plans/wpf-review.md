# WPF independent review

Date: 2026-09-11

Reviewed the current uncommitted filesystem under `src/DesktopPet` and `scripts`, against `docs/superpowers/specs/2026-09-11-desktop-pet-design.md` and `docs/superpowers/plans/core-brief.md`. Core implementation/stubs were excluded as requested. The review did not start the GUI, run builds, or change application code.

## Actionable findings

No reproducible correctness defects identified in the reviewed code.

## Coverage

- Pointer interactions: 5 DIP drag threshold; captured mouse movement; click suppression following drag; loss of capture; clearing the pointer marker before releasing capture; delayed behavior resumption after pointer release.
- Behavior conflicts: decision and movement timer cancellation; temporary suspension during pointer/menu interactions; hidden state scheduling guards; pause preference and drag state contract; settings changes stopping existing movement before applying size/speed/state updates.
- WPF initialization: settings event handlers guarded during XAML initialization and programmatic reload; packaged image URI and Resource inclusion; fixed window/image sizing before restoring position; initially hidden application state.
- Geometry and DPI: primary work area and WPF DIP use; system-DPI manifest; screen-pixel pointer conversion; position clamping after dragging, scale changes, and work-area changes. The image is currently square (3024 × 3024); the standard 80–200% scale range fits the declared animation padding.
- Persistence integration: delayed saves restart their timer; saves capture live window coordinates; hide/exit and settings-window close flush settings; settings changes notify the settings UI and tray.
- Lifecycle: explicit shutdown; single-instance mutex ownership; main-window close converted to hide; independent disposal of controller, tray, settings, pet window, and display subscriptions; animation/timer cancellation before closing the window.
- Tray: dispatcher marshaling, visibility/pause state updates, double-click show, and disposal of NotifyIcon/menu/cloned icon.
- Scripts/project: local SDK setup, official SDK archive hash verification, build/test exit-code propagation, self-contained x64 publish, and expected EXE presence validation.

This is a static review. Runtime resource loading, native window behavior, real dragging, shell tray interactions, and display/DPI transitions still require the primary agent's build and GUI verification; no GUI-pass claim is made here.
