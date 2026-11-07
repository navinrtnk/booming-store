# Foundation checks

Run `./scripts/run.ps1 -Test` on Windows with Godot 4.7.2 .NET and .NET 8. The script discovers the same tools as the normal launcher. `-Export` also requires all checks to pass before packaging. On a WSL share, use `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/run.ps1 -Test` if necessary.

Unit tests use xUnit and reference the actual game assembly. Run them alone with `dotnet test Tests/Unit/BoomingStore.Tests.csproj`. They cover placement clearance and supported objects, moving display contents, reservations and agreed prices, exactly-once payments, stock exhaustion and cancellation, navigation through doors and around furniture, cache invalidation, save validation, backup recovery, and bounded undo/redo.

Debug-only scene checks run the real controller and shopper state machine inside Godot with `-- --test`. They exercise price edits, undo/redo, restocking, display rotation, save/load, opening, disabled editing during trading, customer purchases, closing, reservation cleanup and financial/stock reconciliation. A deterministic random seed and bounded frame count make failures reproducible. Saves go to `.godot/test-results/store.json`; player saves are never loaded or written. A successful run prints `SCENE_CHECKS passed=...`; an assertion failure exits nonzero, and the script rejects a timeout without the marker.

Scene checks are excluded from release assemblies. Unit test sources are excluded from every game build. Logs and TRX reports are ignored under `.godot/test-results/`. The GitHub workflow runs the same verification command on Windows, including LFS asset checkout.

Walking checks sample foot planting over a stride, swing clearance, alternating legs, forward knee bend, ankle orientation, different body sizes, counter-swinging relaxed arms, rounded-corner containment and customer arrival timing. Scene checks also enforce the opening quiet period, three-guest limit and minimum browsing dwell.

For a rendered review, use a debug build and run Godot with `--fixed-fps 30 --write-movie .tools/walking-review.avi -- --preview-walk`. This opens an isolated starter shop, follows its first real customer, captures stride frames, fast-forwards the opening wait, then exits after eight seconds of the walking review. Review outputs stay in ignored `.tools/walk-review/`; this mode and all scene-check code are excluded from release assemblies.

Shopkeeper checks cover queued one-item delivery, exactly-once collection payment, cancellation refunds, insufficient cash, carrying across save/load, invalid jobs and legacy stock migration. Pose checks bound idle motion and solve arm targets. Scene checks walk the main character, switch keyboard views, verify sold meshes disappear, require explicit restock commands, and save/load during physical delivery.

For a rendered restocking review, run a debug build with `--fixed-fps 30 -- --preview-owner`. It waits for a real sale, follows the shopkeeper's collection and delivery, and captures idle, carrying, placing and overview images under ignored `.tools/owner-review/`. Add `--keep-review-open` for manual camera/input review. This mode uses isolated saves and is excluded from release builds.

Any-angle route cases check direct diagonal travel, exact destinations, swept body clearance at corners and door posts, blocked partitions, valid positions between search nodes, travel-cost approach selection and safe smoothing of shallow turns. Scene checks require one worker, verify her outfit/hair rig, and let a real checkout line form: the current customer finishes, only the newest next command runs, and other queued customers wait. A pending command survives saving during a delivery and starts when that delivery finishes. Walking redirects immediately.

Add `--capture-route` to `--preview-walk` to save every second rendered frame under ignored `.tools/route-review/` for a short 15 fps route-review clip. The viewport follows a real customer; this capture option is debug-only.

Presentation checks cover preference persistence/validation independent of finances, removal of legacy automatic checkout, bounded social gesture blending, mouth-envelope interpolation, WAV hashes/format/duration/levels, and all 20 vocal envelopes. Scene checks keep the owner away from a real waiting line before an explicit checkout command, exercise fullscreen/mute preferences in an isolated location, and confirm live arrival, checkout, stocking, voice and footstep dispatch. Native review verifies the actual fullscreen/windowed transition and Audio dialog.

Audio regression checks trigger simultaneous character reactions, enforce the two-voice limit, freeze mouth movement during pause, and saturate footstep/event channels to verify that checkout cues still play. Paused stores reject new effect playback.
