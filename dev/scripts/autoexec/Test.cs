using Godot;
using System.Diagnostics;

public partial class Test : Node
{
	[Export] 
	public string saveString;

	public override void _Ready()
	{
		saveString = SaveSystem.MakeSave(GetTree().Root);

		// foreach (GodotObject godotObject in SaveSystem.ObtainAllObjects(GetTree().Root))
		// {
		// 	// Debug.Print(godotObject.);
		// }
	}

}
