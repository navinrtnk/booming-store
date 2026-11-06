# Development

## Runtime and layout

- `StoreGame.cs`: coordinates editing, rendering and the shop lifecycle.
- `StoreData.cs`: catalog, versioned save DTOs and validation.
- `StoreLayout.cs`: placement clearance and supported-object transforms.
- `ShopNavigation.cs`: reusable reachability search and routes on the half-metre grid.
- `StoreTrading.cs`: stock reservations, agreed prices and settled payments.
- `StoreHistory.cs`: bounded undo/redo snapshots.
- `StorePersistence.cs`: validated saves and recovery from the last good backup.
- `Shopper.cs`: customer browsing, checkout and departure state machine.
- `PersonActor.cs`: shared skinned person, route following, gestures and carrying.
- `PersonPose.cs`: restrained idle motion and two-bone arm solving.
- `Shopkeeper.cs`: user-directed walking and physical supply-to-display tasks.
- `RestockOrders.cs`: one-item replacement queue, collection payment, delivery and refund.
- `ShopperMotion.cs`: distance-driven foot trajectories, two-bone leg solving and safe corner rounding.
- `CustomerArrivals.cs`: quiet opening interval, varied arrivals and entrance/capacity backpressure.
- `StoreVisuals.cs`: reusable materials, original furniture, floors, walls and surroundings.
- `Geometry.cs`: attributed MIT mesh helpers adapted from Bartender Specialogist.
- `StoreCamera.cs`: orbit, pan, zoom and ray projection.
- `StoreHud.cs`: native Godot interface and contextual inspector.

Use Godot 4.7.2 .NET with .NET 8 SDK. `dotnet build` treats warnings as errors. `scripts/run.ps1` discovers `GODOT_BIN`, a PATH installation, or optional portable tools under `%LOCALAPPDATA%/BoomingStore/Tools` and the existing Bartender tool directory. No engine or SDK is checked into Git.

Forward+ is the visual target. `-Compatibility` offers a reduced renderer for troubleshooting; screenshots and primary visual review use Forward+. The Windows export is self-contained with .NET runtime files. Export templates matching the engine version must be installed. `VERSION`, project metadata and export metadata are currently 0.1.3.

## Source control

The public source repository is `https://github.com/navinrtnk/booming-store`, with `main` as the default branch. The owner controls commits and publishing; make commits or pushes only when explicitly requested. Keep the root rights notice and third-party licenses intact. A public repository does not change the project's proprietary licensing terms.

Git LFS tracks `.glb`, `.png`, `.jpg` and `.wav`. Install Git LFS and run `git lfs install --local` on a new checkout. Clone with LFS enabled or run `git lfs pull` to obtain binary assets. Builds, caches and downloaded development inputs are ignored. Nothing is staged automatically.

## Assets

All runtime models and textures are present in Assets. No download is required to play. Source manifests record upstream URLs and SHA-256 hashes. Read THIRD_PARTY_NOTICES.md before adding assets; a license allowing compiled game distribution may not permit public source redistribution.

To regenerate characters, use Python 3 with NumPy, run `python scripts/fetch_customer.py`, then `python scripts/prepare_avatars.py`. Downloaded MakeHuman inputs stay in ignored `.tools/customer-source`. The adapted pipeline changes arm placement and preserves standing legs; glTF rigs retain skin weights and facial joints. Character meshes do not contain MakeHuman application code. `python scripts/fetch_materials.py` retrieves the four CC0 4K material sets. These tools are not runtime dependencies.

## Verification policy

Run `scripts/run.ps1 -Test` for unit tests and debug-only Godot integration checks. Exports run the same checks before packaging. Tests reference the actual game rules and exercise the production controller/shopper loop, with isolated saves under `.godot/test-results`. The GitHub workflow runs this command on Windows with LFS assets. See Tests/README.md for scope and commands.

The refactor preserves the art, controls and trading behavior. Catalog lookups use an index; reachability queries reuse one flood-fill search per origin cell; price/color/stock changes rebuild only the affected object; the preview compares value state rather than formatting strings each frame; and speed controls restyle only when speed changes. No visual detail was removed.

## Architectural limits

Navigation conservatively expands floor-object footprints on a 0.5 m grid. It supports walls and open doorways; it does not use physical collision meshes or runtime navmesh baking. Footprint rules remain axis-aligned with quarter-turn rotation. Build mode is deliberately closed-store only so layout changes cannot invalidate active queues. Renderer detail is separate from these route and placement rules. Save schema 2 stores one unit per placement and persistent shopkeeper jobs. Schema 1 migrates stacked units into wholesale cash credits. Actor routes replan when stock changes the navigation revision. Introduce explicit migrations before changing the schema.
