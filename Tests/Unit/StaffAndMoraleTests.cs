// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Xunit;
namespace BoomingStore.Tests;
public class StaffAndMoraleTests
{
    [Fact]
    public void WaitingUsesUnscaledQueueTimeAndOnlyWaitingChangesMood()
    {
        var morale = new CustomerMorale(); morale.Advance(1000, false);
        Assert.Equal(CustomerMood.Happy, morale.Mood);
        morale.Advance(60, true); Assert.Equal(1, morale.Value);
        morale.Advance(15, true); Assert.Equal(CustomerMood.Neutral, morale.Mood);
        morale.Advance(6, true); Assert.Equal(CustomerMood.Disappointed, morale.Mood);
        morale.Advance(double.NaN, true); morale.Advance(-9, true); Assert.Equal(81, morale.WaitSeconds);
    }
    [Fact]
    public void ReplacementAndMultipleCartsSettleIndependentlyExactlyOnce()
    {
        var item = Fixtures.Item(); var state = Fixtures.Shop(item); var trade = new StoreTrading(state);
        Assert.True(trade.TryReserve(1, item)); Assert.Equal(0, item.Stock);
        item.RestockNew(); Assert.True(trade.TryReserve(2, item)); item.RestockNew();
        Assert.NotNull(trade.Checkout(1)); Assert.Equal(1, item.Stock);
        Assert.NotNull(trade.Checkout(2)); Assert.Null(trade.Checkout(2)); Assert.Equal(1, item.Stock);
        Assert.Equal(76, state.Revenue); Assert.Equal(36, state.CostOfSales);
    }
    [Fact]
    public void DisappointedCartReturnsAtCostWithoutOverwritingANewReplacement()
    {
        var item = Fixtures.Item(); var state = Fixtures.Shop(item); var trade = new StoreTrading(state);
        trade.TryReserve(1, item); item.RestockNew(); trade.Cancel(1, true);
        Assert.Empty(trade.ProcessReturns()); Assert.Single(state.Returns); Assert.Equal(38, item.Price);
        trade.TryReserve(2, item); Assert.Single(trade.ProcessReturns()); Assert.True(item.Used); Assert.Equal(18, item.Price);
        trade.TryReserve(3, item); Assert.Equal(18, trade.Checkout(3)!.Price); Assert.Equal(state.Revenue, state.CostOfSales);
        item.RestockNew(); Assert.False(item.Used); Assert.Equal(38, item.Price);
        Assert.Equal(38, trade.Checkout(2)!.Price);
    }
    [Fact]
    public void SavingReturnsUnpaidUnitsWithoutTouchingLiveCartsOrLosingPaidDeliveries()
    {
        var item = Fixtures.Item(); var state = Fixtures.Shop(item); var trade = new StoreTrading(state);
        trade.TryReserve(1, item); state.Employees.Add(new() { Target = item.Id, Collected = true });
        var snapshot = trade.SettledSnapshot(); Assert.Single(snapshot.Returns); Assert.True(snapshot.Employees[0].Collected);
        Assert.Empty(state.Returns); Assert.Equal(0, item.Stock); Assert.NotNull(trade.Checkout(1));
        snapshot.Employees.Clear(); var restored = new StoreTrading(snapshot); Assert.Single(restored.ProcessReturns());
        Assert.Equal(1, snapshot.Items[0].Stock); Assert.Equal(9000, snapshot.Cash);
    }
    [Fact]
    public void EmployeesAndUsedReturnsRoundTripAndRejectDuplicateClaims()
    {
        var item = Fixtures.Item(); item.Stock = 0; var state = Fixtures.Shop(item);
        state.Employees.Add(new() { Role = EmployeeRole.Restocking, Target = 1, Collected = true });
        state.Returns.Add(new() { ItemId = 1, Used = true });
        var loaded = StoreState.Parse(state.Serialize()); Assert.True(loaded.Employees[0].Collected); Assert.True(loaded.Returns[0].Used);
        state.RestockQueue.Add(1); Assert.Throws<InvalidDataException>(() => StoreState.Parse(state.Serialize()));
    }
}
