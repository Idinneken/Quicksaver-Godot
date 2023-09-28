using Godot;
using System.Collections.Generic;

namespace Extensions
{
    public static class NodeExtensions
    {
        public static List<Node> GetChildrenRecursive(this Node rootNode, bool includeRoot = false)
        {
            List<Node> childNodes = new List<Node>();
            if (includeRoot) { childNodes.Add(rootNode); }

            AddChildNodesRecursively(rootNode, childNodes);
            return childNodes;
        }

        private static void AddChildNodesRecursively(Node parentNode, List<Node> childNodes)
        {
            foreach (Node childNode in parentNode.GetChildren())
            {
                childNodes.Add(childNode);
                if (childNode.GetChildCount() > 0) { AddChildNodesRecursively(childNode, childNodes); }
            }
        }
    }
}
