// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
using static BoomingStore.Geometry;
namespace BoomingStore;

/// <summary>Original display racks and electronics, shared by world objects and catalogue photographs.</summary>
public static class ElectronicsVisuals
{
    private static readonly StandardMaterial3D Black = StoreVisuals.Mat("151d29", .3f, .25f);
    private static readonly StandardMaterial3D Silver = StoreVisuals.Mat("b7c5cf", .3f, .75f);
    private static readonly StandardMaterial3D Deck = StoreVisuals.Mat("e3e8ea", .55f, .25f);
    private static readonly StandardMaterial3D Blue = new() { AlbedoColor = new("193e73"), EmissionEnabled = true, Emission = new("163b69"), EmissionEnergyMultiplier = .7f };
    private static readonly StandardMaterial3D Teal = new() { AlbedoColor = new("43b4bd"), EmissionEnabled = true, Emission = new("43b4bd"), EmissionEnergyMultiplier = .4f };
    private static readonly StandardMaterial3D Sun = new() { AlbedoColor = new("ffd49a"), EmissionEnabled = true, Emission = new("ffc77e") };
    private static void Screen(Node3D root, float width, float height, Vector3 center)
    {
        RoundedBox(root, "Bezel", new(width, height, .045f), center, Black, .012f);
        Box(root, "Display", new(width - .035f, height - .035f, .008f), center + new Vector3(0, 0, .026f), Blue);
        Box(root, "Horizon", new(width - .04f, height * .23f, .005f), center + new Vector3(0, -height * .26f, .032f), Teal);
        Sphere(root, "Sun", new(height * .23f, height * .23f, .008f), center + new Vector3(width * .22f, height * .15f, .037f), Sun);
        for (var i = 0; i < 4; i++)
            Box(root, "Reflection", new(width * (.25f + i * .08f), height * .018f, .003f), center + new Vector3(-width * .12f, -height * (.19f + i * .045f), .037f), Blue);
    }
    public static bool Build(Node3D root, StoreItem item)
    {
        var product = item.Product; var width = product.Width;
        if (product.Levels != null)
        {
            foreach (var level in product.Levels)
            {
                RoundedBox(root, "Deck", new(width, .06f, product.Depth), new(0, level - .03f, 0), Deck, .01f);
                Box(root, "PriceRail", new(width, .045f, .025f), new(0, level - .025f, product.Depth / 2), Blue);
            }
            foreach (var x in new[] { -width / 2 + .03f, width / 2 - .03f }) foreach (var z in new[] { -product.Depth / 2 + .04f, product.Depth / 2 - .04f })
                Box(root, "SteelPost", new(.045f, product.Height, .045f), new(x, product.Height / 2, z), Silver);
            return true;
        }
        if (item.Kind.StartsWith("tv"))
        {
            var panelHeight = product.Height - .09f;
            Screen(root, width - .025f, panelHeight, new(0, panelHeight / 2 + .09f, -.035f));
            foreach (var x in new[] { -width * .29f, width * .29f })
                Rod(root, "Stand", new(x, .1f, -.035f), new(x + Math.Sign(x) * .06f, .018f, .12f), .018f, Black);
            return true;
        }
        if (item.Kind.StartsWith("laptop"))
        {
            RoundedBox(root, "Base", new(width, .018f, product.Depth), new(0, .018f, 0), Silver, .01f);
            Box(root, "Keyboard", new(width * .85f, .003f, product.Depth * .48f), new(0, .029f, -.025f), Black);
            Box(root, "Trackpad", new(width * .3f, .003f, product.Depth * .24f), new(0, .03f, .08f), Deck);
            Screen(root, width, product.Height - .03f, new(0, product.Height / 2 + .03f, -product.Depth / 2 + .015f)); return true;
        }
        switch (item.Kind)
        {
            case "console":
                RoundedBox(root, "Console", new(.18f, .38f, .26f), new(0, .2f, -.015f), Deck, .02f);
                Box(root, "Core", new(.03f, .36f, .27f), new(.035f, .2f, -.015f), Black);
                Box(root, "Indicator", new(.005f, .3f, .004f), new(.055f, .23f, .124f), Teal); return true;
            case "speaker":
                RoundedBox(root, "Housing", new(.27f, .27f, .2f), new(0, .14f, 0), Black, .035f);
                Sphere(root, "Woofer", new(.17f, .17f, .02f), new(0, .14f, .103f), Silver); return true;
            case "soundbar":
                RoundedBox(root, "Soundbar", new(.95f, .12f, .17f), new(0, .065f, 0), Black, .03f);
                foreach (var x in new[] { -.34f, 0, .34f }) Sphere(root, "Driver", new(.08f, .08f, .012f), new(x, .065f, .085f), Silver); return true;
            case "headphones":
                var band = Ring(root, "Headband", .105f, .017f, new(0, .18f, 0), Black); band.RotationDegrees = new(90, 0, 0);
                foreach (var x in new[] { -.105f, .105f }) RoundedBox(root, "Earcup", new(.065f, .12f, .09f), new(x, .12f, 0), Black, .025f); return true;
            default: return false;
        }
    }
}
