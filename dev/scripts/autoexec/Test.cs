using Godot;
using System.Diagnostics;

public partial class Test : Node
{
	public override void _Ready()
	{
		Debug.Print(SaveSystem.MakeSave(GetTree().Root));

		// foreach (GodotObject godotObject in SaveSystem.ObtainAllObjects(GetTree().Root))
		// {
		// 	// Debug.Print(godotObject.);
		// }
	}

}
