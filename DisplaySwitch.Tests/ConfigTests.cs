// Lecture, validation et écriture atomique de config.json, dans des dossiers temporaires.

using DisplaySwitch;

namespace DisplaySwitch.Tests;

public class ConfigTests
{
    const string ConfigFileName = "config.json";

    // Dossier temporaire propre à chaque test, vidé au début.
    static string TestDir(string name)
    {
        string dir = Path.Combine(Path.GetTempPath(), $"display-switch-cs-test-{name}");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void RejectsEmptyOrIdenticalNames()
    {
        Assert.Throws<InvalidOperationException>(() => Config.Validate(new Settings("", "B")));
        Assert.Throws<InvalidOperationException>(() => Config.Validate(new Settings("A", "  ")));
        Assert.Throws<InvalidOperationException>(() => Config.Validate(new Settings("A", "A")));
        Config.Validate(new Settings("A", "B"));
    }

    [Fact]
    public void SaveThenLoadReturnsSameSettings()
    {
        string path = Path.Combine(TestDir("roundtrip"), ConfigFileName);
        var original = new Settings("MSI MAG342CQR", "MAG321CURV");

        Config.SaveTo(path, original);

        Assert.Equal(original, Config.LoadFrom(path));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, "config.json.tmp")));
        // Même contenu que la version Rust : clés snake_case, indentation de deux espaces.
        Assert.Equal(
            "{\n  \"primary_monitor\": \"MSI MAG342CQR\",\n  \"secondary_monitor\": \"MAG321CURV\"\n}\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void SaveReplacesExistingFile()
    {
        string path = Path.Combine(TestDir("replace"), ConfigFileName);
        Config.SaveTo(path, new Settings("A", "B"));
        Config.SaveTo(path, new Settings("B", "A"));

        Assert.Equal(new Settings("B", "A"), Config.LoadFrom(path));
    }

    [Fact]
    public void InvalidSettingsAreNotWritten()
    {
        string path = Path.Combine(TestDir("invalid-save"), ConfigFileName);

        Assert.Throws<InvalidOperationException>(() => Config.SaveTo(path, new Settings("A", "A")));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ReadsPythonConfigFile()
    {
        string path = Path.Combine(TestDir("python"), ConfigFileName);
        File.WriteAllText(path, "{\n  \"secondary_monitor\": \"MAG321CURV\",\n  \"primary_monitor\": \"MSI MAG342CQR\"\n}");

        Assert.Equal(new Settings("MSI MAG342CQR", "MAG321CURV"), Config.LoadFrom(path));
    }

    [Fact]
    public void ExplainsMissingAndInvalidFiles()
    {
        string dir = TestDir("errors");
        var missing = Assert.Throws<InvalidOperationException>(() => Config.LoadFrom(Path.Combine(dir, ConfigFileName)));
        Assert.Contains("Configuration absente", missing.Message);

        string broken = Path.Combine(dir, "broken.json");
        File.WriteAllText(broken, "{ pas du json");
        Assert.Contains("Configuration invalide",
            Assert.Throws<InvalidOperationException>(() => Config.LoadFrom(broken)).Message);

        string incomplete = Path.Combine(dir, "incomplete.json");
        File.WriteAllText(incomplete, "{\"primary_monitor\": \"A\"}");
        Assert.Contains("Configuration invalide",
            Assert.Throws<InvalidOperationException>(() => Config.LoadFrom(incomplete)).Message);
    }
}
