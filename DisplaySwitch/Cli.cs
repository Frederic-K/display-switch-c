// Commandes de développement : list, check, disable, enable.

using System.Text;

namespace DisplaySwitch;

static class Cli
{
    const string Usage = """
        Usage : DisplaySwitch [list | check | disable | enable]
          list     affiche les écrans disponibles
          check    valide la désactivation auprès de Windows, sans rien appliquer
          disable  mode Télétravail : désactive l'écran secondaire configuré
          enable   mode Personnel : réactive le bureau étendu
        Sans argument, la fenêtre s'ouvre.
        """;

    // Exécuter une commande ; code de sortie 0 en cas de succès, 1 en cas d'erreur.
    public static int Run(string command)
    {
        // Afficher correctement « × », « — » et les accents dans le terminal.
        Console.OutputEncoding = Encoding.UTF8;

        try
        {
            string text = command switch
            {
                "list" => ListDisplays(),
                "check" => Switch.DisableSecondary(Config.Load(), dryRun: true),
                "disable" => Switch.DisableSecondary(Config.Load(), dryRun: false),
                "enable" => Switch.EnableSecondary(Config.Load()),
                "-h" or "--help" or "help" => Usage,
                _ => throw new InvalidOperationException($"Commande inconnue : {command}\n{Usage}"),
            };
            Console.WriteLine(text);
            return 0;
        }
        catch (InvalidOperationException error)
        {
            Console.Error.WriteLine($"Erreur : {error.Message}");
            return 1;
        }
    }

    // Texte de `list` : nom, état et chemin Windows de chaque écran.
    static string ListDisplays()
    {
        var (displays, _) = Displays.ReadDisplays();

        if (displays.Count == 0)
        {
            return "Aucun écran disponible.";
        }

        IEnumerable<string> blocks = displays.Select(display =>
            $"{Displays.DisplayName(display)}\n{Displays.Describe(display)}\nIdentifiant : {display.DevicePath}");
        return string.Join("\n\n", blocks);
    }
}
