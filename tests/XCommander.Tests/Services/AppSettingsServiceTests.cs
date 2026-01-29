using System;
using System.Collections.Generic;
using System.IO;
using XCommander.Services;

namespace XCommander.Tests.Services;

public class AppSettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_PersistsQuickFilterHistory()
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var settingsPath = Path.Combine(tempDir.FullName, "settings.ini");
        try
        {
            var service = new AppSettingsService(settingsPath);
            service.Settings.QuickFilterHistory = new List<string> { "*.log", "*.tmp" };
            service.Save();

            var loaded = new AppSettingsService(settingsPath);
            loaded.Load();

            Assert.Equal(new[] { "*.log", "*.tmp" }, loaded.Settings.QuickFilterHistory);
        }
        finally
        {
            Directory.Delete(tempDir.FullName, true);
        }
    }
}
