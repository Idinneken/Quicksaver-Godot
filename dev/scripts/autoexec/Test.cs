using System.Diagnostics;
using Godot;

public partial class Test : Node
{
	[Export] 
	public string saveString;

	public bool printMessages;

	public override void _Ready()
	{
		Debug.Print("hello");

		SaveSystem.LoadSave(SaveSystem.MakeSave(GetTree().Root));
	}

}
