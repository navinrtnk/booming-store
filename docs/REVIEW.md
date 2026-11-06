# Foundation review · 0.1.3

Reviewed on Windows with Godot 4.7.2 .NET, Forward+ and Intel Graphics.

- The C# build completed with zero warnings and zero errors; Godot imported the character scenes and assets successfully.
- 86 unit cases passed, covering placement, reservations, settled payments, navigation, persistence/recovery, history, walking, arrivals, relaxed poses and physical restocking rules.
- 50 headless Godot assertions passed using the real controller and shoppers. The session completed five purchases for $474, drained the queue after closing, and reconciled single-item stock, cash, retail revenue and wholesale cost.
- Scene checks enforced the quiet opening, three-guest maximum and minimum browsing time. They walked the shopkeeper, verified a single keyboard movement tap, switched views/follow mode and checked camera limits.
- A sold object's mesh disappeared, its empty spot stayed empty without a command, and clicking either its center label or outer floor outline queued a replacement. Canceling an uncollected task left cash and stock unchanged. Pausing stopped the task. The shopkeeper physically visited the supply cabinet, paid once, carried the item, resumed after save/load and delivered one unit at the display. No restock button remained in the object's inspector.
- Unit checks exercised multiple queued spots, insufficient cash, invalid jobs, exactly-once delivery, cancellation refunds and conversion of old stacked stock into wholesale cash credits.
- Forward+ captures showed the updated idle stance, lower resting wrists, softly bent arms, curled fingers, carton carrying and placement. A rendered preview waited for a real customer purchase, then completed a physical chair replacement while trading.
- Manual input review confirmed clicking an empty display, close and overhead keyboard views, follow mode and movement on clear floor. The latest rendered overview is the README's single image.
- Original character meshes, material textures, creator credit, third-party notices and provenance were retained. The new supply cabinet and carton use original procedural geometry.
- Tests use isolated saves under `.godot/test-results`; rendered review captures stay under ignored `.tools`. Debug review/check code and unit sources are excluded from release assemblies. Export runs the same automated checks before packaging.

The animations remain a procedural foundation rather than a final authored animation library. Unusual user-created layouts, very narrow aisles, large object counts and other GPUs need broader playtesting. See GAMEPLAY.md for current limits.
