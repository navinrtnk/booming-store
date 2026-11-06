// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
using Xunit;

namespace BoomingStore.Tests;

public class ShopperMotionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ArmsCounterSwingAgainstTheLeadingLeg(bool right)
    {
        var distance = right ? ShopperMotion.StrideLength / 2 : 0;
        Assert.True(ShopperMotion.Foot(distance, right).Forward > 0);
        Assert.True(ShopperMotion.ArmAngle(distance, right, false, 1) > 0, "The arm beside the leading leg should swing back.");
        Assert.True(ShopperMotion.ArmAngle(distance, !right, false, 1) < 0);
    }
    [Fact]
    public void ArmSwingStaysRelaxedAndTheBagArmIsQuieter()
    {
        for (var n = 0; n < 200; n++)
        {
            var distance = n / 200f * ShopperMotion.StrideLength;
            Assert.InRange(Math.Abs(ShopperMotion.ArmAngle(distance, true, false, 1)), 0, .101f);
            Assert.InRange(Math.Abs(ShopperMotion.ArmAngle(distance, false, true, 1)), 0, .046f);
            Assert.Equal(0, ShopperMotion.ArmAngle(distance, false, true, 0));
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StanceFeetStayPlantedAsTheBodyMovesForward(bool right)
    {
        var start = right ? ShopperMotion.StrideLength * .5f : 0;
        var worldFoot = start + ShopperMotion.Foot(start, right).Forward;
        for (var n = 1; n <= 50; n++)
        {
            var distance = start + n / 50f * ShopperMotion.StrideLength * .60f;
            var foot = ShopperMotion.Foot(distance, right);
            Assert.Equal(0, foot.Lift); Assert.Equal(0, foot.Pitch);
            Assert.Equal(worldFoot, distance + foot.Forward, .00001f);
        }
    }
    [Fact]
    public void SwingHasLowClearanceAndReturnsSmoothlyToGround()
    {
        for (var n = 0; n <= 1000; n++)
        {
            var foot = ShopperMotion.Foot(ShopperMotion.StrideLength * n / 1000f, false);
            Assert.InRange(foot.Lift, 0, .046f); Assert.InRange(Math.Abs(foot.Pitch), 0, .081f);
        }
        var takeoff = ShopperMotion.Foot(ShopperMotion.StrideLength * ShopperMotion.StanceFraction, false);
        var landing = ShopperMotion.Foot(ShopperMotion.StrideLength, false);
        Assert.InRange(takeoff.Lift, 0, .00001f); Assert.Equal(0, landing.Lift);
        Assert.Equal(-landing.Forward, takeoff.Forward, .00001f);
    }
    [Theory]
    [InlineData(0f)] [InlineData(.1f)] [InlineData(.3f)] [InlineData(.7f)]
    public void LegsAlternateHalfAStrideApart(float distance) =>
        Assert.Equal(ShopperMotion.Foot(distance, true).Forward,
            ShopperMotion.Foot(distance + ShopperMotion.StrideLength / 2, false).Forward, .00001f);
    [Theory]
    [InlineData(.8f)] [InlineData(.9f)] [InlineData(1f)] [InlineData(1.1f)]
    public void LegSolverKeepsAnklesOnTrajectoryAndKneesForward(float scale)
    {
        var hip = new Vector3(.11f, 1.005f, -.004f) * scale;
        var knee = new Vector3(.153f, .579f, .031f) * scale;
        var ankle = new Vector3(.196f, .144f, .019f) * scale;
        for (var sample = 0; sample < 200; sample++)
        {
            var distance = sample / 200f * ShopperMotion.StrideLength * scale;
            var foot = ShopperMotion.Foot(distance, false, scale); var body = ShopperMotion.BodyOffset(distance, 1, scale);
            var target = ankle + new Vector3(.105f * scale - ankle.X, foot.Lift, foot.Forward) - body;
            var pose = ShopperMotion.SolveLeg(hip, knee, ankle, target, foot.Pitch);
            var posedKnee = hip + pose.Hip * (knee - hip);
            var actual = posedKnee + (pose.Hip * pose.Knee) * (ankle - knee);
            Assert.InRange(actual.DistanceTo(target), 0, .002f);
            var line = hip.Lerp(actual, (knee - hip).Length() / ((knee - hip).Length() + (ankle - knee).Length()));
            Assert.True(posedKnee.Z > line.Z, "The knee bent backward.");
            if (sample == 50)
                Assert.True((actual - posedKnee).Normalized().Dot((posedKnee - hip).Normalized()) > Mathf.Cos(Mathf.DegToRad(25)),
                    "The stance leg should straighten at mid-stride instead of staying crouched.");
            var actualFoot = pose.Hip * pose.Knee * pose.Ankle;
            Assert.InRange((actualFoot * Vector3.Up).DistanceTo(new Quaternion(Vector3.Right, foot.Pitch) * Vector3.Up), 0, .0001f);
        }
    }
    [Fact]
    public void IdleBodyHasNoWalkBobAndWalkingBobStaysSmall()
    {
        Assert.Equal(Vector3.Zero, ShopperMotion.BodyOffset(.2f, 0));
        for (var n = 0; n < 100; n++)
        {
            var offset = ShopperMotion.BodyOffset(n / 100f, 1);
            Assert.InRange(offset.Y, -.051f, -.009f); Assert.InRange(Math.Abs(offset.X), 0, .0051f);
        }
    }
    [Fact]
    public void RoundedCornersStayInsideTheirClearCellAndRetainTheDestination()
    {
        var start = new Vector3(-.75f, 0, -.75f);
        Vector3[] path = [new(-.25f, 0, -.75f), new(.25f, 0, -.75f), new(.25f, 0, -.25f), new(.25f, 0, .25f)];
        var rounded = ShopperMotion.RoundCorners(start, path);
        Assert.Equal(path[^1], rounded[^1]);
        Assert.Contains(rounded, p => p.X > .13f && p.X < .25f && p.Z > -.75f && p.Z < -.63f);
        Assert.All(rounded.Where(p => p.X > .13f && p.Z < -.5f), p => Assert.Equal(ShopNavigation.Cell(path[1]), ShopNavigation.Cell(p)));
    }
    [Theory]
    [InlineData(0f)] [InlineData(1f)]
    public void ArrivalsAreDelayedIrregularAndLimitedToThree(float random)
    {
        var arrivals = new CustomerArrivals(() => random); arrivals.Reset();
        Assert.False(arrivals.Tick(11, 0, true));
        Assert.True(arrivals.Tick(7, 0, true));
        Assert.False(arrivals.Tick(23, 1, true));
        Assert.True(arrivals.Tick(17, 1, true));
        Assert.False(arrivals.Tick(100, CustomerArrivals.Capacity, true));
        Assert.False(arrivals.Tick(3, 2, true));
        Assert.True(arrivals.Tick(1, 2, true));
    }
    [Fact]
    public void BlockedEntranceAndPauseCannotAccumulateAnArrivalBurst()
    {
        var arrivals = new CustomerArrivals(() => 0); arrivals.Reset();
        Assert.False(arrivals.Tick(100, 0, false));
        Assert.False(arrivals.Tick(0, 0, true));
        Assert.False(arrivals.Tick(3, 0, true)); Assert.True(arrivals.Tick(1, 0, true));
        Assert.False(arrivals.Tick(0, 0, true)); Assert.False(arrivals.Tick(23, 0, true));
    }
    [Fact]
    public void LocalPassingCannotCrossAWall()
    {
        var state = Fixtures.Shop(); state.Walls.Add(new() { X = 0, Z = 0, AlongX = true });
        var navigation = new ShopNavigation(); navigation.Rebuild(state);
        Assert.False(navigation.CanStep(new(.25f, 0, -.1f), new(.25f, 0, .1f)));
        Assert.True(navigation.CanStep(new(.25f, 0, -.1f), new(.4f, 0, -.1f)));
    }
}
