// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
using Xunit;

namespace BoomingStore.Tests;

public static class Fixtures
{
    public static StoreItem Item(string kind = "vase", int id = 1, float x = 0, float z = 0) =>
        new() { Id = id, Kind = kind, X = x, Z = z, Stock = 3, ForSale = true, Price = Catalog.Get(kind).Price };
    public static StoreState Shop(params StoreItem[] items) => new() { Items = [.. items], Cash = 9000 };
    public static StoreState Enclosed()
    {
        var shop = Shop();
        for (var x = -6; x < 6; x++) foreach (var z in new[] { -5, 5 }) shop.Walls.Add(new() { X = x, Z = z, AlongX = true });
        for (var z = -5; z < 5; z++) foreach (var x in new[] { -6, 6 }) shop.Walls.Add(new() { X = x, Z = z });
        return shop;
    }
}

public class PlacementTests
{
    [Fact]
    public void FloorPurchaseCannotOverlapAnExistingFixture()
    {
        var shop = Fixtures.Shop(Fixtures.Item("table"));
        Assert.False(StoreLayout.CanPlace(shop, Fixtures.Item("chair", 2)));
        Assert.True(StoreLayout.CanPlace(shop, Fixtures.Item("chair", 2, 3, 0)));
    }
    [Theory]
    [InlineData(5.8f, 0)] [InlineData(-5.8f, 0)] [InlineData(0, 4.9f)] [InlineData(0, -4.9f)]
    public void EntireObjectMustFitInsideLot(float x, float z) =>
        Assert.False(StoreLayout.CanPlace(Fixtures.Shop(), Fixtures.Item("table", x: x, z: z)));
    [Fact]
    public void RotationChangesClearance()
    {
        var item = Fixtures.Item("table", x: 5.2f);
        Assert.False(StoreLayout.CanPlace(Fixtures.Shop(), item));
        item.Rotation = 90;
        Assert.True(StoreLayout.CanPlace(Fixtures.Shop(), item));
    }
    [Fact]
    public void RugAllowsFurnitureOnTopInEitherPlacementOrder()
    {
        Assert.True(StoreLayout.CanPlace(Fixtures.Shop(Fixtures.Item("rug")), Fixtures.Item("chair", 2)));
        Assert.True(StoreLayout.CanPlace(Fixtures.Shop(Fixtures.Item("chair")), Fixtures.Item("rug", 2)));
    }
    [Fact]
    public void DisplayObjectsNeedSupportHeightAndClearSpace()
    {
        var table = Fixtures.Item("table"); var shop = Fixtures.Shop(table);
        var vase = Fixtures.Item(id: 2); vase.ParentId = table.Id; vase.Y = .8f;
        Assert.True(StoreLayout.CanPlace(shop, vase));
        shop.Items.Add(vase);
        var bowl = Fixtures.Item("bowl", 3); bowl.ParentId = table.Id; bowl.Y = .8f;
        Assert.False(StoreLayout.CanPlace(shop, bowl));
        bowl.X = .65f; Assert.True(StoreLayout.CanPlace(shop, bowl));
        bowl.X = .95f; Assert.False(StoreLayout.CanPlace(shop, bowl));
        bowl.X = .65f; bowl.Y = 1; Assert.False(StoreLayout.CanPlace(shop, bowl));
        bowl.ParentId = 99; Assert.False(StoreLayout.CanPlace(shop, bowl));
    }
    [Fact]
    public void MovingDisplayCarriesAndRotatesContents()
    {
        var table = Fixtures.Item("table"); var vase = Fixtures.Item(id: 2, x: .5f);
        vase.ParentId = table.Id; vase.Y = .8f;
        var shop = Fixtures.Shop(table, vase); var destination = table.Copy();
        destination.X = 2; destination.Z = 1; destination.Rotation = 90;
        StoreLayout.MoveItem(shop, table, destination);
        Assert.InRange(vase.Position.DistanceTo(new(2, .8f, .5f)), 0, .0001f);
        Assert.Equal(90, vase.Rotation); Assert.Equal(table.Id, vase.ParentId);
        Assert.True(StoreLayout.CanPlace(shop, table.Copy(), table.Id));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void ObjectsCannotStraddleWallsOrDoorways(bool alongX)
    {
        var shop = Fixtures.Shop(); shop.Walls.Add(new() { X = 0, Z = 0, AlongX = alongX, Kind = "door" });
        Assert.False(StoreLayout.CanPlace(shop, Fixtures.Item(x: .1f, z: .1f)));
    }
    [Fact]
    public void NewObjectsRespectTheSaveCapacityButExistingObjectsCanMove()
    {
        var shop = Fixtures.Shop();
        for (var id = 1; id <= 600; id++) shop.Items.Add(Fixtures.Item("rug", id));
        Assert.False(StoreLayout.CanPlace(shop, Fixtures.Item("chair", 601, 3, 3)));
        Assert.True(StoreLayout.CanPlace(shop, Fixtures.Item("chair", 1, 3, 3), 1));
    }
    [Theory]
    [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)]
    public void NonFinitePlacementsAreRejected(float x) =>
        Assert.False(StoreLayout.CanPlace(Fixtures.Shop(), Fixtures.Item(x: x)));
}

public class TradingTests
{
    [Fact]
    public void ReservationsDoNotChargeUntilCheckoutAndHonorAgreedPrice()
    {
        var item = Fixtures.Item(); var state = Fixtures.Shop(item); var trade = new StoreTrading(state);
        Assert.True(trade.TryReserve(1, item));
        Assert.Equal(9000m, state.Cash); Assert.Equal(3, item.Stock); Assert.Equal(1, trade.Reserved(item));
        item.Price = 99;
        Assert.Equal(38m, trade.Checkout(1)!.Price);
        Assert.Equal(9038m, state.Cash); Assert.Equal(2, item.Stock);
        Assert.Equal(38m, state.Revenue); Assert.Equal(18m, state.CostOfSales);
        Assert.Equal(38m, state.DayRevenue); Assert.Equal(18m, state.DayCost);
        Assert.Equal(1, state.Sales); Assert.Equal(1, state.DaySales);
        Assert.Null(trade.Checkout(1)); Assert.Equal(9038m, state.Cash);
    }
    [Fact]
    public void LastUnitCannotBeReservedTwiceAndCanceledStockBecomesAvailable()
    {
        var item = Fixtures.Item(); item.Stock = 1;
        var state = Fixtures.Shop(item); var trade = new StoreTrading(state);
        Assert.True(trade.TryReserve(1, item)); Assert.False(trade.TryReserve(2, item));
        Assert.False(trade.TryReserve(1, item));
        trade.Cancel(1); trade.Cancel(1);
        Assert.Equal(0, trade.Reserved(item)); Assert.Equal(9000m, state.Cash);
        Assert.True(trade.TryReserve(2, item)); Assert.NotNull(trade.Checkout(2));
        Assert.False(trade.TryReserve(3, item)); Assert.Equal(0, item.Stock);
    }
    [Theory]
    [InlineData("vase")] [InlineData("chair")] [InlineData("table")] [InlineData("rug")]
    public void AnySupportedCatalogObjectCanBeSold(string kind)
    {
        var item = Fixtures.Item(kind); var state = Fixtures.Shop(item); var trade = new StoreTrading(state);
        Assert.True(trade.TryReserve(1, item)); Assert.NotNull(trade.Checkout(1));
        Assert.Equal(9000 + item.Price, state.Cash); Assert.Equal(item.Product.Cost, state.CostOfSales);
    }
    [Fact]
    public void FixturesAndObjectsOutsideThisStoreCannotBeReserved()
    {
        var item = Fixtures.Item(); var trade = new StoreTrading(Fixtures.Shop(item));
        item.ForSale = false; Assert.False(trade.TryReserve(1, item));
        Assert.False(trade.TryReserve(1, Fixtures.Item(id: 2)));
    }
    [Fact]
    public void InvalidatedReservationCannotChargeMoney()
    {
        var item = Fixtures.Item(); var state = Fixtures.Shop(item); var trade = new StoreTrading(state);
        trade.TryReserve(1, item); state.Items.Remove(item);
        Assert.Null(trade.Checkout(1)); Assert.Equal(9000m, state.Cash); Assert.Equal(0, trade.Reserved(item));
    }
}

public class NavigationTests
{
    [Fact]
    public void ExteriorNeedsAnOpeningAndSupportsMovingTheEntrance()
    {
        var state = Fixtures.Enclosed(); var nav = new ShopNavigation(); nav.Rebuild(state); Assert.False(nav.HasEntrance);
        var doorway = state.Walls.Single(w => !w.AlongX && w.X == -6 && w.Z == 2); doorway.Kind = "door";
        nav.Rebuild(state); Assert.True(nav.HasEntrance); Assert.True(nav.Entrance.X < -5);
        doorway.Kind = "wall"; state.Walls.Remove(doorway);
        nav.Rebuild(state); Assert.True(nav.HasEntrance);
    }
    [Fact]
    public void FurnitureCanBlockAnOtherwiseOpenEntrance()
    {
        var state = Fixtures.Enclosed(); state.Walls.Single(w => w.AlongX && w.Z == 5 && w.X == 0).Kind = "door";
        state.Items.Add(Fixtures.Item("table", x: .5f, z: 4.65f));
        var nav = new ShopNavigation(); nav.Rebuild(state); Assert.False(nav.HasEntrance);
    }
    [Fact]
    public void RebuildInvalidatesCachedReachabilityWhenPartitionChanges()
    {
        var state = Fixtures.Shop(); var nav = new ShopNavigation(); var a = new Vector3(0, 0, -2); var b = new Vector3(0, 0, 2);
        nav.Rebuild(state); Assert.NotNull(nav.Path(a, b));
        for (var x = -6; x < 6; x++) state.Walls.Add(new() { X = x, Z = 0, AlongX = true });
        nav.Rebuild(state); Assert.Null(nav.Path(a, b)); Assert.False(nav.CanReach(a, b));
        state.Walls[6].Kind = "door"; nav.Rebuild(state); Assert.NotNull(nav.Path(a, b));
    }
    [Fact]
    public void PathsAvoidFurnitureAndOnlyStepBetweenAdjacentCells()
    {
        var state = Fixtures.Shop(Fixtures.Item("table")); var nav = new ShopNavigation(); nav.Rebuild(state);
        var start = new Vector3(0, 0, -3); var path = nav.Path(start, new(0, 0, 3)); Assert.NotNull(path);
        var previous = ShopNavigation.Point(ShopNavigation.Cell(start));
        foreach (var point in path)
        {
            Assert.InRange(previous.DistanceTo(point), .4999f, .5001f);
            Assert.True(Math.Abs(point.X) >= 1.16f || Math.Abs(point.Z) >= .66f);
            previous = point;
        }
        Assert.Null(nav.Path(start, Vector3.Zero));
    }
    [Fact]
    public void CachedApproachesChooseReachableSidesWithoutChangingReturnedPaths()
    {
        var table = Fixtures.Item("table"); var nav = new ShopNavigation(); nav.Rebuild(Fixtures.Shop(table));
        var from = new Vector3(0, 0, 4); var before = nav.Path(from, new(4, 0, 4))!;
        var approach = nav.Approach(table, from); Assert.NotNull(approach); Assert.True(approach.Value.Z > .5f);
        nav.Path(new(-4, 0, -4), new(4, 0, -4));
        Assert.Equal(before, nav.Path(from, new(4, 0, 4))!);
    }
    [Theory]
    [InlineData(6, 0)] [InlineData(0, 5)] [InlineData(-7, 0)] [InlineData(float.NaN, 0)]
    public void PathsDoNotClampInvalidDestinationsIntoTheStore(float x, float z)
    { var nav = new ShopNavigation(); nav.Rebuild(Fixtures.Shop()); Assert.Null(nav.Path(Vector3.Zero, new(x, 0, z))); }
}

public class SaveAndHistoryTests
{
    [Fact]
    public void SaveRoundTripPreservesDesignStockAndFinancialDecimals()
    {
        var item = Fixtures.Item(); item.Price = 38.75m; item.Color = 3; item.Rotation = 90;
        var state = Fixtures.Shop(item); state.NextId = 2; state.Cash = 1234.56m; state.StoreName = "Navin's Atelier"; state.Floors[19] = 2;
        state.Walls.Add(new() { X = -1, Z = 5, AlongX = true, Kind = "door", Color = 2 });
        var restored = StoreState.Parse(state.Serialize());
        Assert.Equal(state.Serialize(), restored.Serialize());
        Assert.Equal(38.75m, restored.Items[0].Price); Assert.Equal(1234.56m, restored.Cash);
        Assert.Equal(2, restored.NextId); Assert.DoesNotContain("Footprint", state.Serialize());
    }
    [Fact]
    public void LoadingRepairsAnOutdatedNextObjectId()
    { var restored = StoreState.Parse(Fixtures.Shop(Fixtures.Item(id: 42)).Serialize()); Assert.Equal(43, restored.NextId); }
    [Theory]
    [InlineData("null")] [InlineData("{\"Schema\":2}")] [InlineData("{\"Cash\":-1}")]
    [InlineData("{\"Floors\":[]}")] [InlineData("{\"StoreName\":\"\"}")]
    [InlineData("{\"Floors\":null}")]
    public void CorruptOrUnsupportedSaveDataIsRejected(string json) => Assert.Throws<InvalidDataException>(() => StoreState.Parse(json));
    [Theory]
    [InlineData("unknown")] [InlineData("duplicate")] [InlineData("rotation")] [InlineData("stock")]
    [InlineData("missing-support")] [InlineData("floating")] [InlineData("overflow-id")]
    public void InvalidObjectsCannotEnterAStore(string invalid)
    {
        var item = Fixtures.Item(); var state = Fixtures.Shop(item);
        switch (invalid)
        {
            case "unknown": item.Kind = "no-such-object"; break;
            case "duplicate": state.Items.Add(item.Copy()); break;
            case "rotation": item.Rotation = 45; break;
            case "stock": item.Stock = -1; break;
            case "missing-support": item.ParentId = 99; item.Y = .8f; break;
            case "floating": item.Y = .8f; break;
            case "overflow-id": item.Id = int.MaxValue; break;
        }
        Assert.Throws<InvalidDataException>(() => StoreState.Parse(state.Serialize()));
    }
    [Fact]
    public void DuplicateWallsAndIncorrectSupportHeightAreRejected()
    {
        var state = Fixtures.Shop(); var wall = new StoreWall(); state.Walls.Add(wall); state.Walls.Add(wall);
        Assert.Throws<InvalidDataException>(() => StoreState.Parse(state.Serialize()));
        state.Walls.Clear(); var table = Fixtures.Item("table"); var vase = Fixtures.Item(id: 2);
        vase.ParentId = 1; vase.Y = 1.14f; state.Items.AddRange([table, vase]);
        Assert.Throws<InvalidDataException>(() => StoreState.Parse(state.Serialize()));
    }
    [Fact]
    public void HistoryIsBoundedAndNewEditsClearRedo()
    {
        var state = Fixtures.Shop(Fixtures.Item()); var history = new StoreHistory(2);
        for (var n = 0; n < 3; n++) { history.Record(state); state.Cash += 10; }
        state = history.Undo(state); Assert.Equal(9020m, state.Cash);
        state = history.Undo(state); Assert.Equal(9010m, state.Cash); Assert.False(history.CanUndo);
        state = history.Redo(state); Assert.Equal(9020m, state.Cash);
        history.Record(state); state.Items[0].Stock = 8; Assert.False(history.CanRedo);
        state = history.Undo(state); Assert.Equal(3, state.Items[0].Stock);
        history.Clear(); Assert.False(history.CanRedo); Assert.False(history.CanUndo);
    }
    [Fact]
    public void CorruptPrimaryFallsBackAndDoesNotOverwriteTheGoodBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "booming-store-" + Guid.NewGuid()); var path = Path.Combine(directory, "store.json");
        try
        {
            var state = Fixtures.Shop(); StorePersistence.Save(path, state);
            state.Cash = 8500; StorePersistence.Save(path, state);
            File.WriteAllText(path, "corrupted");
            var result = StorePersistence.Load(path); Assert.True(result.Recovered); Assert.Equal(9000m, result.State!.Cash);
            StorePersistence.Save(path, result.State);
            Assert.Equal(9000m, StoreState.Parse(File.ReadAllText(path + ".bak")).Cash);
            Assert.False(StorePersistence.Load(path).Recovered); Assert.False(File.Exists(path + ".tmp"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public void InvalidStateCannotReplaceTheCurrentSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "booming-store-" + Guid.NewGuid()); var path = Path.Combine(directory, "store.json");
        try
        {
            var state = Fixtures.Shop(); StorePersistence.Save(path, state); state.Cash = -1;
            Assert.Throws<InvalidDataException>(() => StorePersistence.Save(path, state));
            Assert.Equal(9000m, StorePersistence.Load(path).State!.Cash);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
