using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Extensions;
using System.Net;

public static class SaveSystem
{
	private const string saveFilePath = "user://saved_scene.tscn";

	public static readonly JsonSerializerOptions options = new()
	{
		IncludeFields = true,
		WriteIndented = true, 
	};
	
	public static string MakeSave(Node rootNode, bool returnCompressed = true)
	{
		string saveString = returnCompressed ? JsonSerializer.Serialize(new LevelSaveData(rootNode), options).Compressed() : JsonSerializer.Serialize(new LevelSaveData(rootNode), options);
		return saveString;
	}

	public static void LoadSave(string save, bool isCompressed = true)
	{
		LevelSaveData levelSaveData = isCompressed ? JsonSerializer.Deserialize<LevelSaveData>(save.Decompressed(), options) : JsonSerializer.Deserialize<LevelSaveData>(save, options);
		Debug.Print(levelSaveData.hashEntityDataPairs.Count.ToString());
		levelSaveData.Deserialise();
	}

}

[Serializable]
public class LevelSaveData
{
	public Dictionary<ulong, EntitySaveData> hashEntityDataPairs = new();
	[JsonIgnore] public Dictionary<ulong, GodotObject> entities = new();

	#region CONSTRUCTORS

	[JsonConstructor]
	public LevelSaveData(Dictionary<ulong, EntitySaveData> hashEntityDataPairs) 
	{
		this.hashEntityDataPairs = hashEntityDataPairs;
	}

	public LevelSaveData(Node rootNode)
	{
		foreach (Node node in rootNode.GetChildrenRecursive(true))
		{
			entities.Add(node.GetInstanceId(), node);
		}

		Serialise();
	}

	#endregion 

	#region SERIALISING

	public void Serialise()
	{
		foreach (KeyValuePair<ulong, GodotObject> kvp in entities)
		{
			EntitySaveData entitySaveData;
			hashEntityDataPairs.Add(kvp.Key, entitySaveData = new EntitySaveData(this, kvp.Value));
			entitySaveData.Serialise();
		}
	}

	#endregion

	#region DESERIALISING

	public void Deserialise()
	{
		foreach (KeyValuePair<ulong, EntitySaveData> kvp in hashEntityDataPairs)
		{
			kvp.Value.CreateBaseEntity(this);
		}

		foreach (KeyValuePair<ulong, EntitySaveData> kvp in hashEntityDataPairs)
		{
			kvp.Value.Deserialise();
		}

		foreach (KeyValuePair<ulong, EntitySaveData> kvp in hashEntityDataPairs)
		{
			kvp.Value.entity.Dispose();
		}
	}

	#endregion
}

[Serializable]
public class EntitySaveData
{
	public string type;
	public Dictionary<string, string> vals = new();
	[JsonIgnore] public LevelSaveData levelSaveData;
	[JsonIgnore] public GodotObject entity; 

	#region CONSTRUCTORS

	[JsonConstructor]
	public EntitySaveData(string type, Dictionary<string, string> vals)
	{
		this.type = type; this.vals = vals;
	}

	public EntitySaveData(LevelSaveData levelSaveData, GodotObject entity)
	{
		this.levelSaveData = levelSaveData; this.entity = entity;
		type = this.entity.GetType().DeclaringType?.AssemblyQualifiedName ?? this.entity.GetType().AssemblyQualifiedName;
	}

	#endregion

	#region SERIALISING

	public void Serialise()
	{
		DebugPro.PrintIf(SaveDataConfig.dev, $"\nNEW ENTITY type: {type}");

		foreach (var kvp in GetValues())
		{
			bool currentVariableTypeIsIgnored = SaveDataConfig.ignoredVariableTypes.Contains(kvp.Value?.GetType());
			bool currentVariableIsIgnored =
				SaveDataConfig.ignoredVariables.ContainsKey(Type.GetType(type)) 
				&& SaveDataConfig.ignoredVariables[Type.GetType(type)].Contains(kvp.Key);

			if (kvp.Value == null && !currentVariableTypeIsIgnored)
			{
				DebugPro.PrintIf(SaveDataConfig.dev, $"NULL Adding '{kvp.Key}' as null");
				vals.Add(kvp.Key, "null");
			}
			else if (!currentVariableTypeIsIgnored && !currentVariableIsIgnored)
			{
				if (kvp.Value is GodotObject godotObject)
				{
					DebugPro.PrintIf(SaveDataConfig.dev, $"GODOTOBJECT Adding '{kvp.Key}' as '{godotObject.GetInstanceId()}'");
					vals.Add(kvp.Key, godotObject.GetInstanceId().ToString());

					EntitySaveData variableEntitySaveData;
					if (levelSaveData.hashEntityDataPairs.TryAdd(godotObject.GetInstanceId(), variableEntitySaveData = new(levelSaveData, kvp.Value as GodotObject))) //If HashEntityDataPairs doesn't currently have a godotObject with that hash, it adds it and serialises
					{
						variableEntitySaveData.Serialise();
					}
				}
				else
				{
					DebugPro.PrintIf(SaveDataConfig.dev, $"JSON Adding '{kvp.Key}' as '{JsonSerializer.Serialize(kvp.Value)}'. It's a '{kvp.Value.GetType()}'");
					vals.Add(kvp.Key, JsonSerializer.Serialize(kvp.Value));
				}
			}
		}

		DebugPro.PrintIf(SaveDataConfig.dev, $"vals.Count {vals.Count}");
	}

	#endregion

	#region DESERIALISING 

	public void CreateBaseEntity(LevelSaveData levelSaveData)
	{	
		this.levelSaveData = levelSaveData;
		object entityTemp = Activator.CreateInstance(Type.GetType(type)); entity = entityTemp as GodotObject;

		ulong entitySaveDataHash = 0; //Find the hash of 'this' within the levelSaveData, and use it for the entities dictionary
		bool wasFound = false;
		foreach (var kvp in levelSaveData.hashEntityDataPairs)
		{
			if (wasFound == false && kvp.Value == this) 
			{ 
				entitySaveDataHash = kvp.Key; 
				wasFound = true; 
			} 
		}

		levelSaveData.entities.Add(entitySaveDataHash, entity);
	}

	public void Deserialise()
	{
		ApplyFields();
	}

	#endregion

	#region GETTING FIELDS

	private Dictionary<string, object> GetValues()
	{
		Dictionary<string, object> values = new();

		try
		{
			foreach (MemberInfo member in GetMembers())
			{
				object memberValue = null;
				if (member is PropertyInfo property) { memberValue = property.GetValue(entity); }
				else if (member is FieldInfo field) { memberValue = field.GetValue(entity); }

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

		try
		{
			const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
			members = Type.GetType(type)?.GetMembers(flags)
				.Where(member =>
					member.MemberType == MemberTypes.Field ||
					(member.MemberType == MemberTypes.Property && ((PropertyInfo)member).GetSetMethod() != null))
				.ToList();

			return members;
		}
		catch (Exception e)
		{
			Debug.Fail(e.Message);
			return null;
		}
	}

	#endregion

	#region SETTING FIELDS

	public void ApplyFields()
	{
		foreach (var kvp in vals)
		{
			Variant entityVar = entity._Get(kvp.Key);


			EntitySaveData entitySaveData;
			if (ulong.TryParse(kvp.Value, out ulong hash) && levelSaveData.hashEntityDataPairs.TryGetValue(hash, out entitySaveData))
			{
				if (entitySaveData.entity.GetType().IsAssignableTo(property.PropertyType))
				{
					property.SetValue(entity, entitySaveData.entity);
				}
				else
				{
					try { property.SetValue(entity, JsonSerializer.Deserialize(kvp.Value, property.PropertyType));}
					catch (JsonException ex) { Debug.Fail($"PROP 1 {ex.Message}"); }
					catch (Exception ex) { Debug.Fail($"PROP 2 {ex.Message}"); }
				}
			}
			else
			{
				Debug.Print($"{type} {kvp.Key} {kvp.Value} isAssignableTo N/A");
				try { property.SetValue(entity, JsonSerializer.Deserialize(kvp.Value, property.PropertyType));}
				catch (JsonException ex) { Debug.Fail($"PROP 3 {ex.Message}"); }
				catch (Exception ex) { Debug.Fail($"PROP 4 {ex.Message}"); }
			}
	
			// const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

			// if (entity.GetType().GetMember(kvp.Key, flags)[0] is MemberInfo member)
			// {
			// 	EntitySaveData entitySaveData;
			// 	if (member is PropertyInfo property)
			// 	{
			// 		if (ulong.TryParse(kvp.Value, out ulong hash) && levelSaveData.hashEntityDataPairs.TryGetValue(hash, out entitySaveData))
			// 		{
			// 			Debug.Print($"{type} {kvp.Key} {kvp.Value} isAssignableTo {entitySaveData.entity.GetType().IsAssignableTo(property.PropertyType)}");
			// 			if (entitySaveData.entity.GetType().IsAssignableTo(property.PropertyType))
			// 			{
			// 				property.SetValue(entity, entitySaveData.entity);
			// 			}
			// 			else
			// 			{
			// 				try { property.SetValue(entity, JsonSerializer.Deserialize(kvp.Value, property.PropertyType));}
			// 				catch (JsonException ex) { Debug.Fail($"PROP 1 {ex.Message}"); }
			// 				catch (Exception ex) { Debug.Fail($"PROP 2 {ex.Message}"); }
			// 			}
			// 		}
			// 		else
			// 		{
			// 			Debug.Print($"{type} {kvp.Key} {kvp.Value} isAssignableTo N/A");
			// 			try { property.SetValue(entity, JsonSerializer.Deserialize(kvp.Value, property.PropertyType));}
			// 			catch (JsonException ex) { Debug.Fail($"PROP 3 {ex.Message}"); }
			// 			catch (Exception ex) { Debug.Fail($"PROP 4 {ex.Message}"); }
			// 		}
			// 	}
			// 	else if (member is FieldInfo field)
			// 	{
			// 		if (ulong.TryParse(kvp.Value, out ulong hash) && levelSaveData.hashEntityDataPairs.TryGetValue(hash, out entitySaveData))
			// 		{
			// 			Debug.Print($"{type} {kvp.Key} {kvp.Value} isAssignableTo {entitySaveData.entity.GetType().IsAssignableTo(field.FieldType)}");
			// 			if (entitySaveData.entity.GetType().IsAssignableTo(field.FieldType))
			// 			{
			// 				field.SetValue(entity, entitySaveData.entity);
			// 			}
			// 			else
			// 			{
			// 				try { field.SetValue(entity, JsonSerializer.Deserialize(kvp.Value, field.FieldType));}
			// 				catch (JsonException ex) { Debug.Fail($"FIELD 1 {ex.Message}"); }
			// 				catch (Exception ex) { Debug.Fail($"FIELD 2 {ex.Message}"); }
			// 			}
			// 		}
			// 		else
			// 		{
			// 			Debug.Print($"{type} {kvp.Key} {kvp.Value} isAssignableTo N/A");
			// 			try { field.SetValue(entity, JsonSerializer.Deserialize(kvp.Value, field.FieldType));}
			// 			catch (JsonException ex) { Debug.Fail($"FIELD 3 {ex.Message}"); }
			// 			catch (Exception ex) { Debug.Fail($"FIELD 4 {ex.Message}"); }
			// 		}
			// 	}
			// }
		}
	}

	#endregion
}

public static class SaveDataConfig
{
	public static bool dev = false;

	public static List<Type> ignoredVariableTypes = new()
	{
		typeof(IntPtr), // IntPtr isn't liked by JsonSerializer
	};

	public static Dictionary<Type, List<string>> ignoredVariables = new()
	{
		{typeof(Node), new List<string> { "Owner", "NativePtr" }},
		{typeof(Resource), new List<string> { }},

	};
}
