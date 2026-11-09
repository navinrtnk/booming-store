// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Xunit;
namespace BoomingStore.Tests;
public class ElectronicsTests
{
    [Theory]
    [InlineData(0, 0)] [InlineData(7, 0)] [InlineData(8, 25)] [InlineData(22, 25)] [InlineData(23, 50)] [InlineData(60, 100)]
    public void PayrollRoundsOnlyWorkedShiftToNearestQuarter(int minutes, int cost)
    {
        var state = new StoreState(); var employee = new EmployeeData();
        Payroll.Advance(state, employee, minutes / 60d); Payroll.EndShift(state, employee); Payroll.EndShift(state, employee);
        Assert.Equal((decimal)cost, state.WageExpense); Assert.Equal(10000 - cost, state.Cash);
        Payroll.Advance(state, employee, 8); Assert.Equal((decimal)cost, state.WageExpense);
    }
    [Fact]
    public void TrafficRespondsToDayAndRecentMoodOutcomes()
    {
        Assert.True(StoreClock.Traffic(18, .5) > StoreClock.Traffic(9, .5));
        Assert.True(StoreClock.Traffic(2, .5) < StoreClock.Traffic(18, .5) / 5);
        var state = new StoreState(); for (var i = 0; i < 30; i++) StoreClock.RecordDeparture(state, CustomerMood.Disappointed);
        Assert.Equal(20, state.Departures.Count); Assert.Equal(0, StoreClock.Reputation(state));
    }
    [Fact]
    public void ShelvesSupportMultipleLevelsAndRejectTallLowerProducts()
    {
        var rack = Catalog.Get("tv_rack"); Assert.True(rack.FitsLevel(.28f, Catalog.Get("tv43").Height));
        Assert.False(rack.FitsLevel(.28f, Catalog.Get("tv75").Height)); Assert.True(rack.FitsLevel(1.28f, Catalog.Get("tv75").Height));
    }
    [Fact]
    public void MultiItemBasketPaysEveryAgreedPriceOnce()
    {
        var first = Fixtures.Item("tv32"); var second = Fixtures.Item("speaker", 2);
        var state = Fixtures.Shop(first, second); var trading = new StoreTrading(state);
        Assert.True(trading.TryReserve(7, first)); Assert.True(trading.TryReserve(7, second)); second.Price = 999;
        var snapshot = trading.SettledSnapshot(); Assert.All(snapshot.Items, i => Assert.Equal(1, i.Stock));
        Assert.Equal(338, trading.Checkout(7)!.Price); Assert.Null(trading.Checkout(7)); Assert.Equal(2, state.Sales); Assert.Equal(1, state.Transactions);
    }
}
