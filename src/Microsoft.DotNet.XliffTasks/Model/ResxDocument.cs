// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace XliffTasks.Model
{
    /// <summary>
    /// A <see cref="TranslatableDocument"/> for files in .resx format.
    /// See https://msdn.microsoft.com/en-us/library/ekyft91f(v=vs.100).aspx
    /// </summary>
    internal sealed class ResxDocument : TranslatableXmlDocument
    {
        protected override IEnumerable<TranslatableNode> GetTranslatableNodes()
        {
            foreach (XElement node in Document.Descendants("data"))
            {
                // skip non-string data
                if (node.Attribute("type") != null || node.Attribute("mimetype") != null)
                {
                    continue;
                }

                string name = node.Attribute("name").Value;
                XElement valueElement = node.Element("value");
                string value = valueElement?.Value;
                string comment = node.Element("comment")?.Value ?? "";

                // skip designer goo that should not be translated
                if (name.StartsWith(">>") || name.EndsWith(".LayoutSettings"))
                {
                    continue;
                }

                // skip fully "locked" strings
                if (comment == "{Locked}")
                {
                    continue;
                }

                // skip empty strings
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                yield return new TranslatableXmlElement(
                    id: name,
                    source: value, 
                    note: comment, 
                    element: valueElement);
            }
        }

        public override void RewriteRelativePathsForOutputPath(string sourceFullPath, string outputFullPath)
        {
            foreach (XElement node in Document.Descendants("data"))
            {
                if (node.Attribute("type")?.Value == "System.Resources.ResXFileRef, System.Windows.Forms")
                {
                    XElement valueNodeOfFileRef = node.Element("value");
                    string[] splitRelativePathAndSerializedType = valueNodeOfFileRef.Value.Split(';');
                    string resourceRelativePath = splitRelativePathAndSerializedType[0].Replace('\\', Path.DirectorySeparatorChar);

                    string absoluteResourcePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFullPath), resourceRelativePath));
                    string outputDirectory = Path.GetDirectoryName(outputFullPath);
                    string relativePath = GetRelativePath(outputDirectory, absoluteResourcePath);
                    splitRelativePathAndSerializedType[0] = relativePath;

                    valueNodeOfFileRef.Value = string.Join(";", splitRelativePathAndSerializedType);
                }
            }
        }

        private static string GetRelativePath(string fromDirectory, string toPath)
        {
            // Ensure directory URI ends with separator so MakeRelativeUri treats it as a directory.
            Uri fromUri = new Uri(fromDirectory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri toUri = new Uri(toPath);
            Uri relativeUri = fromUri.MakeRelativeUri(toUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.ToString());
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
