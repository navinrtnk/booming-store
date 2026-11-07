# First milestone · 0.1.5

The furnished starter lot is 12 × 10 metres. The initial fixtures, collection and building are provided with $9,000 in cash. There are no character needs, rent, taxes, wages or financial penalties in this first milestone.

## Design

Choose a collection category on the left, then Place. A green outline means the object fits; red means it collides or is outside the lot. Floor objects snap to 25 cm; small objects snap to 10 cm on display surfaces. Rotate in 90° steps with R, and cancel with Esc. Objects stay selected after a move; purchases allow repeated placement until canceled. Selecting a color changes the selected object or the next placement/wall color.

Small objects can sit on the oak table, stone plinth, or gallery shelf's middle shelf. Other shelf levels are decorative in this milestone. Moving or rotating a display carries its contents. Removing a display removes its contents and refunds 70% of their combined remaining stock cost. Copy duplicates the selected object, not its contents. Rugs allow furniture above them.

Drag across the floor with Walls, Windows or Doors to create straight 1 m segments. A short click creates one segment. Segments replace existing sections. Erase wall removes the nearest section. Doorways remain open for navigation. Floor finishes cost $8 per tile; select Oak, Marble or Dark tile in the inspector. Cutaway hides near walls as the camera orbits. The floor area itself is fixed in this milestone.

Select objects to move, rotate, recolor, copy, change price, mark for sale or remove. Each placement costs one wholesale unit and shows exactly one object. A purchase removes that object, leaving an empty outline that remembers its design and price. There is no hidden stack and no restock action in the object's menu.

## Your shopkeeper

Click clear floor space to send her walking, or use I/J/K/L to move relative to the camera. Arrow keys orbit and tilt; Q/E also orbit. Keys 1, 2 and 3 switch between overview, close and overhead views; F toggles following her. WASD pans, wheel or +/− zooms, and Home resets the view. A new destination redirects ordinary walking immediately. While she is serving a customer or completing a restock delivery, it becomes her next command. Only the newest next command is retained. She finishes that customer or delivery, then follows your instruction before taking another customer, even if a line has formed.

Press C or choose Work at checkout on the counter to send her back to serve. Customers wait until you explicitly send her. A line never sends her back after walking or restocking, including when loading older saves. Once you choose checkout, she serves there until you give her another instruction. Stop finishes the current task, then leaves her waiting for your next instruction; C sends her back to checkout. All movement and tasks respect pause and game speed.

Click an empty outline in the world to request one replacement. She walks to the supply cabinet, collects a labeled carton, carries it back, and places exactly one item at the chosen spot. Click another empty outline to choose her next delivery; a later command replaces that next job. Replacements cost the product's wholesale price, charged when collected; delivery does not charge again. A failed or unreachable delivery refunds its undelivered replacement. Stop waits for a delivery already in progress to finish. No item refills automatically. Restocking works while the shop is open or closed, and pauses with the game. Keep the supply cabinet and target reachable. For floor furniture, she waits for customers to clear its footprint before placing it.

The playable shopkeeper uses the former cashier's face and body, with a casual sage cotton dress, tan flats, a warmer smile and loose long hair with restrained tip movement as she walks and turns. There is one worker in the store: she serves and restocks herself. Layout and price edits are locked while she has a restocking task; finish the task to redesign.

The checkout, supply cabinet and occupied displays stay fixtures. Empty displays, rugs and other furniture can be marked for sale. Keep exactly one checkout with a clear customer approach on its front side. Leave space and a clear route behind the counter for her too. Rename the store in the right panel; its sign appears on an intact rear wall.

## Presentation and sound

F11, Alt+Enter or the Full screen button toggles borderless fullscreen. Returning to windowed mode restores the prior size and position. The interface stretches with the window. Fullscreen is remembered in local presentation preferences.

Characters greet with a wave, smile, nod and use small conversational hand gestures. Browsing, starting payment and thanking each other trigger short fictional vocal phrases; mouth movement follows each clip's amplitude envelope. Gestures stay quieter while walking or carrying stock. The shopkeeper has a warmer smile, softened idle posture and small head tilts. These are original performances on the existing credited rigs, with no character needs added.

Arrivals have a door chime; checkout has register/receipt sounds; collection and placement have cardboard and object sounds. Footsteps are quiet and spatial. A softly swung original boutique theme plays underneath; music dips during voices. At most two characters speak together, and per-character cooldowns limit repeated chatter. Speech keeps its natural pitch at 3× game speed. Pausing pauses audio as well as tasks.

Use Audio for master, voices, store effects and music levels; M toggles mute. Presentation preferences are separate from the store save and do not change finances or undo history. Vocal phrases are newly written nonsense syllables synthesized locally, not EA/The Sims recordings or dialogue. The shipped game plays local audio and does not need a network or speech-generation service. See Assets/Audio/SOURCES.md for provenance.

## Trade

Open store checks the route to checkout and that at least one stocked product is reachable. Up to three shoppers visit at once. The first visitor arrives after 12–18 seconds; later arrivals are 24–40 seconds apart, with extra breathing room when the entrance is occupied. Customers walk at a relaxed, slightly varied pace, slow into stops, and make room for one another. Clear floor uses a direct line to the exact destination. Around furniture and partitions, any-angle search uses Euclidean travel cost and visible shortcuts, with 20 cm of body clearance; turns are rounded only when the whole curve stays clear. Feet follow a distance-driven, body-scaled step cycle with planted stance and low swing clearance.

Shoppers look at one to three displays before deciding, linger 8–14 seconds at each, and take short looking-around pauses between displays. They avoid choosing an already occupied viewing spot. They evaluate the price against suggested retail, and either browse elsewhere, leave, or join the checkout queue. Prices agreed when entering the queue are honored at payment. Goods are reserved while queued; money and stock change only when payment finishes.

Your shopkeeper must physically reach the register before payment starts. Customers wait while she is elsewhere. If you issue a command during payment, she completes that one customer first, then leaves the rest of the line waiting while she follows it. Shoppers have anatomically proportioned, skinned MakeHuman models with skin and clothing textures, procedural walking, arm gestures and facial poses. Animations and furniture are an initial art pass; these are not final production-quality characters or a complete character customization system. Purchases are represented by a shopping bag rather than a separate carry animation for every product.

Close store stops new arrivals. Existing shoppers finish before editing resumes; send her to checkout with C if a line still needs serving, then the game shows daily sales, revenue and gross profit and saves automatically. Gross profit means retail revenue minus wholesale cost of sold items; it excludes building and furnishing purchases. Cash changes when buying/restocking inventory and when collecting payments. No time limit forces closing; the clock is illustrative. Trading starts a new day each time the doors reopen.

Layout edits are disabled during trading and while shoppers finish. Pause and 3× speed work during trading. Undo/redo applies to closed-store edits; opening starts a new trading ledger and clears edit history.

## Save and load

Save uses `user://store.json` and keeps the preceding save as `store.json.bak`. On Windows this is normally `%APPDATA%/Godot/app_userdata/Booming Store/`. The game restores a valid save on startup, falling back to the backup if necessary. Load replaces the current session with the saved state, closes the store, and clears unpaid shopping carts. Layout, finishes, store name, single-item stock, cash, cumulative sales, daily financial totals, shopkeeper position, the newest next command, active restocking and any replacement already paid for and being carried are preserved. A carried replacement resumes its delivery without being purchased again. Camera position, speed and transient shoppers are not saved. Older saves with stacked stock are migrated to one item per visible object; excess units are credited back at their wholesale cost, without changing sales or profit. A supply cabinet is provided in a free location when an older layout has room. Loading through the button asks for confirmation; F9 loads immediately.

F12 writes `store-screenshot.png` alongside the save. There is no cloud save, multiplayer, external model import, staff management, mod loader, multiple floors, or expandable lot yet.
