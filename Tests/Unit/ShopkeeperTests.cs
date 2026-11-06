// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
using Xunit;

namespace BoomingStore.Tests;

public class RestockTests
{
    private static StoreItem Empty(int id = 1)
    { var item = Fixtures.Item(id: id); item.Stock = 0; return item; }

    [Fact]
    public void TheNewestCommandAndAutomaticCheckoutPreferenceSurviveSaving()
    {
        var state = Fixtures.Shop(Empty()); state.NextId = 2; state.AutoCheckout = false;
        state.NextKeeperCommand = new(KeeperCommandKind.Walk, 2.1f, 3.2f);
        var loaded = StoreState.Parse(state.Serialize());
        Assert.Equal(state.Serialize(), loaded.Serialize()); Assert.False(loaded.AutoCheckout);
        Assert.Equal(new Vector3(2.1f, 0, 3.2f), loaded.NextKeeperCommand!.Position);
        state.NextKeeperCommand = new(KeeperCommandKind.Restock, ItemId: 1);
        Assert.Equal(1, StoreState.Parse(state.Serialize()).NextKeeperCommand!.ItemId);
    }

    [Theory]
    [InlineData("kind")] [InlineData("outside")] [InlineData("missing-item")] [InlineData("stocked-item")]
    public void InvalidNextCommandsAreRejected(string invalid)
    {
        var item = Empty(); var state = Fixtures.Shop(item);
        state.NextKeeperCommand = invalid switch
        {
            "kind" => new((KeeperCommandKind)99),
            "outside" => new(KeeperCommandKind.Walk, 9, 0),
            "missing-item" => new(KeeperCommandKind.Restock, ItemId: 2),
            _ => new(KeeperCommandKind.Restock, ItemId: 1)
        };
        if (invalid == "stocked-item") item.Stock = 1;
        Assert.Throws<InvalidDataException>(() => StoreState.Parse(state.Serialize()));
    }

    [Fact]
    public void ReplacementIsPaidAtCollectionAndDeliveredOnlyOnce()
    {
        var item = Empty(); var state = Fixtures.Shop(item); var orders = new RestockOrders(state);
        Assert.True(orders.Enqueue(item)); Assert.False(orders.Enqueue(item));
        Assert.Equal(9000m, state.Cash); Assert.Equal(0, item.Stock);
        Assert.Null(orders.Deliver());
        Assert.True(orders.Collect()); Assert.False(orders.Collect());
        Assert.Equal(8982m, state.Cash); Assert.Equal(0, item.Stock);
        Assert.Same(item, orders.Deliver()); Assert.Null(orders.Deliver());
        Assert.Equal(1, item.Stock); Assert.False(orders.Busy); Assert.False(orders.Carrying);
        Assert.False(orders.Enqueue(item)); Assert.Equal(8982m, state.Cash);
        Assert.Equal(0, state.CostOfSales); Assert.Equal(0, state.Sales);
    }

    [Fact]
    public void QueueProcessesOneVisibleSpotAtATime()
    {
        var a = Empty(); var b = Empty(2); var state = Fixtures.Shop(a, b); var orders = new RestockOrders(state);
        Assert.True(orders.Enqueue(a)); Assert.True(orders.Enqueue(b));
        Assert.Same(a, orders.Target); Assert.True(orders.Collect()); orders.Deliver();
        Assert.Same(b, orders.Target); Assert.Equal(0, b.Stock);
        Assert.True(orders.Collect()); orders.Deliver();
        Assert.Equal(1, a.Stock); Assert.Equal(1, b.Stock); Assert.False(orders.Busy);
        Assert.Equal(8964m, state.Cash);
    }

    [Fact]
    public void CancelRefundsOnlyTheReplacementAlreadyCollected()
    {
        var a = Empty(); var b = Empty(2); var state = Fixtures.Shop(a, b); var orders = new RestockOrders(state);
        orders.Enqueue(a); orders.Enqueue(b); orders.Cancel(); Assert.Equal(9000m, state.Cash);
        orders.Enqueue(a); orders.Enqueue(b); orders.Collect(); orders.Cancel(); orders.Cancel();
        Assert.Equal(9000m, state.Cash); Assert.Equal(0, a.Stock); Assert.Equal(0, b.Stock);
        Assert.False(orders.Busy); Assert.False(orders.Carrying);
    }

    [Fact]
    public void InsufficientMoneyAndInvalidTargetsCannotCreateInventory()
    {
        var item = Empty(); var state = Fixtures.Shop(item); state.Cash = 17;
        var orders = new RestockOrders(state);
        Assert.False(orders.Enqueue(Empty(2)));
        item.ForSale = false; Assert.False(orders.Enqueue(item)); item.ForSale = true;
        Assert.True(orders.Enqueue(item)); Assert.False(orders.Collect()); Assert.Null(orders.Deliver());
        Assert.Equal(17m, state.Cash); Assert.Equal(0, item.Stock);
        orders.Cancel(); Assert.Equal(17m, state.Cash);
    }

    [Fact]
    public void SaveWhileCarryingResumesWithoutPayingTwice()
    {
        var item = Empty(); var state = Fixtures.Shop(item); state.NextId = 2; state.KeeperX = 1.25f; state.KeeperZ = -2.25f;
        var orders = new RestockOrders(state); orders.Enqueue(item); orders.Collect();
        var loaded = StoreState.Parse(state.Serialize()); var resumed = new RestockOrders(loaded);
        Assert.Equal(state.Serialize(), loaded.Serialize()); Assert.True(resumed.Carrying);
        Assert.False(resumed.Collect()); Assert.NotNull(resumed.Deliver());
        Assert.Equal(8982m, loaded.Cash); Assert.Equal(1, loaded.Items[0].Stock);
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("missing")] [InlineData("stocked")]
    [InlineData("fixture")] [InlineData("wrong-carried")] [InlineData("no-order")]
    public void InvalidPersistedJobsAreRejected(string invalid)
    {
        var item = Empty(); var state = Fixtures.Shop(item); state.RestockQueue = [1];
        switch (invalid)
        {
            case "duplicate": state.RestockQueue.Add(1); break;
            case "missing": state.RestockQueue[0] = 2; break;
            case "stocked": item.Stock = 1; break;
            case "fixture": item.ForSale = false; break;
            case "wrong-carried": state.CarriedItemId = 2; break;
            case "no-order": state.RestockQueue.Clear(); state.CarriedItemId = 1; break;
        }
        Assert.Throws<InvalidDataException>(() => StoreState.Parse(state.Serialize()));
    }

    [Fact]
    public void OldHiddenStockIsConvertedToOneItemAndRefundedExactlyOnce()
    {
        var item = Fixtures.Item(); item.Stock = 5; var state = Fixtures.Shop(item); state.Schema = 1;
        state.Revenue = 38; state.CostOfSales = 18; state.Sales = 1;
        var loaded = StoreState.Parse(state.Serialize());
        Assert.True(loaded.MigratedFromStackedStock); Assert.Equal(2, loaded.Schema); Assert.Equal(1, loaded.Items[0].Stock); Assert.Equal(9072m, loaded.Cash);
        Assert.Equal(38m, loaded.Revenue); Assert.Equal(18m, loaded.CostOfSales); Assert.Equal(1, loaded.Sales);
        var nextLoad = StoreState.Parse(loaded.Serialize());
        Assert.Equal(9072m, nextLoad.Cash); Assert.False(nextLoad.MigratedFromStackedStock);
        state.Schema = 2; Assert.Throws<InvalidDataException>(() => StoreState.Parse(state.Serialize()));
    }

    [Fact]
    public void SellingAndReplacingFurnitureUpdatesWalkableSpace()
    {
        var item = Fixtures.Item("chair"); var state = Fixtures.Shop(item); var nav = new ShopNavigation();
        nav.Rebuild(state); var revision = nav.Revision;
        Assert.False(nav.CanReach(new(0, 0, 3), Vector3.Zero));
        item.Stock = 0; nav.Rebuild(state);
        Assert.True(nav.Revision > revision); Assert.True(nav.CanReach(new(0, 0, 3), Vector3.Zero));
        var orders = new RestockOrders(state); orders.Enqueue(item); orders.Collect(); orders.Deliver(); nav.Rebuild(state);
        Assert.False(nav.CanReach(new(0, 0, 3), Vector3.Zero));
    }
}

public class PersonPoseTests
{
    [Fact]
    public void IdleMotionIsSmallAsymmetricAndVariesBetweenPeople()
    {
        Assert.NotEqual(PersonPose.Relaxed(1, 0), PersonPose.Relaxed(1, 1));
        Assert.NotEqual(PersonPose.Relaxed(1, 0), PersonPose.Relaxed(5, 0));
        for (var profile = 0; profile < 5; profile++) for (var n = 0; n < 1200; n++)
        {
            var pose = PersonPose.Relaxed(n * .1f, profile);
            Assert.InRange(pose.Body.Length(), 0, .015f);
            Assert.InRange(pose.Torso.Length(), 0, .04f);
            Assert.InRange(pose.Head.Length(), 0, .12f);
            Assert.InRange(pose.Smile, .09f, .20f);
            Assert.NotEqual(pose.LeftHand, pose.RightHand);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BentArmsReachTheBoxAndRemainFiniteAtExtremeTargets(bool right)
    {
        var side = right ? -1 : 1;
        var shoulder = new Vector3(side * .2f, 1.4f, 0);
        var elbow = new Vector3(side * .25f, 1.12f, .01f);
        var wrist = new Vector3(side * .28f, .87f, 0);
        var target = new Vector3(side * .19f, 1.04f, .34f);
        var pose = PersonPose.SolveArm(shoulder, elbow, wrist, target);
        var actual = shoulder + pose.Upper * (elbow - shoulder) + pose.Upper * pose.Lower * (wrist - elbow);
        Assert.InRange(actual.DistanceTo(target), 0, .001f);
        foreach (var extreme in new[] { shoulder, shoulder + Vector3.Down * 20, shoulder + Vector3.Up * 20 })
        {
            var arm = PersonPose.SolveArm(shoulder, elbow, wrist, extreme);
            Assert.True(arm.Upper.IsFinite() && arm.Lower.IsFinite() && arm.Wrist.IsFinite());
            Assert.InRange(Math.Abs(arm.Upper.Length() - 1), 0, .0001f);
        }
    }
}
