// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Debug-only camera review of real shoppers; optionally record with Godot's --write-movie.</summary>
public static class WalkingPreview
{
    public static async Task Run(StoreGame game)
    {
        game.FurnishReviewStore();
        GD.Seed(31); game.ToggleOpen(); game.SetSpeed(3);
        var folder = ProjectSettings.GlobalizePath("res://.tools/walk-review"); Directory.CreateDirectory(folder);
        var walkingFrames = 0; var saved = 0; var startFrame = -1; var movieFrame = 0;
        var captureRoute = OS.GetCmdlineUserArgs().Contains("--capture-route");
        var movieFolder = ProjectSettings.GlobalizePath("res://.tools/route-review");
        if (captureRoute) Directory.CreateDirectory(movieFolder);
        for (var frame = 0; frame < 900; frame++)
        {
            await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
            var shopper = game.Shoppers.FirstOrDefault(s => !s.Finished);
            if (shopper == null) continue;
            if (startFrame < 0 && shopper.State == Shopper.Activity.Walking)
            { startFrame = frame; game.SetSpeed(1); GD.Print($"WALK_START_FRAME={frame}"); }
            game.Camera.ZoomTo(shopper.Position + Vector3.Up * .1f);
            if (startFrame >= 0 && frame - startFrame >= 240) break;
            if (captureRoute && startFrame >= 0 && (frame - startFrame) % 2 == 0)
            {
                await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(movieFolder, $"frame-{movieFrame++:000}.png"));
            }
            if (shopper.State is not (Shopper.Activity.Walking or Shopper.Activity.Leaving) || ++walkingFrames % 6 != 0 || saved >= 12) continue;
            await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, $"stride-{saved++:00}.png"));
        }
        game.Camera.Home();
        await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, "store-overview.png"));
        GD.Print($"WALK_PREVIEW frames={walkingFrames} captures={saved}");
        await game.QuitCleanly();
    }
}
