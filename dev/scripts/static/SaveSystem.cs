using System.Collections.Generic;
using System.Linq;
using Godot;
using System.Text.Json.Serialization;
using System;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;

public static class SaveSystem
{
	public static string MakeSave(Node rootNode)
	{
		LevelSaveData levelSaveData = new();

		foreach(GodotObject godotObject in ObtainAllObjects(rootNode))
		{
			NodeSaveData nodeSaveData = new();
			nodeSaveData.Initialise(levelSaveData, godotObject as Node);
		}
		
		return JsonSerializer.Serialize(levelSaveData);
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

public class LevelSaveData
{
	public Dictionary<ulong, NodeSaveData> hashNodePairs = new();
}

public class NodeSaveData
{
	public string declaringType;
	public string baseType;
	public Dictionary<string, List<string>> vals = new();
	public ulong owner;

	[JsonIgnore] public LevelSaveData levelSaveData;
	[JsonIgnore] public Node node;

	public void Initialise(LevelSaveData levelSaveData, Node node)
	{
		this.levelSaveData = levelSaveData;
		this.node = node;
		owner = node.GetParent() != null ? node.GetParent().GetInstanceId() : 0;
		declaringType = node.GetType().DeclaringType?.AssemblyQualifiedName ?? node.GetType().AssemblyQualifiedName;
		baseType = typeof(Node).AssemblyQualifiedName;

		Debug.Print($"\nNEW NODE {node.Name}");
		GenerateSerializedInformation();

		levelSaveData.hashNodePairs.Add(node.GetInstanceId(), this);
	}
	
	public void GenerateSerializedInformation()
	{
		KeyValuePair<string, object> debugKvp = new();

		try 
		{
			foreach (KeyValuePair<string, object> kvp in GetValues())
			{
				debugKvp = kvp;
				Debug.Print($"	{kvp.Key} {kvp.Value} {kvp.Value.GetType()}");
				if (kvp.Value != null)
				{
					if (kvp.Value is IntPtr owner)
					{
						Debug.Print("IS INTPTR");
						// vals.Add(kvp.Key, new List<string>() {"test", owner.GetInstanceId().ToString()  });
					}
					else
					{
						vals.Add(kvp.Key, new List<string>() {"test", JsonSerializer.Serialize(kvp.Value) });
					}
				}
				else
				{
					vals.Add(kvp.Key, new List<string>() {"test", null });
				}
			}

			node.ow
		}
		catch (Exception e)
		{
			Debug.Print($"FAILED WITH {debugKvp.Key} {debugKvp.Value} valueType:{debugKvp.Value?.GetType()} variable type on node: {node.GetType().GetProperty(debugKvp.Key)?.GetValue(node)?.GetType()}");
			Debug.Fail(e.Message);
			// You can access kvp.Key and kvp.Value here if needed
			// kvp.Key and kvp.Value won't be available here directly, 
			// but you can store them in variables before the try block if needed.
		}
	}

	public Dictionary<string, object> GetValues()
	{
		Dictionary<string, object> values = new();

		try 
		{
			foreach (MemberInfo member in GetMembers())
			{

				object memberValue = null;
				if (member is PropertyInfo property) { memberValue = property.GetValue(node); }
				else if (member is FieldInfo field) { memberValue = field.GetValue(node); }

				// Debug.Print($"{member.Name} {memberValue}");
				values.Add(member.Name, memberValue);
			}
		}
		catch (Exception e)
		{
			Debug.Print(e.Message);
		}

		return values;
	}

	public List<MemberInfo> GetMembers()
	{
		List<MemberInfo> members = new();

		const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public| BindingFlags.NonPublic;

		members.AddRange(Type.GetType(declaringType).GetMembers(flags).Where(member =>
            (member.MemberType == MemberTypes.Field || (member.MemberType == MemberTypes.Property &&    // Check if the member is a field or a property
            ((PropertyInfo)member).GetSetMethod() != null)) &&                                          // Check (if it's a property), that it has a Set method
            !member.IsDefined(typeof(ObsoleteAttribute), inherit: true)                                 // Ensure the field or property isn't obsolete
            ));

		return members;
	}
}
