# Development

## Runtime and layout

- `StoreGame.cs`: coordinates editing, rendering and the shop lifecycle.
- `StoreData.cs`: catalog, versioned save DTOs and validation.
- `StoreLayout.cs`: placement clearance and supported-object transforms.
- `ShopNavigation.cs`: continuous swept clearance, cached connected regions and any-angle Theta* routes on half-metre search nodes.
- `StoreTrading.cs`: stock reservations, agreed prices and settled payments.
- `StoreHistory.cs`: bounded undo/redo snapshots.
- `StorePersistence.cs`: validated saves and recovery from the last good backup.
- `Shopper.cs`: customer browsing, checkout and departure state machine.
- `PersonActor.cs`: shared skinned person, route following, gestures and carrying.
- `PersonPose.cs`: restrained idle motion and two-bone arm solving.
- `PersonExpression.cs`: smoothly blended social gestures and warm reactions.
- `PersonVoice.cs`: per-character speech timing, spatial playback and mouth-envelope sampling.
- `StoreAudio.cs`: bounded spatial effects, character voices, music and pause/ducking.
- `GamePreferences.cs`: local fullscreen/audio preferences, separate from store saves.
- `Shopkeeper.cs`: one playable owner, physical restocking, checkout and task-boundary command priority.
- `KeeperCommand.cs`: the newest pending instruction persisted with the store.
- `RestockOrders.cs`: one-item replacement queue, collection payment, delivery and refund.
- `ShopperMotion.cs`: distance-driven foot trajectories, two-bone leg solving and collision-checked corner rounding.
- `CustomerArrivals.cs`: quiet opening interval, varied arrivals and entrance/capacity backpressure.
- `StoreVisuals.cs`: reusable materials, original furniture, floors, walls and surroundings.
- `Geometry.cs`: attributed MIT mesh helpers adapted from Bartender Specialogist.
- `StoreCamera.cs`: keyboard/mouse views, follow mode, orbit, pan, zoom and ray projection.
- `StoreHud.cs`: native Godot interface and contextual inspector.

Use Godot 4.7.2 .NET with .NET 8 SDK. `dotnet build` treats warnings as errors. `scripts/run.ps1` discovers `GODOT_BIN`, a PATH installation, or optional portable tools under `%LOCALAPPDATA%/BoomingStore/Tools` and the existing Bartender tool directory. No engine or SDK is checked into Git.

Forward+ is the visual target. `-Compatibility` offers a reduced renderer for troubleshooting; screenshots and primary visual review use Forward+. The Windows export is self-contained with .NET runtime files. Export templates matching the engine version must be installed. `VERSION`, project metadata and export metadata are currently 0.1.5.

## Source control

The public source repository is `https://github.com/navinrtnk/booming-store`, with `main` as the default branch. The owner controls commits and publishing; make commits or pushes only when explicitly requested. Keep the root rights notice and third-party licenses intact. A public repository does not change the project's proprietary licensing terms.

Git LFS tracks `.glb`, `.png`, `.jpg` and `.wav`. Install Git LFS and run `git lfs install --local` on a new checkout. Clone with LFS enabled or run `git lfs pull` to obtain binary assets. Builds, caches and downloaded development inputs are ignored. Nothing is staged automatically.

Keep retired assets in `.retired-assets/` when removing them from active use. This recoverable local folder is ignored by Git and excluded from exports; its `.gdignore` also prevents Godot imports. Preserve source/license information alongside any moved assets.

## Assets

All runtime models and textures are present in Assets. No download is required to play. Source manifests record upstream URLs and SHA-256 hashes. Read THIRD_PARTY_NOTICES.md before adding assets; a license allowing compiled game distribution may not permit public source redistribution.

To regenerate characters, use Python 3 with NumPy, run `python scripts/fetch_customer.py`, then `python scripts/prepare_avatars.py`. Downloaded MakeHuman inputs stay in ignored `.tools/customer-source`. The adapted pipeline changes arm placement and preserves standing legs; glTF rigs retain skin weights and facial joints. Use `python scripts/prepare_avatars.py --shopkeeper-only` to rebuild only the owner. Her dress combines the credited CC0 casual top and flared skirt with plain cotton material; her long hair has three bounded tip morphs driven by walking and turning. Character meshes do not contain MakeHuman application code. `python scripts/fetch_materials.py` retrieves the four CC0 4K material sets. These tools are not runtime dependencies.

## Verification policy

Run `scripts/run.ps1 -Test` for unit tests and debug-only Godot integration checks. Exports run the same checks before packaging. Tests reference the actual game rules and exercise the production controller/shopper loop, with isolated saves under `.godot/test-results`. The GitHub workflow runs this command on Windows with LFS assets. See Tests/README.md for scope and commands.

The refactor preserves the art, controls and trading behavior. Catalog lookups use an index; navigation caches connected regions and legal neighbor links per layout revision; price/color/stock changes rebuild only the affected object; the preview compares value state rather than formatting strings each frame; and speed controls restyle only when speed changes. No visual detail was removed.

Character speech scheduling is separate from skeleton animation. The audio controller preloads short cues, reuses fixed effect players, selects their channels without LINQ iterators or per-step clip-name strings, and caches bus indexes for music ducking. Speaker checks loop over the existing shoppers directly. Voice limits, cue selection, pitch variation, gesture timing and the spatial mix are preserved.

## Architectural limits

Navigation conservatively expands floor-object footprints on a 0.5 m grid. It supports walls and open doorways; it does not use physical collision meshes or runtime navmesh baking. Footprint rules remain axis-aligned with quarter-turn rotation. Build mode is closed-store only, with no delivery in progress, so layout edits cannot invalidate active queues or stocking targets. Renderer detail is separate from these route and placement rules. Save schema 2 stores one unit per placement and persistent shopkeeper jobs. Schema 1 migrates stacked units into wholesale cash credits. Backward-compatible optional fields preserve the newest pending owner command without restoring the removed automatic-checkout behavior. Old `AutoCheckout` fields are ignored. Current payment/shoppers remain transient: loading closes the shop. Actor routes replan when stock changes the navigation revision. Introduce explicit migrations before changing the schema.

## Audio preparation

`python scripts/generate_audio.py` uses an isolated development venv with kokoro-onnx 0.4.7, NumPy, SciPy and soundfile. Pinned model and voice-embedding URLs/hashes are in Assets/Audio/sources.json; downloads stay in ignored .tools/audio-source. The script creates original effects/music, fictional vocal bites and mouth envelopes. The app bundles only the resulting WAVs and JSON, with no neural inference dependency. See Assets/Audio/SOURCES.md for licenses and regeneration details.

`StoreGame.QuitCleanly` stops players and briefly lets the audio thread drain before closing, including debug scene checks. This avoids the engine's asynchronous playback references remaining alive at immediate exit. Audio settings use user://preferences.json; automated tests use .godot/test-results/preferences.json.
