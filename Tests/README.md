# Foundation checks

Run `./scripts/run.ps1 -Test` on Windows with Godot 4.7.2 .NET and .NET 8. The script discovers the same tools as the normal launcher. `-Export` also requires all checks to pass before packaging. On a WSL share, use `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/run.ps1 -Test` if necessary.

Unit tests use xUnit and reference the actual game assembly. Run them alone with `dotnet test Tests/Unit/BoomingStore.Tests.csproj`. They cover placement clearance and supported objects, moving display contents, reservations and agreed prices, exactly-once payments, stock exhaustion and cancellation, navigation through doors and around furniture, cache invalidation, save validation, backup recovery, and bounded undo/redo.

Debug-only scene checks run the real controller and shopper state machine inside Godot with `-- --test`. They exercise price edits, undo/redo, restocking, display rotation, save/load, opening, disabled editing during trading, customer purchases, closing, reservation cleanup and financial/stock reconciliation. A deterministic random seed and bounded frame count make failures reproducible. Saves go to `.godot/test-results/store.json`; player saves are never loaded or written. A successful run prints `SCENE_CHECKS passed=...`; an assertion failure exits nonzero, and the script rejects a timeout without the marker.

Scene checks are excluded from release assemblies. Unit test sources are excluded from every game build. Logs and TRX reports are ignored under `.godot/test-results/`. The GitHub workflow runs the same verification command on Windows, including LFS asset checkout.
