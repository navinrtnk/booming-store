// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
using Xunit;

namespace BoomingStore.Tests;

public class NaturalRouteTests
{
    [Theory]
    [InlineData(3.1f, 2.4f)] [InlineData(-3.3f, 2.8f)] [InlineData(2.7f, -3.2f)]
    public void OpenFloorUsesOneDirectSegmentAndKeepsTheExactDestination(float x, float z)
    {
        var nav = new ShopNavigation(); nav.Rebuild(Fixtures.Shop());
        var target = new Vector3(x, 0, z); var route = nav.Path(new(.13f, 0, .37f), target);
        Assert.NotNull(route); Assert.Single(route); Assert.Equal(target, route[0]);
    }

    [Fact]
    public void DiagonalCannotClipFurnitureEvenWhenItsEndpointsAreClear()
    {
        var nav = new ShopNavigation(); nav.Rebuild(Fixtures.Shop(Fixtures.Item("chair")));
        var from = new Vector3(.7f, 0, .4f); var to = new Vector3(.4f, 0, .7f);
        Assert.True(nav.CanStand(from)); Assert.True(nav.CanStand(to));
        Assert.False(nav.CanStep(from, to));
        var route = nav.Path(from, to); Assert.NotNull(route);
        Assert.True(route.Sum(p => p.LengthSquared()) > 0); Assert.True(route.Count > 1);
        AssertClear(nav, from, route);
    }

    [Fact]
    public void APartitionMustBeCrossedThroughItsDoorWithShoulderClearance()
    {
        var state = Fixtures.Enclosed();
        for (var x = -6; x < 6; x++) state.Walls.Add(new() { X = x, Z = 0, AlongX = true, Kind = x == 0 ? "door" : "wall" });
        var nav = new ShopNavigation(); nav.Rebuild(state);
        var from = new Vector3(-2, 0, -2); var to = new Vector3(2, 0, 2);
        Assert.False(nav.IsSegmentClear(from, to));
        Assert.True(nav.IsSegmentClear(new(.5f, 0, -.5f), new(.5f, 0, .5f)));
        Assert.False(nav.IsSegmentClear(new(.1f, 0, -.5f), new(.1f, 0, .5f)));
        var route = nav.Path(from, to); Assert.NotNull(route); AssertClear(nav, from, route);
        state.Walls.Single(w => w.AlongX && w.X == 0 && w.Z == 0).Kind = "wall"; nav.Rebuild(state);
        Assert.False(nav.CanReach(from, to)); Assert.Null(nav.Path(from, to));
    }

    [Fact]
    public void ValidPositionsBesideAnObstacleDoNotHaveToSitAtGridCenters()
    {
        var nav = new ShopNavigation(); nav.Rebuild(Fixtures.Shop(Fixtures.Item("table", x: .1f)));
        var start = new Vector3(1.31f, 0, 0); var target = new Vector3(-3, 0, 0);
        Assert.True(nav.CanStand(start)); Assert.False(nav.CanStand(ShopNavigation.Point(ShopNavigation.Cell(start))));
        var route = nav.Path(start, target); Assert.NotNull(route); AssertClear(nav, start, route);
        Assert.Equal(target, route[^1]);
    }

    [Fact]
    public void SmoothingOnlyRoundsCornersWhenTheEntireCurveIsClear()
    {
        var nav = new ShopNavigation(); nav.Rebuild(Fixtures.Shop(Fixtures.Item("table")));
        var from = new Vector3(0, 0, -3); var target = new Vector3(0, 0, 3);
        var path = nav.Path(from, target)!;
        var curve = ShopperMotion.RoundCorners(from, path, nav.IsSegmentClear);
        Assert.Equal(target, curve[^1]); AssertClear(nav, from, curve);
        foreach (var end in curve)
        {
            for (var t = 0f; t <= 1; t += .02f)
            {
                var p = from.Lerp(end, t);
                Assert.True(Math.Abs(p.X) >= 1.199f || Math.Abs(p.Z) >= .699f, "The person's footprint crossed the table.");
            }
            from = end;
        }
    }

    [Fact]
    public void ShorterClearSideIsChosenRatherThanTheNearestBlockedSide()
    {
        var item = Fixtures.Item("table", x: 1, z: 1); var state = Fixtures.Shop(item);
        // The lower side is geometrically close, but a partition makes reaching it a long detour.
        for (var x = -3; x <= 2; x++) state.Walls.Add(new() { X = x, Z = 0, AlongX = true });
        var nav = new ShopNavigation(); nav.Rebuild(state); var start = new Vector3(3.4f, 0, -.5f);
        var approach = nav.Approach(item, start);
        Assert.NotNull(approach); Assert.True(approach.Value.X > 2);
        AssertClear(nav, start, nav.Path(start, approach.Value)!);
    }

    private static void AssertClear(ShopNavigation nav, Vector3 from, IEnumerable<Vector3> path)
    {
        foreach (var point in path) { Assert.True(nav.IsSegmentClear(from, point)); from = point; }
    }

    [Fact]
    public void AShallowTurnCannotBeDroppedWhenItProtectsAnObstacleCorner()
    {
        var nav = new ShopNavigation(); nav.Rebuild(Fixtures.Shop(Fixtures.Item("chair")));
        var from = new Vector3(-2, 0, .55f);
        Vector3[] path = [new(-.7f, 0, .635f), new(2, 0, .65f)];
        Assert.False(nav.IsSegmentClear(from, path[^1])); AssertClear(nav, from, path);
        AssertClear(nav, from, ShopperMotion.RoundCorners(from, path, nav.IsSegmentClear));
    }
}
