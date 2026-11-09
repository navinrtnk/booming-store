// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

public partial class Shopper : PersonActor
{
    public enum Activity { Arriving, Walking, Browsing, LookingAround, Queueing, Paying, Leaving }
    public Activity State { get; private set; }
    public int RegisterId { get; set; }
    public float BrowseDuration { get; private set; }
    public StoreItem? Choice { get; private set; }
    public CustomerMorale Morale { get; } = new();
    private Label3D _moraleLabel = null!;
    private int _lastMeter = -1;
    private float _timer;
    private int _browseGoal, _basketGoal;
    private readonly HashSet<int> _viewed = [];
    private readonly HashSet<int> _picked = [];
    private string SayText { set => Say(value); }
    public new void Build(StoreGame game, int profile, bool shopkeeper = false)
    {
        base.Build(game, profile, shopkeeper);
        _moraleLabel = Geometry.Text(this, "Morale", "Happy ▰▰▰", new(0, 2.28f, 0), .004f, new("ffffff"), 32);
        _moraleLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _moraleLabel.OutlineSize = 6; _moraleLabel.OutlineModulate = new("18392b");
        var basketRoll = GD.Randf(); _basketGoal = basketRoll < .02f ? 3 : basketRoll < .14f ? 2 : 1;
        _browseGoal = 1 + (int)(GD.Randf() * 3);
        State = Activity.Arriving; _timer = Between(2.5f, 4.5f);
    }
    public override void _Process(double delta)
    {
        var dt = Mathf.Min((float)delta, .1f) * _game.Speed;
        if (Finished || dt == 0) return;
        Morale.Advance(delta, State == Activity.Queueing);
        var meter = (int)Math.Ceiling(Morale.Value * 5);
        if (_lastMeter != meter + (int)Morale.Mood * 10)
        {
            _lastMeter = meter + (int)Morale.Mood * 10;
            _moraleLabel.Text = $"{Morale.Mood} {new string('▰', meter)}{new string('▱', 5 - meter)}";
            _moraleLabel.Modulate = Morale.Mood == CustomerMood.Happy ? new Color("a5efb7") : Morale.Mood == CustomerMood.Neutral ? new Color("ffe394") : new Color("ffada2");
        }
        if (Morale.Mood == CustomerMood.Disappointed && State == Activity.Queueing)
        { _game.Abandon(this); SayText = "Too long… maybe next time"; Leave(); }
        _timer -= dt;
        var motion = StepPerson(dt);
        if (ConsumeRouteFailure())
        {
            if (State == Activity.Walking) LookAround();
            else if (State is Activity.Queueing or Activity.Paying) { _game.LeaveQueue(this); Leave(); }
            else if (State == Activity.Leaving) WalkTo(_game.Navigation.Entrance);
        }
        if (!IsWalking)
            switch (State)
            {
                case Activity.Arriving: if (_timer <= 0) BrowseNext(); break;
                case Activity.Walking:
                    State = Activity.Browsing; _timer = BrowseDuration = Between(8, 14); SayText = "A closer look…";
                    React(PersonBeat.Admire, Choice?.Position + Vector3.Up * .4f, VoiceLine.Browse); break;
                case Activity.Browsing:
                    if (Choice != null) Face(Choice.Position, dt);
                    if (_timer <= 0) Decide(); break;
                case Activity.LookingAround: if (_timer <= 0) BrowseNext(); break;
                case Activity.Queueing:
                    var index = _game.Registers.IndexOf(this);
                    var spot = _game.QueueSpot(this);
                    if (Position.DistanceTo(spot) > .2f) WalkTo(spot);
                    else if (index == 0 && _game.CanServe(this))
                    { State = Activity.Paying; _timer = Between(3.5f, 5.5f); SayText = "Checking out"; _game.BeginPayment(this); }
                    else if (index == 0) SayText = "Waiting for you";
                    break;
                case Activity.Paying:
                    if (!_game.CanServe(this)) { State = Activity.Queueing; _game.Registers.Assign(this); WalkTo(_game.QueueSpot(this)); SayText = "Waiting for a cashier"; break; }
                    Face(_game.RegisterFor(this)!.Position, dt);
                    if (_timer <= 0)
                    {
                        _bought = _game.CompleteSale(this);
                        SayText = _bought ? "Lovely. Thank you!" : "Maybe next time";
                        _game.LeaveQueue(this); Leave();
                    }
                    break;
                case Activity.Leaving: Finished = true; _game.LeaveQueue(this); QueueFree(); break;
            }
        AttentionTarget = State == Activity.Browsing ? Choice?.Position + Vector3.Up * .35f : null;
        GestureAmount = State == Activity.Browsing && _timer < 3 && _timer > .7f ? Mathf.Sin((_timer - .7f) / 2.3f * Mathf.Pi) * .55f : 0;
        Animate(dt, motion, Mathf.Min((float)delta, .1f));
    }
    private static float Between(float minimum, float maximum) => Mathf.Lerp(minimum, maximum, GD.Randf());
    private void BrowseNext()
    {
        var candidates = _game.State.Items.Where(i => i.ForSale && i.Stock > 0 && !_viewed.Contains(i.Id)).OrderBy(_ => GD.Randf());
        foreach (var item in candidates)
        {
            var point = _game.Navigation.Approach(item, Position);
            if (point == null || _game.Shoppers.Any(other => other != this && !other.Finished && other.State != Activity.Leaving &&
                (other.Position.DistanceTo(point.Value) < .65f || other.Destination is { } destination && destination.DistanceTo(point.Value) < .65f))) continue;
            Choice = item; _viewed.Add(item.Id); WalkTo(point.Value); State = Activity.Walking; SayText = "Browsing"; return;
        }
        if (Choice != null && Choice.Stock > 0 && !_picked.Contains(Choice.Id)) Decide(true);
        else { if (_game.CartCount(this) > 0) QueueCart(); else { SayText = "Maybe another day"; Leave(); } }
    }
    public void ReceiveHelp(Vector3 employee)
    { if (State == Activity.Browsing) { _timer = Math.Max(1, _timer - 1); React(PersonBeat.Chat, employee + Vector3.Up * 1.4f, VoiceLine.Agree); } }
    private void LookAround()
    { State = Activity.LookingAround; _timer = Between(1.5f, 3.5f); SayText = "Looking around"; }
    private void Decide(bool finalChoice = false)
    {
        if (Choice == null || Choice.Stock <= 0)
        { if (finalChoice) Leave(); else LookAround(); return; }
        if (!finalChoice && _viewed.Count < _browseGoal) { LookAround(); return; }
        var ratio = (float)(Choice.Price / Choice.Product.Price);
        var chance = Mathf.Clamp(.94f - Math.Max(0, ratio - .8f) * .7f, .04f, .98f);
        if (GD.Randf() < chance)
        {
            if (!_game.TakeItem(this, Choice)) { LookAround(); return; }
            _picked.Add(Choice.Id);
            if (_game.CartCount(this) < _basketGoal) { LookAround(); return; }
            QueueCart();
        }
        else if (!finalChoice && _viewed.Count < 4) LookAround();
        else { if (_game.CartCount(this) > 0) QueueCart(); else { SayText = "Maybe another day"; Leave(); } }
    }
    public void RedirectQueue(Vector3 point) { if (State == Activity.Queueing) WalkTo(point); }
    internal void QueueCart()
    {
        if (!_game.JoinQueue(this)) { Leave(); return; }
        State = Activity.Queueing; SayText = $"{_game.CartCount(this)} item(s) · ready to pay";
        React(PersonBeat.Chat, _game.Keeper.Actor.Position, VoiceLine.Agree);
        if (!WalkTo(_game.QueueSpot(this))) { _game.LeaveQueue(this); Leave(); }
    }
    private void Leave()
    {
        State = Activity.Leaving;
        if (!WalkTo(_game.Navigation.Entrance)) { Finished = true; QueueFree(); }
    }
}
