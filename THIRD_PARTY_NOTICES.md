# Third-party notices and provenance

Booming Store is an original project by **Navin Ratnayake (@navinrtnk)**. The root LICENSE applies to new project code and original furniture/design material, except as noted here. External assets retain their own licenses; they are not exclusive property of this project. No EA/The Sims code, models, textures, interface artwork, branding or audio is included.

## Bartender Specialogist · MIT

`Scripts/Geometry.cs`, `scripts/fetch_customer.py`, `scripts/fetch_materials.py`, and `scripts/prepare_avatars.py` are adapted from [Bartender Specialogist](https://github.com/navinrtnk/bartender-specialogist), copyright © 2026 Navin Ratnayake. Its MIT terms are preserved in LICENSES/Bartender-MIT.txt. The avatar pipeline has been adapted for standing shoppers. MIT-covered portions retain that license even though new game code has a separate rights notice.

## MakeHuman graphical data · CC0-1.0

Character geometry, skeletons, skin weights, targets, clothing, hair, eyes, teeth and facial pose inputs come from MakeHuman's CC0 graphical assets. Original authors include the MakeHuman Team, Data Collection AB, Joel Palmius and Jonas Hauquier. See [asset sources](Assets/Customer/SOURCES.md), the URL/hash manifest in Assets/Customer/sources.json, and [MakeHuman licensing](https://www.makehumancommunity.org/about/license.html). No MakeHuman application source is bundled.

## Poly Haven materials · CC0-1.0

White Oak Veneer, Wood Floor, Marble 01 and White Plaster 02 supply 4K photographic albedo, normal and roughness maps. See [asset sources](Assets/Materials/SOURCES.md), Assets/Materials/sources.json and [Poly Haven's license](https://polyhaven.com/license).

## Godot Engine · MIT and bundled dependency licenses

Godot Engine is copyright © 2014–present Godot Engine contributors and © 2007–2014 Juan Linietsky and Ariel Manzur. Its license and dependency notices are included in LICENSES/Godot-LICENSE.txt and LICENSES/Godot-COPYRIGHT.txt. See [Godot licensing](https://godotengine.org/license/). The engine license does not require this game's original code to use MIT.

## Microsoft .NET

Windows builds include .NET runtime files, licensed under MIT and other applicable dependency terms. See LICENSES/Dotnet-LICENSE.txt and LICENSES/Dotnet-THIRD-PARTY-NOTICES.txt, and the [runtime repository](https://github.com/dotnet/runtime). Keep the accompanying runtime notices in distributed builds.

## Development tools

Python and NumPy are used only to prepare assets. Git LFS is used only for development source storage. They are not embedded into the game. The runtime uses Godot's built-in default font; no proprietary system font files are redistributed.
