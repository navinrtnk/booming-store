// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>One playable owner: movement, complete physical deliveries and one customer at a time.</summary>
public partial class Shopkeeper : Node
{
    private enum Work { Idle, Fetching, Collecting, Carrying, Placing, ToRegister, Serving }
    public PersonActor Actor { get; private set; } = null!;
    public int OperatingRegisterId => _checkoutRequested ? _game.State.KeeperRegisterId : 0;
    private StoreItem? Register => _game.Registers.Find(_game.State.KeeperRegisterId) ?? _game.Checkout;
    public bool Busy => _orders.Busy;
    public bool IsAtRegister => _game.CashierSpot is { } spot && !Actor.IsWalking && Actor.Position.DistanceTo(spot) < .18f;
    public string Status => _status + (Next is { } command ? " · next: " + command.Description : "");
    public KeeperCommand? Next => _game.State.NextKeeperCommand;
    private StoreGame _game = null!;
    private RestockOrders _orders = null!;
    private Work _work;
    private float _timer, _keyRepeat, _retryCheckout;
    private bool _manualWalk, _checkoutRequested;
    private string _status = "Ready · click floor to walk";
    private bool ActivePayment => _work == Work.Serving && IsAtRegister && _game.Registers.Head(OperatingRegisterId)?.State == Shopper.Activity.Paying;
    private bool ActiveTask => ActivePayment || _orders.Carrying || _orders.Busy && _work != Work.Idle;
    public bool CanServe(Shopper shopper) => _work == Work.Serving && !_orders.Busy && !_manualWalk && IsAtRegister &&
        _game.Registers.Head(OperatingRegisterId) == shopper && shopper.RegisterId == OperatingRegisterId && Register != null && (Register.CheckoutOpen || shopper.State == Shopper.Activity.Paying) && (Next == null || shopper.State == Shopper.Activity.Paying);

    public void Build(StoreGame game)
    {
        _game = game; Actor = new PersonActor { Name = "Shopkeeper" }; AddChild(Actor); Actor.Build(game, 1, shopkeeper: true);
        Actor.Say("YOU"); BindState();
    }
    public void BindState()
    {
        _orders = new(_game.State); _work = Work.Idle; _manualWalk = false; _checkoutRequested = _game.State.OwnerWorkingCheckout; if (_checkoutRequested) _work = Work.ToRegister;
        var position = new Vector3(_game.State.KeeperX, 0, _game.State.KeeperZ);
        if (!_game.Navigation.CanStand(position))
            position = Enumerable.Range(0, ShopNavigation.Width * ShopNavigation.Depth)
                .Select(i => ShopNavigation.Point(new(i % ShopNavigation.Width, i / ShopNavigation.Width)))
                .Where(p => _game.Navigation.CanReach(_game.Navigation.Entrance, p)).OrderBy(p => p.DistanceSquaredTo(position)).FirstOrDefault(_game.Navigation.Entrance);
        Actor.PlaceAt(position);
        if (_game.CashierSpot is { } counterSpot && position.DistanceTo(counterSpot) < .2f && _game.Checkout != null)
            Actor.Rotation = new(0, Mathf.DegToRad(_game.Checkout.Rotation), 0);
        Actor.SetCargo(_orders.Carrying ? _orders.Target?.Product.Name : null);
        if (_checkoutRequested && _game.CashierSpot is { } resumeSpot)
        {
            if (Actor.Position.DistanceTo(resumeSpot) < .18f) _work = Work.Serving;
            else Actor.WalkTo(resumeSpot);
        }
        ResetGesture(); _status = _orders.Busy ? "Resuming restock" : "Ready · click floor to walk";
        SyncPosition(); _game.Hud?.RefreshInspector();
    }
    public void SyncPosition() { _game.State.KeeperX = Actor.Position.X; _game.State.KeeperZ = Actor.Position.Z; }
    public void Welcome(Shopper shopper)
    {
        shopper.React(PersonBeat.Greeting, Actor.Position + Vector3.Up * 1.4f, VoiceLine.Greeting);
        if (Actor.Position.DistanceTo(shopper.Position) < 10)
            Actor.React(PersonBeat.Greeting, shopper.Position + Vector3.Up * 1.4f, VoiceLine.Greeting, .65f);
    }
    public void BeginCheckout(Shopper shopper)
    {
        Actor.React(PersonBeat.Chat, shopper.Position + Vector3.Up * 1.4f, VoiceLine.Agree);
        shopper.React(PersonBeat.Chat, Actor.Position + Vector3.Up * 1.4f, VoiceLine.Agree, .65f);
    }
    public void ThankCustomer(Shopper shopper)
    {
        Actor.React(PersonBeat.Thanks, shopper.Position + Vector3.Up * 1.4f, VoiceLine.Thanks, .45f);
        shopper.React(PersonBeat.Thanks, Actor.Position + Vector3.Up * 1.4f, VoiceLine.Thanks);
    }
    public bool Request(StoreItem item)
    {
        if (!_game.State.Items.Contains(item) || item.Stock != 0 || !item.ForSale)
        { _game.Notify("That display is already stocked."); return false; }
        if (_orders.Target == item && ActiveTask)
        { _game.State.NextKeeperCommand = null; _game.Notify("She is already restocking this display."); return true; }
        return Submit(new(KeeperCommandKind.Restock, ItemId: item.Id));
    }
    public bool MoveTo(Vector3 position)
    {
        if (!_game.Navigation.CanReach(Actor.Position, position)) { _game.Notify("She can't reach that spot."); return false; }
        return Submit(new(KeeperCommandKind.Walk, position.X, position.Z));
    }
    public bool ServeCheckout(StoreItem? requested = null)
    {
        var register = _game.Registers.Available(Actor, requested?.Id ?? _game.State.KeeperRegisterId);
        if (register == null || requested != null && requested.Id != register.Id) { _game.Notify("No available register", "The other cashier already has that register, or its route is blocked."); return false; }
        return Submit(new(KeeperCommandKind.Checkout, ItemId: register.Id));
    }
    public void Cancel() => Submit(new(KeeperCommandKind.Stop));
    private bool Submit(KeeperCommand command)
    {
        _game.ClearEditHistory();
        if (ActiveTask)
        {
            _game.State.NextKeeperCommand = command;
            _game.Notify("Next: " + command.Description, "She will finish this customer or delivery, then follow your newest command.");
            return true;
        }
        _game.State.NextKeeperCommand = null;
        return StartCommand(command);
    }
    private bool StartCommand(KeeperCommand command)
    {
        if (_checkoutRequested && (command.Kind != KeeperCommandKind.Checkout || command.ItemId != OperatingRegisterId) && Register is { } oldRegister) _game.Registers.SetOpen(oldRegister, false);
        _orders.Cancel(); _work = Work.Idle; _manualWalk = false; _checkoutRequested = false;
        if (command.Kind != KeeperCommandKind.Walk) Actor.Stop(); Actor.SetCargo(null); ResetGesture();
        switch (command.Kind)
        {
            case KeeperCommandKind.Walk:
                _manualWalk = Actor.WalkTo(command.Position); _status = _manualWalk ? "Walking" : "No clear route";
                return _manualWalk;
            case KeeperCommandKind.Restock:
                var item = _game.State.Items.FirstOrDefault(i => i.Id == command.ItemId);
                if (item == null || !_orders.Enqueue(item)) { _game.Notify("That display is no longer empty."); return false; }
                _status = "Fetching a replacement"; _game.Hud?.RefreshInspector();
                _game.Notify("Restocking requested", "She will collect and carry back one replacement."); return true;
            case KeeperCommandKind.Checkout:
                var assigned = _game.Registers.Available(Actor, command.ItemId);
                if (assigned == null) { _status = "No available register"; return false; }
                _game.State.KeeperRegisterId = assigned.Id;
                if (_game.CashierSpot is not { } spot || !Actor.WalkTo(spot)) { _status = "Checkout route blocked"; return false; }
                _checkoutRequested = true; _work = Work.ToRegister; _status = "Returning to checkout"; return true;
            default:
                _status = "Stopped · choose your next task"; _game.Hud?.RefreshInspector(); return true;
        }
    }
    private bool StartNextCommand()
    {
        if (Next is not { } command) return false;
        _game.State.NextKeeperCommand = null; StartCommand(command); return true;
    }
    public override void _Process(double delta)
    {
        var realDelta = Mathf.Min((float)delta, .1f); var dt = realDelta * _game.Speed;
        HandleMovementKeys(realDelta); SyncPosition();
        if (dt <= 0) return;
        if (!ActiveTask && Next != null) StartNextCommand();
        var item = _orders.Target;
        if (_orders.Busy && item == null) { Fail("That restocking display is no longer available."); return; }
        if (item == null)
        {
            if (_manualWalk)
            {
                if (Actor.IsWalking) return;
                _manualWalk = false;
                if (Actor.ConsumeRouteFailure()) _game.Notify("Her route is blocked. Choose another floor spot.");
            }
            ServeQueue(dt); return;
        }
        if (Actor.RouteFailed) { Fail("The restocking route is blocked."); return; }
        var source = _game.State.Items.FirstOrDefault(i => i.Kind == "stockroom" && i.Stock == 1);
        switch (_work)
        {
            case Work.Idle:
                if (_orders.Carrying) { CarryTo(item); break; }
                if (source == null) { Fail("Place a supply cabinet to collect replacements."); break; }
                var point = _game.Navigation.Approach(source, Actor.Position);
                if (point == null || !Actor.WalkTo(point.Value)) { Fail("She can't reach the supply cabinet."); break; }
                _work = Work.Fetching; _status = "Fetching a replacement"; break;
            case Work.Fetching:
                if (Actor.IsWalking) break;
                _work = Work.Collecting; _timer = 1.5f;
                if (source != null) _game.Audio.Play(ShopSound.Pickup, source.Position);
                Actor.React(PersonBeat.Work, source?.Position + Vector3.Up, VoiceLine.Work); break;
            case Work.Collecting:
                _timer -= dt; Actor.AttentionTarget = source?.Position + Vector3.Up;
                if (source != null) Actor.Face(source.Position, dt);
                Actor.GestureAmount = Mathf.Sin(Mathf.Clamp(1 - _timer / 1.5f, 0, 1) * Mathf.Pi);
                Actor.ReachTarget = source?.Position + Vector3.Up;
                if (_timer > 0) break;
                if (!_orders.Collect()) { Fail("Not enough cash to buy this replacement."); break; }
                Actor.SetCargo(item.Product.Name); ResetGesture(); CarryTo(item); break;
            case Work.Carrying:
                if (Actor.IsWalking) break;
                _work = Work.Placing; _timer = 2.2f; _status = "Placing one item"; break;
            case Work.Placing:
                Actor.Face(item.Position, dt); Actor.AttentionTarget = item.Position + Vector3.Up * .2f;
                Actor.ReachTarget = item.Position + Vector3.Up * .18f;
                Actor.GestureAmount = Mathf.Sin(Mathf.Clamp(1 - _timer / 2.2f, 0, 1) * Mathf.Pi); _timer -= dt;
                if (_timer > 0) break;
                if (item.ParentId == 0 && item.Kind != "rug" && _game.People.Any(s => InsideFootprint(s.Position, item)))
                { _timer = .2f; _status = "Waiting for the aisle to clear"; break; }
                var delivered = _orders.Deliver();
                if (delivered == null) { Fail("This display spot is no longer available."); break; }
                Actor.SetCargo(null); ResetGesture(); _work = Work.Idle; _status = "Ready · click floor to walk";
                _game.Audio.Play(ShopSound.Place, delivered.Position); Actor.React(PersonBeat.Thanks, delivered.Position + Vector3.Up, VoiceLine.Work);
                _game.StockChanged(delivered); _game.Notify($"Restocked {delivered.Product.Name}", "One visible object · one item available for sale");
                if (Next != null) StartNextCommand(); break;
        }
    }
    private void ResetGesture()
    { Actor.GestureAmount = 0; Actor.AttentionTarget = null; Actor.ReachTarget = null; Actor.Say("YOU"); }
    private void ServeQueue(float dt)
    {
        if (!_checkoutRequested)
        {
            if (_work is Work.ToRegister or Work.Serving) { Actor.Stop(); ResetGesture(); _work = Work.Idle; }
            if (!Actor.IsWalking) _status = "Ready · click floor to walk";
            return;
        }
        _retryCheckout -= dt;
        if (Actor.ConsumeRouteFailure()) { _work = Work.Idle; _retryCheckout = 1; }
        if (_work == Work.Idle)
        {
            if (_retryCheckout > 0) return;
            if (_game.CashierSpot is not { } spot || !Actor.WalkTo(spot)) { _status = "Checkout route blocked"; _retryCheckout = 1; return; }
            _work = Work.ToRegister; _status = "Returning to checkout";
        }
        if (_work == Work.ToRegister && !Actor.IsWalking) { _work = Work.Serving; if (Register != null) _game.Registers.SetOpen(Register, true); }
        if (_work != Work.Serving || Register == null) return;
        Actor.Face(Register.Position, dt);
        var customer = _game.Registers.Head(OperatingRegisterId);
        if (customer == null) { _status = "At checkout · waiting"; ResetGesture(); return; }
        _status = "Serving customers"; Actor.Say("Welcome in!");
        Actor.AttentionTarget = customer.Position + Vector3.Up * 1.45f;
        Actor.ReachTarget = Register.Position + new Vector3(.4f, 1.22f, -.12f).Rotated(Vector3.Up, Mathf.DegToRad(Register.Rotation));
        Actor.GestureAmount = .20f + Mathf.Sin((float)_game.State.TradingSeconds * 1.5f) * .07f;
    }
    private void CarryTo(StoreItem item)
    {
        var point = WorkPoint(item);
        if (point == null || !Actor.WalkTo(point.Value)) { Fail("She can't reach the empty display spot."); return; }
        _work = Work.Carrying; Actor.AttentionTarget = null; _status = $"Carrying {item.Product.Name}";
    }
    private Vector3? WorkPoint(StoreItem item)
    {
        var h = item.Footprint / 2 + Vector2.One * .3f;
        return Enumerable.Range(0, ShopNavigation.Width * ShopNavigation.Depth)
            .Select(i => ShopNavigation.Point(new(i % ShopNavigation.Width, i / ShopNavigation.Width)))
            .Where(p => item.ParentId != 0 || Mathf.Abs(p.X - item.X) >= h.X || Mathf.Abs(p.Z - item.Z) >= h.Y)
            .Where(p => p.DistanceTo(new(item.X, 0, item.Z)) >= .4f)
            .OrderBy(p => p.DistanceSquaredTo(new(item.X, 0, item.Z))).ThenBy(p => p.DistanceSquaredTo(Actor.Position))
            .Where(p => _game.Navigation.CanReach(Actor.Position, p)).Cast<Vector3?>().FirstOrDefault();
    }
    private void Fail(string message)
    {
        _orders.Cancel(); Actor.Stop(); Actor.SetCargo(null); ResetGesture(); _work = Work.Idle;
        _game.Notify(message, "Any undelivered replacement has been refunded."); _game.Hud?.RefreshInspector();
        StartNextCommand();
    }
    private static bool InsideFootprint(Vector3 point, StoreItem item)
    { var h = item.Footprint / 2 + Vector2.One * .18f; return Mathf.Abs(point.X - item.X) < h.X && Mathf.Abs(point.Z - item.Z) < h.Y; }
    public void MoveFromKey(Key key)
    {
        var direction = key switch { Key.I => Vector3.Forward, Key.K => Vector3.Back, Key.J => Vector3.Left, Key.L => Vector3.Right, _ => Vector3.Zero };
        if (direction.IsZeroApprox()) return;
        _keyRepeat = .18f; MoveTo(Actor.Position + direction.Rotated(Vector3.Up, _game.Camera.Yaw) * .8f);
    }
    private void HandleMovementKeys(float delta)
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit) return;
        _keyRepeat -= delta;
        var move = new Vector3((Input.IsPhysicalKeyPressed(Key.L) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.J) ? 1 : 0), 0,
            (Input.IsPhysicalKeyPressed(Key.K) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.I) ? 1 : 0));
        if (move.IsZeroApprox() || _keyRepeat > 0) return;
        _keyRepeat = .18f; MoveTo(Actor.Position + move.Normalized().Rotated(Vector3.Up, _game.Camera.Yaw) * .8f);
    }
}
