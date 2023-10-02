using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Extensions;

public static class SaveSystem
{
    public static string MakeSave(Node rootNode)
    {
        LevelSaveData levelSaveData = new LevelSaveData(rootNode);

        JsonSerializerOptions options = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true, // Format the JSON for readability
        };

        Debug.Print(JsonSerializer.Serialize(levelSaveData, options));
        Debug.Print(JsonSerializer.Serialize(levelSaveData, options).Compressed());
        return JsonSerializer.Serialize(levelSaveData, options);
        
    }

    public static void LoadSave(string save, bool isCompressed = false)
    {
        LevelSaveData levelSaveData = isCompressed ? JsonSerializer.Deserialize<LevelSaveData>(save.Decompressed()) : JsonSerializer.Deserialize<LevelSaveData>(save);
        levelSaveData.PopulateLevel();
        
    }
}

[Serializable]
public class LevelSaveData
{
    public Dictionary<ulong, NodeSaveData> hashNodePairs = new();
    public Dictionary<ulong, ResourceSaveData> hashResourcePairs = new();

    [JsonIgnore] public List<Node> nodes = new(); 
    [JsonIgnore] public List<Resource> resources = new(); 

    public LevelSaveData(Node rootNode)
    {
        nodes = rootNode.GetChildrenRecursive(true);
        CreateNodeSavedata(nodes);
        
        // all the resources that need to be saved are obtained from the CreateNodeSavedata(nodes);
        CreateResourceSaveData(resources);
    }

    private void CreateNodeSavedata(List<Node> nodes)
    {
        foreach (Node node in nodes)
        {
            hashNodePairs.Add(node.GetInstanceId(), new NodeSaveData(this, node));
        }
    }

    private void CreateResourceSaveData(List<Resource> resources)
    {
        foreach (Resource resource in resources)
        {
            hashResourcePairs.Add(resource.GetInstanceId(), new ResourceSaveData(this, resource));
        }
    }

    public void PopulateLevel()
    {
        CreateNodes();
        CreateResources();
    }

    public void CreateNodes()
    {
        foreach (KeyValuePair<ulong, NodeSaveData> kvp in hashNodePairs)
        {
            kvp.Value.ToNode(this);
        }
    }

    public void CreateResources()
    {
        foreach (KeyValuePair<ulong, ResourceSaveData> kvp in hashResourcePairs)
        {
            kvp.Value.ToResource(this);
        }
    }

    [JsonConstructor]
    public LevelSaveData(Dictionary<ulong, NodeSaveData> hashNodePairs, Dictionary<ulong, ResourceSaveData> hashResourcePairs)
    {
        this.hashNodePairs = hashNodePairs;
        this.hashResourcePairs = hashResourcePairs;
    }
}

[Serializable]
public class NodeSaveData
{
    public string type;
    public ulong parentHash;
    public ulong ownerHash;

    [JsonIgnore] public LevelSaveData levelSaveData;
    [JsonIgnore] public Node node;
    [JsonIgnore] public Node parent;

    public Dictionary<string, string> vals = new();

    public NodeSaveData(LevelSaveData levelSaveData, Node node)
    {
        type = node.GetType().DeclaringType?.AssemblyQualifiedName ?? node.GetType().AssemblyQualifiedName;
        parentHash = node.GetParent()?.GetInstanceId() ?? 0;
        ownerHash = node.Owner?.GetInstanceId() ?? 0;

        this.levelSaveData = levelSaveData;
        this.node = node;
		this.parent = node.GetParent();

        Debug.Print($"\nNEW NODE {node.Name} \ntype: {type}");
        GenerateSerializedInformation();
        Debug.Print($"vals.Count {vals.Count}");
    }

    public void GenerateSerializedInformation()
    {
        foreach (KeyValuePair<string, object> kvp in GetValues())
        {
            bool currentVariableTypeIsIgnored = SaveDataConfig.ignoredVariableTypes.Contains(kvp.Value?.GetType());
            bool currentVariableIsIgnored = 
            (SaveDataConfig.ignoredVariables.ContainsKey(Type.GetType(type)) && 
            SaveDataConfig.ignoredVariables[Type.GetType(type)].Contains(kvp.Key)) ||
            (SaveDataConfig.ignoredVariables.ContainsKey(typeof(Node)) && SaveDataConfig.ignoredVariables[typeof(Node)].Contains(kvp.Key));

            if (kvp.Value == null && !currentVariableTypeIsIgnored)
            {
                // Debug.Print($"NULL Adding '{kvp.Key}' as null");
                vals.Add(kvp.Key, "null");
            }
            else if (!currentVariableTypeIsIgnored && !currentVariableIsIgnored)
            {
                if (kvp.Value is Resource variableResource)
                {
                    // Debug.Print($"RESOURCE Adding '{kvp.Key}' as '{variableResource.GetInstanceId()}'");
                    vals.Add(kvp.Key, variableResource.GetInstanceId().ToString());
                    levelSaveData.resources.Add(variableResource);
                }
                else if (kvp.Value is Node)
                {
                    // Debug.Print($"NODE Adding '{kvp.Key}' as '{variableNode.GetInstanceId().ToString()}'");
                    Node variableNode = kvp.Value as Node;
                    vals.Add(kvp.Key, variableNode.GetInstanceId().ToString());
                }
                else
                {
                    // Debug.Print($"JSON Adding '{kvp.Key}' as '{JsonSerializer.Serialize(kvp.Value)}'. It's a '{kvp.Value.GetType()}'");
                    vals.Add(kvp.Key, JsonSerializer.Serialize(kvp.Value));
                }
            }
        }
    }

    public void ToNode(LevelSaveData levelSaveData)
    {
        this.levelSaveData = levelSaveData;
        node = new();

        foreach (KeyValuePair<string, string> kvp in vals)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            if (node.GetType().GetMember(kvp.Key, flags)[0] is MemberInfo member)
            {
                if (member is PropertyInfo property) 
                { 
                    if (property.GetType() == typeof(Node))
                    {
                        property.SetValue(node, this.levelSaveData.hashNodePairs[ulong.Parse(kvp.Value)]);
                    }
                    else if (property.GetType() == typeof(Resource))
                    {
                        property.SetValue(node, this.levelSaveData.hashResourcePairs[ulong.Parse(kvp.Value)]);
                    }
                    else 
                    {
                        property.SetValue(node, JsonSerializer.Deserialize(kvp.Value, property.GetType()));
                    }

                }
                else if (member is FieldInfo field) 
                {
                    if (field.GetType() == typeof(Node))
                    {
                        field.SetValue(node, this.levelSaveData.hashNodePairs[ulong.Parse(kvp.Value)]);
                    }
                    else if (field.GetType() == typeof(Resource))
                    {
                        field.SetValue(node, this.levelSaveData.hashResourcePairs[ulong.Parse(kvp.Value)]);
                    }
                    else 
                    {
                        field.SetValue(node, JsonSerializer.Deserialize(kvp.Value, field.GetType()));
                    }
                }

                // The member with the specified name exists in the type.
                // You can perform actions related to 'member' here.
            }
        }
    }

    #region GETTING

    private Dictionary<string, object> GetValues()
    {
        Dictionary<string, object> values = new();

        try 
        {
            foreach (MemberInfo member in GetMembers())
            {
                object memberValue = null;
                if (member is PropertyInfo property) { memberValue = property.GetValue(node); }
                else if (member is FieldInfo field) { memberValue = field.GetValue(node); }

                values.Add(member.Name, memberValue);
            }
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
        }
        return values;
    }

    private Dictionary<string, object> GetValues(List<MemberInfo> members)
    {
        Dictionary<string, object> values = new();

        try 
        {
            foreach (MemberInfo member in members)
            {
                object memberValue = null;
                if (member is PropertyInfo property) { memberValue = property.GetValue(node); }
                else if (member is FieldInfo field) { memberValue = field.GetValue(node); }

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
            members = Type.GetType(type).GetMembers(flags)
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
}

[Serializable]
public class ResourceSaveData
{
    public string type;
    public string filePath;
    public Dictionary<string, string> vals = new();

    [JsonIgnore] public LevelSaveData levelSaveData;
    [JsonIgnore] public Resource resource;

    public ResourceSaveData(LevelSaveData levelSaveData, Resource resource)
    {
        type = resource.GetType().AssemblyQualifiedName;
        filePath = resource.ResourcePath;

        this.levelSaveData = levelSaveData;
        this.resource = resource;

        Debug.Print($"\nNEW RESOURCE {resource.ResourceName} \ntype: {type} \nfilepath: {filePath}");
        GenerateSerializedInformation();
        Debug.Print($"vals.Count {vals.Count}");
    }

    public void GenerateSerializedInformation()
    {
        foreach (KeyValuePair<string, object> kvp in GetValues())
        {
            bool currentVariableTypeIsIgnored = SaveDataConfig.ignoredVariableTypes.Contains(kvp.Value?.GetType());
            bool currentVariableIsIgnored = 
            (SaveDataConfig.ignoredVariables.ContainsKey(Type.GetType(type)) && 
            SaveDataConfig.ignoredVariables[Type.GetType(type)].Contains(kvp.Key)) ||
            (SaveDataConfig.ignoredVariables.ContainsKey(Type.GetType(type)) && SaveDataConfig.ignoredVariables[Type.GetType(type)].Contains(kvp.Key));


            if (kvp.Value == null && !currentVariableTypeIsIgnored)
            {
                // Debug.Print($"NULL Adding '{kvp.Key}' as null");
                vals.Add(kvp.Key, "null");
            }
            else if (!currentVariableTypeIsIgnored && !currentVariableIsIgnored)
            {
                if (kvp.Value is Resource variableResource)
                {
                    // Debug.Print($"RESOURCE Adding '{kvp.Key}' as '{variableResource.GetInstanceId()}'");
                    vals.Add(kvp.Key, variableResource.GetInstanceId().ToString());
                    levelSaveData.resources.Add(variableResource);
                }
                else if (kvp.Value is Node)
                {
                    // Debug.Print($"NODE Adding '{kvp.Key}' as '{variableNode.GetInstanceId().ToString()}'");
                    Node variableNode = kvp.Value as Node;
                    vals.Add(kvp.Key, variableNode.GetInstanceId().ToString());
                }
                else
                {
                    // Debug.Print($"JSON Adding '{kvp.Key}' as '{JsonSerializer.Serialize(kvp.Value)}'. It's a '{kvp.Value.GetType()}'");
                    vals.Add(kvp.Key, JsonSerializer.Serialize(kvp.Value));
                }
            }
        }
    }

    #region GETTING

    private Dictionary<string, object> GetValues()
    {
        Dictionary<string, object> values = new();

        try 
        {
            foreach (MemberInfo member in GetMembers())
            {
                object memberValue = null;
                if (member is PropertyInfo property) { memberValue = property.GetValue(resource); }
                else if (member is FieldInfo field) { memberValue = field.GetValue(resource); }

                values.Add(member.Name, memberValue);
            }
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
        }
        return values;
    }

    private Dictionary<string, object> GetValues(List<MemberInfo> members)
    {
        Dictionary<string, object> values = new();

        try 
        {
            foreach (MemberInfo member in members)
            {
                object memberValue = null;
                if (member is PropertyInfo property) { memberValue = property.GetValue(resource); }
                else if (member is FieldInfo field) { memberValue = field.GetValue(resource); }

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
            members = Type.GetType(type).GetMembers(flags)
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

    public List<MemberInfo> GetMembers(List<string> memberNames)
    {
        List<MemberInfo> members = new();

        try 
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            members = Type.GetType(type).GetMembers(flags)
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

    internal void ToResource(LevelSaveData levelSaveData)
    {
        throw new NotImplementedException();
    }

    #endregion
}

public static class SaveDataConfig
{
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
