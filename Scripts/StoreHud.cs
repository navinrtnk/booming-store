// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Native, resolution-aware Godot controls. The world remains interactive between panels.</summary>
public partial class StoreHud : CanvasLayer
{
    private StoreGame _game = null!;
    private Control _root = null!;
    private VBoxContainer _catalog = null!, _inspector = null!;
    private Label _cash = null!, _day = null!, _revenue = null!, _status = null!, _detail = null!, _mode = null!;
    private Button _open = null!, _undo = null!, _redo = null!;
    private readonly List<Control> _panels = [];
    private readonly List<Button> _speed = [];
    private static readonly float[] Speeds = [0, 1, 3];
    private float _lastSpeed = float.NaN;
    private static readonly Color Ink = new("233e35"), Muted = new("738077"), Paper = new("f6f3eb"), Accent = new("a27d43");
    public void Build(StoreGame game)
    {
        _game = game;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore }; _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); AddChild(_root);
        _root.Theme = new Theme();
        _root.AddThemeFontSizeOverride("font_size", 17);
        var top = Panel(24, 20, -24, 100, true);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 25); top.AddChild(row);
        var brand = new VBoxContainer { CustomMinimumSize = new(235, 0) }; row.AddChild(brand);
        brand.AddChild(Label("BOOMING STORE", 23)); brand.AddChild(Label("A little space. Endless possibilities.", 12, Muted));
        row.AddChild(Stat("AVAILABLE CASH", out _cash)); row.AddChild(Stat("TODAY'S SALES", out _revenue)); row.AddChild(Stat("TRADING DAY", out _day));
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        row.AddChild(actions);
        actions.AddChild(Button("Save", () => game.Save(), "F5 · Save your layout, stock and finances"));
        actions.AddChild(Button("Load", () =>
        {
            var dialog = new ConfirmationDialog { Title = "Load saved store?", DialogText = "Replace the current session with your saved store?\nChanges since the last save will be lost.", OkButtonText = "Load store" };
            _root.AddChild(dialog); dialog.Confirmed += () => { game.TryLoad(); dialog.QueueFree(); }; dialog.Canceled += () => dialog.QueueFree(); dialog.PopupCentered(new(440, 160));
        }, "F9 · Restore the last saved store"));
        _open = Button("Open store  →", game.ToggleOpen); _open.CustomMinimumSize = new(140, 34); StyleButton(_open, true); actions.AddChild(_open);

        var left = Panel(24, 120, 280, -194);
        var library = new VBoxContainer(); library.AddThemeConstantOverride("separation", 10); left.AddChild(library);
        library.AddChild(Label("THE COLLECTION", 12, Accent));
        var tabs = new GridContainer { Columns = 2 }; tabs.AddThemeConstantOverride("h_separation", 6); tabs.AddThemeConstantOverride("v_separation", 6); library.AddChild(tabs);
        foreach (var c in new[] { "Objects", "Furniture", "Displays", "Decor" })
        { var category = c; tabs.AddChild(Button(c, () => { game.Category = category; RefreshCatalog(); })); }
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        library.AddChild(scroll); _catalog = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _catalog.AddThemeConstantOverride("separation", 8); scroll.AddChild(_catalog);
        library.AddChild(Label("Objects include 3 units of stock.\nFixtures include one object.", 12, Muted));

        var right = Panel(-278, 120, -24, 698, false, true);
        var inspectorScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(inspectorScroll); _inspector = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _inspector.AddThemeConstantOverride("separation", 10); inspectorScroll.AddChild(_inspector);

        var message = Panel(300, -185, -300, -127, true, false, true);
        var status = new VBoxContainer(); message.AddChild(status); _status = Label("", 15); _detail = Label("", 12, Muted); status.AddChild(_status); status.AddChild(_detail);
        var bottom = Panel(24, -114, -24, -43, true, false, true);
        var tools = new HBoxContainer(); tools.AddThemeConstantOverride("separation", 7); bottom.AddChild(tools);
        _mode = Label("DESIGN STUDIO", 12, Accent); _mode.CustomMinimumSize = new(130, 0); _mode.VerticalAlignment = VerticalAlignment.Center; tools.AddChild(_mode);
        tools.AddChild(Button("Select", () => game.SetTool("select"), "Click an object to inspect it"));
        tools.AddChild(Button("Walls", () => game.SetTool("wall"), "Drag along the floor to draw walls"));
        tools.AddChild(Button("Windows", () => game.SetTool("window")));
        tools.AddChild(Button("Doors", () => game.SetTool("door")));
        tools.AddChild(Button("Floors", () => game.SetFloor((game.FloorStyle + 1) % 3), "Click to cycle Oak / Marble / Dark tile. Then click floor tiles."));
        tools.AddChild(Button("Erase wall", () => game.SetTool("erase")));
        tools.AddChild(Button("Cutaway", game.ToggleWalls));
        _undo = Button("Undo", game.Undo, "Ctrl+Z"); _redo = Button("Redo", game.Redo, "Ctrl+Y"); tools.AddChild(_undo); tools.AddChild(_redo);
        tools.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        foreach (var (text, speed) in new[] { ("Ⅱ", 0f), ("1×", 1f), ("3×", 3f) })
        { var value = speed; var b = Button(text, () => game.SetSpeed(value)); tools.AddChild(b); _speed.Add(b); }
        var footer = Label("WASD pan    ·    Right-drag orbit    ·    Wheel zoom    ·    R rotate    ·    Esc cancel    ·    Home reset camera    ·    Space pause", 12, Muted);
        footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide); footer.OffsetTop = -32; footer.OffsetBottom = -10; footer.HorizontalAlignment = HorizontalAlignment.Center; _root.AddChild(footer);
        RefreshCatalog(); RefreshInspector(); RefreshStats();
    }
    private MarginContainer Panel(float left, float top, float right, float bottom, bool rightStretch = false, bool rightAnchored = false, bool bottomAnchored = false)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AnchorLeft = rightAnchored ? 1 : 0; panel.AnchorRight = rightAnchored || rightStretch ? 1 : 0;
        panel.AnchorTop = bottomAnchored ? 1 : 0; panel.AnchorBottom = bottomAnchored || bottom < 0 ? 1 : 0;
        panel.OffsetLeft = left; panel.OffsetRight = right; panel.OffsetTop = top; panel.OffsetBottom = bottom;
        panel.AddThemeStyleboxOverride("panel", Box(Paper, 12, true)); _root.AddChild(panel); _panels.Add(panel);
        var margin = new MarginContainer(); foreach (var side in new[] { "left", "right" }) margin.AddThemeConstantOverride("margin_" + side, 16);
        margin.AddThemeConstantOverride("margin_top", 13); margin.AddThemeConstantOverride("margin_bottom", 13); panel.AddChild(margin); return margin;
    }
    private static StyleBoxFlat Box(Color color, int radius = 7, bool shadow = false) => new()
    {
        BgColor = color, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8,
        ShadowColor = new(0, 0, 0, shadow ? .12f : 0), ShadowSize = shadow ? 8 : 0, ShadowOffset = new(0, 3)
    };
    private static Label Label(string text, int size = 16, Color? color = null)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color ?? Ink); return label;
    }
    private static Label Wrap(string text, int size = 14)
    { var label = Label(text, size, Muted); label.AutowrapMode = TextServer.AutowrapMode.WordSmart; label.TextOverrunBehavior = TextServer.OverrunBehavior.NoTrimming; label.CustomMinimumSize = new(160, 0); return label; }
    private static void StyleButton(Button button, bool active = false)
    {
        button.AddThemeStyleboxOverride("normal", Box(active ? Ink : new("e9e9df")));
        button.AddThemeStyleboxOverride("hover", Box(active ? new("3a5e4d") : new("dedfd2")));
        button.AddThemeStyleboxOverride("pressed", Box(new("bacdbb")));
        button.AddThemeStyleboxOverride("disabled", Box(new("e8e7df")));
        button.AddThemeColorOverride("font_color", active ? Paper : Ink);
        button.AddThemeColorOverride("font_hover_color", active ? Paper : Ink);
        button.AddThemeColorOverride("font_disabled_color", new("a4aaa0"));
    }
    private static Button Button(string text, Action action, string tooltip = "")
    {
        var b = new Button { Text = text, TooltipText = tooltip, FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            ClipText = true, CustomMinimumSize = new(text.Length <= 12 ? text.Length * 8 + 28 : 0, 34) };
        b.AddThemeFontSizeOverride("font_size", 14); StyleButton(b); b.Pressed += action; return b;
    }
    private static VBoxContainer Stat(string heading, out Label value)
    {
        var v = new VBoxContainer { CustomMinimumSize = new(180, 0) }; v.AddChild(Label(heading, 11, Muted)); value = Label("", 23); v.AddChild(value); return v;
    }
    public bool IsOverUi()
    {
        var mouse = _root.GetGlobalMousePosition();
        return _panels.Any(p => p.GetGlobalRect().HasPoint(mouse)) || _root.GetChildren().OfType<Window>().Any(w => w.Visible);
    }
    private static void Clear(Node n) { foreach (var c in n.GetChildren()) { n.RemoveChild(c); c.QueueFree(); } }
    public void RefreshCatalog()
    {
        Clear(_catalog);
        foreach (var p in Catalog.Products.Where(p => p.Category == _game.Category))
        {
            var product = p;
            var card = new PanelContainer(); card.AddThemeStyleboxOverride("panel", Box(new("eeede4"))); _catalog.AddChild(card);
            var col = new VBoxContainer(); col.AddThemeConstantOverride("separation", 5); card.AddChild(col);
            col.AddChild(Label(p.Name, 16)); col.AddChild(Wrap(p.Description, 12));
            var price = p.Cost * (p.Merchandise ? 3 : 1);
            var button = Button($"+  Place  ·  ${price:0}", () => _game.BeginPlace(product.Id));
            button.Alignment = HorizontalAlignment.Left; col.AddChild(button);
        }
    }
    public void RefreshInspector()
    {
        if (_inspector == null) return;
        Clear(_inspector);
        var item = _game.Selected;
        _inspector.AddChild(Label(item == null ? "MAKE ROOM FOR YOUR IDEAS" : "OBJECT DETAILS", 11, Accent));
        if (item == null)
        {
            var title = Wrap(_game.Open ? "The doors are open." : $"Welcome to {_game.State.StoreName}.", 21);
            title.AddThemeColorOverride("font_color", Ink); _inspector.AddChild(title);
            _inspector.AddChild(Wrap(_game.Open ? "Watch shoppers explore your collection. Price, stock and access shape each visit." : "A furnished starter shop is yours to reshape. Select anything in the store, or choose an object from the collection."));
            _inspector.AddChild(new HSeparator());
            _inspector.AddChild(Label("01  Make it yours", 16)); _inspector.AddChild(Wrap("Move furniture, change colors, draw rooms, and put small objects on displays.", 13));
            _inspector.AddChild(Label("02  Curate a collection", 16)); _inspector.AddChild(Wrap("Mark objects for sale, set prices and keep stock ready. Leave room for people to browse.", 13));
            _inspector.AddChild(Label("03  Open the doors", 16)); _inspector.AddChild(Wrap("The checkout is staffed automatically. Close to review your day, restock and redesign.", 13));
            _inspector.AddChild(new HSeparator());
            var nameField = new LineEdit { Text = _game.State.StoreName, PlaceholderText = "Your store name", MaxLength = 32, Editable = _game.Editing };
            nameField.AddThemeFontSizeOverride("font_size", 15); _inspector.AddChild(nameField);
            nameField.TextSubmitted += text => { _game.Rename(text); nameField.ReleaseFocus(); };
            _inspector.AddChild(EditButton("Update store name", () => { _game.Rename(nameField.Text); nameField.ReleaseFocus(); }));
            _inspector.AddChild(Label("DESIGN PALETTE", 11, Accent)); Swatches();
            if (_game.Tool == "floor")
            {
                _inspector.AddChild(Label("Floor finish", 15));
                foreach (var (name, index) in new[] { ("Oak · $8 / tile", 0), ("Marble · $8 / tile", 1), ("Dark tile · $8 / tile", 2) })
                { var selected = index; var b = Button(name, () => _game.SetFloor(selected)); StyleButton(b, _game.FloorStyle == index); _inspector.AddChild(b); }
            }
            return;
        }
        _inspector.AddChild(Label(item.Product.Name, 20)); _inspector.AddChild(Wrap(item.Product.Description));
        var availability = _game.Navigation.HasEntrance && _game.Navigation.Approach(item, _game.Navigation.Entrance) != null;
        _inspector.AddChild(Label(item.ForSale ? (availability ? "●  FOR SALE · ACCESSIBLE" : "●  FOR SALE · BLOCKED") : "●  STORE FIXTURE", 11, availability ? new("42705a") : new("b96545")));
        _inspector.AddChild(new HSeparator());
        _inspector.AddChild(Label("FINISH", 11, Accent)); Swatches();
        var row = new GridContainer { Columns = 2 }; row.AddThemeConstantOverride("h_separation", 5); _inspector.AddChild(row);
        row.AddChild(EditButton("Move", _game.MoveSelected)); row.AddChild(EditButton("Rotate", _game.Rotate)); row.AddChild(EditButton("Copy", _game.DuplicateSelected));
        if (item.Kind == "checkout")
        {
            _inspector.AddChild(Wrap("Automatically staffed. Keep a clear path to its front side before opening the store.", 13));
            _inspector.AddChild(EditButton("Remove checkout", _game.RemoveSelected));
            _inspector.AddChild(Button("Look closer", () => _game.Camera.ZoomTo(item.Position))); return;
        }
        _inspector.AddChild(EditButton(item.ForSale ? "Keep as store fixture" : "Mark for sale", _game.ToggleSale));
        _inspector.AddChild(new HSeparator());
        _inspector.AddChild(Label($"Retail price       ${item.Price:0}", 18));
        var prices = new HBoxContainer(); _inspector.AddChild(prices);
        foreach (var (text, value) in new[] { ("− $5", -5m), ("+ $5", 5m), ("+ $25", 25m) })
        { var amount = value; var b = EditButton(text, () => _game.ChangePrice(amount)); b.CustomMinimumSize = new(50, 34); b.AddThemeFontSizeOverride("font_size", 12); prices.AddChild(b); }
        _inspector.AddChild(Wrap($"Unit cost ${item.Product.Cost:0}  ·  Margin ${item.Price - item.Product.Cost:0}\nSuggested retail ${item.Product.Price:0}", 12));
        _inspector.AddChild(Label($"Stock                 {item.Stock} units", 17));
        _inspector.AddChild(EditButton($"Restock +{Math.Min(5, 99 - item.Stock)} · ${Math.Min(5, 99 - item.Stock) * item.Product.Cost:0}", _game.Restock));
        _inspector.AddChild(EditButton("Remove · recover 70% of stock cost", _game.RemoveSelected));
        _inspector.AddChild(Button("Look closer", () => _game.Camera.ZoomTo(item.Position)));
        if (!_game.Editing) _inspector.AddChild(Wrap("Close the store and let shoppers finish to edit this object.", 12));
    }
    private Button EditButton(string text, Action action) { var b = Button(text, action); b.Disabled = !_game.Editing; return b; }
    private void Swatches()
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 4); _inspector.AddChild(row);
        for (var i = 0; i < 6; i++)
        {
            var index = i; var b = Button("", () => _game.SetColor(index), Catalog.ColorNames[i]); b.CustomMinimumSize = new(28, 29);
            var style = Box(new(Catalog.Colors[i]), 6); style.BorderColor = Accent; var active = (_game.Selected?.Color ?? _game.PaintColor) == i;
            style.SetBorderWidthAll(active ? 3 : 0); b.AddThemeStyleboxOverride("normal", style); row.AddChild(b);
        }
    }
    public void RefreshStats()
    {
        if (_cash == null) return;
        _cash.Text = $"${_game.State.Cash:N0}"; _revenue.Text = $"${_game.State.DayRevenue:N0}";
        _day.Text = _game.Open ? $"{_game.State.Day:00}  /  {9 + (int)(_game.State.TradingSeconds / 30) % 12:00}:{(int)(_game.State.TradingSeconds * 2) % 60:00}" : $"{Math.Max(1, _game.State.Day):00}  /  CLOSED";
        _open.Text = _game.Open ? "Close store" : _game.Editing ? "Open store  →" : "Finishing up…"; _open.Disabled = !_game.Open && !_game.Editing;
        _undo.Disabled = !_game.CanUndo; _redo.Disabled = !_game.CanRedo;
        _status.Text = _game.Status; _detail.Text = _game.StatusDetail;
        _status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; _detail.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _mode.Text = _game.Open ? $"TRADING · {_game.Shoppers.Count} GUESTS" : _game.Tool == "floor" ? new[] { "OAK FLOOR", "MARBLE FLOOR", "DARK TILE FLOOR" }[_game.FloorStyle] : "DESIGN STUDIO";
        if (_lastSpeed != _game.Speed)
        {
            for (var i = 0; i < _speed.Count; i++) StyleButton(_speed[i], _game.Speed == Speeds[i]);
            _lastSpeed = _game.Speed;
        }
    }
}
