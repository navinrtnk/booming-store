# Foundation review · 0.1.1

The approved foundation now has automated unit and engine integration checks in addition to the original manual review.

Observed on Windows using Godot 4.7.2 .NET, Forward+, and Intel Graphics:

- C# build completed with zero warnings and zero errors; Godot imported the runtime scenes and assets successfully.
- Opening the starter shop admitted shoppers. Customers browsed, queued, paid and left with shopping bags.
- A completed day produced 20 sales, $1,743 revenue and $846 wholesale cost of goods sold: $897 gross profit. Cash moved from $9,000 to $10,743.
- Closing stopped arrivals and allowed remaining shoppers to complete their visits, then re-enabled editing.
- A product price change was reversed through Ctrl+Z.
- Attempting to place a vase over an occupied display space was rejected without changing cash. Placing a vase in a clear space purchased three units for $54.
- Saving and loading restored that new object with three units at $38 retail and preserved $10,689 cash and the completed day's ledger.
- Dragging a new four-metre partition charged $100; undo removed the partition and restored the $9,000 starter balance in a fresh session.
- The camera's close-up view showed the skinned cashier, textured clothing, anatomical face and hands. Surface noise was reduced and the interface was adjusted after screenshot review.
- All 12 PBR material files and all 53 cached MakeHuman source inputs matched the SHA-256 hashes recorded in their provenance manifests.
- 50 unit cases passed, covering placement, trading reservations and payments, navigation, save validation/recovery and undo/redo.
- 25 headless Godot integration assertions passed against the real controller and shoppers, including five completed purchases and reconciled stock, cash and wholesale costs.
- The test command isolates saves from player data, detects failures/timeouts, and runs before Windows packaging. The same command is configured in GitHub Actions.

Known limits are documented in GAMEPLAY.md. This review is not evidence of exhaustive coverage. In particular, stress cases involving hundreds of objects, unusual queue layouts, every furniture combination, other GPUs and every possible user-created store remain future validation work.
