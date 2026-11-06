// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
using System.IO;

namespace BoomingStore;

/// <summary>Owns the editable store, transactional history, and settled trading ledger.</summary>
public partial class StoreGame : Node3D
{
    public StoreState State { get; private set; } = new();
    public ShopNavigation Navigation { get; } = new();
    public StoreCamera Camera { get; private set; } = null!;
    public StoreHud Hud { get; private set; } = null!;
    public StoreVisuals Visuals { get; private set; } = null!;
    public List<Shopper> Shoppers { get; } = [];
    public List<Shopper> Queue { get; } = [];
    public bool Open { get; private set; }
    public bool CanOpen => !Open && Shoppers.Count == 0;
    public bool Editing => CanOpen && Keeper?.Busy != true;
    public Shopkeeper Keeper { get; private set; } = null!;
    public IEnumerable<PersonActor> People => Keeper == null ? Shoppers : Shoppers.Cast<PersonActor>().Append(Keeper.Actor);
    public float Speed { get; private set; } = 1;
    public bool CutWalls { get; private set; } = true;
    public string Tool { get; private set; } = "select";
    public string Category { get; set; } = "Objects";
    public int PaintColor { get; private set; }
    public int FloorStyle { get; private set; }
    public StoreItem? Selected { get; private set; }
    public StoreItem? Checkout => State.Items.FirstOrDefault(i => i.Kind == "checkout" && !i.ForSale);
    public string Status { get; private set; } = "Your shop, your way. Open the doors when you're ready.";
    public string StatusDetail { get; private set; } = "";
    public bool CanUndo => Editing && _history.CanUndo;
    public bool CanRedo => Editing && _history.CanRedo;
    private Node3D _layout = null!, _floors = null!, _walls = null!, _overlay = null!;
    private PersonActor? _cashier;
    private readonly Dictionary<int, Node3D> _objects = [];
    private readonly StoreHistory _history = new();
    private StoreTrading _trading = null!;
    private Product? _placing;
    private StoreItem? _ghost;
    private int _moving, _rotation;
    private Vector3? _wallStart;
    private float _hudTime, _lastYaw;
    private readonly CustomerArrivals _arrivals = new(() => GD.Randf());
    private int _profile;
    private (Vector3? Point, int Rotation, int Color, int? Selected, string Tool, Vector3? WallStart, bool OverUi)? _previewState;
    private bool _previewValid, _closing, _floorGrid;
    internal string SavePath => ProjectSettings.GlobalizePath(_automatedChecks ? "res://.godot/test-results/store.json" : "user://store.json");
    private bool _automatedChecks = false;

    public override void _Ready()
    {
        Visuals = new(); Visuals.Plaster.NormalScale = .16f; Visuals.Floor.NormalScale = .25f; Visuals.BuildSurroundings(this);
        _layout = Geometry.Group(this, "Layout"); _floors = Geometry.Group(this, "Floors");
        _walls = Geometry.Group(this, "Walls"); _overlay = Geometry.Group(this, "Placement");
        Camera = new StoreCamera { Current = true }; AddChild(Camera);
#if DEBUG
        _automatedChecks = OS.GetCmdlineUserArgs().Any(arg => arg is "--test" or "--preview-walk" or "--preview-owner");
#endif
        Seed();
        if (!_automatedChecks && File.Exists(SavePath)) TryLoad(false);
        if (State.MigratedFromStackedStock) EnsureSupplyCabinet();
        Rebuild();
        Keeper = new Shopkeeper(); AddChild(Keeper); Keeper.Build(this);
        Rebuild(); Hud = new StoreHud(); AddChild(Hud); Hud.Build(this);
        SetProcess(true);
#if DEBUG
        if (OS.GetCmdlineUserArgs().Contains("--preview-owner")) CallDeferred(nameof(ReviewOwner));
        else if (OS.GetCmdlineUserArgs().Contains("--preview-walk")) CallDeferred(nameof(ReviewWalking));
        else if (_automatedChecks) CallDeferred(nameof(RunChecks));
#endif
    }
#if DEBUG
    private async void RunChecks() => await FoundationChecks.Run(this);
    private async void ReviewOwner() => await OwnerPreview.Run(this);
    private async void ReviewWalking() => await WalkingPreview.Run(this);
#endif
    private void Seed()
    {
        State = new();
        for (var x = -6; x < 6; x++)
        {
            State.Walls.Add(new() { X = x, Z = -5, AlongX = true });
            State.Walls.Add(new() { X = x, Z = 5, AlongX = true, Kind = x is -1 or 0 ? "door" : "window" });
        }
        for (var z = -5; z < 5; z++)
            foreach (var x in new[] { -6, 6 }) State.Walls.Add(new() { X = x, Z = z, Kind = z >= 1 ? "window" : "wall" });
        StoreItem Add(string kind, float x, float z, int color = 0, int rotation = 0, int parent = 0)
        {
            var p = Catalog.Get(kind); var support = State.Items.FirstOrDefault(i => i.Id == parent);
            var item = new StoreItem { Id = State.NextId++, Kind = kind, X = x, Z = z, Y = support?.Product.Surface ?? 0,
                Color = color, Rotation = rotation, ParentId = parent, ForSale = p.Merchandise, Stock = 1, Price = p.Price };
            State.Items.Add(item); return item;
        }
        Add("checkout", -3.6f, -3, 2);
        Add("stockroom", -5.15f, -3.1f, 2);
        var table = Add("table", 0, .1f);
        Add("vase", -.62f, .1f, 1, parent: table.Id); Add("candle", 0, .1f, parent: table.Id); Add("books", .62f, .1f, 2, parent: table.Id);
        var table2 = Add("table", 2.4f, -2.7f);
        Add("bowl", 1.9f, -2.7f, 2, parent: table2.Id); Add("vase", 2.95f, -2.7f, parent: table2.Id);
        var shelf = Add("shelf", -5.15f, .2f, 2, 90);
        Add("candle", -5.15f, -.25f, 1, parent: shelf.Id); Add("books", -5.15f, .5f, 4, 90, shelf.Id);
        Add("rug", 3, 1.5f, 4); Add("chair", 3.3f, 1.5f, 2); Add("lamp", 4.45f, 1.1f);
        Add("plant", 4.7f, -3.7f, 1); Add("plant", -4.8f, 3.7f, 0);
        State.Cash = 9000;
    }
    private void EnsureSupplyCabinet()
    {
        if (State.Items.Any(i => i.Kind == "stockroom") || State.Items.Count >= 600) return;
        foreach (var point in Enumerable.Range(0, 24 * 20).Select(i => ShopNavigation.Point(new(i % 24, i / 24)))
            .OrderBy(p => p.DistanceSquaredTo(new(-5.15f, 0, -3.1f))))
        {
            var item = new StoreItem { Id = State.NextId, Kind = "stockroom", X = point.X, Z = point.Z, Stock = 1, Price = 480, Color = 2 };
            if (!StoreLayout.CanPlace(State, item)) continue;
            State.NextId++; State.Items.Add(item); break;
        }
    }
    public void ClearEditHistory() => _history.Clear();
    public void StockChanged(StoreItem item)
    { _history.Clear(); Navigation.Rebuild(State); RefreshItem(item); }
    private static void Clear(Node parent)
    { foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
    private void Rebuild()
    {
        Clear(_layout); Clear(_floors); _objects.Clear();
        _floorGrid = Tool != "select" && Editing; Visuals.Floors(_floors, State, _floorGrid);
        foreach (var i in State.Items) _objects[i.Id] = Visuals.Item(_layout, i);
        RebuildWalls(); Navigation.Rebuild(State); RefreshStaff(); UpdateOverlay(true);
        Hud?.RefreshInspector();
    }
    private void RefreshItem(StoreItem item)
    {
        if (_objects.Remove(item.Id, out var old)) { _layout.RemoveChild(old); old.QueueFree(); }
        _objects[item.Id] = Visuals.Item(_layout, item);
        UpdateOverlay(true); Hud?.RefreshInspector();
    }
    private void RebuildWalls()
    {
        Clear(_walls); _lastYaw = Camera.Yaw;
        foreach (var w in State.Walls)
        {
            var cut = CutWalls && (w.AlongX ? (Camera.Position.Z > 0 ? w.Z >= 0 : w.Z <= 0) : (Camera.Position.X > 0 ? w.X >= 0 : w.X <= 0));
            Visuals.Wall(_walls, w, cut);
        }
        if (State.Walls.Count(w => w.AlongX && w.Z == -5 && w.X >= -2 && w.X <= 1 && w.Kind == "wall") == 4 && (!CutWalls || Camera.Position.Z > 0))
        {
            Geometry.RoundedBox(_walls, "StoreSign", new(3.5f, .84f, .045f), new(0, 2.02f, -4.9f), Visuals.Dark, .025f);
            Geometry.Text(_walls, "StoreName", State.StoreName.ToUpperInvariant(), new(0, 2.13f, -4.865f), .0044f, new("e6d8b8"), 64);
            Geometry.Text(_walls, "StoreSubtitle", "O B J E C T S   /   L I V I N G", new(0, 1.82f, -4.86f), .0013f, new("b8c4b5"), 40);
        }
    }
    private void RefreshStaff()
    {
        if (Checkout == null) { _cashier?.QueueFree(); _cashier = null; return; }
        if (_cashier == null) { _cashier = new(); AddChild(_cashier); _cashier.Build(this, 1, true); }
        _cashier.Position = Checkout.Position + Vector3.Back.Rotated(Vector3.Up, Mathf.DegToRad(Checkout.Rotation)) * -.95f;
        _cashier.RotationDegrees = new(0, Checkout.Rotation, 0);
    }
    public override void _Process(double delta)
    {
        Camera.OverUi = Hud.IsOverUi();
        if (_floorGrid != (Tool != "select" && Editing))
        { _floorGrid = Tool != "select" && Editing; Clear(_floors); Visuals.Floors(_floors, State, _floorGrid); }
        var dt = Mathf.Min((float)delta, .1f) * Speed;
        Shoppers.RemoveAll(s => s.Finished);
        if (Open)
        {
            State.TradingSeconds += dt;
            var entranceClear = People.All(s => s.Position.DistanceTo(Navigation.Entrance) > .9f);
            if (_arrivals.Tick(dt, Shoppers.Count, entranceClear))
            {
                var s = new Shopper(); AddChild(s); s.Build(this, _profile++); Shoppers.Add(s);
            }
        }
        if (_closing && Shoppers.Count == 0)
        {
            _closing = false; Notify($"Day {State.Day} complete", $"{State.DaySales} sales · Revenue ${State.DayRevenue:0} · Gross profit ${State.DayRevenue - State.DayCost:0}");
            Save(false); Hud.RefreshInspector();
        }
        if (Mathf.Abs(Mathf.Sin(Camera.Yaw) - Mathf.Sin(_lastYaw)) > .25f || Mathf.Abs(Mathf.Cos(Camera.Yaw) - Mathf.Cos(_lastYaw)) > .25f) RebuildWalls();
        if (_cashier != null) _cashier.AttentionTarget = Queue.Count > 0 ? Queue[0].Position + Vector3.Up * 1.5f : null;
        UpdateOverlay();
        _hudTime += (float)delta; if (_hudTime > .15f) { Hud.RefreshStats(); _hudTime = 0; }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } k)
        {
            switch (k.Keycode)
            {
                case Key.Escape: CancelTool(); return;
                case Key.R: Rotate(); return;
                case Key.Delete: RemoveSelected(); return;
                case Key.Space: SetSpeed(Speed == 0 ? 1 : 0); return;
                case Key.Home: Camera.Home(); return;
                case Key.Key1: Camera.SetView(1); return;
                case Key.Key2: Camera.SetView(2); return;
                case Key.Key3: Camera.SetView(3); return;
                case Key.F: Camera.ToggleFollow(); return;
                case Key.I or Key.J or Key.K or Key.L: Keeper.MoveFromKey(k.Keycode); return;
                case Key.F5: Save(); return;
                case Key.F9: TryLoad(); return;
                case Key.F12: Capture(); return;
                case Key.Z when k.CtrlPressed: Undo(); return;
                case Key.Y when k.CtrlPressed: Redo(); return;
            }
        }
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left } m || Hud.IsOverUi()) return;
        var point = Camera.Ground(m.Position); if (point == null) return;
        if (Tool is "wall" or "window" or "door")
        {
            if (!Editing) { Notify("Close the store and let shoppers finish before building."); return; }
            if (m.Pressed) _wallStart = point;
            else if (_wallStart != null) { PaintWalls(_wallStart.Value, point.Value); _wallStart = null; }
            return;
        }
        if (!m.Pressed) return;
        if (_placing != null || _moving != 0) { Place(); return; }
        if (Tool == "floor") { PaintFloor(point.Value); return; }
        if (Tool == "erase") { EraseWall(point.Value); return; }
        var empty = State.Items.Where(i => i.ForSale && i.Stock == 0)
            .FirstOrDefault(i => Camera.UnprojectPosition(i.Position + Vector3.Up * .05f).DistanceTo(m.Position) < 22);
        var item = empty ?? Pick(m.Position);
        if (item is { Stock: 0, ForSale: true }) { Select(null); Keeper.Request(item); return; }
        if (item != null) Select(item);
        else { Select(null); Keeper.MoveTo(point.Value); }
    }
    public void Notify(string text, string detail = "") { Status = text; StatusDetail = detail; Hud?.RefreshStats(); }
    public void Rename(string name)
    {
        name = name.Trim(); if (!Editing || name.Length is < 1 or > 32 || name.Any(char.IsControl)) return;
        Remember(); State.StoreName = name; RebuildWalls(); Notify($"Welcome to {name}.");
    }
    public void SetSpeed(float speed) { Speed = speed; Hud.RefreshStats(); }
    public void ToggleWalls() { CutWalls = !CutWalls; RebuildWalls(); }
    public void SetFloor(int index) { FloorStyle = index; SetTool("floor"); }
    public void SetColor(int color)
    {
        PaintColor = color;
        if (Selected != null && Editing && _placing == null && _moving == 0)
        { Remember(); Selected.Color = color; RefreshItem(Selected); }
        _previewState = null; Hud.RefreshInspector();
    }
    public void SetTool(string tool)
    {
        if (!Editing && tool != "select") { Notify("Close the store and let shoppers finish before editing."); return; }
        CancelTool(); Tool = tool; _previewState = null;
        if (tool != "select") Selected = null;
        Hud.RefreshInspector();
        Notify(tool switch { "wall" => "Drag to draw walls · $25 per metre", "window" => "Drag to add windows · $45 per metre",
            "door" => "Drag to add doorways · $35 per metre", "floor" => "Click a floor tile to change its finish · $8", "erase" => "Click near a wall to remove it", _ => "Select an object to make it yours." });
    }
    public void CancelTool()
    {
        _placing = null; _moving = 0; _ghost = null; _wallStart = null; Tool = "select"; _previewState = null;
        foreach (var obj in _objects.Values) obj.Visible = true;
        UpdateOverlay(true); Hud?.RefreshInspector();
    }
    public void BeginPlace(string kind)
    {
        if (!Editing) { Notify("Close the store and let shoppers finish before furnishing."); return; }
        CancelTool(); Selected = null; _placing = Catalog.Get(kind); Tool = "place";
        Notify($"Placing {_placing.Name}", "Click to place · R to rotate · Esc to cancel · Small objects snap onto displays"); Hud.RefreshInspector();
    }
    public void Select(StoreItem? item) { CancelTool(); Selected = item; Hud.RefreshInspector(); UpdateOverlay(true); }
    public void MoveSelected()
    {
        if (!Editing || Selected == null) return;
        _moving = Selected.Id; _rotation = Selected.Rotation; Tool = "move"; _previewState = null;
        _objects[_moving].Visible = false;
        foreach (var i in State.Items.Where(i => i.ParentId == _moving)) _objects[i.Id].Visible = false;
        Notify("Move your object", "Click to confirm · R to rotate · Esc to cancel");
    }
    public void DuplicateSelected() { if (Selected == null || !Editing) return; PaintColor = Selected.Color; _rotation = Selected.Rotation; BeginPlace(Selected.Kind); }
    public void Rotate()
    {
        if (!Editing) return;
        if (_placing != null || _moving != 0) { _rotation = (_rotation + 90) % 360; _previewState = null; return; }
        if (Selected == null) return;
        var trial = Selected.Copy(); trial.Rotation = (trial.Rotation + 90) % 360;
        if (!StoreLayout.CanPlace(State, trial, Selected.Id)) { Notify("There isn't enough room to rotate here."); return; }
        Remember(); StoreLayout.MoveItem(State, Selected, trial); Rebuild();
    }
    private void UpdateOverlay(bool force = false)
    {
        if (_overlay == null || Camera == null) return;
        var point = Camera.Ground(GetViewport().GetMousePosition());
        Vector3? previewPoint = Editing && Tool != "select" && point is { } p0
            ? new Vector3(Mathf.Snapped(p0.X, .01f), 0, Mathf.Snapped(p0.Z, .01f)) : null;
        var key = (previewPoint, _rotation, PaintColor, Selected?.Id, Tool, _wallStart, Camera.OverUi);
        if (!force && key == _previewState) return; _previewState = key;
        Clear(_overlay);
        if (Selected != null && _moving == 0) Visuals.Selection(_overlay, Selected, "b78d4d");
        if (Camera.OverUi || point == null || !Editing) return;
        if (_placing != null || _moving != 0)
        {
            _ghost = _moving != 0 ? State.Items.First(i => i.Id == _moving).Copy() : new StoreItem { Kind = _placing!.Id, Color = PaintColor, ForSale = _placing.Merchandise, Stock = 1, Price = _placing.Price };
            var p = point.Value; _ghost.ParentId = 0; _ghost.Y = 0;
            if (_ghost.Kind is "vase" or "candle" or "books" or "bowl")
                foreach (var support in State.Items.Where(i => i.Product.Surface > 0 && i.Id != _moving))
                {
                    var surfacePoint = Camera.Ground(GetViewport().GetMousePosition(), support.Product.Surface);
                    if (surfacePoint == null) continue;
                    var h = support.Footprint / 2;
                    if (Mathf.Abs(surfacePoint.Value.X - support.X) < h.X && Mathf.Abs(surfacePoint.Value.Z - support.Z) < h.Y)
                    { p = surfacePoint.Value; _ghost.Y = support.Product.Surface; _ghost.ParentId = support.Id; break; }
                }
            var snap = _ghost.ParentId != 0 ? .1f : .25f;
            _ghost.X = Mathf.Snapped(p.X, snap); _ghost.Z = Mathf.Snapped(p.Z, snap); _ghost.Rotation = _rotation;
            _previewValid = StoreLayout.CanPlace(State, _ghost, _moving);
            Visuals.Item(_overlay, _ghost, true); Visuals.Selection(_overlay, _ghost, _previewValid ? "60b58a" : "dc6f55");
        }
        else if (Tool is "wall" or "window" or "door")
            foreach (var wall in WallStroke(_wallStart ?? point.Value, point.Value))
            {
                var preview = Visuals.Wall(_overlay, wall, false);
                foreach (var m in preview.GetChildren().OfType<GeometryInstance3D>()) m.Transparency = .5f;
            }
        else if (Tool == "floor" && InLot(point.Value))
        {
            var p = point.Value;
            Geometry.Box(_overlay, "PaintTile", new(.98f, .012f, .98f), new(Mathf.Floor(p.X) + .5f, .01f, Mathf.Floor(p.Z) + .5f), Visuals.Color(2));
        }
    }
    private static bool InLot(Vector3 p) => p.X >= -6 && p.X < 6 && p.Z >= -5 && p.Z < 5;
    private void Place()
    {
        UpdateOverlay(true);
        if (_ghost == null || !_previewValid) { Notify("This object needs a clear space inside the store."); return; }
        var price = _moving != 0 ? 0 : _ghost.Product.Cost * _ghost.Stock;
        if (State.Cash < price) { Notify("You don't have enough cash for that purchase."); return; }
        Remember();
        if (_moving != 0)
        { var item = State.Items.First(i => i.Id == _moving); StoreLayout.MoveItem(State, item, _ghost); Selected = item; _moving = 0; Tool = "select"; }
        else { var i = _ghost.Copy(); i.Id = State.NextId++; State.Items.Add(i); State.Cash -= price; }
        Rebuild(); Notify(price > 0 ? $"Added {_ghost.Product.Name} · ${price:0}" : "Layout updated");
    }
    public void RemoveSelected()
    {
        if (!Editing || Selected == null) return;
        var removed = State.Items.Where(i => i.Id == Selected.Id || i.ParentId == Selected.Id).ToArray();
        var refund = decimal.Round(removed.Sum(i => i.Product.Cost * i.Stock) * .7m, 0);
        Remember(); foreach (var item in removed) State.Items.Remove(item); State.Cash += refund;
        Selected = null; CancelTool(); Rebuild(); Notify($"Removed object and supported items · ${refund:0} recovered");
    }
    public void ToggleSale()
    {
        if (!Editing || Selected == null) return;
        if (Selected.Kind is "checkout" or "stockroom" || Selected.Stock == 0 || State.Items.Any(i => i.ParentId == Selected.Id))
        { Notify("Keep the checkout and occupied displays as store fixtures."); return; }
        Remember(); Selected.ForSale = !Selected.ForSale; RefreshItem(Selected);
    }
    public void ChangePrice(decimal amount)
    { if (!Editing || Selected == null) return; Remember(); Selected.Price = Math.Clamp(Selected.Price + amount, 1, 99999); RefreshItem(Selected); }
    private List<StoreWall> WallStroke(Vector3 a, Vector3 b)
    {
        var along = Mathf.Abs(a.X - b.X) >= Mathf.Abs(a.Z - b.Z);
        var fixedAxis = Mathf.RoundToInt(along ? a.Z : a.X);
        fixedAxis = Mathf.Clamp(fixedAxis, along ? -5 : -6, along ? 5 : 6);
        var start = Mathf.Clamp(Mathf.FloorToInt(along ? a.X : a.Z), along ? -6 : -5, along ? 5 : 4);
        var end = Mathf.Clamp(Mathf.FloorToInt(along ? b.X : b.Z), along ? -6 : -5, along ? 5 : 4);
        var result = new List<StoreWall>();
        for (var n = Math.Min(start, end); n <= Math.Max(start, end); n++)
            result.Add(new() { X = along ? n : fixedAxis, Z = along ? fixedAxis : n, AlongX = along, Kind = Tool, Color = PaintColor });
        return result;
    }
    private void PaintWalls(Vector3 a, Vector3 b)
    {
        if (!InLot(a) && !(Mathf.Abs(a.X) <= 6.2f && Mathf.Abs(a.Z) <= 5.2f)) return;
        var walls = WallStroke(a, b);
        foreach (var w in walls)
            foreach (var i in State.Items.Where(i => i.Blocks))
            {
                var h = i.Footprint / 2;
                if (w.AlongX ? Mathf.Abs(i.Z - w.Z) < h.Y && i.X + h.X > w.X && i.X - h.X < w.X + 1
                    : Mathf.Abs(i.X - w.X) < h.X && i.Z + h.Y > w.Z && i.Z - h.Y < w.Z + 1)
                { Notify("Move the furniture clear of the new wall first."); return; }
            }
        var changed = walls.Where(w => !State.Walls.Any(old => old.Key == w.Key && old.Kind == w.Kind && old.Color == w.Color)).ToArray();
        var cost = changed.Length * (Tool == "wall" ? 25 : Tool == "window" ? 45 : 35);
        if (cost == 0) return;
        if (State.Cash < cost) { Notify("Not enough cash to build this section."); return; }
        Remember(); foreach (var w in changed) { State.Walls.RemoveAll(old => old.Key == w.Key); State.Walls.Add(w); }
        State.Cash -= cost; Rebuild(); Notify($"Built {changed.Length} metre(s) · ${cost}");
    }
    private void PaintFloor(Vector3 p)
    {
        if (!Editing || !InLot(p)) return;
        var tile = ((int)Mathf.Floor(p.Z) + 5) * 12 + (int)Mathf.Floor(p.X) + 6;
        if (State.Floors[tile] == FloorStyle || State.Cash < 8) return;
        Remember(); State.Floors[tile] = FloorStyle; State.Cash -= 8; Rebuild();
    }
    private void EraseWall(Vector3 p)
    {
        if (!Editing) return;
        var w = State.Walls.OrderBy(w => p.DistanceSquaredTo(new(w.X + (w.AlongX ? .5f : 0), 0, w.Z + (w.AlongX ? 0 : .5f)))).FirstOrDefault();
        if (w == null || p.DistanceTo(new(w.X + (w.AlongX ? .5f : 0), 0, w.Z + (w.AlongX ? 0 : .5f))) > .8f) return;
        Remember(); State.Walls.Remove(w); Rebuild(); Notify("Removed wall section");
    }
    private void Remember() => _history.Record(State);
    public void Undo() => RestoreHistory(false);
    public void Redo() => RestoreHistory(true);
    private void RestoreHistory(bool redo)
    {
        if (redo ? !CanRedo : !CanUndo) return;
        var keeperPosition = Keeper.Actor.Position;
        State = redo ? _history.Redo(State) : _history.Undo(State);
        State.KeeperX = keeperPosition.X; State.KeeperZ = keeperPosition.Z;
        Selected = null; CancelTool(); Rebuild(); Keeper.BindState(); Notify(redo ? "Redid last edit" : "Undid last edit");
    }
    public void ToggleOpen()
    {
        if (Open)
        { Open = false; _closing = true; if (Speed == 0) Speed = 1; Notify("Closing time", "Shoppers will finish their purchases before editing resumes."); return; }
        if (!CanOpen) { Notify("Your last shoppers are still finishing up."); return; }
        Navigation.Rebuild(State);
        if (!Navigation.HasEntrance) { Notify("Add an accessible doorway or gap in an exterior wall."); return; }
        if (Checkout == null || State.Items.Count(i => i.Kind == "checkout") != 1)
        { Notify("The store needs exactly one checkout counter, kept as a fixture."); return; }
        if (Navigation.Path(Navigation.Entrance, QueueSpot(0)) == null)
        { Notify("The checkout is blocked. Leave a clear route from the entrance."); return; }
        var available = State.Items.Where(i => i.ForSale && i.Stock > 0).ToArray();
        var reachable = available.Count(i => Navigation.Approach(i, Navigation.Entrance) != null);
        if (reachable == 0) { Notify("Stock at least one accessible object marked for sale."); return; }
        CancelTool(); Selected = null; _history.Clear();
        State.Day++; State.DayRevenue = 0; State.DayCost = 0; State.DaySales = 0; State.TradingSeconds = 0;
        _trading = new(State); Open = true; _arrivals.Reset(); Speed = 1;
        Notify($"Day {State.Day} · Open for business", reachable < available.Length ? $"{available.Length - reachable} product(s) cannot be reached. Review the layout after closing." : "Customers are on their way. Prices influence their buying decisions."); Hud.RefreshInspector();
    }
    public int Reserved(StoreItem item) => _trading?.Reserved(item) ?? 0;
    public bool JoinQueue(Shopper shopper)
    {
        if (shopper.Choice == null || !_trading.TryReserve(shopper.GetInstanceId(), shopper.Choice)) return false;
        Queue.Add(shopper); return true;
    }
    public void LeaveQueue(Shopper shopper)
    {
        Queue.Remove(shopper); _trading?.Cancel(shopper.GetInstanceId());
    }
    public Vector3 QueueSpot(int index)
    {
        if (Checkout == null) return Navigation.Entrance;
        return ShopNavigation.Point(ShopNavigation.Cell(Checkout.Position + new Vector3(0, 0, 1.1f + index * .55f).Rotated(Vector3.Up, Mathf.DegToRad(Checkout.Rotation))));
    }
    public bool CompleteSale(Shopper shopper)
    {
        var receipt = _trading.Checkout(shopper.GetInstanceId());
        if (receipt == null) return false;
        var (item, price) = receipt;
        StockChanged(item);
        Notify($"Sold {item.Product.Name} · ${price:0}", $"${price - item.Product.Cost:0} gross profit · {item.Stock} left in stock"); Hud.RefreshInspector(); return true;
    }
    public void Save(bool announce = true)
    {
        try
        {
            Keeper?.SyncPosition(); StorePersistence.Save(SavePath, State);
            if (announce) Notify("Store saved", "Layout, stock and settled finances saved. Loading starts with the doors closed.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { Notify("Couldn't save the store", ex.Message); }
    }
    public void TryLoad(bool announce = true)
    {
        var loaded = StorePersistence.Load(SavePath);
        if (loaded.State == null)
        {
            if (announce) Notify("Couldn't load a valid save", "Your current store has been kept.");
            return;
        }
        ApplyLoaded(loaded.State);
        if (loaded.Recovered) Notify("Recovered the previous save", "The latest save couldn't be read.");
        else if (announce) Notify("Store loaded", "Unpaid shopping carts are cleared; your settled finances are preserved.");
    }
    private void ApplyLoaded(StoreState loaded)
    {
        foreach (var s in Shoppers) s.QueueFree(); Shoppers.Clear(); Queue.Clear();
        State = loaded; _trading = new(State); Open = false; _closing = false; Speed = 1; Selected = null; _history.Clear();
        if (Hud != null) { if (State.MigratedFromStackedStock) EnsureSupplyCabinet(); CancelTool(); Rebuild(); Keeper.BindState(); }
    }
    private StoreItem? Pick(Vector2 screen)
    {
        var origin = Camera.ProjectRayOrigin(screen); var direction = Camera.ProjectRayNormal(screen);
        StoreItem? selected = null; var nearest = float.PositiveInfinity;
        foreach (var i in State.Items)
        {
            var height = i.Kind switch { "shelf" => 2.2f, "lamp" or "plant" => 1.8f, "chair" => 1.1f, "vase" => .53f, "rug" => .03f, "books" or "candle" or "bowl" => .23f, _ => i.Product.Surface > 0 ? i.Product.Surface : 1.1f };
            if (i.Stock == 0) height = .12f;
            var size = i.Footprint + (i.Stock == 0 ? Vector2.One * .12f : Vector2.Zero);
            var min = i.Position - new Vector3(size.X / 2, 0, size.Y / 2); var max = min + new Vector3(size.X, height, size.Y);
            float near = 0, far = 1000;
            for (var axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < .00001f) { if (origin[axis] < min[axis] || origin[axis] > max[axis]) { far = -1; break; } continue; }
                var a = (min[axis] - origin[axis]) / direction[axis]; var b = (max[axis] - origin[axis]) / direction[axis];
                near = Math.Max(near, Math.Min(a, b)); far = Math.Min(far, Math.Max(a, b));
            }
            if (near <= far && near < nearest) { nearest = near; selected = i; }
        }
        return selected;
    }
    public async void Capture()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var path = ProjectSettings.GlobalizePath("user://store-screenshot.png"); GetViewport().GetTexture().GetImage().SavePng(path); Notify("Screenshot saved", path);
    }
}
