using System.Collections.Generic;
using System.Linq;
using Godot;

public static class SaveSystem
{
	public static void MakeSave(Node rootNode)
	{
		ObtainAllObjects(rootNode);
	}

	public static void LoadSave()
	{

	}

	public static List<GodotObject> ObtainAllObjects(Node rootNode)
	{
		List<GodotObject> godotObjects = new();
		GetChildNodesRecursively(rootNode, godotObjects);
		return godotObjects;	
	}

	private static void GetChildNodesRecursively(Node startingNode, List<GodotObject> output)
	{
		List<GodotObject> childNodes = startingNode.GetChildren().Cast<GodotObject>().ToList();
		output.AddRange(childNodes);

		foreach (Node childNode in childNodes)
		{			
			GetChildNodesRecursively(childNode, output);
		}
	}
}
