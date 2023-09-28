using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

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

    public static class StringExtensions
    {
        public static string Compressed(this string input)
        {
            // Check for null or empty input
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentException("Input string cannot be null or empty.");
            }

            // Convert the input string to bytes
            byte[] uncompressedBytes = Encoding.UTF8.GetBytes(input);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                // Create a GZipStream to compress the data
                using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                {
                    gzipStream.Write(uncompressedBytes, 0, uncompressedBytes.Length);
                }

                // Convert the compressed data to a byte array
                byte[] compressedBytes = memoryStream.ToArray();

                // Convert the compressed bytes to a Base64-encoded string
                string compressedString = Convert.ToBase64String(compressedBytes);

                return compressedString;
            }
        }
    }
}
