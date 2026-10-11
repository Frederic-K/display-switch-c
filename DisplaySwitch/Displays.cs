// Lecture des écrans connectés, sous une forme simple indépendante des structures Windows.

namespace DisplaySwitch;

// Un écran disponible pour Windows, actif ou non.
// Primary : surface du bureau placée en (0, 0) ; toujours faux si l'écran est inactif.
// SourceModeIndex : deux écrans actifs avec le même mode source sont en duplication.
// PathIndex : position du chemin de cet écran dans Snapshot.Paths.
record Display(
    string Name,
    string DevicePath,
    bool Active,
    bool Primary,
    (uint Width, uint Height)? Resolution,
    uint? SourceModeIndex,
    int PathIndex);

static class Displays
{
    // Lire les écrans et garder la lecture Windows brute, nécessaire pour les modifier ensuite.
    public static (List<Display> Displays, Snapshot Snapshot) ReadDisplays()
    {
        Snapshot snapshot = Win.QueryAllPaths();
        var displays = new List<Display>();

        foreach (int pathIndex in SelectPaths(snapshot))
        {
            DisplayConfigPathInfo path = snapshot.Paths[pathIndex];
            var (name, devicePath) = Win.MonitorIdentity(path.TargetInfo);
            bool active = (path.Flags & Win.DisplayConfigPathActive) != 0;
            var display = new Display(name, devicePath, active, false, null, null, pathIndex);

            if (active)
            {
                uint index = Win.SourceModeIndex(path)
                    ?? throw new InvalidOperationException($"Mode d'affichage absent pour {name}.");
                DisplayConfigSourceMode mode = Win.SourceMode(snapshot.Modes, index)
                    ?? throw new InvalidOperationException($"Mode d'affichage invalide pour {name}.");

                // `with` crée une copie du record avec quelques champs modifiés.
                display = display with
                {
                    Primary = mode.PositionX == 0 && mode.PositionY == 0,
                    Resolution = (mode.Width, mode.Height),
                    SourceModeIndex = index,
                };
            }

            displays.Add(display);
        }

        return (displays, snapshot);
    }

    // Windows renvoie plusieurs chemins possibles pour un même écran.
    // Garder un seul chemin par cible disponible, en privilégiant le chemin actif.
    static List<int> SelectPaths(Snapshot snapshot)
    {
        var selected = new List<int>();

        for (int index = 0; index < snapshot.Paths.Length; index++)
        {
            DisplayConfigPathTargetInfo target = snapshot.Paths[index].TargetInfo;
            if (target.TargetAvailable == 0)
            {
                continue;
            }

            int sameTarget = selected.FindIndex(kept =>
            {
                DisplayConfigPathTargetInfo keptTarget = snapshot.Paths[kept].TargetInfo;
                return keptTarget.AdapterId == target.AdapterId && keptTarget.Id == target.Id;
            });
            bool isActive = (snapshot.Paths[index].Flags & Win.DisplayConfigPathActive) != 0;

            if (sameTarget < 0)
            {
                selected.Add(index);
            }
            else if (isActive)
            {
                selected[sameTarget] = index;
            }
        }

        return selected;
    }

    // Nom à afficher, même si Windows n'en fournit pas.
    public static string DisplayName(Display display) =>
        display.Name.Length == 0 ? "Moniteur sans nom" : display.Name;

    // État d'un écran en une ligne, commun au terminal et à la fenêtre.
    public static string Describe(Display display)
    {
        if (display.Active && display.Resolution is (var width, var height))
        {
            string role = display.Primary ? "Principal" : "Secondaire";
            return $"{width} × {height} — {role} — Actif";
        }
        return "Inactif — Résolution courante indisponible";
    }
}
