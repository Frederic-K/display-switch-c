// Préférences Télétravail : un petit fichier JSON placé à côté de l'exécutable.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DisplaySwitch;

// Noms exacts des deux écrans, tels que Windows les renvoie.
// En JSON : primary_monitor et secondary_monitor, comme dans la version Python.
record Settings(string PrimaryMonitor, string SecondaryMonitor);

// Code de conversion JSON généré à la compilation (pas de réflexion : compatible Native AOT).
// Les deux clés sont obligatoires et ne peuvent pas valoir null.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    NewLine = "\n",
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(Settings))]
partial class SettingsJson : JsonSerializerContext;

static class Config
{
    const string ConfigFileName = "config.json";
    const string TemporaryFileName = "config.json.tmp";

    // Chemin du JSON : dans le dossier de l'exécutable, quel que soit le dossier courant.
    public static string ConfigPath() => Path.Combine(AppContext.BaseDirectory, ConfigFileName);

    public static Settings Load() => LoadFrom(ConfigPath());

    public static void Save(Settings settings) => SaveTo(ConfigPath(), settings);

    // Vérifier que les deux noms sont renseignés et différents.
    internal static void Validate(Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.PrimaryMonitor))
        {
            throw new InvalidOperationException("Nom de moniteur manquant ou invalide : primary_monitor");
        }
        if (string.IsNullOrWhiteSpace(settings.SecondaryMonitor))
        {
            throw new InvalidOperationException("Nom de moniteur manquant ou invalide : secondary_monitor");
        }
        if (settings.PrimaryMonitor == settings.SecondaryMonitor)
        {
            throw new InvalidOperationException("Les deux moniteurs doivent avoir des noms différents.");
        }
    }

    // Lire et valider le fichier, avec un message différent selon le problème rencontré.
    internal static Settings LoadFrom(string path)
    {
        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Configuration absente : {path}. Utilisez « Configurer Télétravail… ».");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException($"Lecture impossible de {path} : {error.Message}");
        }

        Settings? settings;
        try
        {
            settings = JsonSerializer.Deserialize(content, SettingsJson.Default.Settings);
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException($"Configuration invalide ({path}) : {error.Message}");
        }

        if (settings is null)
        {
            throw new InvalidOperationException($"Configuration invalide ({path}) : fichier vide (null).");
        }
        Validate(settings);
        return settings;
    }

    // Écrire d'abord un fichier temporaire complet, puis le renommer.
    // Le renommage remplace l'ancien fichier d'un coup : jamais de JSON à moitié écrit.
    internal static void SaveTo(string path, Settings settings)
    {
        Validate(settings);

        string content = JsonSerializer.Serialize(settings, SettingsJson.Default.Settings) + "\n";
        string temporaryPath = Path.Combine(Path.GetDirectoryName(path)!, TemporaryFileName);

        try
        {
            WriteFile(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Nettoyage au mieux : une erreur ici ne doit pas masquer la première.
            try { File.Delete(temporaryPath); } catch { }
            throw new InvalidOperationException($"Enregistrement impossible de {path} : {error.Message}");
        }
    }

    // Écrire le contenu et attendre qu'il soit sur le disque avant le renommage.
    static void WriteFile(string path, string content)
    {
        using var file = new FileStream(path, FileMode.Create, FileAccess.Write);
        // UTF-8 sans BOM, comme Rust et Python.
        file.Write(System.Text.Encoding.UTF8.GetBytes(content));
        file.Flush(flushToDisk: true);
    }
}
