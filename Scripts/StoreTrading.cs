// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using System.Text.Json;
namespace BoomingStore;
public sealed record PurchaseReceipt(StoreItem Item, decimal Price);
public sealed class ReturnedUnit
{
    public int ItemId { get; set; }
    public bool Used { get; set; }
}
public sealed record CheckoutReceipt(IReadOnlyList<PurchaseReceipt> Lines)
{
    public StoreItem Item => Lines[0].Item;
    public decimal Price => Lines.Sum(line => line.Price);
    public void Deconstruct(out StoreItem item, out decimal price) { item = Item; price = Price; }
}
/// <summary>Carts own units independently of displays. A checkout settles its whole basket once.</summary>
public sealed class StoreTrading(StoreState state)
{
    private sealed record Unit(PurchaseReceipt Receipt, bool Used);
    private readonly Dictionary<ulong, List<Unit>> _carts = [];
    public int Reserved(StoreItem item) => _carts.Values.Sum(cart => cart.Count(u => u.Receipt.Item.Id == item.Id));
    public int CartCount(ulong buyer) => _carts.TryGetValue(buyer, out var cart) ? cart.Count : 0;
    public decimal CartValue(ulong buyer) => _carts.TryGetValue(buyer, out var cart) ? cart.Sum(u => u.Receipt.Price) : 0;
    public bool TryReserve(ulong buyer, StoreItem item)
    {
        if (!state.Items.Contains(item) || !item.ForSale || item.Stock < 1 || item.Price is < 1 or > 99999) return false;
        if (!_carts.TryGetValue(buyer, out var cart)) _carts[buyer] = cart = [];
        if (cart.Count >= 3 || cart.Any(u => u.Receipt.Item.Id == item.Id)) return false;
        cart.Add(new(new(item, item.Used ? item.Product.Cost : item.Price), item.Used)); item.Stock = 0; return true;
    }
    public void Cancel(ulong buyer, bool used = false)
    {
        if (!_carts.Remove(buyer, out var cart)) return;
        foreach (var unit in cart)
            if (state.Items.Contains(unit.Receipt.Item)) state.Returns.Add(new() { ItemId = unit.Receipt.Item.Id, Used = used || unit.Used });
    }
    public IEnumerable<StoreItem> ProcessReturns(Func<StoreItem, bool>? canPlace = null)
    {
        foreach (var unit in state.Returns.ToArray())
        {
            var item = state.Items.FirstOrDefault(i => i.Id == unit.ItemId);
            if (item == null) { state.Returns.Remove(unit); continue; }
            if (canPlace != null && !canPlace(item) || item.Stock != 0 || state.RestockQueue.Contains(item.Id) || state.Employees.Any(e => e.Target == item.Id)) continue;
            item.RestockNew();
            if (unit.Used) { item.NewPrice = item.Price; item.Used = true; item.Price = item.Product.Cost; }
            state.Returns.Remove(unit); yield return item;
        }
    }
    public StoreState SettledSnapshot()
    {
        var snapshot = JsonSerializer.Deserialize<StoreState>(state.Serialize())!;
        foreach (var cart in _carts.Values) foreach (var unit in cart)
            snapshot.Returns.Add(new() { ItemId = unit.Receipt.Item.Id, Used = unit.Used });
        var trading = new StoreTrading(snapshot); _ = trading.ProcessReturns().ToArray();
        return StoreState.Parse(snapshot.Serialize());
    }
    public CheckoutReceipt? Checkout(ulong buyer)
    {
        if (!_carts.TryGetValue(buyer, out var cart)) return null;
        if (cart.Any(u => !state.Items.Contains(u.Receipt.Item) || !u.Receipt.Item.ForSale)) { Cancel(buyer); return null; }
        _carts.Remove(buyer);
        var receipt = new CheckoutReceipt(cart.Select(u => u.Receipt).ToArray());
        var cost = cart.Sum(u => u.Receipt.Item.Product.Cost);
        state.Cash += receipt.Price; state.Revenue += receipt.Price; state.DayRevenue += receipt.Price;
        state.CostOfSales += cost; state.DayCost += cost; state.Sales += cart.Count; state.DaySales += cart.Count; state.Transactions++;
        return receipt;
    }
}
