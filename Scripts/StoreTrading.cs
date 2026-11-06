// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
namespace BoomingStore;

public sealed record PurchaseReceipt(StoreItem Item, decimal Price);

/// <summary>Reserves available stock and honors each shopper's agreed price exactly once.</summary>
public sealed class StoreTrading(StoreState state)
{
    private readonly Dictionary<ulong, PurchaseReceipt> _reservations = [];
    private readonly Dictionary<int, int> _reservedStock = [];
    public int Reserved(StoreItem item) => _reservedStock.GetValueOrDefault(item.Id);

    public bool TryReserve(ulong buyer, StoreItem item)
    {
        if (_reservations.ContainsKey(buyer) || !state.Items.Contains(item) || !item.ForSale ||
            item.Stock <= Reserved(item) || item.Price is < 1 or > 99999) return false;
        _reservations.Add(buyer, new(item, item.Price));
        _reservedStock[item.Id] = Reserved(item) + 1;
        return true;
    }

    public void Cancel(ulong buyer)
    {
        if (!_reservations.Remove(buyer, out var receipt)) return;
        var count = Reserved(receipt.Item) - 1;
        if (count == 0) _reservedStock.Remove(receipt.Item.Id);
        else _reservedStock[receipt.Item.Id] = count;
    }

    public PurchaseReceipt? Checkout(ulong buyer)
    {
        if (!_reservations.TryGetValue(buyer, out var receipt)) return null;
        Cancel(buyer);
        var (item, price) = receipt;
        if (!state.Items.Contains(item) || !item.ForSale || item.Stock < 1) return null;
        item.Stock--;
        state.Cash += price;
        state.Revenue += price;
        state.DayRevenue += price;
        state.CostOfSales += item.Product.Cost;
        state.DayCost += item.Product.Cost;
        state.Sales++;
        state.DaySales++;
        return receipt;
    }
}
