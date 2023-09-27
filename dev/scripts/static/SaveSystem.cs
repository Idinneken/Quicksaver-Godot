using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

public static class SaveSystem
{
    public static string MakeSave(Node rootNode)
    {
        LevelSaveData levelSaveData = new();

        foreach (GodotObject godotObject in ObtainAllObjects(rootNode))
        {
            NodeSaveData nodeSaveData = new();
            nodeSaveData.Initialize(levelSaveData, godotObject as Node);
        }

        return JsonSerializer.Serialize(levelSaveData);
    }

    public static void LoadSave()
    {
        // Implement load save logic
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
    public Dictionary<ulong, NodeSaveData> HashNodePairs = new();
}

public class NodeSaveData
{
    public string declaringType;
    public string baseType;
    public ulong parentHash;
    public ulong ownerHash;

    [JsonIgnore] public LevelSaveData levelSaveData;
    [JsonIgnore] public Node node;
    [JsonIgnore] public Node parent;

    public Dictionary<string, List<string>> Vals = new();

    public static Dictionary<Type, List<string>> specificallyIgnoredVariables = new()
    {
        {typeof(Node), new List<string> { "Owner", "NativePtr" }},
        {typeof(Test), new List<string> { "Owner" }}
    };

    public static List<Type> ignoredVariableTypes = new()
    {
        typeof(IntPtr), // IntPtr isn't liked by JsonSerializer
    };

    public void Initialize(LevelSaveData levelSaveData, Node node)
    {
        declaringType = node.GetType().DeclaringType?.AssemblyQualifiedName ?? node.GetType().AssemblyQualifiedName;
        baseType = typeof(Node).AssemblyQualifiedName;
        parentHash = node.GetParent()?.GetInstanceId() ?? 0;
        ownerHash = node.Owner?.GetInstanceId() ?? 0;

        this.levelSaveData = levelSaveData;
        this.node = node;
		this.parent = node.GetParent();

        Debug.Print($"\nNEW NODE {node.Name} \ndeclaringType: {declaringType} \nbaseType: {baseType}");
        GenerateSerializedInformation();
        Debug.Print($"vals.Count {Vals.Count}");

        levelSaveData.HashNodePairs.Add(node.GetInstanceId(), this);
    }

    public void GenerateSerializedInformation()
    {
        foreach (KeyValuePair<string, object> kvp in GetValues())
        {
            bool currentVariableIsIgnored = false;

            if (kvp.Value == null)
            {
                Debug.Print($"Adding '{kvp.Key}' as null to vals");
                Vals.Add(kvp.Key, new List<string>() { "test", null });
            }
            else
            {
                // This code checks if a variable should be ignored based on its value's type,
                // and whether the declaringType or baseType's listing contains the current variable.
                // If so, it sets currentVariableIsIgnored to true.

                if (ignoredVariableTypes.Contains(kvp.Value.GetType()) ||
                    (specificallyIgnoredVariables.ContainsKey(Type.GetType(declaringType)) &&
                     specificallyIgnoredVariables[Type.GetType(declaringType)].Contains(kvp.Key)) ||
                    (specificallyIgnoredVariables.ContainsKey(Type.GetType(baseType)) &&
                     specificallyIgnoredVariables[Type.GetType(baseType)].Contains(kvp.Key)))
                {
                    currentVariableIsIgnored = true;
                }

                if (!currentVariableIsIgnored) // If the variable is not ignored
                {
                    if (kvp.Value is Node || kvp.Value.GetType().IsSubclassOf(typeof(Node)))
                    {
						Node variableNode = kvp.Value as Node;
                        Debug.Print($"Adding the Node on variable '{kvp.Key}' as '{variableNode.GetInstanceId().ToString()}' to vals");
                        Vals.Add(kvp.Key, new List<string>() { "test", variableNode.GetInstanceId().ToString() });
                    }
                    else
                    {
                        Debug.Print($"Adding '{kvp.Key}' '{kvp.Value}' to vals. It's a '{kvp.Value.GetType()}'");
                        Vals.Add(kvp.Key, new List<string>() { "test", JsonSerializer.Serialize(kvp.Value) });
                    }
                }
            }
        }
    }

    public Dictionary<string, object> GetValues()
    {
        Dictionary<string, object> values = new();

        foreach (MemberInfo member in GetMembers())
        {
            object memberValue = null;
            if (member is PropertyInfo property) { memberValue = property.GetValue(node); }
            else if (member is FieldInfo field) { memberValue = field.GetValue(node); }

            values.Add(member.Name, memberValue);
        }

        return values;
    }

    public List<MemberInfo> GetMembers()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        return Type.GetType(declaringType).GetMembers(flags)
            .Where(member =>
                (member.MemberType == MemberTypes.Field || (member.MemberType == MemberTypes.Property &&
                ((PropertyInfo)member).GetSetMethod() != null)) &&
                !member.IsDefined(typeof(ObsoleteAttribute), inherit: true))
            .ToList();
    }
}
