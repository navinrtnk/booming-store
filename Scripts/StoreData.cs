// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace BoomingStore;

public sealed record Product(string Id, string Name, string Category, string Description,
    float Width, float Depth, decimal Cost, decimal Price, bool Merchandise = true, float Surface = 0, float Height = .5f, float[]? Levels = null)
{
    public bool Supports(float y) => Levels == null ? Surface > 0 && Math.Abs(y - Surface) < .001f : Levels.Any(level => Math.Abs(y - level) < .001f);
    public bool FitsLevel(float y, float height) => Supports(y) && (Levels == null || !Levels.Any(level => level > y && level < y + height + .025f));
    public bool Electronics => Category is "TVs" or "Computers" or "Audio";
}

public static class Catalog
{
    public static readonly Product[] Products = [
        new("tv32", "32-inch HD TV", "TVs", "Entry · HD panel · 60 Hz · HDMI. Compact and affordable.", .78f, .32f, 140, 249, Height: .52f),
        new("tv43", "43-inch 4K Smart TV", "TVs", "Standard · 4K UHD · streaming apps · HDR10.", 1.01f, .34f, 270, 449, Height: .67f),
        new("tv55", "55-inch QLED TV", "TVs", "Premium · quantum-dot color · 4K · 120 Hz · HDMI 2.1.", 1.28f, .38f, 530, 849, Height: .84f),
        new("tv65", "65-inch OLED TV", "TVs", "Flagship · OLED contrast · 4K · 144 Hz · VRR.", 1.5f, .4f, 950, 1499, Height: .96f),
        new("tv75", "75-inch Cinema OLED", "TVs", "Cinema · OLED · 4K · immersive HDR · 144 Hz. Upper shelf only.", 1.74f, .44f, 1500, 2299, Height: 1.12f),
        new("laptop_basic", "Everyday laptop", "Computers", "Entry · 14-inch · 8 GB RAM · 256 GB SSD.", .34f, .26f, 240, 399, Height: .26f),
        new("laptop_pro", "Creator laptop", "Computers", "Premium · 16-inch high-color display · 32 GB RAM · 1 TB SSD.", .39f, .28f, 850, 1299, Height: .3f),
        new("console", "Gaming console", "Computers", "4K gaming · 1 TB SSD · wireless controller.", .24f, .3f, 320, 499, Height: .4f),
        new("speaker", "Wireless speaker", "Audio", "Entry · Bluetooth · portable stereo.", .28f, .22f, 45, 89, Height: .28f),
        new("soundbar", "Cinema soundbar", "Audio", "Premium · spatial audio · HDMI eARC · wireless streaming.", .95f, .18f, 150, 279, Height: .13f),
        new("headphones", "Studio headphones", "Audio", "Noise cancelling · wireless · 40-hour battery.", .25f, .22f, 85, 159, Height: .3f),
        new("tv_rack", "Two-level TV shelving", "Displays", "Steel display bay · lower deck 0.28 m · upper deck 1.28 m. Use B to choose a deck.", 2.4f, .72f, 260, 520, false, 1.28f, 1.36f, [.28f, 1.28f]),
        new("tech_shelf", "Three-level electronics shelf", "Displays", "Small electronics on 0.25 m, 0.85 m and 1.45 m decks. B changes deck.", 1.6f, .65f, 190, 380, false, 1.45f, 1.55f, [.25f, .85f, 1.45f]),
        new("vase", "Arc ceramic vase", "Objects", "Sculptural stoneware, finished by hand.", .45f, .45f, 18, 38),
        new("candle", "Amber candle", "Objects", "Warm amber in a reusable glass vessel.", .3f, .3f, 9, 22),
        new("books", "Design collection", "Objects", "A considered stack of art and interiors books.", .45f, .35f, 16, 34),
        new("bowl", "Stone serving bowl", "Objects", "A generous bowl in a satin ceramic glaze.", .5f, .5f, 22, 48),
        new("plant", "Fiddle-leaf fig", "Furniture", "Living greenery in a terracotta planter.", .8f, .8f, 65, 135),
        new("chair", "Atelier lounge chair", "Furniture", "Soft upholstery on a solid oak frame.", .85f, .85f, 125, 245),
        new("lamp", "Linen floor lamp", "Furniture", "A warm pool of light beneath a linen shade.", .65f, .65f, 72, 155),
        new("table", "Oak display table", "Displays", "A low platform for your collection. Place objects on top.", 2, 1, 180, 360, false, .8f),
        new("shelf", "Gallery shelving", "Displays", "Open oak shelves. Objects can sit on the middle shelf.", 1.5f, .6f, 240, 475, false, 1.14f),
        new("plinth", "Stone plinth", "Displays", "Give a special piece room to stand out.", .8f, .8f, 95, 195, false, .95f),
        new("rug", "Woven wool rug", "Decor", "A soft foundation; furniture may sit above it.", 3, 2, 120, 250, false),
        new("stockroom", "Supply cabinet", "Displays", "Collect one replacement here before carrying it to an empty spot.", 1, .7f, 240, 480, false),
        new("checkout", "Checkout counter", "Displays", "Your shopkeeper serves here. Keep both sides of the counter accessible.", 1.8f, .9f, 420, 750, false)
    ];
    private static readonly Dictionary<string, Product> ById = Products.ToDictionary(p => p.Id, StringComparer.Ordinal);
    public static Product Get(string id) => ById[id];
    public static bool Contains(string id) => id != null && ById.ContainsKey(id);
    public static readonly string[] Colors = ["e9dfcb", "c27f60", "567065", "313c3b", "bda886", "a2b7bd"];
    public static readonly string[] ColorNames = ["Chalk", "Terracotta", "Sage", "Charcoal", "Sand", "Mist"];
}

public sealed class StoreItem
{
    public int Id { get; set; }
    public string Kind { get; set; } = "vase";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public int Rotation { get; set; }
    public int Color { get; set; }
    public int ParentId { get; set; }
    public bool CheckoutOpen { get; set; }
    public bool ForSale { get; set; }
    public bool Used { get; set; }
    public decimal NewPrice { get; set; }
    public void RestockNew() { Stock = 1; if (Used && NewPrice > 0) Price = NewPrice; Used = false; }
    public int Stock { get; set; } = 1;
    public decimal Price { get; set; }
    [JsonIgnore] public Vector3 Position => new(X, Y, Z);
    [JsonIgnore] public Product Product => Catalog.Get(Kind);
    [JsonIgnore] public Vector2 Footprint => Rotation % 180 == 0 ? new(Product.Width, Product.Depth) : new(Product.Depth, Product.Width);
    [JsonIgnore] public bool Blocks => Stock == 1 && Y < .1f && Kind != "rug";
    public StoreItem Copy() => (StoreItem)MemberwiseClone();
}

public sealed class StoreWall
{
    public int X { get; set; }
    public int Z { get; set; }
    public bool AlongX { get; set; }
    public string Kind { get; set; } = "wall";
    public int Color { get; set; }
    [JsonIgnore] public string Key => $"{X}:{Z}:{AlongX}";
}

/// <summary>Only settled business data is saved. Shoppers and unpaid reservations are transient.</summary>
public sealed class StoreState
{
    public int Schema { get; set; } = 3;
    [JsonIgnore] public bool MigratedFromStackedStock { get; private set; }
    public string StoreName { get; set; } = "Circuit & Co.";
    public List<StoreItem> Items { get; set; } = [];
    public List<StoreWall> Walls { get; set; } = [];
    public int[] Floors { get; set; } = new int[120];
    public decimal Cash { get; set; } = 10000;
    public double WorldHours { get; set; } = 9;
    public decimal WageExpense { get; set; }
    public decimal DayWages { get; set; }
    public decimal WagesPayable { get; set; }
    public List<CustomerMood> Departures { get; set; } = [];
    public decimal Revenue { get; set; }
    public decimal CostOfSales { get; set; }
    public decimal DayRevenue { get; set; }
    public decimal DayCost { get; set; }
    public bool WasOpen { get; set; }
    public int Transactions { get; set; }
    public int Sales { get; set; }
    public int DaySales { get; set; }
    public int Day { get; set; }
    public double TradingSeconds { get; set; }
    public int NextId { get; set; } = 1;
    public bool OwnerWorkingCheckout { get; set; }
    public int KeeperRegisterId { get; set; }
    public float KeeperX { get; set; } = -3.6f;
    public float KeeperZ { get; set; } = -3.95f;
    public List<int> RestockQueue { get; set; } = [];
    public int CarriedItemId { get; set; }
    public List<EmployeeData> Employees { get; set; } = [];
    public List<ReturnedUnit> Returns { get; set; } = [];
    public KeeperCommand? NextKeeperCommand { get; set; }
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions CompactJson = new();
    public string Serialize(bool indented = true) => JsonSerializer.Serialize(this, indented ? Json : CompactJson);
    public static StoreState Parse(string json)
    {
        var s = JsonSerializer.Deserialize<StoreState>(json, Json) ?? throw new InvalidDataException("Empty save.");
        if (s.Schema is not (1 or 2 or 3) || string.IsNullOrWhiteSpace(s.StoreName) || s.StoreName.Length > 32 || s.StoreName.Any(char.IsControl) || s.Items == null || s.Walls == null || s.Floors == null || s.Floors.Length != 120 ||
            s.Items.Count > 600 || s.Walls.Count > 300 || s.Floors.Any(f => f is < 0 or > 2) || s.Cash < 0 || s.Cash > 1_000_000_000 ||
            !double.IsFinite(s.TradingSeconds) || s.TradingSeconds < 0 || s.Day < 0 || s.Sales < 0 ||
            s.Revenue < 0 || s.CostOfSales < 0 || s.DayRevenue < 0 || s.DayCost < 0 || s.DaySales < 0)
            throw new InvalidDataException("Unsupported or invalid save data.");
        if (!double.IsFinite(s.WorldHours) || s.WorldHours < 0 || s.WorldHours > 100000000 || s.Transactions < 0 || s.WageExpense < 0 || s.DayWages < 0 ||
            s.WagesPayable < 0 || s.WagesPayable > s.WageExpense || s.Departures == null || s.Departures.Count > 20 || s.Departures.Any(m => !Enum.IsDefined(m)))
            throw new InvalidDataException("Invalid clock, payroll or reputation.");
        var ids = new HashSet<int>();
        foreach (var item in s.Items)
        {
            if (item == null || !Catalog.Contains(item.Kind) || !ids.Add(item.Id) || item.Id <= 0 || item.Id == int.MaxValue ||
                !float.IsFinite(item.X) || !float.IsFinite(item.Y) || !float.IsFinite(item.Z) ||
                Math.Abs(item.X) > 6 || Math.Abs(item.Z) > 5 || item.Y < 0 || item.Y > 2 ||
                item.Rotation is < 0 or >= 360 || item.Rotation % 90 != 0 || item.Color is < 0 or > 5 ||
                item.Price < 1 || item.Price > 99999 || item.Stock < 0 || item.Stock > (s.Schema == 1 ? 99 : 1))
                throw new InvalidDataException("Invalid catalog object in save.");
        }
        var byId = s.Items.ToDictionary(i => i.Id);
        if (s.Employees == null || s.Employees.Count > 3 || s.Employees.Any(e => e == null || !Enum.IsDefined(e.Role) ||
            !float.IsFinite(e.X) || !float.IsFinite(e.Z) || Math.Abs(e.X) >= 6 || Math.Abs(e.Z) >= 5 ||
            e.RegisterId != 0 && (!byId.TryGetValue(e.RegisterId, out var register) || register.Kind != "checkout" || e.Role != EmployeeRole.Cashier || !e.Summoned) ||
            e.Target != 0 && (!byId.ContainsKey(e.Target) || byId[e.Target].Stock != 0 || !byId[e.Target].ForSale) || e.Collected && e.Target == 0 || e.Role == EmployeeRole.Sales && e.Target != 0 || !double.IsFinite(e.ShiftHours) || e.ShiftHours < 0 || e.ShiftHours > 100000000 ||
            e.PaidQuarters < 0 || e.PaidQuarters > Payroll.CompletedQuarters(e.ShiftHours) || !e.Summoned && (e.Target != 0 || e.ClockOutRequested)) ||
            s.Employees.Where(e => e.Target != 0).Select(e => e.Target).Distinct().Count() != s.Employees.Count(e => e.Target != 0) ||
            s.Returns == null || s.Returns.Count > 10000 || s.Returns.Any(r => r == null || !byId.ContainsKey(r.ItemId)) ||
            s.Items.Any(i => i.NewPrice < 0 || i.NewPrice > 99999 || i.Used && i.Price != i.Product.Cost))
            throw new InvalidDataException("Invalid employees or returned stock.");
        if (s.Employees.Where(e => e.RegisterId != 0).Select(e => e.RegisterId).Distinct().Count() != s.Employees.Count(e => e.RegisterId != 0) ||
            s.Items.Count(i => i.Kind == "checkout") > 2 || s.KeeperRegisterId != 0 && (!byId.TryGetValue(s.KeeperRegisterId, out var ownerRegister) || ownerRegister.Kind != "checkout"))
            throw new InvalidDataException("Invalid checkout assignments.");
        if (s.Items.Any(i => i.ParentId == 0 ? Math.Abs(i.Y) > .001f :
            !byId.TryGetValue(i.ParentId, out var parent) || parent.ParentId != 0 || parent.Product.Surface <= 0 ||
            i.Id == i.ParentId || !parent.Product.FitsLevel(i.Y, i.Product.Height)))
            throw new InvalidDataException("A display object is missing its support.");
        if (!float.IsFinite(s.KeeperX) || !float.IsFinite(s.KeeperZ) || Math.Abs(s.KeeperX) >= 6 || Math.Abs(s.KeeperZ) >= 5 ||
            s.RestockQueue == null || s.RestockQueue.Count > 600 || s.RestockQueue.Distinct().Count() != s.RestockQueue.Count ||
            s.RestockQueue.Any(id => !byId.TryGetValue(id, out var item) || item.Stock != 0 || !item.ForSale || s.Employees.Any(e => e.Target == id)) ||
            s.CarriedItemId != 0 && (s.RestockQueue.Count == 0 || s.RestockQueue[0] != s.CarriedItemId))
            throw new InvalidDataException("Invalid shopkeeper or restocking state.");
        if (s.NextKeeperCommand is { } next && (!Enum.IsDefined(next.Kind) || !next.Position.IsFinite() ||
            Math.Abs(next.X) >= 6 || Math.Abs(next.Z) >= 5 || next.Kind == KeeperCommandKind.Restock &&
            (!byId.TryGetValue(next.ItemId, out var nextItem) || nextItem.Stock != 0 || !nextItem.ForSale)))
            throw new InvalidDataException("Invalid next shopkeeper command.");
        if (s.Schema == 1)
        {
            foreach (var item in s.Items)
            {
                if (item.Stock > 1) s.Cash += (item.Stock - 1) * item.Product.Cost;
                item.Stock = Math.Min(item.Stock, 1);
            }
            s.Schema = 2; s.MigratedFromStackedStock = true;
        }
        var walls = new HashSet<string>();
        foreach (var w in s.Walls)
            if (w == null || w.Kind is not ("wall" or "window" or "door") || w.Color is < 0 or > 5 ||
                w.X < -6 || w.Z < -5 || w.X > (w.AlongX ? 5 : 6) || w.Z > (w.AlongX ? 5 : 4) || !walls.Add(w.Key))
                throw new InvalidDataException("Invalid wall in save.");
        s.Schema = 3;
        s.NextId = Math.Max(1, Math.Max(s.NextId, s.Items.Select(i => i.Id).DefaultIfEmpty(0).Max() + 1));
        return s;
    }
}
