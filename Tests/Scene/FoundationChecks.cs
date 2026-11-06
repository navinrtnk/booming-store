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
            Check(game.State.Items.Count == 17, "Starter collection is incomplete.");
            Check(game.FindChildren("*", "Skeleton3D", true, false).Count > 0, "Cashier rig did not load.");
            var vase = game.State.Items.First(i => i.Kind == "vase"); var id = vase.Id;
            game.Select(vase); game.ChangePrice(5);
            Check(game.State.Items.Single(i => i.Id == id).Price == 43, "Price edit failed.");
            game.Undo(); Check(game.State.Items.Single(i => i.Id == id).Price == 38, "Undo did not restore price.");
            game.Redo(); Check(game.State.Items.Single(i => i.Id == id).Price == 43, "Redo did not restore price."); game.Undo();
            vase = game.State.Items.Single(i => i.Id == id); game.Select(vase);
            Check(game.State.Items.All(i => i.Stock == 1), "Every visible object must represent one item.");
            game.Keeper.MoveTo(new(-1.5f, 0, 2.8f)); game.SetSpeed(3);
            for (var frame = 0; frame < 300 && game.Keeper.Actor.IsWalking; frame++) await Frame();
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
            game.ToggleOpen(); Check(game.Open && !game.Editing, "Starter shop did not open.");
            game.Select(game.State.Items.Single(i => i.Id == id));
            var before = game.State.Serialize(); game.Rotate();
            Check(game.State.Serialize() == before, "Editing changed inventory or layout while trading.");
            for (var frame = 0; frame < 600; frame++) await Frame();
            Check(game.Shoppers.Count == 0, "Customers arrived without the opening quiet period.");
            game.SetSpeed(3);
            var maximumGuests = 0;
            var browsingStarted = new Dictionary<Shopper, double>();
            var shortestBrowse = double.PositiveInfinity;
            for (var frame = 0; frame < 6000 && game.State.Sales < 3; frame++)
            {
                await Frame(); maximumGuests = Math.Max(maximumGuests, game.Shoppers.Count);
                foreach (var shopper in game.Shoppers)
                {
                    if (shopper.State == Shopper.Activity.Browsing) browsingStarted.TryAdd(shopper, game.State.TradingSeconds);
                    else if (browsingStarted.Remove(shopper, out var started)) shortestBrowse = Math.Min(shortestBrowse, game.State.TradingSeconds - started);
                }
            }
            Check(maximumGuests <= CustomerArrivals.Capacity, "Shop exceeded its quieter customer limit.");
            Check(double.IsFinite(shortestBrowse) && shortestBrowse >= 7.9, "Customers did not linger at displays.");
            Check(game.State.Sales >= 3, "Live shoppers failed to complete three purchases before timeout.");
            game.ToggleOpen(); Check(!game.Open, "Closing did not stop new arrivals.");
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
            Check(game.Keeper.Actor.Position == pausedPosition && empty.Stock == 0 && game.State.Cash == cashBeforeRestock,
                "Pausing did not pause physical restocking.");
            game.SetSpeed(3);
            for (var frame = 0; frame < 3000 && game.State.CarriedItemId == 0; frame++) await Frame();
            Check(game.State.CarriedItemId == emptyId && empty.Stock == 0, "She did not collect and carry one replacement.");
            Check(game.Keeper.Actor.Position.DistanceTo(start) > 1, "Restocking did not require physical travel.");
            Check(game.State.Cash == cashBeforeRestock - empty.Product.Cost, "The replacement was not charged exactly once.");
            game.Save(false); game.TryLoad(false);
            Check(game.State.CarriedItemId == emptyId, "Loading lost the item already being carried.");
            for (var frame = 0; frame < 3600 && game.State.Items.Single(i => i.Id == emptyId).Stock == 0; frame++) await Frame();
            var restocked = game.State.Items.Single(i => i.Id == emptyId);
            Check(restocked.Stock == 1 && !game.Keeper.Busy, "The main character failed to finish stocking one item.");
            Check(game.State.Cash == cashBeforeRestock - restocked.Product.Cost, "Loading charged for the carried replacement twice.");
            Check(game.Keeper.Actor.Position.DistanceTo(restocked.Position) < 1.8f, "Restocking completed without reaching the display.");
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
            GD.Print($"SCENE_CHECKS passed={passed} sales={game.State.Sales} revenue={game.State.Revenue}");
            game.GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); game.GetTree().Quit(1); }
    }
}
