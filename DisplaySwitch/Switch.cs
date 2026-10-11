// Modes Télétravail et Personnel, et détection des rôles pour la configuration.
// Les règles de sécurité travaillent sur des Display : elles se testent sans toucher l'affichage.
// Une règle non respectée lève une InvalidOperationException avec un message lisible.

namespace DisplaySwitch;

static class Switch
{
    // Retrouver un écran par son nom exact, en refusant l'absence ou l'ambiguïté.
    static Display FindDisplay(List<Display> displays, string name)
    {
        List<Display> matches = displays.Where(display => display.Name == name).Take(2).ToList();

        return matches.Count switch
        {
            0 => throw new InvalidOperationException($"Moniteur introuvable : {name}"),
            1 => matches[0],
            _ => throw new InvalidOperationException($"Plusieurs moniteurs portent le nom : {name}"),
        };
    }

    // Retrouver les deux écrans configurés et vérifier qu'ils sont distincts.
    static (Display Primary, Display Secondary) FindBoth(List<Display> displays, Settings settings)
    {
        Display primary = FindDisplay(displays, settings.PrimaryMonitor);
        Display secondary = FindDisplay(displays, settings.SecondaryMonitor);

        if (primary.PathIndex == secondary.PathIndex)
        {
            throw new InvalidOperationException("Les deux rôles désignent le même écran.");
        }
        return (primary, secondary);
    }

    // L'écran à conserver doit être actif et déjà principal dans Windows.
    static void CheckPrimary(Display primary)
    {
        if (!primary.Active)
        {
            throw new InvalidOperationException("Le moniteur principal configuré est inactif.");
        }
        if (!primary.Primary)
        {
            throw new InvalidOperationException(
                "Le moniteur principal configuré n'est pas principal dans Windows. "
                + "Rétablissez-le dans les paramètres Windows ou utilisez « Configurer Télétravail… ».");
        }
    }

    static bool IsClone(Display primary, Display secondary) =>
        primary.SourceModeIndex is not null && primary.SourceModeIndex == secondary.SourceModeIndex;

    // Préparer la désactivation : index des chemins à garder actifs (tous les actifs
    // sauf le secondaire, principal toujours inclus), ou null si le secondaire est déjà inactif.
    internal static List<int>? PlanDisable(List<Display> displays, Settings settings)
    {
        var (primary, secondary) = FindBoth(displays, settings);
        CheckPrimary(primary);

        if (!secondary.Active)
        {
            return null;
        }
        if (IsClone(primary, secondary))
        {
            throw new InvalidOperationException(
                "Les écrans sont en duplication : passez en bureau étendu avant cette action.");
        }

        List<int> keep = displays
            .Where(display => display.Active && display.PathIndex != secondary.PathIndex)
            .Select(display => display.PathIndex)
            .ToList();

        // Garantit aussi qu'il reste au moins un écran actif.
        if (!keep.Contains(primary.PathIndex))
        {
            throw new InvalidOperationException("Le principal doit rester actif.");
        }
        return keep;
    }

    // État attendu après Télétravail : principal conservé, secondaire inactif.
    internal static void VerifyDisabled(List<Display> displays, Settings settings)
    {
        var (primary, secondary) = FindBoth(displays, settings);
        CheckPrimary(primary);

        if (secondary.Active)
        {
            throw new InvalidOperationException("Après application, l'écran secondaire est encore actif.");
        }
    }

    // État attendu après Personnel : principal conservé, secondaire actif, pas de duplication.
    internal static void VerifyEnabled(List<Display> displays, Settings settings)
    {
        var (primary, secondary) = FindBoth(displays, settings);
        CheckPrimary(primary);

        if (!secondary.Active)
        {
            throw new InvalidOperationException("L'écran secondaire est resté inactif.");
        }
        if (IsClone(primary, secondary))
        {
            throw new InvalidOperationException("Windows a activé une duplication au lieu d'un bureau étendu.");
        }
    }

    // Mode Télétravail : valider auprès de Windows, puis appliquer sauf si dryRun, puis relire.
    public static string DisableSecondary(Settings settings, bool dryRun)
    {
        var (displays, snapshot) = Displays.ReadDisplays();

        List<int>? keep = PlanDisable(displays, settings);
        if (keep is null)
        {
            return "L'écran secondaire est déjà désactivé.";
        }
        DisplayConfigPathInfo[] paths = keep.Select(index => snapshot.Paths[index]).ToArray();

        Win.SetSuppliedConfig(paths, snapshot.Modes, DisplayAction.Validate);
        if (dryRun)
        {
            return "Désactivation validée par Windows — aucun changement appliqué.";
        }

        Win.SetSuppliedConfig(paths, snapshot.Modes, DisplayAction.Apply);

        var (updated, _) = Displays.ReadDisplays();
        VerifyDisabled(updated, settings);
        return "Écran secondaire désactivé — principal actif et conservé.";
    }

    // Mode Personnel : demander à Windows sa dernière topologie étendue mémorisée,
    // vérifier le résultat, et revenir à l'état précédent en cas d'échec.
    public static string EnableSecondary(Settings settings)
    {
        var (displays, snapshot) = Displays.ReadDisplays();

        if (displays.Count != 2)
        {
            throw new InvalidOperationException("La réactivation nécessite exactement deux écrans disponibles.");
        }
        var (primary, secondary) = FindBoth(displays, settings);
        CheckPrimary(primary);

        if (secondary.Active)
        {
            if (IsClone(primary, secondary))
            {
                throw new InvalidOperationException("Les écrans sont en duplication, pas en extension.");
            }
            return "L'écran secondaire est déjà actif en bureau étendu.";
        }

        // Copie de l'état actuel, pour le retour arrière (les structures sont copiées par valeur).
        DisplayConfigPathInfo[] previousPaths = displays
            .Where(display => display.Active)
            .Select(display => snapshot.Paths[display.PathIndex])
            .ToArray();

        Win.SetExtendTopology(DisplayAction.Validate);

        string error;
        try
        {
            ApplyExtendAndVerify(settings);
            return "Écran secondaire activé — bureau étendu et principal conservé.";
        }
        catch (InvalidOperationException failure)
        {
            error = failure.Message;
        }

        try
        {
            Win.SetSuppliedConfig(previousPaths, snapshot.Modes, DisplayAction.Apply);
        }
        catch (InvalidOperationException restoreError)
        {
            throw new InvalidOperationException(
                $"Réactivation échouée : {error}. Retour arrière échoué : {restoreError.Message}. "
                + "Rétablissez l'affichage dans les paramètres Windows.");
        }

        // Relire pour dire précisément si l'état précédent est revenu.
        try
        {
            var (restored, _) = Displays.ReadDisplays();
            VerifyDisabled(restored, settings);
        }
        catch (InvalidOperationException checkError)
        {
            throw new InvalidOperationException(
                $"Réactivation non confirmée : {error}. Windows a accepté le retour arrière, "
                + $"mais l'état relu est inattendu : {checkError.Message}");
        }
        throw new InvalidOperationException(
            $"Réactivation non confirmée : {error}. L'état précédent a été rétabli.");
    }

    static void ApplyExtendAndVerify(Settings settings)
    {
        Win.SetExtendTopology(DisplayAction.Apply);
        var (updated, _) = Displays.ReadDisplays();
        VerifyEnabled(updated, settings);
    }

    // Proposer les préférences à partir de la configuration Windows actuelle, sans rien modifier.
    public static Settings DetectTeleworkConfig() => SettingsFromDisplays(Displays.ReadDisplays().Displays);

    // Exiger deux écrans actifs en bureau étendu, un seul principal et des noms distincts.
    internal static Settings SettingsFromDisplays(List<Display> displays)
    {
        if (displays.Count != 2)
        {
            throw new InvalidOperationException("Deux écrans doivent être disponibles.");
        }

        foreach (Display display in displays)
        {
            if (!display.Active)
            {
                throw new InvalidOperationException("Activez les deux écrans avant de configurer.");
            }
            if (string.IsNullOrWhiteSpace(display.Name))
            {
                throw new InvalidOperationException("Un écran ne possède pas de nom utilisable.");
            }
        }

        Display first = displays[0];
        Display second = displays[1];
        if (first.Name == second.Name)
        {
            throw new InvalidOperationException("Les deux écrans portent le même nom.");
        }

        var (primary, secondary) = (first.Primary, second.Primary) switch
        {
            (true, false) => (first, second),
            (false, true) => (second, first),
            _ => throw new InvalidOperationException(
                "Un bureau étendu avec un seul écran principal est nécessaire."),
        };

        return new Settings(primary.Name, secondary.Name);
    }
}
