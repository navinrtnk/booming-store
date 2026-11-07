// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace BoomingStore;

public sealed record Product(string Id, string Name, string Category, string Description,
    float Width, float Depth, decimal Cost, decimal Price, bool Merchandise = true, float Surface = 0);

public static class Catalog
{
    public static readonly Product[] Products = [
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
    public bool ForSale { get; set; }
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
    public int Schema { get; set; } = 2;
    [JsonIgnore] public bool MigratedFromStackedStock { get; private set; }
    public string StoreName { get; set; } = "Atelier";
    public List<StoreItem> Items { get; set; } = [];
    public List<StoreWall> Walls { get; set; } = [];
    public int[] Floors { get; set; } = new int[120];
    public decimal Cash { get; set; } = 12000;
    public decimal Revenue { get; set; }
    public decimal CostOfSales { get; set; }
    public decimal DayRevenue { get; set; }
    public decimal DayCost { get; set; }
    public int Sales { get; set; }
    public int DaySales { get; set; }
    public int Day { get; set; }
    public double TradingSeconds { get; set; }
    public int NextId { get; set; } = 1;
    public float KeeperX { get; set; } = -3.6f;
    public float KeeperZ { get; set; } = -3.95f;
    public List<int> RestockQueue { get; set; } = [];
    public int CarriedItemId { get; set; }
    public KeeperCommand? NextKeeperCommand { get; set; }
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions CompactJson = new();
    public string Serialize(bool indented = true) => JsonSerializer.Serialize(this, indented ? Json : CompactJson);
    public static StoreState Parse(string json)
    {
        var s = JsonSerializer.Deserialize<StoreState>(json, Json) ?? throw new InvalidDataException("Empty save.");
        if (s.Schema is not (1 or 2) || string.IsNullOrWhiteSpace(s.StoreName) || s.StoreName.Length > 32 || s.StoreName.Any(char.IsControl) || s.Items == null || s.Walls == null || s.Floors == null || s.Floors.Length != 120 ||
            s.Items.Count > 600 || s.Walls.Count > 300 || s.Floors.Any(f => f is < 0 or > 2) || s.Cash < 0 || s.Cash > 1_000_000_000 ||
            !double.IsFinite(s.TradingSeconds) || s.TradingSeconds < 0 || s.Day < 0 || s.Sales < 0 ||
            s.Revenue < 0 || s.CostOfSales < 0 || s.DayRevenue < 0 || s.DayCost < 0 || s.DaySales < 0)
            throw new InvalidDataException("Unsupported or invalid save data.");
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
        if (s.Items.Any(i => i.ParentId == 0 ? Math.Abs(i.Y) > .001f :
            !byId.TryGetValue(i.ParentId, out var parent) || parent.ParentId != 0 || parent.Product.Surface <= 0 ||
            i.Id == i.ParentId || Math.Abs(i.Y - parent.Product.Surface) > .001f))
            throw new InvalidDataException("A display object is missing its support.");
        if (!float.IsFinite(s.KeeperX) || !float.IsFinite(s.KeeperZ) || Math.Abs(s.KeeperX) >= 6 || Math.Abs(s.KeeperZ) >= 5 ||
            s.RestockQueue == null || s.RestockQueue.Count > 600 || s.RestockQueue.Distinct().Count() != s.RestockQueue.Count ||
            s.RestockQueue.Any(id => !byId.TryGetValue(id, out var item) || item.Stock != 0 || !item.ForSale) ||
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
        s.NextId = Math.Max(1, Math.Max(s.NextId, s.Items.Select(i => i.Id).DefaultIfEmpty(0).Max() + 1));
        return s;
    }
}
