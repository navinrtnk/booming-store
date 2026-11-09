// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
namespace BoomingStore;

public static class ElectronicsChecks
{
    internal static StoreItem Add(StoreGame game, string kind, float x, float z, StoreItem? support = null, float y = 0)
    {
        var product = Catalog.Get(kind);
        var item = new StoreItem { Id = game.State.NextId++, Kind = kind, X = x, Z = z, Y = y, ParentId = support?.Id ?? 0,
            Price = product.Price, ForSale = product.Merchandise };
        if (!StoreLayout.CanPlace(game.State, item)) throw new InvalidOperationException("Invalid electronics fixture: " + kind);
        game.State.Items.Add(item); game.State.Cash -= product.Cost; game.StockChanged(item); return item;
    }
    internal static void Furnish(StoreGame game)
    {
        Add(game, "checkout", -3.6f, -3); Add(game, "stockroom", -5.15f, -3.1f);
        var rack = Add(game, "tv_rack", -.9f, -1.6f);
        Add(game, "tv43", -.9f, -1.6f, rack, .28f); Add(game, "tv65", -.9f, -1.6f, rack, 1.28f);
        var rack2 = Add(game, "tv_rack", 2.5f, -2.8f);
        Add(game, "tv32", 2.5f, -2.8f, rack2, .28f); Add(game, "tv75", 2.5f, -2.8f, rack2, 1.28f);
        var shelf = Add(game, "tech_shelf", 3.8f, .5f);
        Add(game, "console", 3.8f, .5f, shelf, .25f); Add(game, "laptop_pro", 3.8f, .5f, shelf, .85f); Add(game, "speaker", 3.8f, .5f, shelf, 1.45f);
    }
    public static async Task Run(StoreGame game, Action<bool, string> check)
    {
        async Task Frame() => await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        game.NewStore(); check(game.State.Items.Count == 0 && game.State.Cash == 10000 && game.State.Employees.Count == 0, "New game did not reset to empty $10,000 store.");
        Furnish(game);
        var lowerTelevision = game.State.Items.First(i => i.Kind == "tv43");
        game.Camera.ZoomTo(lowerTelevision.Position);
        for (var i = 0; i < 30; i++) await Frame();
        game._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = game.Camera.UnprojectPosition(lowerTelevision.Position + Vector3.Up * .3f) });
        check(game.Selected?.Id == lowerTelevision.Id, "Shelf frame swallowed selection of the lower-deck TV."); game.Camera.Home();
        var rack = game.State.Items.First(i => i.Kind == "tv_rack");
        var huge = new StoreItem { Id = 999, Kind = "tv75", ParentId = rack.Id, X = rack.X, Z = rack.Z, Y = .28f, Price = 2299 };
        check(!StoreLayout.CanPlace(game.State, huge), "Oversized TV fit through the upper shelf.");
        check(rack.Product.Supports(.28f) && rack.Product.Supports(1.28f), "Both shelf decks were not supported.");
        check(Catalog.Get("tv75").Width > Catalog.Get("tv32").Width && Catalog.Get("tv75").Price > Catalog.Get("tv32").Price, "TV tiers did not scale size and price.");
        var trade = new StoreTrading(game.State); var televisions = game.State.Items.Where(i => i.Product.Category == "TVs").ToArray();
        var cash = game.State.Cash; var expected = televisions.Take(3).Sum(i => i.Price);
        check(trade.TryReserve(700, televisions[0]) && trade.TryReserve(700, televisions[1]) && trade.TryReserve(700, televisions[2]) && !trade.TryReserve(700, televisions[3]), "Basket limit or multiple-item pickup failed.");
        var receipt = trade.Checkout(700);
        check(receipt?.Lines.Count == 3 && receipt.Price == expected && game.State.Cash == cash + expected && trade.Checkout(700) == null, "Basket checkout did not settle once at the combined price.");
        foreach (var item in televisions.Take(3)) { item.RestockNew(); game.StockChanged(item); }
        var employee = new EmployeeData { Summoned = true }; var payrollState = new StoreState();
        Payroll.Advance(payrollState, employee, 10d / 60); Payroll.EndShift(payrollState, employee);
        check(payrollState.WageExpense == 25 && payrollState.Cash == 9975, "Nearest-quarter shift settlement failed.");
        Payroll.EndShift(payrollState, employee); Payroll.Advance(payrollState, employee, 10);
        check(payrollState.WageExpense == 25, "Off-shift or repeated clock-out charged wages.");
        employee = new() { Summoned = true }; payrollState = new(); payrollState.Employees.Add(employee);
        Payroll.Advance(payrollState, employee, .34); payrollState = StoreState.Parse(payrollState.Serialize());
        Payroll.Advance(payrollState, payrollState.Employees[0], .16); Payroll.EndShift(payrollState, payrollState.Employees[0]);
        check(payrollState.WageExpense == 50 && payrollState.Cash == 9950, "Payroll save/load paid the same quarter twice.");
        var poor = new StoreState { Cash = 5 }; var worker = new EmployeeData(); Payroll.Advance(poor, worker, 1);
        check(poor.Cash == 0 && poor.WagesPayable == 95, "Insufficient payroll cash lost debt or became negative.");
        poor.Cash += 100; Payroll.PayArrears(poor); check(poor.WagesPayable == 0 && poor.Cash == 5, "Wage arrears did not settle from later income.");
        check(StoreClock.Traffic(18, .5) > StoreClock.Traffic(10, .5) && StoreClock.Traffic(15, .5) > StoreClock.Traffic(2, .5) * 5, "Afternoon/evening and overnight traffic were not distinct.");
        check(StoreClock.Traffic(18, 1) > StoreClock.Traffic(18, 0) * 2, "Customer mood outcomes did not affect traffic.");
        for (var i = 0; i < 30; i++) StoreClock.RecordDeparture(game.State, CustomerMood.Disappointed);
        check(game.State.Departures.Count == 20 && StoreClock.Reputation(game.State) == 0, "Recent unhappy visits did not lower reputation.");
        for (var i = 0; i < 20; i++) StoreClock.RecordDeparture(game.State, CustomerMood.Happy);
        check(StoreClock.Reputation(game.State) == 1, "Happy visits could not restore reputation.");
        check(game.Staff.Hire(EmployeeRole.Restocking) && game.Staff.Hire(EmployeeRole.Restocking), "Multiple employees could not share restocking role.");
        check(!game.Staff.Actors.Any(), "Off-shift hires spawned or began getting paid.");
        game.Staff.Summon(0); game.Staff.Summon(1); game.SetSpeed(3);
        foreach (var item in televisions.Take(2)) { item.Stock = 0; game.StockChanged(item); }
        await Frame(); await Frame();
        check(game.State.Employees.All(e => e.Target != 0) && game.State.Employees.Select(e => e.Target).Distinct().Count() == 2, "Restockers claimed the same spot.");
        for (var frame = 0; frame < 2800 && !game.State.Employees.Any(e => e.Collected); frame++) await Frame();
        check(game.State.Employees.Any(e => e.Collected), "Summoned employees did not physically collect stock.");
        game.Save(false); var paidBeforeLoad = game.State.WageExpense; game.TryLoad(false);
        check(game.State.Employees.Any(e => e.Collected) && game.State.WageExpense == paidBeforeLoad, "Paid employee delivery or wages did not survive saving.");
        for (var frame = 0; frame < 2800 && game.State.Items.Any(i => i.Product.Category == "TVs" && i.Stock == 0); frame++) await Frame();
        check(game.State.Items.Where(i => i.Product.Category == "TVs").All(i => i.Stock == 1), "Coordinated employees did not finish shelf replenishment: " + game.Staff.DebugStatus);
        check(game.Staff.ClockOut(0) && !game.State.Employees[0].Summoned && game.Staff.Actors.Count() == 1, "Dismiss/clock-out did not remove active worker.");
        check(game.Staff.Dismiss(1) && game.State.Employees.Count == 1, "Employee could not be fired.");
        game.ToggleOpen(); game.Save(false); var savedHour = game.State.WorldHours; game.TryLoad(false);
        check(game.Open && game.State.WorldHours == savedHour && game.State.Employees.Count == 1, "Save/load did not preserve open state, clock and roster.");
        game.SetSpeed(0); var pausedHour = game.State.WorldHours; var pausedPay = game.State.WageExpense;
        for (var i = 0; i < 30; i++) await Frame();
        check(game.State.WorldHours == pausedHour && game.State.WageExpense == pausedPay, "Pause advanced the clock or payroll.");
        game.Save(false); var archived = StorePersistence.Archive(game.SavePath);
        check(StorePersistence.Load(archived).State?.WorldHours == game.State.WorldHours, "Archived save did not preserve the store clock.");
        GD.Print("ELECTRONICS_CHECKS complete"); await RegisterChecks.Run(game, check);
    }
}
