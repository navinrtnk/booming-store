// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
namespace BoomingStore;

public static class ElectronicsPreview
{
    public static async Task Run(StoreGame game)
    {
        var folder = ProjectSettings.GlobalizePath("res://.tools/electronics-review"); Directory.CreateDirectory(folder);
        async Task Frame() => await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Capture(string name)
        {
            await game.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            game.GetViewport().GetTexture().GetImage().SavePng(Path.Combine(folder, name + ".png"));
        }
        for (var i = 0; i < 90; i++) await Frame(); await Capture("empty-store");
        ElectronicsChecks.Furnish(game); ElectronicsChecks.Add(game, "checkout", 1.8f, -.1f); game.Staff.Hire(EmployeeRole.Restocking); game.Staff.Hire(EmployeeRole.Cashier); game.Staff.Hire(EmployeeRole.Cashier);
        game.Staff.Summon(0); game.Staff.Summon(1); game.Staff.Summon(2); game.SetSpeed(3); game.Keeper.MoveTo(new(-2.2f, 0, 1.7f));
        for (var i = 0; i < 180; i++) await Frame(); await Capture("electronics-store");
        game.State.WorldHours = 17; game.ToggleOpen(); game.SetSpeed(3);
        for (var i = 0; i < 360; i++) await Frame(); await Capture("electronics-trading");
        game.Hud.ShowStaff(); await Frame(); await Capture("staff-shifts");
        foreach (var dialog in game.Hud.FindChildren("*", "AcceptDialog", true, false)) dialog.QueueFree();
        await Frame(); game.Hud.ToggleChrome(); await Frame(); await Capture("compact-store");
        GD.Print("ELECTRONICS_REVIEW completed"); await game.QuitCleanly();
    }
}
