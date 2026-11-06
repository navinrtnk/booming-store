# Booming Store

A customizable 3D shop built with **Godot 4.7.2 .NET and C#**.

**Original project by [Navin Ratnayake (@navinrtnk)](https://github.com/navinrtnk).** Copyright © 2026 Navin Ratnayake. [Rights and licensing](LICENSE).

![The editable shop and its collection](docs/store-overview.png)

Start with a furnished boutique and $9,000. Draw walls, add doors and windows, change floors, place and recolor furniture, and arrange products on displays. Set prices, open the shop, and watch customers stroll between displays, take their time browsing, queue and purchase. Move your shopkeeper around and click sold-out spots to have her collect and carry back one replacement. Each visible object is one item. Close to redesign. Undo/redo and local save/load preserve your layout, stock and settled finances.

Install **Godot 4.7.2 .NET** and **.NET 8**, open `project.godot`, build, and press **F5**. On Windows, `./scripts/run.ps1` builds and launches; `-BuildOnly` checks the build/import; `-Test` runs unit and headless gameplay checks; `-Export` verifies and creates a versioned Windows ZIP. Extract the entire ZIP before running `BoomingStore.exe`.

If Windows blocks scripts on the WSL share, run `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/run.ps1` for this invocation.

**Click clear floor** or use **I/J/K/L** to move her. **Arrow keys** change the camera angle; **1/2/3** select views; **F** follows her. **WASD** pans; **right-drag** or **Q/E** orbits; **wheel** or **+/−** zooms; **Home** resets the view. Click an object to inspect it. **R** rotates; **Esc** cancels; **Delete** removes the selected object; **Ctrl+Z/Y** undo/redo; **Space** pauses; **F5/F9** save/load; **F12** captures a screenshot.

See [gameplay and limitations](docs/GAMEPLAY.md), [development](docs/DEVELOPMENT.md), and [asset credits](THIRD_PARTY_NOTICES.md). See [automated checks](Tests/README.md) for test coverage and commands.
