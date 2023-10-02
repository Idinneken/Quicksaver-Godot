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

		saveString = SaveSystem.MakeSave(GetTree().Root, false);

		Debug.Print(saveString);

		SaveSystem.LoadSave(saveString, false);

		// SaveSystem.LoadSave(SaveSystem.MakeSave(GetTree().Root));
	}

}
