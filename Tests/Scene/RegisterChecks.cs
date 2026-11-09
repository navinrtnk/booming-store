// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
namespace BoomingStore;

public static class RegisterChecks
{
    public static async Task Run(StoreGame game, Action<bool, string> check)
    {
        async Task Frame() => await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        game.SetSpeed(0);
        var first = game.Checkout!;
        var second = ElectronicsChecks.Add(game, "checkout", 1.8f, -.1f);
        check(!StoreLayout.CanPlace(game.State, new StoreItem { Kind = "checkout", X = -3, Z = 1 }), "A third checkout register was allowed.");
        if (game.Open) game.ToggleOpen(); game.SetSpeed(0);
        game.Registers.SetOpen(first, true); game.Registers.SetOpen(second, false);
        var customers = new List<Shopper>();
        for (var i = 0; i < 6; i++)
        {
            var item = ElectronicsChecks.Add(game, "speaker", -4.8f + i * .4f, 2.8f);
            var shopper = new Shopper(); game.AddChild(shopper); shopper.Build(game, i); game.Shoppers.Add(shopper);
            shopper.PlaceAt(new(-2.5f + i * .6f, 0, 3.6f));
            check(game.TakeItem(shopper, item), "Could not set up register basket."); shopper.QueueCart(); customers.Add(shopper);
        }
        check(customers.All(s => s.RegisterId == first.Id), "Customers did not select the sole open register.");
        game.Registers.SetOpen(second, true);
        check(customers[0].RegisterId == second.Id && game.Registers.Line(first.Id).Count() == 3 && game.Registers.Line(second.Id).Count() == 3,
            "Opening register two did not move the first customer and split remaining customers evenly.");
        game.Registers.SetOpen(first, false);
        check(customers.All(s => s.RegisterId == second.Id), "Closing register one did not merge its line into register two.");
        game.Registers.SetOpen(second, false);
        check(customers.All(s => s.RegisterId == 0), "Customers continued queueing at closed registers.");
        game.Staff.Dismiss(0);
        game.Staff.Hire(EmployeeRole.Cashier); game.Staff.Hire(EmployeeRole.Cashier);
        game.Staff.Summon(0); game.Staff.Summon(1); game.SetSpeed(3);
        var revenue = game.State.Revenue; var transactions = game.State.Transactions; var parallel = false; var closedDuringPayment = false;
        for (var frame = 0; frame < 5000 && game.State.Transactions < transactions + 6; frame++)
        {
            await Frame();
            var paying = game.Shoppers.Where(s => s.State == Shopper.Activity.Paying).ToArray();
            if (paying.Length >= 2)
            {
                parallel = true;
                if (!closedDuringPayment && paying.Any(s => s.RegisterId == first.Id))
                {
                    var current = paying.First(s => s.RegisterId == first.Id); game.Registers.SetOpen(first, false); closedDuringPayment = true;
                    check(game.CanServe(current), "Closing a register interrupted its current payment.");
                    check(game.Queue.Where(s => s.State != Shopper.Activity.Paying).All(s => s.RegisterId == second.Id), "Waiting customers failed to merge during an active payment.");
                }
            }
        }
        GD.Print($"REGISTER_CHECKOUTS delta={game.State.Transactions - transactions} parallel={parallel} closed={closedDuringPayment} staff={game.Staff.DebugStatus}");
        check(game.State.Employees.Select(e => e.RegisterId).Distinct().Count() == 2, "Cashiers selected the same register.");
        check(parallel && closedDuringPayment, "Two cashiers did not serve customers in parallel.");
        check(game.State.Transactions == transactions + 6 && game.State.Revenue == revenue + 6 * 89, "Parallel registers lost or duplicated payments.");
        check(!first.CheckoutOpen, "An idle cashier immediately reopened a manually closed register.");
        game.Staff.ClockOut(0); game.Staff.ClockOut(1);
        game.Registers.SetOpen(first, false); game.Registers.SetOpen(second, false);
        check(game.Keeper.ServeCheckout(first), "Owner could not claim the freed register.");
        for (var frame = 0; frame < 1200 && !first.CheckoutOpen; frame++) await Frame();
        check(first.CheckoutOpen && game.Keeper.IsAtRegister, "Owner manning a closed register did not open it.");
        game.Registers.SetOpen(first, false); game.Save(false); game.TryLoad(false);
        for (var frame = 0; frame < 3; frame++) await Frame();
        check(!game.Checkout!.CheckoutOpen && game.State.Employees.All(e => !e.Summoned), "Load reopened a manually closed register or summoned dismissed employees.");
    }
}
