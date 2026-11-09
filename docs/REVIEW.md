# Electronics review · 0.1.7

Reviewed on Windows with Godot 4.7.2 .NET and Intel Graphics.

- Build: zero warnings/errors, nullable checking and warnings-as-errors retained.
- 158 Godot scene checks passed, retaining movement, camera, audio, stock, command order, morale and save/load regressions. New checks cover empty $10,000 starts, shelf clearance and picking lower-shelf products, baskets, clock/payroll pause, nearest-quarter billing and saved paid blocks, wage arrears, recent reputation, summoned workers, coordinated restock positions and archive preservation.
- 127 unit cases passed, including electronics tiers, multi-level support, payroll rounding, traffic/reputation and atomic basket settlement. The standalone runner succeeded in this review.
- Six live shoppers queued at one register, then split evenly with the first waiting shopper transferred when register two opened. Closure merged waiters. Two employees claimed different counters and paid customers in parallel. Closing during payment preserved that transaction; all six baskets paid exactly once. Owner auto-opening and persistent manual closure were verified.
- Movement redirects retain speed instead of repeatedly stopping. Reachability checks stop after finding the nearest valid work point; stand probes avoid per-call LINQ closures. Art and walking poses remain intact.
- The final cleanup separates payroll from the clock, names employee work stages, removes temporary line lists/sorting from per-frame lookups, and skips empty return processing. The full 127-unit/158-scene suite passed again, including parallel checkout, line redistribution, wages and saved employee deliveries.

Morale uses active real time; payroll uses store hours (two real minutes per hour at 1×). Only summoned employees receive wages; the owner is unpaid. Hiring is free. Staffing and prices remain prototype balance. See ELECTRONICS.md for current controls and limits. Crowded custom layouts and other GPUs need wider playtesting.

Forward+ review showed pictured catalog cards, both TV shelf levels, audio/computer shelving, physical register signs, staffed trading, the empty start and compact view. A clipped shift button found in screenshot review received an explicit width. The existing TAA/MSAA lighting and credited skinned characters are retained. Catalog pictures render actual models in one cached studio viewport.
