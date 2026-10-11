// Display Switch : désactive ou réactive l'écran secondaire configuré.
// En version Release, l'exécutable est une application fenêtrée sans console.
// En Debug (dotnet run), la console reste disponible pour les commandes.

namespace DisplaySwitch;

static class Program
{
    // Sans argument : la fenêtre. Avec un argument : une commande de développement.
    // [STAThread] est exigé par WinForms (presse-papiers, boîtes de dialogue Windows).
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            return Cli.Run(args[0]);
        }

        // Généré à partir du .csproj : haute résolution (DPI) et styles visuels Windows.
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
