using Godot;
using System;

public partial class FirstNode : Node
{
	[Export] public string ayooooooo;
	[Export] Godot.Collections.Dictionary<string,Variant> dict = new();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
