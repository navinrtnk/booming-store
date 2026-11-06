# First milestone · 0.1.0

The furnished starter lot is 12 × 10 metres. The initial fixtures, collection and building are provided with $9,000 in cash. There are no character needs, rent, taxes, wages or financial penalties in this first milestone.

## Design

Choose a collection category on the left, then Place. A green outline means the object fits; red means it collides or is outside the lot. Floor objects snap to 25 cm; small objects snap to 10 cm on display surfaces. Rotate in 90° steps with R, and cancel with Esc. Objects stay selected after a move; purchases allow repeated placement until canceled. Selecting a color changes the selected object or the next placement/wall color.

Small objects can sit on the oak table, stone plinth, or gallery shelf's middle shelf. Other shelf levels are decorative in this milestone. Moving or rotating a display carries its contents. Removing a display removes its contents and refunds 70% of their combined remaining stock cost. Copy duplicates the selected object, not its contents. Rugs allow furniture above them.

Drag across the floor with Walls, Windows or Doors to create straight 1 m segments. A short click creates one segment. Segments replace existing sections. Erase wall removes the nearest section. Doorways remain open for navigation. Floor finishes cost $8 per tile; select Oak, Marble or Dark tile in the inspector. Cutaway hides near walls as the camera orbits. The floor area itself is fixed in this milestone.

Select objects to move, rotate, recolor, copy, change price, mark for sale, restock or remove. Objects include three units when bought from the catalog; fixtures include one. The starter shop's merchandise has five units per product placement. Stock is an abstract quantity attached to that displayed object. Out-of-stock displays remain as faded placeholders for easy restocking.

The checkout and occupied displays stay fixtures. Empty displays, rugs and other furniture can be marked for sale. Keep exactly one checkout with a clear customer approach on its front side. A cashier is supplied automatically. Rename the store in the right panel; its sign appears on an intact rear wall.

## Trade

Open store checks the route to checkout and that at least one stocked product is reachable. Up to six shoppers visit at once. They choose accessible products, examine them, evaluate the price against suggested retail, and either browse elsewhere, leave, or join the checkout queue. Prices agreed when entering the queue are honored at payment. Goods are reserved while queued; money and stock change only when payment finishes.

The cashier serves automatically. Shoppers have anatomically proportioned, skinned MakeHuman models with skin and clothing textures, procedural walking, arm gestures and facial poses. Animations and furniture are an initial art pass; these are not final production-quality characters or a complete character customization system. Purchases are represented by a shopping bag rather than a separate carry animation for every product.

Close store stops new arrivals. Existing shoppers finish before editing resumes, then the game shows daily sales, revenue and gross profit and saves automatically. Gross profit means retail revenue minus wholesale cost of sold items; it excludes building and furnishing purchases. Cash changes when buying/restocking inventory and when collecting payments. No time limit forces closing; the clock is illustrative. Trading starts a new day each time the doors reopen.

Layout edits are disabled during trading and while shoppers finish. Pause and 3× speed work during trading. Undo/redo applies to closed-store edits; opening starts a new trading ledger and clears edit history.

## Save and load

Save uses `user://store.json` and keeps the preceding save as `store.json.bak`. On Windows this is normally `%APPDATA%/Godot/app_userdata/Booming Store/`. The game restores a valid save on startup, falling back to the backup if necessary. Load replaces the current session with the saved state, closes the store, and clears unpaid shopping carts. Layout, finishes, store name, stock, cash, cumulative sales, and daily financial totals are preserved. Camera position, speed and transient shoppers are not saved. Loading through the button asks for confirmation; F9 loads immediately.

F12 writes `store-screenshot.png` alongside the save. There is no cloud save, multiplayer, external model import, staff management, mod loader, multiple floors, or expandable lot yet.
