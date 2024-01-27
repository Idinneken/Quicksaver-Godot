using System.Diagnostics;
using Godot;

public partial class Test : Node
{
	[Export] public string saveString;
	[Export] public string saveStringCompressed;

	public bool printMessages;

	public override void _Ready()
	{
		// saveString = SaveSystem.MakeSave(GetTree().Root, false);
		// saveStringCompressed = SaveSystem.MakeSave(GetTree().Root, true);

		// Debug.Print(saveString);

		// SaveSystem.LoadSave(saveString, false);

		// SaveSystem.LoadSave(saveString, false);

		// SaveSystem.LoadSave(SaveSystem.MakeSave(GetTree().Root));

		GD.Print(GetTree().Root);
		
		saveString = SaveSystem.MakeSave(GetTree().Root);

		SaveSystem.LoadSave(saveString);

	}

    public override void _Process(double delta)
    {
		if (Input.IsActionJustPressed("ui_accept"))
        {
            // Handle key press
            // GD.Print("UIAccept key pressed");
			// SaveSystem.LoadSavedScene(GetTree().Root);
        }
    }

}
