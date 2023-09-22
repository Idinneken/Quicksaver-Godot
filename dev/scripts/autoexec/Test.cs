using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class Test : Node
{
	public override void _Ready()
	{
		foreach (GodotObject godotObject in SaveSystem.ObtainAllObjects(GetTree().Root))
		{
			Debug.Print(godotObject.);
		}
	}

}
