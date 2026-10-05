# P5 visual and play improvements

User intent: replace unattractive ball/frisbee visuals and left/right walking, preserving the identity in Assets/Pet/pet.jpg and 比熊犬三视图参考图.png, and add unexpected useful play features. Existing animation/world/menu/settings flows remain the foundation. User has authorized implementation; optional preference question defaults to fun interaction.

Ruling: no Git repository; source snapshot artifacts/verification/p5-source-clean-before.zip and distinct P5 output preserve rollback. Prior P4 stays available.

1. Walking art: independent agent creates genuine-alpha 8-frame cycles for both directions using built-in ImageGen, preserving camera, body scale and baseline. Generated sheets/prompts/previews saved; old runtime frames moved into artifact backup before replacing with only new frames.
2. Toy art: independent agent owns only ToyVisualFactory.cs. WPF vector rubber ball and silicone frisbee retain the 32 DIP simulation footprint. Root integrates and renders actual-size/zoom previews.
3. New play: root implements bounded 30-second cursor-follow walk, finite three-round fetch challenge with progress/cancel, and PNG pet portrait export. User explicitly requested new features; avoid additional confirmation. Features use existing world timer/state/menu/settings; no network, no new package dependencies.
4. Verification: meaningful WPF regressions cover movement/cancellation, finite challenge/cancellation, export decoding/filename collisions, menu/settings events, original world behavior. Inspect generated animation previews and WPF toy/settings/portrait renders. Run existing offline and Windows checks, retain logs.
5. Review and delivery: fresh read-only reviewer, fix important findings with regression evidence. Version 0.5.0; publish separate P5 directory and ZIP, verify assets/private runtime/duplicate reveal, preserve current settings/profile. Leave P5 running.

Review focus: follow dead zone and leaving current display; challenge timers stopped by menu/hide/fullscreen/settings/dispose; optional bubbles/paused behavior; generated-frame alpha and loop jitter; portrait excludes desktop and does not overwrite; existing custom skins still function.

Progress: source snapshot complete. Toy vector factory and isolated render checks completed and root inspected the preview. Built-in ImageGen generated selected left/right sheets; runtime directories contain 8 frames each, original 96 frames each are archived, feet registered to y=228. Root inspected both contact sheets; natural head bob and mild generated fur variation remain.

Play implementation complete. Valid failing-to-passing logs: p5-play-red.log / p5-play-green-initial.log (all3newfeatures), p5-menu-red.log / p5-menu-green.log (challenge start from open pet menu), p5-cancel-red.log / p5-cancel-green.log (disabled animation cancellation between rounds). Expanded real WPF verification p5-play-expanded.log passes21, including3actualsimulationcatches andPNGdecode/collision checks. Fresh read-only reviewer found no Critical/Important or material minor issues. Full suite and final P5 package checks follow.

Ruling: clean WPF output assets before verification because CopyToOutputDirectory preserves stale files after source frame counts shrink. Final release uses fresh staging, preserving prior P4 directory.

Final verification: full offline script passed Core113/Skin28/Journal83/WPF110, total334. Latest Windows run p5-final-windows.log also passes110 after wiring the preview fixture to real status notifications. Debug output was cleaned as well. Root visually inspected both direction contact sheets, vector props at native/enlarged size, settings-surprises.png, pet-postcard.png and transparent portrait. Fresh P5 publish/private-runtime/asset/ZIP/duplicate-instance checks passed; actual P5 remains running. Exact metadata and original user settings/profile/diary preservation are recorded in p5-release-metadata.json and p5-user-migration.json.
