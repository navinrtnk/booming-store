# Booming Store

A customizable 3D electronics store built with **Godot 4.7.2 .NET and C#**.

**Original project by [Navin Ratnayake (@navinrtnk)](https://github.com/navinrtnk).** Copyright © 2026 Navin Ratnayake. [Rights and licensing](LICENSE).

![Electronics shelving, pictured catalog and coordinated checkout](docs/store-overview.png)

Start with an empty shop and $10,000. Build multi-level shelving, arrange tiered TVs, computers and audio products, and set prices. The pictured catalog renders the actual models. Displays empty at pickup; shoppers usually buy one item and occasionally collect a small basket. Hire up to three employees into restocking, sales or cashier roles. Summon/dismiss shifts at $100 per store hour, rounded to 15-minute increments; the owner is unpaid. Two registers coordinate their checkout lines. Traffic rises in afternoons/evenings, falls overnight and responds to recent customer moods. Long queues lower morale; abandoned goods return at cost. Saves preserve stock, shifts, payroll, clock and reputation.

Install **Godot 4.7.2 .NET**, **.NET 8 SDK** and **Git LFS**. Clone with LFS assets, open `project.godot`, build, and press **F5**. On Windows, `./scripts/run.ps1` builds and launches; `-Test` runs unit/headless checks and `-Export` creates a versioned Windows ZIP. Extract the entire ZIP before running `BoomingStore.exe`.

If Windows blocks scripts on the WSL share, run `pwsh -NoProfile -ExecutionPolicy Bypass -File ./scripts/run.ps1` for this invocation.

Use **Game** for save/load, archives and a new electronics store; existing saves are preserved. **Staff** hires, assigns, summons, clocks out and fires employees. **B** cycles shelf decks while placing goods. Click floor or use **I/J/K/L** to move the owner. **C** mans a free register; select a counter to open/close it or work there. **Tab** hides side/bottom panels; **F11 / Alt+Enter** toggles fullscreen. **Arrows/Q/E** orbit, **WASD** pans, **wheel/+/-** zooms, **1/2/3** choose views, **F** follows, **Home** resets. **R** rotates; **Esc** cancels; **Space** pauses; **F5/F9** save/load; **M** mutes; **Ctrl+Z/Y** undo/redo; **F12** captures a screenshot.

See [electronics gameplay and limits](docs/ELECTRONICS.md), [development](docs/DEVELOPMENT.md), [tests](Tests/README.md) and [asset credits](THIRD_PARTY_NOTICES.md).
