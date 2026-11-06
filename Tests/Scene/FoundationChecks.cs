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
            Check(game.State.Items.Count == 16, "Starter collection is incomplete.");
            Check(game.FindChildren("*", "Skeleton3D", true, false).Count > 0, "Cashier rig did not load.");
            var vase = game.State.Items.First(i => i.Kind == "vase"); var id = vase.Id;
            game.Select(vase); game.ChangePrice(5);
            Check(game.State.Items.Single(i => i.Id == id).Price == 43, "Price edit failed.");
            game.Undo(); Check(game.State.Items.Single(i => i.Id == id).Price == 38, "Undo did not restore price.");
            game.Redo(); Check(game.State.Items.Single(i => i.Id == id).Price == 43, "Redo did not restore price."); game.Undo();
            vase = game.State.Items.Single(i => i.Id == id); game.Select(vase);
            var balance = game.State.Cash; var stock = vase.Stock;
            game.Restock(); Check(vase.Stock == stock + 5 && game.State.Cash == balance - 90, "Restock quantity or payment is incorrect.");
            game.Undo();
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
            var before = game.State.Serialize(); game.Restock(); game.Rotate();
            Check(game.State.Serialize() == before, "Editing changed inventory or layout while trading.");
            game.SetSpeed(3);
            for (var frame = 0; frame < 6000 && game.State.Sales < 3; frame++) await Frame();
            Check(game.State.Sales >= 3, "Live shoppers failed to complete three purchases before timeout.");
            game.ToggleOpen(); Check(!game.Open, "Closing did not stop new arrivals.");
            for (var frame = 0; frame < 6000 && !game.Editing; frame++) await Frame();
            await Frame(); await Frame();
            Check(game.Editing && game.Shoppers.Count == 0 && game.Queue.Count == 0, "Shoppers or queue failed to drain after closing.");
            Check(game.State.Items.All(i => game.Reserved(i) == 0), "Closing left stock reservations behind.");
            Check(game.State.Cash == initialCash + game.State.Revenue, "Payments do not match the cash balance.");
            Check(initialStock.Sum(p => p.Value - game.State.Items.Single(i => i.Id == p.Key).Stock) == game.State.Sales,
                "Paid purchases do not match stock consumption.");
            var wholesale = game.State.Items.Sum(i => (initialStock[i.Id] - i.Stock) * i.Product.Cost);
            Check(wholesale == game.State.CostOfSales, "Cost of sales does not match sold inventory.");
            Check(game.State.DayRevenue == game.State.Revenue && game.State.DaySales == game.State.Sales, "Daily ledger differs from settled purchases.");
            Check(StorePersistence.Load(game.SavePath).State!.Revenue == game.State.Revenue, "Closing did not save the settled ledger.");
            GD.Print($"SCENE_CHECKS passed={passed} sales={game.State.Sales} revenue={game.State.Revenue}");
            game.GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); game.GetTree().Quit(1); }
    }
}
