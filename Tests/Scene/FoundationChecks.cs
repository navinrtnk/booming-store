// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Debug-only integration checks against the real controller, shoppers and scene graph.</summary>
public static class FoundationChecks
{
    public static async Task Run(StoreGame game)
    {
        var passed = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            passed++;
        }
        async Task Frame() => await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        try
        {
            GD.Seed(42);
            Check(game.Editing && !game.Open, "Starter shop must be closed and editable.");
            Check(game.State.Items.Count == 0 && game.State.Cash == 10000, "New electronics store must be empty with $10,000.");
            game.FurnishReviewStore();
            Check(game.State.Items.Count == 17, "Starter collection is incomplete.");
            Check(game.FindChildren("*", "Skeleton3D", true, false).Count == 1, "The shop must contain only its playable shopkeeper before customers arrive.");
            Check(game.Keeper.Actor.IsShopkeeper && game.Keeper.Actor.FindChild("FlowingHair", true, false) is MeshInstance3D hair && hair.Mesh is ArrayMesh hairMesh && hairMesh.GetBlendShapeCount() == 3,
                "The cashier-based shopkeeper or her flowing hair did not load.");
            var settledBeforePreferences = game.State.Serialize();
            game._UnhandledInput(new InputEventKey { Keycode = Key.F11, Pressed = true });
            Check(game.Preferences.Fullscreen, "Fullscreen shortcut was ignored.");
            game._UnhandledInput(new InputEventKey { Keycode = Key.F11, Pressed = true });
            game._UnhandledInput(new InputEventKey { Keycode = Key.M, Pressed = true });
            Check(!game.Preferences.Fullscreen && game.Preferences.Muted && AudioServer.IsBusMute(0), "Window/mute preferences were not applied.");
            game._UnhandledInput(new InputEventKey { Keycode = Key.M, Pressed = true });
            Check(game.PreferencesPath.Contains("test-results") && !GamePreferences.Load(game.PreferencesPath).Muted,
                "Preferences were not saved to the isolated test location.");
            Check(game.State.Serialize() == settledBeforePreferences, "Presentation preferences changed settled store data.");
            Check(game.Audio.EnvelopeCount == 20, "Voice clips or mouth envelopes did not load.");
            var vase = game.State.Items.First(i => i.Kind == "vase"); var id = vase.Id;
            game.Select(vase); game.ChangePrice(5);
            Check(game.State.Items.Single(i => i.Id == id).Price == 43, "Price edit failed.");
            game.Undo(); Check(game.State.Items.Single(i => i.Id == id).Price == 38, "Undo did not restore price.");
            game.Redo(); Check(game.State.Items.Single(i => i.Id == id).Price == 43, "Redo did not restore price."); game.Undo();
            vase = game.State.Items.Single(i => i.Id == id); game.Select(vase);
            Check(game.State.Items.All(i => i.Stock == 1), "Every visible object must represent one item.");
            game.Keeper.MoveTo(new(-1.5f, 0, 2.8f));
            var redirectedWalk = new Vector3(-1.35f, 0, 2.85f); game.Keeper.MoveTo(redirectedWalk);
            Check(game.Keeper.Actor.Destination == redirectedWalk && game.Keeper.Next == null, "A new walking command did not redirect the current route immediately.");
            game.SetSpeed(3);
            var diagonalMovement = false; var lastKeeperPosition = game.Keeper.Actor.Position;
            for (var frame = 0; frame < 300 && game.Keeper.Actor.IsWalking; frame++)
            {
                await Frame(); var now = game.Keeper.Actor.Position; var step = now - lastKeeperPosition;
                diagonalMovement |= Math.Abs(step.X) > .0001f && Math.Abs(step.Z) > .0001f;
                if (!game.Navigation.IsSegmentClear(lastKeeperPosition, now)) throw new InvalidOperationException("Shopkeeper crossed an obstacle while following her route.");
                lastKeeperPosition = now;
                if (frame == 20 && game.Keeper.Actor.IsWalking)
                {
                    game.Keeper.MoveTo(redirectedWalk); var beforeRedirect = game.Keeper.Actor.Position;
                    await Frame();
                    Check(game.Keeper.Actor.Position.DistanceTo(beforeRedirect) > .015f, "Repeated movement commands restarted acceleration instead of retaining momentum.");
                    lastKeeperPosition = game.Keeper.Actor.Position;
                }
            }
            Check(diagonalMovement, "The main character still follows only horizontal and vertical steps.");
            Check(game.Keeper.Actor.Position.Z > 2.5f, "The main character did not walk to the requested floor location.");
            game._UnhandledInput(new InputEventKey { Keycode = Key.Key2, Pressed = true });
            Check(game.Camera.Distance < 10 && game.Camera.Pitch < .5f, "Keyboard close camera view failed.");
            game._UnhandledInput(new InputEventKey { Keycode = Key.Key3, Pressed = true });
            Check(game.Camera.Pitch > 1, "Keyboard overhead camera view failed.");
            game._UnhandledInput(new InputEventKey { Keycode = Key.F, Pressed = true });
            Check(game.Camera.Following, "Keyboard follow mode failed.");
            game.Camera.AdjustAngles(.2f, -20, 40);
            Check(game.Camera.Pitch >= .22f && game.Camera.Distance == 32, "Camera angle and zoom limits failed.");
            game.Camera.Home();
            var beforeKeyMove = game.Keeper.Actor.Position;
            game._UnhandledInput(new InputEventKey { Keycode = Key.L, Pressed = true });
            for (var frame = 0; frame < 180 && game.Keeper.Actor.IsWalking; frame++) await Frame();
            Check(game.Keeper.Actor.Position.DistanceTo(beforeKeyMove) > .4f, "A single keyboard movement command was ignored.");
            game.SetSpeed(1);
            var table = game.State.Items.First(i => i.Kind == "table");
            var children = game.State.Items.Where(i => i.ParentId == table.Id).ToDictionary(i => i.Id, i => i.Position);
            game.Select(table); game.Rotate();
            Check(table.Rotation == 90, "Display rotation was rejected.");
            foreach (var child in game.State.Items.Where(i => i.ParentId == table.Id))
                Check(child.Position.DistanceTo((children[child.Id] - table.Position).Rotated(Vector3.Up, Mathf.Pi / 2) + table.Position) < .0001f,
                    "Display contents did not move with the table.");
            game.Undo(); game.Save(false);
            Check(File.Exists(game.SavePath), "Controller did not save to its isolated test path.");
            game.Select(game.State.Items.Single(i => i.Id == id)); game.ChangePrice(25); game.TryLoad(false);
            Check(game.State.Items.Single(i => i.Id == id).Price == 38 && game.Editing, "Controller load did not restore the saved store.");
            Check(game.SavePath.Contains("test-results"), "Automated checks must not write player saves.");

            var initialCash = game.State.Cash;
            var initialStock = game.State.Items.ToDictionary(i => i.Id, i => i.Stock);
            game.SetSpeed(0);
            game.ToggleOpen(); Check(game.Open && !game.Editing, "Starter shop did not open.");
            Check(game.Speed == 1 && !game.Audio.IsPaused, "Opening a paused store did not resume its audio and tasks together.");

            game.Select(game.State.Items.Single(i => i.Id == id));
            var before = game.State.Serialize(); game.Rotate();
            Check(game.State.Serialize() == before, "Editing changed inventory or layout while trading.");
            for (var frame = 0; frame < 600; frame++) await Frame();
            Check(game.Shoppers.Count == 0, "Customers arrived without the opening quiet period.");
            game.SetSpeed(3);
            var maximumGuests = 0;
            var browsingStarted = new Dictionary<Shopper, double>();
            var shortestBrowse = double.PositiveInfinity;
            var testedServiceInterruption = false; var requestedCheckout = false; var resumedCheckout = false; var previousSales = game.State.Sales;
            for (var frame = 0; frame < 6000 && game.State.Transactions < 3; frame++)
            {
                await Frame(); maximumGuests = Math.Max(maximumGuests, game.Shoppers.Count);
                if (game.State.Sales > previousSales)
                {
                    Check(game.Keeper.IsAtRegister, "A customer paid without the main character at the checkout.");
                    previousSales = game.State.Sales;
                }
                if (!requestedCheckout && game.Queue.Count >= 2)
                {
                    var waitingPosition = game.Keeper.Actor.Position;
                    for (var waiting = 0; waiting < 120; waiting++) await Frame();
                    Check(!game.Keeper.Actor.IsWalking && game.Keeper.Actor.Position.DistanceTo(waitingPosition) < .01f && game.State.Sales == 0,
                        "Waiting customers forced the shopkeeper to return to checkout.");
                    foreach (var person in game.People) { person.StopVoice(); person.React(PersonBeat.Thanks, line: VoiceLine.Thanks); }
                    var spokenBefore = game.Audio.SpokenLines;
                    await Frame(); await Frame();
                    Check(game.People.Count(p => p.IsSpeaking) == 2 && game.Audio.SpokenLines == spokenBefore + 2,
                        "Concurrent reactions exceeded the two-voice limit or suppressed both voices.");
                    game.SetSpeed(0);
                    var pausedMouths = game.People.Select(p => p.MouthAmount).ToArray();
                    for (var waiting = 0; waiting < 5; waiting++) await Frame();
                    Check(game.People.Select(p => p.MouthAmount).SequenceEqual(pausedMouths), "Pausing advanced character mouth animation.");
                    game.SetSpeed(3);
                    Check(game.Keeper.ServeCheckout(), "Could not send the shopkeeper to checkout."); requestedCheckout = true;
                }
                if (!testedServiceInterruption && game.Shoppers.Any(s => s.State == Shopper.Activity.Paying))
                {
                    testedServiceInterruption = true; var cashBeforeCommand = game.State.Cash; var salesBeforeCommand = game.State.Sales;
                    var currentCustomer = game.Queue[0]; var agreedPrice = game.CartValue(currentCustomer); var basketUnits = game.CartCount(currentCustomer);
                    var newest = new Vector3(4.25f, 0, 3.4f);
                    Check(game.Keeper.MoveTo(new(4.9f, 0, 3.8f)) && game.Keeper.MoveTo(newest), "Could not queue the cashier's next walking command.");
                    Check(game.Keeper.Next?.Position == newest && !game.Keeper.Actor.IsWalking && game.Keeper.CanServe(currentCustomer),
                        "The command interrupted payment or failed to replace the previous next command.");
                    for (var away = 0; away < 120; away++) await Frame();
                    Check(game.State.Cash == cashBeforeCommand + agreedPrice && game.State.Sales == salesBeforeCommand + basketUnits,
                        "She must finish exactly the current customer before following the next command.");
                    Check(game.Queue.Count > 0 && !game.Shoppers.Any(s => s.State == Shopper.Activity.Paying) && game.Keeper.Actor.Destination == newest,
                        "She served another lined-up customer before following the newest command.");
                    previousSales = game.State.Sales;
                }
                if (testedServiceInterruption && !resumedCheckout && !game.Keeper.Actor.IsWalking && game.Keeper.Actor.Position.DistanceTo(new(4.25f, 0, 3.4f)) < .03f)
                {
                    var cashWhileFree = game.State.Cash;
                    for (var waiting = 0; waiting < 120; waiting++) await Frame();
                    Check(game.State.Cash == cashWhileFree && !game.Keeper.Actor.IsWalking && game.Keeper.Actor.Position.DistanceTo(new(4.25f, 0, 3.4f)) < .03f,
                        "Finishing a walking command automatically sent her back to the line.");
                    Check(game.Keeper.ServeCheckout(), "Explicit checkout command failed after walking away."); resumedCheckout = true;
                }
                foreach (var shopper in game.Shoppers)
                {
                    if (shopper.State == Shopper.Activity.Browsing) { browsingStarted.TryAdd(shopper, game.State.TradingSeconds); shortestBrowse = Math.Min(shortestBrowse, shopper.BrowseDuration); }
                    else browsingStarted.Remove(shopper);
                }
            }
            Check(testedServiceInterruption, "No live checkout was available to test the shopkeeper leaving the register.");
            Check(game.FindChildren("*", "Skeleton3D", true, false).Count == game.Shoppers.Count + 1,
                "An extra cashier or shopkeeper was spawned.");
            Check(maximumGuests <= CustomerArrivals.Capacity, "Shop exceeded its quieter customer limit.");
            Check(double.IsFinite(shortestBrowse) && shortestBrowse >= 7.9, "Customers did not linger at displays.");
            Check(game.Audio.EventCounts.GetValueOrDefault(ShopSound.Door) > 0 && game.Audio.EventCounts.GetValueOrDefault(ShopSound.Checkout) > 0,
                "Arrivals or purchases did not dispatch their sounds.");
            Check(game.Audio.SpokenLines > 0 && game.Keeper.Actor.ExpressionsStarted > 0, "Characters did not greet or speak during live trading.");
            Check(game.State.Sales >= 3, "Live shoppers failed to complete three purchases before timeout.");
            game.SetSpeed(0);
            game.ToggleOpen(); Check(!game.Open, "Closing did not stop new arrivals.");
            Check(game.Speed == 1 && !game.Audio.IsPaused, "Closing a paused store did not resume its audio and tasks together.");
            for (var frame = 0; frame < 6000 && !game.Editing; frame++) await Frame();
            await Frame(); await Frame();
            Check(game.Editing && game.Shoppers.Count == 0 && game.Queue.Count == 0, "Shoppers or queue failed to drain after closing: " + string.Join("; ", game.Shoppers.Select(s => $"{s.State} at {s.Position}, destination {s.Destination}, walking {s.IsWalking}")) + $"; keeper {game.Keeper.Actor.Position}");
            Check(game.State.Items.All(i => game.Reserved(i) == 0), "Closing left stock reservations behind.");
            Check(game.State.Cash == initialCash + game.State.Revenue, "Payments do not match the cash balance.");
            Check(initialStock.Sum(p => p.Value - game.State.Items.Single(i => i.Id == p.Key).Stock) == game.State.Sales,
                "Paid purchases do not match stock consumption.");
            var wholesale = game.State.Items.Sum(i => (initialStock[i.Id] - i.Stock) * i.Product.Cost);
            Check(wholesale == game.State.CostOfSales, "Cost of sales does not match sold inventory.");
            Check(game.State.DayRevenue == game.State.Revenue && game.State.DaySales == game.State.Sales, "Daily ledger differs from settled purchases.");
            Check(StorePersistence.Load(game.SavePath).State!.Revenue == game.State.Revenue, "Closing did not save the settled ledger.");
            var empty = game.State.Items.First(i => i.ForSale && i.Stock == 0); var emptyId = empty.Id;
            Check(game.GetNode<Node3D>($"Layout/Object{emptyId}").FindChild("EmptySpot", true, false) != null,
                "A sold product must disappear and leave only an empty marker.");
            var start = game.Keeper.Actor.Position; var cashBeforeRestock = game.State.Cash;
            for (var frame = 0; frame < 60; frame++) await Frame();
            Check(empty.Stock == 0, "A sold item automatically restocked without a user command.");
            game._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true,
                Position = game.Camera.UnprojectPosition(empty.Position + Vector3.Up * .05f) });
            Check(game.State.RestockQueue.Contains(emptyId), "Clicking the empty world spot did not queue a replacement.");
            Check(empty.Stock == 0 && game.State.Cash == cashBeforeRestock, "Clicking a spot must not instantly restock or charge it.");
            game.SetSpeed(0); var pausedPosition = game.Keeper.Actor.Position;
            for (var frame = 0; frame < 60; frame++) await Frame();
            Check(game.Audio.IsPaused && game.Keeper.Actor.Position == pausedPosition && empty.Stock == 0 && game.State.Cash == cashBeforeRestock,
                "Pausing did not pause physical restocking.");
            game.SetSpeed(3);
            for (var frame = 0; frame < 3000 && game.State.CarriedItemId == 0; frame++) await Frame();
            Check(game.State.CarriedItemId == emptyId && empty.Stock == 0, "She did not collect and carry one replacement.");
            Check(game.Keeper.Actor.Position.DistanceTo(start) > 1, "Restocking did not require physical travel.");
            Check(game.State.Cash == cashBeforeRestock - empty.Product.Cost, "The replacement was not charged exactly once.");
            var afterDelivery = new Vector3(1.8f, 0, 3.45f);
            game.Keeper.MoveTo(new(1.4f, 0, 3.4f)); game.Keeper.MoveTo(afterDelivery);
            Check(game.Keeper.Next?.Position == afterDelivery && game.Keeper.Actor.CarryingStock,
                "A new command must wait for the current delivery and replace any older next command.");
            game.Save(false); game.TryLoad(false);
            Check(game.Keeper.Next?.Position == afterDelivery, "Loading lost the newest command waiting behind a delivery.");
            Check(game.State.CarriedItemId == emptyId, "Loading lost the item already being carried.");
            for (var frame = 0; frame < 3600 && game.State.Items.Single(i => i.Id == emptyId).Stock == 0; frame++) await Frame();
            var restocked = game.State.Items.Single(i => i.Id == emptyId);
            Check(restocked.Stock == 1 && !game.Keeper.Busy, "The main character failed to finish stocking one item.");
            Check(game.State.Cash == cashBeforeRestock - restocked.Product.Cost, "Loading charged for the carried replacement twice.");
            Check(game.Keeper.Actor.Position.DistanceTo(restocked.Position) < 1.8f, "Restocking completed without reaching the display.");
            Check(game.Keeper.Actor.Destination == afterDelivery && game.Keeper.Next == null, "The next command did not start immediately after delivery.");
            for (var frame = 0; frame < 900 && game.Keeper.Actor.IsWalking; frame++) await Frame();
            Check(game.Keeper.Actor.Position.DistanceTo(afterDelivery) < .02f,
                $"The shopkeeper did not finish the newest walking command: {game.Keeper.Actor.Position}, destination {game.Keeper.Actor.Destination}, failed {game.Keeper.Actor.RouteFailed}.");
            Check(game.Audio.EventCounts.GetValueOrDefault(ShopSound.Pickup) > 0 && game.Audio.EventCounts.GetValueOrDefault(ShopSound.Place) > 0 &&
                game.Audio.EventCounts.GetValueOrDefault(ShopSound.Step) > 0, "Physical stocking or walking sounds were not dispatched.");
            var floorEmpty = game.State.Items.First(i => i.ForSale && i.Stock == 0 && i.Y == 0);
            game.Camera.ZoomTo(floorEmpty.Position);
            var edge = floorEmpty.Position + new Vector3(floorEmpty.Footprint.X * .42f, .05f, floorEmpty.Footprint.Y * .42f);
            var edgeClick = game.Camera.UnprojectPosition(edge);
            Check(edgeClick.DistanceTo(game.Camera.UnprojectPosition(floorEmpty.Position + Vector3.Up * .05f)) > 22,
                "Outline click must exercise the floor marker outside its center label.");
            game._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = edgeClick });
            Check(game.State.RestockQueue.Contains(floorEmpty.Id), "Clicking the edge of an empty outline did not request a replacement.");
            var cashBeforeCancel = game.State.Cash; game.Keeper.Cancel();
            Check(!game.Keeper.Busy && floorEmpty.Stock == 0 && game.State.Cash == cashBeforeCancel,
                "Canceling an uncollected job changed stock or money.");
            game.Camera.Home(); game.Select(restocked);
            Check(!game.Hud.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.Text.StartsWith("Restock")),
                "The object's menu still offers instant restocking.");
            foreach (var player in game.Audio.GetChildren().OfType<AudioStreamPlayer3D>()) player.Stop();
            var stepsBefore = game.Audio.EventCounts.GetValueOrDefault(ShopSound.Step);
            var checkoutsBefore = game.Audio.EventCounts.GetValueOrDefault(ShopSound.Checkout);
            for (var cue = 0; cue < 12; cue++) game.Audio.Play(ShopSound.Step, Vector3.Zero);
            Check(game.Audio.EventCounts.GetValueOrDefault(ShopSound.Step) == stepsBefore + 6,
                "Footstep bursts were not bounded by their dedicated channels.");
            for (var cue = 0; cue < 8; cue++) game.Audio.Play(ShopSound.Checkout, Vector3.Zero);
            Check(game.Audio.EventCounts.GetValueOrDefault(ShopSound.Checkout) == checkoutsBefore + 8,
                "Busy footstep or event channels dropped checkout sounds.");
            game.SetSpeed(0); game.Audio.Play(ShopSound.Checkout, Vector3.Zero);
            Check(game.Audio.EventCounts.GetValueOrDefault(ShopSound.Checkout) == checkoutsBefore + 8,
                "A paused store started a new sound.");
            game.SetSpeed(3);
            game.Hud.ToggleChrome(); Check(game.Hud.Compact, "Compact UI did not hide side panels.");
            game.Hud.ToggleChrome(); Check(!game.Hud.Compact, "Compact UI did not restore panels.");
            var employeeCash = game.State.Cash;
            Check(game.Staff.Hire(EmployeeRole.Restocking) && game.Staff.Summon(0) && game.State.Cash == employeeCash, "Hiring did not create and charge one employee.");
            for (var frame = 0; frame < 2500 && !game.State.Employees[0].Collected; frame++) await Frame();
            Check(game.State.Employees[0].Collected, "Employee failed to collect a physical replacement.");
            var employeeTarget = game.State.Employees[0].Target;
            var paidCash = game.State.Cash; var paidWages = game.State.WageExpense;
            game.Save(false); game.TryLoad(false);
            Check(game.State.Employees[0].Collected && game.State.Cash == paidCash, "Employee delivery was lost or charged twice on load.");
            for (var frame = 0; frame < 2500 && game.State.Items.Single(i => i.Id == employeeTarget).Stock == 0; frame++) await Frame();
            Check(game.State.Items.Single(i => i.Id == employeeTarget).Stock == 1 && game.State.Cash + game.State.WageExpense == paidCash + paidWages,
                "Employee did not physically deliver exactly one paid replacement.");
            Check(game.Staff.SetRole(0, EmployeeRole.Sales), "Idle employee could not change roles.");
            game.ToggleOpen(); game.SetSpeed(3);
            for (var frame = 0; frame < 2800 && game.Staff.HelpedCustomers == 0; frame++) await Frame();
            Check(game.Staff.HelpedCustomers > 0, "Sales employee did not approach and help a browsing customer.");
            var queued = game.Shoppers.FirstOrDefault(s => s.State == Shopper.Activity.Queueing);
            for (var frame = 0; frame < 2800 && queued == null; frame++) { await Frame(); queued = game.Shoppers.FirstOrDefault(s => s.State == Shopper.Activity.Queueing); }
            Check(queued != null, "No live cart was available for the morale test.");
            var cartItem = queued!.Choice!; var salesBeforeAbandon = game.State.Sales;
            Check(cartItem.Stock == 0, "Customer pickup did not immediately empty its display.");
            queued.Morale.Advance(81, true); await Frame(); await Frame();
            Check(queued.State == Shopper.Activity.Leaving && !game.Queue.Contains(queued) && game.State.Sales == salesBeforeAbandon,
                "Disappointed customer did not leave without paying.");
            Check(cartItem.Stock == 1 && cartItem.Used && cartItem.Price == cartItem.Product.Cost,
                "Abandoned product did not return automatically at a zero-profit price.");
            game.State.NextKeeperCommand = new(KeeperCommandKind.Restock, ItemId: cartItem.Id);
            game.StockChanged(cartItem);
            Check(game.State.NextKeeperCommand == null, "An automatically returned item left a stale next-restock command.");
            var trial = new StoreState(); var trialItem = new StoreItem { Id = 1, Price = 38, ForSale = true }; trial.Items.Add(trialItem);
            var trade = new StoreTrading(trial);
            Check(trade.TryReserve(1, trialItem), "Test cart could not take used-return fixture.");
            trialItem.RestockNew(); trade.TryReserve(2, trialItem); trialItem.RestockNew();
            trade.Checkout(1); trade.Checkout(2);
            Check(trialItem.Stock == 1 && trial.Revenue == 76 && trial.CostOfSales == 36 && trade.Checkout(1) == null,
                "Two carts consumed a replacement or charged twice.");
            trade.TryReserve(3, trialItem); var savedTrial = trade.SettledSnapshot();
            Check(savedTrial.Items[0].Stock == 1 && trialItem.Stock == 0 && trial.Cash == savedTrial.Cash,
                "Settled snapshot lost an unpaid item or mutated a live cart.");
            trade.Cancel(3, true); trialItem.RestockNew();
            Check(!trade.ProcessReturns().Any() && trial.Returns.Count == 1, "Used return overwrote fresh stock.");
            trade.TryReserve(4, trialItem); _ = trade.ProcessReturns().ToArray();
            Check(trialItem.Used && trialItem.Price == trialItem.Product.Cost && trial.Returns.Count == 0, "Buffered return did not refill the next empty slot.");
            trade.TryReserve(5, trialItem); Check(trade.Checkout(5)!.Price == trialItem.Product.Cost, "Used unit produced a retail margin.");
            trialItem.RestockNew(); Check(!trialItem.Used && trialItem.Price == 38, "Fresh replacement did not restore its regular price.");
            Check(game.Staff.Hire(EmployeeRole.Restocking) && game.Staff.Hire(EmployeeRole.Sales) && !game.Staff.Hire(EmployeeRole.Sales), "Three-employee hiring cap failed.");
            Check(game.Staff.Dismiss(2) && game.State.Employees.Count == 2, "Idle employee could not be dismissed.");
            var morale = new CustomerMorale(); morale.Advance(100, false); morale.Advance(60, true);
            Check(morale.Value == 1, "Morale declined before a real minute of queue time.");
            morale.Advance(15, true); Check(morale.Mood == CustomerMood.Neutral, "Morale skipped neutral.");
            morale.Advance(6, true); Check(morale.Mood == CustomerMood.Disappointed, "Long queue did not disappoint.");
            GD.Print("FOUNDATION_CHECKS complete");
            await ElectronicsChecks.Run(game, Check);
            GD.Print($"SCENE_CHECKS passed={passed} sales={game.State.Sales} revenue={game.State.Revenue}");
            await game.QuitCleanly();
        }
        catch (Exception error) { GD.PushError(error.ToString()); await game.QuitCleanly(1); }
    }
}
