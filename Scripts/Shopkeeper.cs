// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>User-directed movement and physical supply-cabinet-to-display restocking.</summary>
public partial class Shopkeeper : Node
{
    private enum Work { Idle, Fetching, Collecting, Carrying, Placing }
    public PersonActor Actor { get; private set; } = null!;
    public bool Busy => _orders.Busy;
    public string Status { get; private set; } = "Ready · click floor to walk";
    private StoreGame _game = null!;
    private RestockOrders _orders = null!;
    private Work _work;
    private float _timer, _keyRepeat;
    public void Build(StoreGame game)
    {
        _game = game; Actor = new PersonActor { Name = "Shopkeeper" }; AddChild(Actor); Actor.Build(game, 3);
        Actor.Say("YOU"); BindState();
    }
    public void BindState()
    {
        _orders = new(_game.State); _work = Work.Idle;
        var position = new Vector3(_game.State.KeeperX, 0, _game.State.KeeperZ);
        if (!_game.Navigation.CanReach(position, position))
            position = Enumerable.Range(0, ShopNavigation.Width * ShopNavigation.Depth)
                .Select(i => ShopNavigation.Point(new(i % ShopNavigation.Width, i / ShopNavigation.Width)))
                .Where(p => _game.Navigation.CanReach(_game.Navigation.Entrance, p)).OrderBy(p => p.DistanceSquaredTo(position)).FirstOrDefault(_game.Navigation.Entrance);
        Actor.PlaceAt(position); Actor.SetCargo(_orders.Carrying ? _orders.Target?.Product.Name : null);
        Actor.GestureAmount = 0; Actor.ReachTarget = null; Actor.AttentionTarget = null;
        Status = _orders.Busy ? "Resuming restock" : "Ready · click floor to walk"; SyncPosition(); _game.Hud?.RefreshInspector();
    }
    public void SyncPosition() { _game.State.KeeperX = Actor.Position.X; _game.State.KeeperZ = Actor.Position.Z; }
    public bool Request(StoreItem item)
    {
        if (!_orders.Enqueue(item)) { _game.Notify("That spot is stocked or already queued."); return false; }
        _game.ClearEditHistory(); _game.Hud?.RefreshInspector();
        Status = $"Queued: {item.Product.Name}";
        _game.Notify("Restocking requested", "She will collect one replacement from the supply cabinet.");
        return true;
    }
    public void Cancel()
    {
        _orders.Cancel(); _work = Work.Idle; Actor.Stop(); Actor.SetCargo(null); Actor.GestureAmount = 0;
        Actor.AttentionTarget = null; Actor.ReachTarget = null; Status = "Ready · click floor to walk";
        _game.ClearEditHistory(); _game.Hud?.RefreshInspector();
    }
    public bool MoveTo(Vector3 position)
    {
        if (!_game.Navigation.CanReach(Actor.Position, position)) { _game.Notify("She can't reach that spot."); return false; }
        if (Busy) Cancel();
        var result = Actor.WalkTo(ShopNavigation.Point(ShopNavigation.Cell(position)));
        Status = result ? "Walking" : "No clear route"; return result;
    }
    public override void _Process(double delta)
    {
        var realDelta = Mathf.Min((float)delta, .1f); var dt = realDelta * _game.Speed;
        HandleMovementKeys(realDelta);
        SyncPosition();
        if (dt <= 0) return;
        var item = _orders.Target;
        if (_orders.Busy && item == null) { Cancel(); return; }
        if (item == null) { if (!Actor.IsWalking) Status = "Ready · click floor to walk"; return; }
        if (Actor.RouteFailed) { Fail("The restocking route is blocked."); return; }
        var source = _game.State.Items.FirstOrDefault(i => i.Kind == "stockroom" && i.Stock == 1);
        switch (_work)
        {
            case Work.Idle:
                if (_orders.Carrying) { CarryTo(item); break; }
                if (source == null) { Fail("Place a supply cabinet to collect replacements."); break; }
                var point = _game.Navigation.Approach(source, Actor.Position);
                if (point == null || !Actor.WalkTo(point.Value)) { Fail("She can't reach the supply cabinet."); break; }
                _work = Work.Fetching; Status = "Fetching a replacement"; break;
            case Work.Fetching:
                if (Actor.IsWalking) break;
                _work = Work.Collecting; _timer = 1.5f; break;
            case Work.Collecting:
                _timer -= dt; Actor.AttentionTarget = source?.Position + Vector3.Up;
                if (source != null) Actor.Face(source.Position, dt);
                Actor.GestureAmount = Mathf.Sin(Mathf.Clamp(1 - _timer / 1.5f, 0, 1) * Mathf.Pi);
                Actor.ReachTarget = source?.Position + Vector3.Up * 1.0f;
                if (_timer > 0) break;
                if (!_orders.Collect()) { Fail("Not enough cash to buy this replacement."); break; }
                Actor.SetCargo(item.Product.Name); Actor.GestureAmount = 0; Actor.ReachTarget = null; CarryTo(item); break;
            case Work.Carrying:
                if (Actor.IsWalking) break;
                _work = Work.Placing; _timer = 2.2f; Status = "Placing one item"; break;
            case Work.Placing:
                Actor.Face(item.Position, dt); Actor.AttentionTarget = item.Position + Vector3.Up * .2f;
                Actor.ReachTarget = item.Position + Vector3.Up * .18f;
                Actor.GestureAmount = Mathf.Sin(Mathf.Clamp(1 - _timer / 2.2f, 0, 1) * Mathf.Pi);
                _timer -= dt;
                if (_timer > 0) break;
                if (item.ParentId == 0 && item.Kind != "rug" && _game.Shoppers.Any(s => InsideFootprint(s.Position, item)))
                { _timer = .2f; Status = "Waiting for the aisle to clear"; break; }
                var delivered = _orders.Deliver();
                if (delivered == null) { Fail("This display spot is no longer available."); break; }
                Actor.SetCargo(null); Actor.GestureAmount = 0; Actor.ReachTarget = null; Actor.AttentionTarget = null;
                _work = Work.Idle; Status = "Ready · click floor to walk"; _game.StockChanged(delivered);
                _game.Notify($"Restocked {delivered.Product.Name}", "One visible object · one item available for sale"); break;
        }
    }
    private void CarryTo(StoreItem item)
    {
        var point = WorkPoint(item);
        if (point == null || !Actor.WalkTo(point.Value)) { Fail("She can't reach the empty display spot."); return; }
        _work = Work.Carrying; Actor.AttentionTarget = null; Status = $"Carrying {item.Product.Name}";
    }
    private Vector3? WorkPoint(StoreItem item)
    {
        var h = item.Footprint / 2 + Vector2.One * .3f;
        return Enumerable.Range(0, ShopNavigation.Width * ShopNavigation.Depth)
            .Select(i => ShopNavigation.Point(new(i % ShopNavigation.Width, i / ShopNavigation.Width)))
            .Where(p => item.ParentId != 0 || Mathf.Abs(p.X - item.X) >= h.X || Mathf.Abs(p.Z - item.Z) >= h.Y)
            .Where(p => p.DistanceTo(new(item.X, 0, item.Z)) >= .4f && _game.Navigation.CanReach(Actor.Position, p))
            .OrderBy(p => p.DistanceSquaredTo(new(item.X, 0, item.Z))).ThenBy(p => p.DistanceSquaredTo(Actor.Position))
            .Cast<Vector3?>().FirstOrDefault();
    }
    private void Fail(string message) { Cancel(); _game.Notify(message, "Any undelivered replacement has been refunded."); }
    private static bool InsideFootprint(Vector3 point, StoreItem item)
    { var h = item.Footprint / 2 + Vector2.One * .18f; return Mathf.Abs(point.X - item.X) < h.X && Mathf.Abs(point.Z - item.Z) < h.Y; }
    public void MoveFromKey(Key key)
    {
        var direction = key switch { Key.I => Vector3.Forward, Key.K => Vector3.Back, Key.J => Vector3.Left, Key.L => Vector3.Right, _ => Vector3.Zero };
        if (direction.IsZeroApprox()) return;
        _keyRepeat = .18f;
        MoveTo(Actor.Position + direction.Rotated(Vector3.Up, _game.Camera.Yaw) * .8f);
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
