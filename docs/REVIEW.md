# Foundation review · 0.1.4

Reviewed on Windows with Godot 4.7.2 .NET, Forward+ and Intel Graphics.

- The C# build completed with zero warnings and zero errors. All runtime assets imported successfully, including the new skinned shopkeeper and her three hair-tip blend shapes.
- 100 unit cases passed. New cases cover direct diagonal routes to exact destinations, body clearance at furniture corners and door posts, blocked partitions, valid positions between search nodes, travel-cost display approaches, and smoothing that cannot discard a protective shallow turn.
- 66 headless assertions passed using the real controller and customers. Four purchases produced $440, with settled cash, single-item stock and wholesale cost reconciled after closing.
- The starter scene contained one worker. Customers formed a real line; the shopkeeper returned to serve them. Two commands during payment retained only the newest one. She finished exactly the current sale, followed that command, and left the other lined-up customers waiting.
- Ordinary walking redirected immediately and included diagonal motion. The opening quiet period, three-customer maximum, browsing dwell, keyboard views, follow mode and camera limits remained covered.
- A physical restock still required a command, travel to supplies, one wholesale charge and delivery. A newer walking command waited for that delivery, survived saving while she carried the carton, and began as soon as placement finished. Pausing stopped the task; saving/loading did not charge twice.
- A Forward+ preview completed a real chair sale and replacement, then returned the owner to checkout. The revised character retains the former cashier's face, with a sage casual dress, slimmer tan flats, a warmer smile and gently moving long hair. Existing customers, store geometry and textures remain intact.
- Additional CC0 garment inputs have recorded source URLs and SHA-256 hashes. Creator credit, root rights notice and upstream notices are preserved. The tailored dress and hair remain adapted CC0 graphics; the MIT asset pipeline keeps its license.
- Automated saves stay under `.godot/test-results`, and visual captures under ignored `.tools`. Unit sources and debug checks/previews are excluded from release assemblies. Export requires the same checks to pass.

Routes use any-angle search on half-metre nodes with continuous swept clearance, rather than a global exact geometric shortest-path guarantee. Animations remain procedural. Very narrow or crowded custom layouts, large object counts and other GPUs need broader playtesting. See GAMEPLAY.md for the implemented behavior and limits.
