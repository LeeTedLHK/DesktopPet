# P7 motion and fetch design

User authorized starting the recommended improvements 1, 2, 4 and 6. Optional scope question remains open; this design covers those four.

Preserve the actual bichon identity from `Assets/Pet/pet.jpg`, the three-view reference, and the approved P6 IdentityAnchor. Replace the remaining mismatched base actions, preserving P6 directional walking/running. New bitmap assets have genuine generated alpha, square 256px frames, whole-frame packing only, and review contacts on dark backgrounds.

Movement presentation plays start, stop and direction-change clips without blocking the existing behavior state machine. Interrupted, hidden, disabled and skin-replaced animations cannot restart old movement. Missing transition clips are skipped; other missing poses use same-direction fallback.

Fetch follows a single ground line. A ball settles then gets picked up with a bend; an accessible frisbee is caught during a bounded jump. The toy attaches to the mouth during returning, follows the dog to the throw origin, drops on the ground and remains briefly visible. Only completed delivery awards play credit or advances the three-round challenge. Cancellation immediately releases all phase timers and toy visibility, respecting dragging/hiding/interaction state.

Ground chase, bounded jumps, mouth positions, return destination and phase progression are deterministic Core logic. WPF owns rendering and dispatcher clocks. Ground and carry positions are clamped to the selected activity work area; narrow and negative-origin monitors remain supported. Pose phase timing respects animation speed with finite watchdogs; physical jumping intentionally stays between .28 and .7 seconds, and pose frames follow its normalized progress even at slow settings. Keep settings/profile/journal and old release artifacts, produce a separate 0.7.0 portable release.
