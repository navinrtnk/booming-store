// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
namespace BoomingStore;

/// <summary>Two physical registers, exclusive cashiers and stable per-register queues.</summary>
public sealed class CheckoutCoordinator(StoreGame game)
{
    public IEnumerable<StoreItem> Registers => game.State.Items.Where(i => i.Kind == "checkout" && !i.ForSale).OrderBy(i => i.Id);
    public StoreItem? Find(int id)
    {
        foreach (var item in game.State.Items)
            if (item.Id == id && item.Kind == "checkout" && !item.ForSale) return item;
        return null;
    }
    public static Vector3 StaffSpot(StoreItem register) => register.Position + new Vector3(0, 0, -.95f).Rotated(Vector3.Up, Mathf.DegToRad(register.Rotation));
    public static Vector3 LineSpot(StoreItem register, int index) => register.Position + new Vector3(0, 0, 1.1f + index * .68f).Rotated(Vector3.Up, Mathf.DegToRad(register.Rotation));
    public IEnumerable<Shopper> Line(int id) => game.Queue.Where(s => s.RegisterId == id);
    public Shopper? Head(int id)
    {
        foreach (var shopper in game.Queue) if (shopper.RegisterId == id) return shopper;
        return null;
    }
    public int IndexOf(Shopper shopper)
    {
        var index = 0;
        foreach (var waiting in game.Queue)
        {
            if (waiting.RegisterId != shopper.RegisterId) continue;
            if (waiting == shopper) return index;
            index++;
        }
        return -1;
    }
    public StoreItem? Available(PersonActor actor, int preferred = 0)
    {
        var occupied = game.State.Employees.Where(e => e.Summoned && game.Staff.ActorFor(e) != actor).Select(e => e.RegisterId).Where(id => id != 0).ToHashSet();
        if (game.Keeper.Actor != actor && game.Keeper.OperatingRegisterId != 0) occupied.Add(game.Keeper.OperatingRegisterId);
        return Registers.Where(r => !occupied.Contains(r.Id) && game.Navigation.CanReach(actor.Position, StaffSpot(r)))
            .OrderBy(r => r.Id == preferred ? 0 : 1).ThenBy(r => actor.Position.DistanceSquaredTo(StaffSpot(r))).FirstOrDefault();
    }
    public void SetOpen(StoreItem register, bool open)
    {
        if (register.CheckoutOpen == open) return;
        register.CheckoutOpen = open; game.StockChanged(register); Rebalance(open ? register.Id : 0);
    }
    public void Assign(Shopper shopper)
    {
        shopper.RegisterId = Registers.Where(r => r.CheckoutOpen).OrderBy(r => Line(r.Id).Count()).ThenBy(r => r.Id).FirstOrDefault()?.Id ?? 0;
    }
    public void Rebalance(int newlyOpened = 0)
    {
        var open = Registers.Where(r => r.CheckoutOpen).ToArray();
        var waiting = game.Queue.Where(s => s.State != Shopper.Activity.Paying).ToList();
        var counts = open.ToDictionary(r => r.Id, r => game.Queue.Count(s => s.State == Shopper.Activity.Paying && s.RegisterId == r.Id));
        foreach (var shopper in waiting) shopper.RegisterId = 0;
        if (newlyOpened != 0 && counts.ContainsKey(newlyOpened) && waiting.Count > 0)
        {
            waiting[0].RegisterId = newlyOpened; counts[newlyOpened]++; waiting.RemoveAt(0);
        }
        foreach (var shopper in waiting)
        {
            if (open.Length == 0) break;
            var id = open.OrderBy(r => counts[r.Id]).ThenBy(r => r.Id).First().Id;
            shopper.RegisterId = id; counts[id]++;
        }
        RefreshRoutes();
    }
    public Vector3 Spot(Shopper shopper)
    {
        var index = Math.Max(0, IndexOf(shopper));
        if (Find(shopper.RegisterId) is { } register) return LineSpot(register, index);
        var desired = new Vector3(-1.5f + index % 3 * .75f, 0, 2.5f + index / 3 * .65f);
        return game.Navigation.CanStand(desired) ? desired : game.Navigation.Entrance;
    }
    public void RefreshRoutes()
    {
        foreach (var shopper in game.Queue) if (shopper.State == Shopper.Activity.Queueing) shopper.RedirectQueue(Spot(shopper));
    }
}
