// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Native, resolution-aware Godot controls. The world remains interactive between panels.</summary>
public partial class StoreHud : CanvasLayer
{
    private StoreGame _game = null!;
    private Control _root = null!;
    private VBoxContainer _catalog = null!, _inspector = null!;
    private Label _reputation = null!;
    private Label _cash = null!, _day = null!, _revenue = null!, _status = null!, _detail = null!, _mode = null!, _keeperStatus = null!;
    private Button _open = null!, _undo = null!, _redo = null!, _fullscreen = null!;
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
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); top.AddChild(row);
        var brand = new VBoxContainer { CustomMinimumSize = new(200, 0) }; row.AddChild(brand);
        brand.AddChild(Label("BOOMING STORE", 23)); brand.AddChild(_reputation = Label("Electronics · new business", 12, Muted));
        row.AddChild(Stat("AVAILABLE CASH", out _cash)); row.AddChild(Stat("SESSION SALES", out _revenue)); var tradingDay = Stat("STORE CLOCK", out _day); tradingDay.CustomMinimumSize = new(180, 0); row.AddChild(tradingDay);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        row.AddChild(actions);
        _fullscreen = Button("Full screen", () => game.SetFullscreen(!game.Preferences.Fullscreen), "F11 or Alt+Enter"); actions.AddChild(_fullscreen);
        actions.AddChild(Button("UI", ToggleChrome, "Tab · show only the top store bar"));
        actions.AddChild(Button("Staff", ShowStaff));
        actions.AddChild(Button("Audio", ShowAudioSettings, "Sound levels · M mutes"));
        var gameMenu = Button("Game", ShowGameMenu, "Save F5 · Load F9 · New electronics store"); gameMenu.CustomMinimumSize = new(72, 34); actions.AddChild(gameMenu);
        _open = Button("Open store  →", game.ToggleOpen); _open.CustomMinimumSize = new(140, 34); StyleButton(_open, true); actions.AddChild(_open);

        var left = Panel(24, 120, 280, -242);
        var library = new VBoxContainer(); library.AddThemeConstantOverride("separation", 10); left.AddChild(library);
        library.AddChild(Label("TECH CATALOG", 12, Accent));
        var tabs = new GridContainer { Columns = 2 }; tabs.AddThemeConstantOverride("h_separation", 6); tabs.AddThemeConstantOverride("v_separation", 6); library.AddChild(tabs);
        foreach (var c in new[] { "TVs", "Computers", "Audio", "Displays" })
        { var category = c; tabs.AddChild(Button(c, () => { game.Category = category; RefreshCatalog(); })); }
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        library.AddChild(scroll); _catalog = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _catalog.AddThemeConstantOverride("separation", 8); scroll.AddChild(_catalog);
        library.AddChild(Label("One visible object = one item.\nClick an empty spot to restock it.", 12, Muted));

        var right = Panel(-278, 120, -24, 698, false, true);
        var inspectorScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(inspectorScroll); _inspector = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; _inspector.AddThemeConstantOverride("separation", 10); inspectorScroll.AddChild(_inspector);

        var keeperPanel = Panel(24, -224, 280, -122, false, false, true);
        var keeperBox = new VBoxContainer(); keeperPanel.AddChild(keeperBox);
        _keeperStatus = Label("Shopkeeper ready", 12); keeperBox.AddChild(_keeperStatus);
        var keeperActions = new HBoxContainer(); keeperBox.AddChild(keeperActions);
        keeperActions.AddChild(Button("Follow F", game.Camera.ToggleFollow));
        keeperActions.AddChild(Button("Stop", game.Keeper.Cancel, "Finish the current customer or delivery, then stop. C resumes checkout."));
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
        var footer = Label("Floor: walk · Empty spot: stock · IJKL: move · Arrows: camera · F: follow · C: checkout · F11: full screen · M: mute · WASD: pan · Tab: UI · B: shelf deck · Space: pause", 12, new("35493c"));
        footer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide); footer.OffsetTop = -32; footer.OffsetBottom = -10; footer.HorizontalAlignment = HorizontalAlignment.Center; _root.AddChild(footer);
        RefreshCatalog(); RefreshInspector(); RefreshStats();
    }
    public bool Compact { get; private set; }
    public void ToggleChrome()
    {
        Compact = !Compact; _game.CancelTool();
        foreach (var child in _root.GetChildren()) if (child is Control control && child != _panels[0]) control.Visible = !Compact;
    }
    private void ShowGameMenu()
    {
        var popup = new AcceptDialog { Title = "Your store", OkButtonText = "Done" }; _root.AddChild(popup);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 10); popup.AddChild(box);
        var note = Wrap("Save includes the clock, payroll, employee shifts, prices, shelves and stock. Unpaid basket items return to stock on load. Autosave runs each active real minute and at closing."); note.AddThemeColorOverride("font_color", Paper); box.AddChild(note);
        box.AddChild(Button("Save game · F5", () => { _game.Save(); popup.QueueFree(); }));
        void Confirm(string title, string text, Action action)
        {
            var dialog = new ConfirmationDialog { Title = title, DialogText = text, OkButtonText = "Continue" }; _root.AddChild(dialog);
            dialog.Confirmed += () => { action(); dialog.QueueFree(); popup.QueueFree(); }; dialog.Canceled += () => dialog.QueueFree(); dialog.PopupCentered(new(470, 180));
        }
        box.AddChild(Button("Load game · F9", () => Confirm("Load saved game?", "Replace this session with your last save? Unpaid baskets return to stock.", () => _game.TryLoad())));
        box.AddChild(Button("New electronics store · $10,000", () => Confirm("Start a new store?", "Start with an empty shop and $10,000? This store will be archived first so you can restore it later.", _game.NewStore)));
        box.AddChild(Button("Restore archived store", () =>
        {
            var archives = new AcceptDialog { Title = "Archived stores", OkButtonText = "Done" }; _root.AddChild(archives);
            var list = new VBoxContainer(); archives.AddChild(list);
            var files = _game.Archives();
            if (files.Length == 0) list.AddChild(Label("No archived stores yet.", 15, Paper));
            foreach (var file in files)
            {
                var path = file;
                list.AddChild(Button(System.IO.Path.GetFileNameWithoutExtension(file), () => Confirm("Restore archive?", "Your current store will be archived first.", () => { _game.RestoreArchive(path); archives.QueueFree(); })));
            }
            archives.Confirmed += () => archives.QueueFree(); archives.Canceled += () => archives.QueueFree(); archives.PopupCentered(new(480, 350));
        }));
        popup.Confirmed += () => popup.QueueFree(); popup.Canceled += () => popup.QueueFree(); popup.PopupCentered(new(480, 380));
    }
    internal void ShowStaff()
    {
        var popup = new AcceptDialog { Title = "Employees & shifts", OkButtonText = "Done" }; _root.AddChild(popup);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 9); popup.AddChild(box);
        var note = Wrap($"Up to 3 employees · $100 per store hour while summoned. Final shifts round to the nearest 15 minutes. 1 store hour = 2 real minutes at 1×. Wages so far: ${_game.State.WageExpense:0.00}; owed: ${_game.State.WagesPayable:0.00}. The owner is unpaid.");
        note.AddThemeColorOverride("font_color", Paper); box.AddChild(note);
        void Refresh() { popup.QueueFree(); ShowStaff(); }
        foreach (var role in Enum.GetValues<EmployeeRole>())
        {
            var chosen = role; var hire = Button("Hire · " + role, () => { _game.Staff.Hire(chosen); Refresh(); });
            hire.Disabled = _game.State.Employees.Count >= 3; box.AddChild(hire);
        }
        for (var i = 0; i < _game.State.Employees.Count; i++)
        {
            var index = i; var employee = _game.State.Employees[i];
            var shift = employee.ClockOutRequested ? "finishing delivery" : employee.Summoned ? $"on shift · {employee.ShiftHours:0.00} h" : "off shift";
            box.AddChild(Label($"Employee {i + 1} · {employee.Role} · {shift}", 14, Paper));
            var row = new HBoxContainer(); box.AddChild(row);
            var clock = Button(employee.Summoned ? "Dismiss / clock out" : "Summon / clock in", () => { if (employee.Summoned) _game.Staff.ClockOut(index); else _game.Staff.Summon(index); Refresh(); });
            clock.Disabled = employee.ClockOutRequested; row.AddChild(clock);
            clock.CustomMinimumSize = new(200, 34);
            var role = Button("Change role", () => { _game.Staff.SetRole(index, (EmployeeRole)(((int)employee.Role + 1) % 3)); Refresh(); }); role.Disabled = employee.Target != 0 || _game.Staff.HasPayment(employee); row.AddChild(role);
            row.AddChild(Button("Fire", () => { _game.Staff.Dismiss(index); Refresh(); }));
        }
        popup.Confirmed += () => popup.QueueFree(); popup.Canceled += () => popup.QueueFree(); popup.PopupCentered(new(550, 500));
    }
    private void ShowAudioSettings()
    {
        var popup = new AcceptDialog { Title = "Shop audio", OkButtonText = "Done" };
        _root.AddChild(popup);
        var content = new VBoxContainer(); content.AddThemeConstantOverride("separation", 9); popup.AddChild(content);
        var introduction = Wrap("Character chatter, store sounds and the boutique theme.", 14);
        introduction.AddThemeColorOverride("font_color", new("d9ded5")); content.AddChild(introduction);
        var mute = new CheckBox { Text = "Mute all sound", ButtonPressed = _game.Preferences.Muted }; content.AddChild(mute);
        mute.Toggled += value => { _game.Preferences.Muted = value; _game.SavePreferences(); };
        void Slider(string title, float value, Action<float> change)
        {
            var heading = Label(title + $" · {value * 100:0}%", 14, Paper); content.AddChild(heading);
            var slider = new HSlider { MinValue = 0, MaxValue = 1, Step = .05, Value = value, CustomMinimumSize = new(340, 24) };
            content.AddChild(slider);
            slider.ValueChanged += number => { heading.Text = title + $" · {number * 100:0}%"; change((float)number); _game.SavePreferences(); };
        }
        Slider("Master", _game.Preferences.Master, value => _game.Preferences.Master = value);
        Slider("Voices", _game.Preferences.Voices, value => _game.Preferences.Voices = value);
        Slider("Store effects", _game.Preferences.Effects, value => _game.Preferences.Effects = value);
        Slider("Music", _game.Preferences.Music, value => _game.Preferences.Music = value);
        popup.Confirmed += () => popup.QueueFree(); popup.Canceled += () => popup.QueueFree();
        popup.PopupCentered(new(410, 390));
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
        var v = new VBoxContainer { CustomMinimumSize = new(135, 0) }; v.AddChild(Label(heading, 11, Muted)); value = Label("", 23); v.AddChild(value); return v;
    }
    public bool IsOverUi()
    {
        var mouse = _root.GetGlobalMousePosition();
        return _panels.Any(p => p.Visible && p.GetGlobalRect().HasPoint(mouse)) || _root.GetChildren().OfType<Window>().Any(w => w.Visible);
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
            var picture = new TextureRect { CustomMinimumSize = new(180, 112), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore };
            col.AddChild(picture); _game.Pictures.Request(product, picture);
            col.AddChild(Label(p.Name, 16)); col.AddChild(Wrap(p.Description, 12));
            var price = p.Cost;
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
            _inspector.AddChild(Wrap(_game.Open ? "Watch shoppers explore your collection. Price, stock and access shape each visit." : "Build your electronics store from an empty room. Add a checkout counter, supply cabinet and shelving from Displays, then stock products. B changes shelf deck."));
            _inspector.AddChild(new HSeparator());
            _inspector.AddChild(Label("01  Your shopkeeper", 16)); _inspector.AddChild(Wrap("Click clear floor space to walk, or use I/J/K/L. Press F to follow her and 1/2/3 for different views.", 13));
            _inspector.AddChild(Button("Shelf deck · B", _game.CycleShelfLevel));
            _inspector.AddChild(Label("02  Curate a collection", 16)); _inspector.AddChild(Wrap("Each visible object is one item. When a customer takes it, click its empty outline to send her for one replacement.", 13));
            _inspector.AddChild(Label("03  Open the doors", 16)); _inspector.AddChild(Wrap("Press C or choose Work at checkout to serve. Customers wait until you send her; she stays free for your other tasks.", 13));
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
        if (item.Kind is "checkout" or "stockroom")
        {
            _inspector.AddChild(Wrap(item.Kind == "checkout" ? "Your shopkeeper serves here. Keep both the customer side and her route behind it clear." : "Your shopkeeper collects one replacement here before carrying it to an empty spot.", 13));
            if (item.Kind == "checkout")
            {
                _inspector.AddChild(Button(item.CheckoutOpen ? "Close this register" : "Open this register", () => _game.Registers.SetOpen(item, !item.CheckoutOpen)));
                _inspector.AddChild(Button("Work at this register · C", () => _game.Keeper.ServeCheckout(item)));
            }
            _inspector.AddChild(EditButton("Remove fixture", _game.RemoveSelected));
            _inspector.AddChild(Button("Look closer", () => _game.Camera.ZoomTo(item.Position))); return;
        }
        _inspector.AddChild(EditButton(item.ForSale ? "Keep as store fixture" : "Mark for sale", _game.ToggleSale));
        _inspector.AddChild(new HSeparator());
        if (item.Used) _inspector.AddChild(Label("USED · AT COST", 16, Accent));
        _inspector.AddChild(Label($"Retail price       ${item.Price:0}", 18));
        var prices = new HBoxContainer(); _inspector.AddChild(prices);
        foreach (var (text, value) in new[] { ("− $5", -5m), ("+ $5", 5m), ("+ $25", 25m) })
        { var amount = value; var b = EditButton(text, () => _game.ChangePrice(amount)); b.CustomMinimumSize = new(50, 34); b.AddThemeFontSizeOverride("font_size", 12); prices.AddChild(b); }
        _inspector.AddChild(Wrap($"Unit cost ${item.Product.Cost:0}  ·  Margin ${item.Price - item.Product.Cost:0}\nSuggested retail ${item.Product.Price:0}", 12));
        _inspector.AddChild(Label(item.Stock == 1 ? "On display: 1 item" : "Empty display spot", 17));
        if (item.Stock == 0) _inspector.AddChild(Wrap("Click the empty outline in the shop to send your shopkeeper for one replacement.", 12));
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
        _fullscreen.Text = _game.Preferences.Fullscreen ? "Windowed" : "Full screen";
        _keeperStatus.Text = _game.Keeper.Status; _keeperStatus.TooltipText = _game.Keeper.Status;
        _cash.Text = $"${_game.State.Cash:N0}"; _revenue.Text = $"${_game.State.DayRevenue:N0}";
        _day.Text = $"D{1 + (int)(_game.State.WorldHours / 24):00} {(_game.Open ? "OPEN" : "CLOSED")}  {StoreClock.Time(_game.State)}";
        _day.AddThemeFontSizeOverride("font_size", 19);
        _reputation.Text = $"Happy reputation · {StoreClock.Reputation(_game.State):P0}";
        _cash.TooltipText = $"Wages ${_game.State.WageExpense:0.00} · owed ${_game.State.WagesPayable:0.00} · net profit ${_game.State.Revenue - _game.State.CostOfSales - _game.State.WageExpense:0.00}";
        _open.Text = _game.Open ? "Close store" : _game.CanOpen ? "Open store  →" : "Finishing up…"; _open.Disabled = !_game.Open && !_game.CanOpen;
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
