// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
namespace BoomingStore;

/// <summary>Photographs the actual merchandise once, then caches the textures. One studio viewport.</summary>
public partial class CatalogPictures : Node
{
    private readonly Dictionary<string, Texture2D> _cache = [];
    private readonly Queue<(Product Product, TextureRect Target)> _queue = new();
    private SubViewport _studio = null!;
    private Node3D _mount = null!;
    private Camera3D _camera = null!;
    private Node3D? _model;
    private StoreVisuals _visuals = null!;
    private bool _busy;
    public void Build(StoreVisuals visuals)
    {
        _visuals = visuals;
        if (DisplayServer.GetName() == "headless") return;
        _studio = new SubViewport { Size = new(256, 176), TransparentBg = true, OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
        AddChild(_studio); _mount = new Node3D(); _studio.AddChild(_mount);
        _studio.AddChild(new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new("dce4e8"), AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = Colors.White, AmbientLightEnergy = .8f } });
        _studio.AddChild(new DirectionalLight3D { RotationDegrees = new(-40, -25, 0), LightEnergy = 1.5f });
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Current = true }; _studio.AddChild(_camera);
    }
    public void Request(Product product, TextureRect target)
    {
        if (_cache.TryGetValue(product.Id, out var image)) { target.Texture = image; return; }
        if (DisplayServer.GetName() == "headless") return;
        _queue.Enqueue((product, target)); if (!_busy) RenderNext();
    }
    private async void RenderNext()
    {
        _busy = true;
        while (IsInsideTree() && _queue.TryDequeue(out var request))
        {
            if (!_cache.TryGetValue(request.Product.Id, out var texture))
            {
                if (_model != null) { _mount.RemoveChild(_model); _model.QueueFree(); }
                _model = _visuals.Item(_mount, new StoreItem { Kind = request.Product.Id, Stock = 1 }, true);
                var height = request.Product.Electronics || request.Product.Levels != null ? request.Product.Height : 1.8f;
                var scale = Math.Max(1, Math.Max(request.Product.Width * 1.35f, height * 1.65f));
                var center = new Vector3(0, height * .45f, 0);
                _camera.Size = scale; _camera.Position = center + new Vector3(2, 1.3f, 3); _camera.LookAt(center);
                _studio.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                if (!IsInsideTree()) return;
                texture = ImageTexture.CreateFromImage(_studio.GetTexture().GetImage()); _cache[request.Product.Id] = texture;
            }
            if (GodotObject.IsInstanceValid(request.Target) && request.Target.IsInsideTree()) request.Target.Texture = texture;
        }
        _busy = false;
    }
}
