// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Debug-only rendered review, using a real sale and the production restocking controller.</summary>
public static class OwnerPreview
{
    public static async Task Run(StoreGame game)
    {
        var folder = ProjectSettings.GlobalizePath("res://.tools/owner-review"); Directory.CreateDirectory(folder);
        async Task Frame() => await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Capture(string name)
        {
            await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, name + ".png"));
        }
        GD.Seed(42); game.SetSpeed(3); game.Keeper.MoveTo(new(-1.4f, 0, 2.5f));
        for (var i = 0; i < 400 && game.Keeper.Actor.IsWalking; i++) await Frame();
        game.SetSpeed(1); game.Camera.ZoomTo(game.Keeper.Actor.Position);
        for (var i = 0; i < 60; i++) await Frame();
        await Capture("relaxed-owner");
        game.Keeper.Actor.React(PersonBeat.Greeting, game.Camera.Position, VoiceLine.Greeting);
        for (var i = 0; i < 24; i++) await Frame();
        await Capture("friendly-owner");
        game.ToggleOpen(); game.Keeper.ServeCheckout(); game.SetSpeed(3);
        for (var i = 0; i < 2400 && game.State.Sales == 0; i++) await Frame();
        var target = game.State.Items.FirstOrDefault(i => i.ForSale && i.Stock == 0);
        if (target == null) { GD.PushError("Owner preview timed out waiting for a real sale."); await game.QuitCleanly(1); return; }
        game.Keeper.Request(target); var frame = 0; var carryingFrame = -1; var placingFrames = 0;
        for (; frame < 1800 && game.Keeper.Busy; frame++)
        {
            await Frame(); game.Camera.ZoomTo(game.Keeper.Actor.Position);
            if (game.State.CarriedItemId != 0 && carryingFrame < 0)
            { carryingFrame = frame; game.SetSpeed(1); GD.Print($"CARRY_START_FRAME={Engine.GetProcessFrames()}"); }
            if (carryingFrame >= 0 && frame - carryingFrame == 30) await Capture("carrying-one-item");
            if (carryingFrame >= 0 && frame - carryingFrame == 150) game.SetSpeed(3);
            if (game.Keeper.Status == "Placing one item" && ++placingFrames == 10) await Capture("placing-one-item");
        }
        game.SetSpeed(1); game.Camera.Home(); await Frame(); await Capture("store-overview");
        GD.Print($"OWNER_PREVIEW stock={target.Stock} frames={frame} carrying={carryingFrame} status={game.Status}");
        if (!OS.GetCmdlineUserArgs().Contains("--keep-review-open")) await game.QuitCleanly(target.Stock == 1 ? 0 : 1);
    }
}
