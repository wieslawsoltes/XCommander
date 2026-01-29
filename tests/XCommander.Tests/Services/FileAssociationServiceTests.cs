using System;
using System.IO;
using XCommander.Services;

namespace XCommander.Tests.Services;

public class FileAssociationServiceTests
{
    [Fact]
    public void GetOpenCommand_UsesDefaultsWhenFileMissing()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var associationsPath = Path.Combine(tempDir.FullName, "file_associations.json");
        try
        {
            var service = new FileAssociationService(associationsPath);
            var command = service.GetOpenCommand(Path.Combine(tempDir.FullName, "archive.zip"));

            Assert.Equal("internal:archive", command);
        }
        finally
        {
            Directory.Delete(tempDir.FullName, true);
        }
    }

    [Fact]
    public void GetViewerCommand_ReloadsWhenFileChanges()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var associationsPath = Path.Combine(tempDir.FullName, "file_associations.json");
        try
        {
            File.WriteAllText(associationsPath, """
            [
              {
                "Extension": ".txt",
                "Description": "Text",
                "ViewerCommand": "viewer-one %s",
                "EditorCommand": "",
                "OpenCommand": "",
                "UseSystemDefault": false
              }
            ]
            """);

            var service = new FileAssociationService(associationsPath);
            var first = service.GetViewerCommand(Path.Combine(tempDir.FullName, "note.txt"));
            Assert.Equal("viewer-one %s", first);

            File.WriteAllText(associationsPath, """
            [
              {
                "Extension": ".txt",
                "Description": "Text",
                "ViewerCommand": "viewer-two %s",
                "EditorCommand": "",
                "OpenCommand": "",
                "UseSystemDefault": false
              }
            ]
            """);
            File.SetLastWriteTimeUtc(associationsPath, DateTime.UtcNow.AddMinutes(1));

            var second = service.GetViewerCommand(Path.Combine(tempDir.FullName, "note.txt"));
            Assert.Equal("viewer-two %s", second);
        }
        finally
        {
            Directory.Delete(tempDir.FullName, true);
        }
    }
}
