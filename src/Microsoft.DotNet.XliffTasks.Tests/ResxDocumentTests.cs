// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using XliffTasks.Model;
using Xunit;

namespace XliffTasks.Tests
{
    public class ResxTranslationTests
    {
        [Fact]
        public void BasicLoadAndTranslate()
        {
            string source =
@"<root>
  <data name=""Hello"" xml:space=""preserve"">
    <value>Hello!</value>
  </data>
  <data name=""Goodbye"" xml:space=""preserve"">
    <value>Goodbye!</value>
  </data>
</root>";

            Dictionary<string, string> translations = new()
            {
                ["Hello"] = "Bonjour!",
                ["Goodbye"] = "Au revoir!",
            };

            string expectedTranslation =
@"<root>
  <data name=""Hello"" xml:space=""preserve"">
    <value>Bonjour!</value>
  </data>
  <data name=""Goodbye"" xml:space=""preserve"">
    <value>Au revoir!</value>
  </data>
</root>";

            ResxDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.Translate(translations);
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());
        }

        [Fact]
        public void RewriteFileReferenceToAbsoluteInDestinyFolder()
        {
            string sourceFolder = Directory.GetCurrentDirectory();
            string expectedAbsoluteLocation = Path.Combine(
              Directory.GetCurrentDirectory(),
              @"Resources\Package.ico".Replace('\\', Path.DirectorySeparatorChar));
            string source =
@"<root>
  <data name=""400"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>Resources\Package.ico;System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
  </data>
</root>";

            string expectedTranslation =
@"<root>
  <data name=""400"" type=""System.Resources.ResXFileRef, System.Windows.Forms"">
    <value>ABSOLUTEPATH;System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
  </data>
</root>".Replace("ABSOLUTEPATH", expectedAbsoluteLocation);

            ResxDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.RewriteRelativePathsToAbsolute(
                Path.Combine(sourceFolder, "Resources.resx"));
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());
        }

        [Fact]
        public void RewriteFileReferenceToRelativePathFromOutputFolder()
        {
            // Source .resx is in C:\repo\src\MyProject\Resources.resx
            // Output (translated) .resx is in C:\repo\artifacts\obj\MyProject.xlf\MyProject.resx
            // The resource file is at C:\repo\src\MyProject\Resources\Package.ico
            // Expected relative path from output dir to resource: ..\..\..\src\MyProject\Resources\Package.ico (or OS equivalent)

            string sourceFolder = Path.Combine(Path.GetTempPath(), "repo", "src", "MyProject");
            string outputFolder = Path.Combine(Path.GetTempPath(), "repo", "artifacts", "obj", "MyProject.xlf");
            string sourceFullPath = Path.Combine(sourceFolder, "Resources.resx");
            string outputFullPath = Path.Combine(outputFolder, "MyProject.resx");

            string resourceRelativePath = Path.Combine("Resources", "Package.ico");
            string resourceAbsolutePath = Path.Combine(sourceFolder, resourceRelativePath);

            // Compute expected relative path from output directory to resource file (same logic as the fix)
            Uri fromUri = new Uri(outputFolder.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri toUri = new Uri(resourceAbsolutePath);
            string expectedRelativePath = Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);

            string source = """
                <root>
                  <data name="400" type="System.Resources.ResXFileRef, System.Windows.Forms">
                    <value>RESOURCEPATH;System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
                  </data>
                </root>
                """.TrimEnd().Replace("RESOURCEPATH", resourceRelativePath);

            string expectedTranslation = """
                <root>
                  <data name="400" type="System.Resources.ResXFileRef, System.Windows.Forms">
                    <value>RELATIVEPATH;System.Drawing.Icon, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
                  </data>
                </root>
                """.TrimEnd().Replace("RELATIVEPATH", expectedRelativePath);

            ResxDocument document = new();
            StringWriter writer = new();
            document.Load(new StringReader(source));
            document.RewriteRelativePathsForOutputPath(sourceFullPath, outputFullPath);
            document.Save(writer);

            AssertEx.EqualIgnoringLineEndings(expectedTranslation, writer.ToString());

            // Crucially, the path in the output must NOT be absolute — it must be relative,
            // so that the translated .resx remains valid after the repo is renamed or moved.
            string outputValue = writer.ToString();
            Assert.DoesNotContain(Path.GetTempPath(), outputValue);
        }

        
    }
}
