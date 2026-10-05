# Reference-faithful walking and toy-chase running

> For agentic workers: use scoped parallel work for raster assets and core motion contracts; root integrates WPF and verifies the release.

**Goal:** Restore the actual reference dog's likeness in both walking directions and introduce distinct running cycles for ball/frisbee chasing.

**Architecture:** Append directional running states and clips without changing existing enum values. Toy simulation selects running; cursor following, returning home and autonomous wandering retain walking. AI-authored frames share one reference-derived character anchor and are packaged with true alpha.

**Tech stack:** .NET 10, WPF, dependency-free offline/Windows checks, ImageGen, Pillow for atlas slicing and previews only.

**Spec:** User's current request; `Assets/Pet/pet.jpg` is primary identity and the three-view image is supporting anatomy. Preserve cloudlike large head/cheeks/ears, curled white coat, warm muzzle, black eyes/nose, red patterned collar and yellow bell. Avoid the trimmed generic dog in P5.

## Constraints and review focus

- Keep originals, previous movement assets and P5 release; source backup is `artifacts/verification/p6-source-clean-before.zip`.
- Running must include gather, propulsion, airborne and landing poses; do not substitute faster walking or software-deformed limbs.
- Existing custom skins without running folders must fall back to the corresponding walk, generic walk, idle or default image.
- Toy direction changes, cancellation, dragging, hiding and challenge completion must leave no stranded running state.
- Keep each direction's camera scale stable; retain running airborne height rather than forcing every pose onto the ground.
- Preserve settings/profile/diary and publish a fresh P6 folder without stale animation frames.

## Tasks

- [x] Art: inspect both references, generate and review an identity anchor before four 8-frame cycles (WalkLeft/Right at 10 fps; RunLeft/Right at 12 fps); record prompts, create composite contact sheets/GIFs and validate alpha/framing.
- [x] Core: add failing movement/mapping/fallback checks, append RunningLeft/Right and RunLeft/Right, implement completion/cancellation and compatible loader fallback, run offline checks (194 core, 29 skin, 83 journal checks passed).
- [x] WPF: add failing checks for real ball/frisbee/challenge running and walking-only home/follow behavior; integrate world state selection and exclude running from synthetic breathing transforms (41 focused authored-playback checks and 151 full Windows checks passed).
- [x] Verify all offline/Windows checks and rendered movement previews; independently review the changes and resolve actionable findings.
- [x] Publish version 0.6.0 to `artifacts/publish/p6-win-x64`, verify source/runtime asset hashes and private runtime/startup, package ZIP, leave the verified pet running and document evidence.
