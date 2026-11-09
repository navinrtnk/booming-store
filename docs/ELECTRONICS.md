# Electronics store · 0.1.7

## Starting and building

A new store is an empty 12 × 10 m shop with $10,000 and one unpaid playable owner. Existing saves load normally; use Game → New electronics store for the empty start. The outgoing store is saved and archived first. Recent archives can be restored from Game.

Place checkout counters and a supply cabinet from Displays, then shelving and merchandise. TV shelving has decks at 0.28 m and 1.28 m; electronics shelving supports 0.25 m, 0.85 m and 1.45 m. B or Shelf deck cycles the chosen deck while placing products. Two-deck racks map selection three to their upper deck. Products that would intersect the deck above are rejected. Moving/rotating shelves carries supported items. The placement outline checks bounds, supports, footprints and walls. Catalog pictures are cached renders of actual game models.

TVs range from 32-inch HD through 43-inch 4K, 55-inch QLED, 65-inch OLED and 75-inch cinema OLED, with increasing size, features and pricing. Computers include two laptop tiers and a console; audio includes a speaker, soundbar and headphones. Products are fictional. Older boutique objects remain supported in saved stores.

Build/price edits need a closed shop with no customers or active deliveries. Each placement is one visible unit. Click empty outlines for owner restocking, or assign restockers to refill them. Wholesale is charged once at cabinet collection. Workers claim distinct items and standing positions. Blocked targets receive a shared retry cooldown. Buffered returns take priority when a clear, unclaimed spot becomes empty.

## Time, traffic and reputation

The continuous 24-hour clock begins at 09:00. One store hour is 120 active simulation seconds: two real minutes at 1×, faster at 3×. Time advances while running, including closed hours. Pause stops it. No clock time or wages accrue while the application is closed.

Morning traffic is lighter; afternoons and evenings are busiest. Overnight, 22:00–06:00, demand is roughly one fifteenth of evening demand at the same reputation. At most six customers can be present. Entrance clearance prevents bursts. Reputation uses the last 20 departures: Happy contributes 1, Neutral 0.5, Disappointed 0; a new store starts at 50%. All-happy reputation has three times the arrival demand of all-disappointed reputation at the same hour.

Roughly 86% of visitors plan one item, 12% up to two, and 2% up to three. Availability and buying decisions may shorten a basket. Displays empty immediately at pickup; each unpaid basket is independent of replacements. One checkout settles the basket at its agreed line-item prices. Units sold and transactions are separate totals.

Customer morale uses real active queue seconds, independent of game speed. The first 60 seconds cause no loss; mood then declines through Neutral and becomes Disappointed after 80 seconds. Payment itself does not add queue time. Disappointed visitors leave unpaid and return their basket units as USED at wholesale cost. Occupied displays and paid deliveries are protected; returns wait in buffered stock. Fresh replacements restore normal retail prices. Sales assistance helps browsing without overriding queue morale.

## Two checkout registers

Build up to two counters. Select a counter to open/close it or send the owner to work there. Its OPEN/CLOSED sign reflects its state. Open, unmanned counters can attract lines, but payment requires a cashier behind the counter.

Opening a second register transfers the first waiting customer to it, then splits the remaining waiters evenly. A customer already paying stays put. Closing a register merges its waiters into the remaining open line; closing both sends them to a holding area. Changing lines preserves queue age and baskets.

The owner and summoned Cashier employees open their stations automatically upon arrival. Cashiers claim different registers and can serve simultaneously. Manual closure stays closed while a cashier remains there; an in-progress payment finishes while other waiters merge. Leaving or clocking out closes the station. An extra cashier waits when both stations are occupied. Keep customer approaches, line positions and cashier work spots clear.

## Employees and pay

Staff hires up to three employees with no hiring fee. Change role cycles Restocking, Sales and Cashier when no delivery/payment is active. Hires start off shift. Summon/clock in creates their actor and starts pay. Dismiss/clock out removes them after a current delivery/payment finishes. Fire permanently removes the roster member, refunds undelivered paid stock and settles wages. An interrupted unpaid shopper requeues with their basket.

Multiple employees can share a role. Restockers coordinate claims and approach positions; sales staff approach browsers and chat; cashiers coordinate registers. The owner walks modestly faster and receives no wages.

Pay is $100 per store hour. Completed 15-minute blocks cost $25 while on shift. Clock-out rounds the whole shift to the nearest 15 minutes, half-way up, and charges only the remaining difference. A seven-minute shift costs $0; eight minutes costs $25. Off-shift time costs nothing. Saved worked time and paid blocks prevent duplicate billing. Cash shortages become wages owed, paid from later income; cash never goes negative. Staff and the cash tooltip show wage/owed totals. Net profit subtracts wage expense; session sales reset on reopening.

## Saving and controls

Game/F5 saves user://store.json and a previous-good .bak. Autosave runs every active real minute, at store closing and on normal application exit. Saves include layout and deck positions, prices, stock/returns, finances, clock/reputation, employee roles/positions/shifts, paid wage blocks, register signs/claims and paid deliveries. Open/closed state is restored. Unpaid baskets return to stock/buffered returns in a separate snapshot; live carts are untouched by saving. Transient customers are not recreated on load. Schemas 1/2 migrate to schema 3.

New Store and archive restoration archive the outgoing session first. Game lists the ten newest archives; older files remain in the user save folder's archives directory. Saves and preferences are outside Git. F9 loads immediately; menu loading asks for confirmation.

Click floor or I/J/K/L moves the owner. She finishes an atomic customer/delivery before following the newest command. C chooses a free checkout; waiting customers never take over her movement. Tab/UI hides side/bottom panels while retaining the top bar. F11/Alt+Enter toggles fullscreen. F follows, 1/2/3 choose views, arrows/Q/E orbit, WASD pans, wheel/+/- zooms, Home resets, R rotates, Esc cancels and Space pauses. M mutes; Audio sets bus levels; F12 saves a screenshot. Characters retain procedural grounded walking, gestures, fictional chatter and facial poses.

There is no rent, tax, character-needs simulation, multiplayer, external model import, mod loader or expandable lot. Traffic/pricing/staffing are prototype balance. Unusual layouts and other GPUs need wider playtesting.
