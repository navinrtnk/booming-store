// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
namespace BoomingStore;

public enum EmployeeRole { Restocking, Sales, Cashier }
public sealed class EmployeeData
{
    public EmployeeRole Role { get; set; }
    public bool Summoned { get; set; } = true;
    public bool ClockOutRequested { get; set; }
    public double ShiftHours { get; set; }
    public long PaidQuarters { get; set; }
    public float X { get; set; } = 1;
    public float Z { get; set; } = 3;
    public int RegisterId { get; set; }
    public bool RegisterManned { get; set; }
    public int Target { get; set; }
    public bool Collected { get; set; }
}
/// <summary>Persistent staff deliveries and independent physical actors.</summary>
public sealed class EmployeeManager(StoreGame game)
{
    private enum WorkStage { Idle, WalkingToSource, Collecting, Delivering, Placing, WalkingToRegister, Serving }
    private sealed class Worker(EmployeeData data, PersonActor actor)
    {
        public EmployeeData Data = data;
        public PersonActor Actor = actor;
        public float Timer, Retry;
        public WorkStage Stage;
    }
    private readonly List<Worker> _workers = [];
    private readonly Dictionary<int, double> _blockedUntil = [];
    public IEnumerable<PersonActor> Actors { get { foreach (var worker in _workers) yield return worker.Actor; } }
    internal string DebugStatus => string.Join("; ", _workers.Select(w => $"{w.Data.Role} target={w.Data.Target} register={w.Data.RegisterId} carrying={w.Data.Collected} stage={w.Stage} at={w.Actor.Position} to={w.Actor.Destination}"));
    public int HelpedCustomers { get; private set; }
    public bool Busy => _workers.Any(w => w.Data.Target != 0);
    public bool Hire(EmployeeRole role)
    {
        if (!Enum.IsDefined(role) || game.State.Employees.Count >= 3) return false;
        game.ClearEditHistory(); game.State.Employees.Add(new() { Role = role, Summoned = false });
        game.Notify("Employee hired", "Use Staff → Summon to start their $100/hour shift."); return true;
    }
    public bool Summon(int index)
    {
        if (index < 0 || index >= game.State.Employees.Count) return false;
        var data = game.State.Employees[index]; if (data.Summoned) return false;
        game.ClearEditHistory(); data.Summoned = true; data.ClockOutRequested = false; data.ShiftHours = 0; data.PaidQuarters = 0; data.RegisterId = 0;
        data.X = game.Navigation.Entrance.X; data.Z = game.Navigation.Entrance.Z; Spawn(data); return true;
    }
    public bool ClockOut(int index)
    {
        if (index < 0 || index >= game.State.Employees.Count) return false;
        var data = game.State.Employees[index]; if (!data.Summoned) return false;
        game.ClearEditHistory(); data.ClockOutRequested = true;
        if (data.Target == 0 && !HasPayment(data)) FinishShift(_workers.Single(w => w.Data == data));
        return true;
    }
    private void FinishShift(Worker worker)
    {
        ReleaseRegister(worker);
        Payroll.EndShift(game.State, worker.Data); worker.Actor.StopVoice(); worker.Actor.QueueFree(); _workers.Remove(worker);
    }
    private void Spawn(EmployeeData data)
    {
        var actor = new PersonActor(); game.AddChild(actor); actor.Build(game, 2 + _workers.Count);
        var position = new Vector3(data.X, 0, data.Z);
        actor.PlaceAt(game.Navigation.CanStand(position) ? position : game.Navigation.Entrance);
        actor.Say(data.Role == EmployeeRole.Sales ? "Sales assistant" : "Restocking"); var worker = new Worker(data, actor);
        if (data.Role == EmployeeRole.Cashier && data.RegisterManned && game.Registers.Find(data.RegisterId) is { } register && actor.Position.DistanceTo(CheckoutCoordinator.StaffSpot(register)) < .18f) worker.Stage = WorkStage.Serving;
        _workers.Add(worker);
    }
    public void Bind()
    {
        foreach (var w in _workers) { w.Actor.StopVoice(); w.Actor.QueueFree(); }
        _workers.Clear(); _blockedUntil.Clear(); foreach (var data in game.State.Employees.Where(e => e.Summoned)) Spawn(data);
    }
    public void Sync()
    { foreach (var w in _workers) { w.Data.X = w.Actor.Position.X; w.Data.Z = w.Actor.Position.Z; } }
    public bool SetRole(int index, EmployeeRole role)
    {
        if (index < 0 || index >= game.State.Employees.Count || !Enum.IsDefined(role)) return false;
        var data = game.State.Employees[index]; if (data.Target != 0 || HasPayment(data)) return false;
        game.ClearEditHistory(); if (_workers.FirstOrDefault(w => w.Data == data) is { } previous) ReleaseRegister(previous); data.Role = role;
        var worker = _workers.FirstOrDefault(w => w.Data == data);
        if (worker != null) { worker.Actor.Stop(); worker.Stage = WorkStage.Idle; worker.Retry = 0; worker.Actor.Say(role == EmployeeRole.Sales ? "Sales assistant" : "Restocking"); }
        return true;
    }
    public bool Dismiss(int index)
    {
        if (index < 0 || index >= game.State.Employees.Count) return false;
        game.ClearEditHistory(); var data = game.State.Employees[index];
        var worker = _workers.FirstOrDefault(w => w.Data == data);
        if (worker != null)
        {
            Cancel(worker, game.State.Items.FirstOrDefault(i => i.Id == data.Target)); FinishShift(worker);
        }
        game.State.Employees.Remove(data); return true;
    }
    public void Tick(float dt)
    {
        if (dt <= 0) return;
        foreach (var w in _workers)
        {
            Payroll.Advance(game.State, w.Data, dt / StoreClock.SecondsPerHour);
            w.Retry -= dt;
            if (w.Data.ClockOutRequested && w.Data.Target == 0 && !HasPayment(w.Data)) continue;
            if (w.Data.Role == EmployeeRole.Cashier) Serve(w, dt); else if (w.Data.Role == EmployeeRole.Sales) Help(w); else Restock(w, dt);
        }
        for (var index = _workers.Count - 1; index >= 0; index--)
        {
            var worker = _workers[index];
            if (worker.Data.ClockOutRequested && worker.Data.Target == 0 && !HasPayment(worker.Data)) FinishShift(worker);
        }
        Payroll.PayArrears(game.State);
        Sync();
    }
    public PersonActor? ActorFor(EmployeeData data) => _workers.FirstOrDefault(w => w.Data == data)?.Actor;
    public bool HasPayment(EmployeeData data)
    {
        if (data.RegisterId == 0) return false;
        foreach (var shopper in game.Queue)
            if (shopper.RegisterId == data.RegisterId && shopper.State == Shopper.Activity.Paying) return true;
        return false;
    }
    public PersonActor? CashierFor(Shopper shopper)
    {
        foreach (var worker in _workers)
        {
            var data = worker.Data;
            if (data.Role != EmployeeRole.Cashier || data.RegisterId == 0 || data.RegisterId != shopper.RegisterId ||
                worker.Stage != WorkStage.Serving || worker.Actor.IsWalking) continue;
            var register = game.Registers.Find(data.RegisterId);
            if (register == null || worker.Actor.Position.DistanceTo(CheckoutCoordinator.StaffSpot(register)) >= .18f) continue;
            var paying = shopper.State == Shopper.Activity.Paying;
            if ((register.CheckoutOpen || paying) && (!data.ClockOutRequested || paying) && game.Registers.Head(register.Id) == shopper)
                return worker.Actor;
        }
        return null;
    }
    public bool CanServe(Shopper shopper) => CashierFor(shopper) != null;
    private void ReleaseRegister(Worker worker)
    {
        var register = game.Registers.Find(worker.Data.RegisterId); worker.Data.RegisterId = 0; worker.Data.RegisterManned = false;
        if (register != null) game.Registers.SetOpen(register, false);
    }
    public void LayoutChanged()
    {
        foreach (var w in _workers)
            if (w.Data.RegisterId != 0 && game.Registers.Find(w.Data.RegisterId) == null) { w.Data.RegisterId = 0; w.Stage = WorkStage.Idle; w.Actor.Stop(); }
    }
    private void Serve(Worker worker, float dt)
    {
        var data = worker.Data;
        if (game.Registers.Find(data.RegisterId) is not { } register)
        {
            if (worker.Retry > 0 || data.ClockOutRequested) return;
            register = game.Registers.Available(worker.Actor); worker.Retry = 1;
            if (register == null) { worker.Actor.Say("Waiting for a register"); Idle(worker); return; }
            data.RegisterId = register.Id; data.RegisterManned = false; worker.Stage = WorkStage.Idle;
        }
        if (worker.Actor.ConsumeRouteFailure()) { ReleaseRegister(worker); worker.Stage = WorkStage.Idle; worker.Retry = 3; return; }
        if (worker.Stage == WorkStage.Idle)
        {
            if (!worker.Actor.WalkTo(CheckoutCoordinator.StaffSpot(register))) { ReleaseRegister(worker); worker.Retry = 3; return; }
            worker.Stage = WorkStage.WalkingToRegister;
        }
        if (worker.Stage == WorkStage.WalkingToRegister && !worker.Actor.IsWalking) { worker.Stage = WorkStage.Serving; data.RegisterManned = true; game.Registers.SetOpen(register, true); }
        if (worker.Stage != WorkStage.Serving) return;
        worker.Actor.Face(register.Position, dt); worker.Actor.Say(register.CheckoutOpen ? "Cashier · open" : "Cashier · closed");
        var customer = game.Registers.Head(register.Id);
        worker.Actor.AttentionTarget = customer?.Position + Vector3.Up * 1.4f;
        worker.Actor.ReachTarget = register.Position + Vector3.Up * 1.1f;
        worker.Actor.GestureAmount = customer != null ? .22f : 0;
    }
    private void Idle(Worker w)
    {
        if (w.Actor.IsWalking || w.Actor.Position.DistanceTo(game.Navigation.Entrance) > 1.2f) return;
        var spot = new Vector3(2 + _workers.IndexOf(w) * .7f, 0, 3);
        if (game.Navigation.CanReach(w.Actor.Position, spot)) w.Actor.WalkTo(spot);
    }
    private void Help(Worker w)
    {
        if (!game.Open) { Idle(w); return; }
        if (w.Retry > 0) return;
        var shopper = game.Shoppers.FirstOrDefault(s => s.State == Shopper.Activity.Browsing);
        if (shopper == null) { Idle(w); return; }
        if (w.Actor.Position.DistanceTo(shopper.Position) < 1.5f)
        {
            w.Actor.Stop(); w.Actor.Face(shopper.Position, .5f); w.Actor.React(PersonBeat.Chat, shopper.Position + Vector3.Up, VoiceLine.Greeting);
            shopper.ReceiveHelp(w.Actor.Position); HelpedCustomers++; w.Retry = 8; return;
        }
        var spot = WorkPoint(shopper.Position, Vector2.One * .5f, w.Actor);
        if (spot is { } point) w.Actor.WalkTo(point); w.Retry = 2;
    }
    private Vector3? WorkPoint(Vector3 target, Vector2 footprint, PersonActor actor)
    {
        var half = footprint / 2 + Vector2.One * .35f;
        return Enumerable.Range(0, ShopNavigation.Width * ShopNavigation.Depth).Select(i => ShopNavigation.Point(new(i % ShopNavigation.Width, i / ShopNavigation.Width)))
            .Where(p => Math.Abs(p.X - target.X) >= half.X || Math.Abs(p.Z - target.Z) >= half.Y)
            .OrderBy(p => p.DistanceSquaredTo(target)).Where(p => game.People.All(other => other == actor || other.Position.DistanceTo(p) > .55f && (other.Destination is not { } goal || other.Finished || goal.DistanceTo(p) > .55f)))
            .Where(p => game.Navigation.CanReach(actor.Position, p)).Cast<Vector3?>().FirstOrDefault();
    }
    private void Restock(Worker w, float dt)
    {
        var data = w.Data;
        if (data.Target == 0)
        {
            if (w.Retry > 0 || data.ClockOutRequested) return; w.Retry = 1;
            var empty = game.State.Items.FirstOrDefault(i => i.ForSale && i.Stock == 0 && (!_blockedUntil.TryGetValue(i.Id, out var until) || until <= game.State.WorldHours) && !(game.State.NextKeeperCommand is { Kind: KeeperCommandKind.Restock } next && next.ItemId == i.Id) && !game.State.RestockQueue.Contains(i.Id) && !game.State.Employees.Any(e => e.Target == i.Id));
            if (empty == null) { Idle(w); return; } data.Target = empty.Id; w.Stage = WorkStage.Idle;
        }
        var item = game.State.Items.FirstOrDefault(i => i.Id == data.Target);
        if (item == null || item.Stock != 0 || w.Actor.ConsumeRouteFailure()) { Cancel(w, item); return; }
        if (w.Stage == WorkStage.Idle)
        {
            if (data.Collected) { DeliverRoute(w, item); return; }
            var source = game.State.Items.FirstOrDefault(i => i.Kind == "stockroom" && i.Stock == 1);
            var point = source == null ? null : game.Navigation.Approach(source, w.Actor.Position);
            if (point == null || !w.Actor.WalkTo(point.Value)) { Cancel(w, item); return; } w.Stage = WorkStage.WalkingToSource;
        }
        else if (w.Stage == WorkStage.WalkingToSource && !w.Actor.IsWalking) { w.Stage = WorkStage.Collecting; w.Timer = 1.5f; }
        else if (w.Stage == WorkStage.Collecting)
        {
            w.Timer -= dt; if (w.Timer > 0) return;
            if (game.State.Cash < item.Product.Cost) { Cancel(w, item); return; }
            game.State.Cash -= item.Product.Cost; data.Collected = true; w.Actor.SetCargo(item.Product.Name);
            game.Audio.Play(ShopSound.Pickup, w.Actor.Position); DeliverRoute(w, item);
        }
        else if (w.Stage == WorkStage.Delivering && !w.Actor.IsWalking) { w.Stage = WorkStage.Placing; w.Timer = 2.2f; }
        else if (w.Stage == WorkStage.Placing)
        {
            w.Timer -= dt; w.Actor.Face(item.Position, dt);
            if (w.Timer > 0 || item.ParentId == 0 && item.Kind != "rug" && game.People.Any(p => Math.Abs(p.Position.X - item.X) < item.Footprint.X / 2 + .18f && Math.Abs(p.Position.Z - item.Z) < item.Footprint.Y / 2 + .18f)) return;
            item.RestockNew(); data.Target = 0; data.Collected = false; w.Actor.SetCargo(null);
            game.Audio.Play(ShopSound.Place, item.Position); game.StockChanged(item); w.Actor.Say("Restocking"); w.Retry = 1;
        }
    }
    private void DeliverRoute(Worker w, StoreItem item)
    {
        var point = WorkPoint(item.Position, item.Footprint, w.Actor); w.Actor.SetCargo(item.Product.Name);
        if (point == null || !w.Actor.WalkTo(point.Value)) { Cancel(w, item); return; } w.Stage = WorkStage.Delivering;
    }
    private void Cancel(Worker w, StoreItem? item)
    {
        if (item != null) _blockedUntil[item.Id] = game.State.WorldHours + .05;
        if (w.Data.Collected && item != null) game.State.Cash += item.Product.Cost;
        w.Data.Target = 0; w.Data.Collected = false; w.Actor.SetCargo(null); w.Actor.Stop(); w.Stage = WorkStage.Idle; w.Retry = 5;
    }
}
