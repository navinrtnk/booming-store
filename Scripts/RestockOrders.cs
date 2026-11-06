// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
namespace BoomingStore;

/// <summary>One purchased replacement in transit, paid at collection and delivered exactly once.</summary>
public sealed class RestockOrders(StoreState state)
{
    public StoreItem? Target => state.RestockQueue.Count == 0 ? null : state.Items.FirstOrDefault(i => i.Id == state.RestockQueue[0]);
    public bool Carrying => state.CarriedItemId != 0;
    public bool Busy => state.RestockQueue.Count > 0;
    public bool Enqueue(StoreItem item)
    {
        if (!state.Items.Contains(item) || !item.ForSale || item.Stock != 0 || state.RestockQueue.Contains(item.Id)) return false;
        state.RestockQueue.Add(item.Id); return true;
    }
    public bool Collect()
    {
        var item = Target;
        if (item == null || item.Stock != 0 || Carrying || state.Cash < item.Product.Cost) return false;
        state.Cash -= item.Product.Cost; state.CarriedItemId = item.Id; return true;
    }
    public StoreItem? Deliver()
    {
        var item = Target;
        if (item == null || item.Id != state.CarriedItemId || item.Stock != 0) return null;
        item.Stock = 1; state.CarriedItemId = 0; state.RestockQueue.RemoveAt(0); return item;
    }
    public void Cancel()
    {
        if (Carrying && state.Items.FirstOrDefault(i => i.Id == state.CarriedItemId) is { } item) state.Cash += item.Product.Cost;
        state.CarriedItemId = 0; state.RestockQueue.Clear();
    }
}
