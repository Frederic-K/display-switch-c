# Display Switch (C# / .NET 10 / WinForms)

Troisième implémentation de Display Switch, écrite pour comparer avec la version Rust + egui (`display-switch-rs`) et la version Python + PySide6 ([display-switch](https://github.com/Frederic-K/display-switch)). Même comportement, mêmes messages, même fichier `config.json`, même interface. Ce n'est pas une migration : c'est une expérience.

- **Télétravail** : désactive l'écran secondaire configuré, garde le principal.
- **Personnel** : redemande à Windows le bureau étendu mémorisé.
- **Actualiser** : relit les écrans.
- **Configurer Télétravail…** : enregistre les rôles actuellement choisis dans Windows.

Les règles de sécurité sont celles de la version Rust (voir son README, section « Contrôles de sécurité ») : l'écran principal configuré doit rester actif et principal, le dernier écran actif n'est jamais désactivé, une identification ambiguë est refusée, Personnel revient en arrière en cas d'échec.

## Prérequis

- Windows 11 x64.
- Pour développer : SDK .NET 10 (`winget install Microsoft.DotNet.SDK.10`). Testé avec le SDK 10.0.401.
- Pour Native AOT uniquement : Visual Studio Build Tools avec le composant C++ (déjà nécessaire pour Rust).
- Sur le PC cible : rien, avec les publications self-contained ou Native AOT.

## Commandes

Depuis le dossier du dépôt :

```powershell
dotnet build                                    # compiler (Debug)
dotnet test                                     # 17 tests, comme la version Rust
dotnet run --project DisplaySwitch              # ouvrir la fenêtre
dotnet run --project DisplaySwitch -- list      # commandes : list, check, disable, enable
```

En Debug, l'exécutable est `DisplaySwitch\bin\Debug\net10.0-windows\DisplaySwitch.exe` et lit le `config.json` placé à côté de lui. Comme en Rust, le Debug est une application console (les commandes affichent leur texte) et le Release une application fenêtrée (`<OutputType>` dans le `.csproj`).

Publication, un profil par variante (voir « Taille et distribution ») :

```powershell
dotnet publish DisplaySwitch -p:PublishProfile=SelfContained       # recommandé
dotnet publish DisplaySwitch -p:PublishProfile=NativeAot           # expérimental
dotnet publish DisplaySwitch -p:PublishProfile=FrameworkDependent  # pour comparaison
```

Le résultat va dans `publish\<variante>\DisplaySwitch.exe`. Pour Native AOT, si la publication échoue avec `'vswhere.exe' n'est pas reconnu`, ajouter son dossier au PATH avant de publier :

```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
```

## Taille et distribution

Mesuré le 11/10/2026 sur la machine de développement (SDK 10.0.401, runtime 10.0.12) :

| Version | Fichier | Taille | À installer sur le PC cible |
| --- | --- | --- | --- |
| Rust + egui | `DisplaySwitch.exe` | 5 006 848 o (4,8 Mo) | rien |
| Python + PySide6 | `DisplaySwitch.exe` | 46 741 737 o (44,6 Mo) | rien |
| C# Native AOT | `DisplaySwitch.exe` | 18 123 264 o (17,3 Mo) | rien |
| C# self-contained | `DisplaySwitch.exe` | 51 652 341 o (49,3 Mo) | rien |
| C# framework-dependent | `DisplaySwitch.exe` | 217 285 o (0,2 Mo) | .NET Desktop Runtime 10 |

Dans les trois cas C#, **un seul fichier**, sans DLL ni dossier à côté (hors `config.json`). Les DLL utilisées sont celles de Windows : `user32`, `kernel32`, `gdi32`/`gdiplus` (dessin), `ole32`, et le runtime C universel (`api-ms-win-crt-*`), présent sur tout Windows 10/11. Aucune extraction dans `%TEMP%` n'a été observée avec la publication self-contained.

### Native AOT : possible, mais pas officiellement pris en charge

Native AOT compile tout le programme en code machine à la publication, comme Rust. Pour cela, il retire le code inutilisé (« trimming ») et interdit ce qui dépend de la réflexion à l'exécution. **WinForms n'est pas encore compatible avec le trimming** : le SDK refuse par défaut (erreur `NETSDK1175`). Le profil `NativeAot.pubxml` contourne ce refus avec la propriété interne `_SuppressWinFormsTrimError`.

Résultat sur ce projet : la publication réussit avec 6 avertissements, tous situés dans WinForms lui-même (`IL3053`, `IL3000`), aucun dans notre code. L'exe démarre, affiche la fenêtre et la boîte de configuration, et les commandes `list` et `check` fonctionnent. Mais :

- c'est un contournement non supporté par Microsoft : une fonction de WinForms qui dépend de la réflexion (glisser-déposer, presse-papiers, accessibilité, boîte d'erreur non gérée…) peut échouer seulement à l'exécution ;
- il faut les outils C++ (éditeur de liens MSVC) pour publier ;
- chaque mise à jour de .NET peut changer ce comportement.

Notre propre code est, lui, compatible AOT : appels Windows par `[LibraryImport]` (générés à la compilation) et JSON par générateur de source (`SettingsJson`), donc sans réflexion.

**Recommandation** : la publication **self-contained single-file** (49 Mo, supportée), en gardant Native AOT comme mesure de comparaison. La variante framework-dependent est minuscule mais exige d'installer le runtime .NET sur le PC cible.

Le trimming seul (sans AOT) n'est pas proposé : il bute sur la même incompatibilité WinForms.

### Démarrage et mémoire

Indicatif : temps jusqu'à l'apparition de la fenêtre et mémoire de travail, sur trois lancements sur la même machine (la fenêtre lit les écrans au démarrage).

| Version | Démarrage | Mémoire |
| --- | --- | --- |
| Rust + egui | ~340 ms | ~66 Mo |
| C# Native AOT | ~120 ms | ~28 Mo |
| C# self-contained | ~370 ms | ~111 Mo |
| C# framework-dependent | ~240 ms | ~46 Mo |

La version self-contained compressée doit décompresser le runtime embarqué au lancement, ce qui explique probablement sa mémoire plus élevée. egui dessine avec OpenGL, WinForms avec les contrôles natifs de Windows.

## Structure du projet

```
display-switch-c/
├── DisplaySwitch.slnx                 solution : regroupe les deux projets (dotnet build/test à la racine)
├── DisplaySwitch/                     l'application
│   ├── DisplaySwitch.csproj           équivalent de Cargo.toml
│   ├── Properties/PublishProfiles/    les trois façons de publier (.pubxml)
│   ├── Program.cs                     main.rs : fenêtre ou commande
│   ├── Cli.cs                         cli.rs : list, check, disable, enable
│   ├── Config.cs                      config.rs : config.json, écriture atomique
│   ├── Displays.cs                    displays.rs : liste des écrans
│   ├── Switch.cs                      switch.rs : règles de sécurité, Télétravail, Personnel
│   ├── Win.cs                         win.rs : structures et appels Windows (seul code unsafe)
│   ├── Theme.cs                       theme.rs : couleurs, tailles, boutons
│   └── MainForm.cs                    ui.rs : fenêtre et boîte « Configurer Télétravail »
└── DisplaySwitch.Tests/               les tests (en Rust, ils sont dans les mêmes fichiers)
    ├── SwitchTests.cs
    └── ConfigTests.cs
```

Un fichier C# correspond à un fichier Rust, avec les mêmes fonctions et les mêmes messages. Pour relire : `Program.cs`, `Switch.cs`, `Displays.cs`, puis `Win.cs`.

Les tests utilisent xUnit, la seule dépendance du dépôt (téléchargée par NuGet, uniquement pour les tests). L'application n'utilise que la bibliothèque standard .NET et WinForms.

Volume de code (lignes, commentaires compris) : application ~1 250 lignes en C# contre ~1 100 en Rust, tests ~230 contre ~260.

## C# et Rust : les différences qui se voient dans ce code

| Notion | Rust | C# |
| --- | --- | --- |
| Erreurs | `Result<T, String>` et `?` | exceptions : `throw`, `try`/`catch` |
| Absence de valeur | `Option<u32>` | `uint?` (nullable), `null` |
| Données simples | `struct` + `#[derive(PartialEq)]` | `record` : égalité par valeur incluse |
| Copie modifiée | `Display { primary: true, ..d }` | `d with { Primary = true }` |
| Fonctions sans objet | fonctions de module | `static class` + méthodes `static` |
| Visibilité | `pub` / privé par défaut | `public` / `internal` / `private` (privé par défaut) |
| Mémoire | possession, emprunts (`&`), durées de vie | ramasse-miettes : pas d'emprunts ni de `'a` |
| Correspondance | `match` | `switch` (expression), motifs `is (var w, var h)` |
| Itérateurs | `.iter().filter().map().collect()` | LINQ : `.Where().Select().ToList()` |
| Texte formaté | `format!("{name}")` | `$"{name}"` |
| Tuples | `(String, String)` | `(string Name, string DevicePath)` (champs nommés) |
| Ressources | `Drop` automatique | `using var` : libéré à la fin du bloc |
| Code non vérifié | `unsafe { }` | `unsafe` aussi, mais seulement pour les pointeurs |

Exemple, la même règle dans les deux langages :

```rust
fn find_display<'a>(displays: &'a [Display], name: &str) -> Result<&'a Display, String> {
    let mut matches = displays.iter().filter(|display| display.name == name);
    match (matches.next(), matches.next()) {
        (None, _) => Err(format!("Moniteur introuvable : {name}")),
        (Some(_), Some(_)) => Err(format!("Plusieurs moniteurs portent le nom : {name}")),
        (Some(display), None) => Ok(display),
    }
}
```

```csharp
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
```

Point d'attention : en Rust, le type de retour oblige l'appelant à traiter l'erreur. En C#, rien ne signale qu'une méthode lève une exception ; ici, toutes les erreurs prévues sont des `InvalidOperationException`, attrapées par la fenêtre (`ShowResult`) et par la CLI (`Cli.Run`) qui affichent `Erreur : …`.

## Comment les API Windows sont appelées

Tout est dans `Win.cs`, comme `win.rs` en Rust. Trois étapes :

1. **Recopier les structures Windows** en C#, champ par champ, dans le même ordre que dans les en-têtes C (`wingdi.h`). Pour une union, `[StructLayout(LayoutKind.Explicit)]` et `[FieldOffset]` placent les champs à la main : `DisplayConfigModeInfo` décrit le mode source à l'octet 16. Les tampons de texte fixes (`WCHAR[64]`) deviennent `fixed char …[64]`, ce qui demande `unsafe`.
2. **Déclarer les fonctions** de `user32.dll` avec `[LibraryImport]`. Le compilateur génère le code d'appel (le crate `windows` de Rust fournit ces déclarations toutes faites ; ici, elles sont écrites à la main) :

   ```csharp
   [LibraryImport("user32.dll")]
   private static partial int GetDisplayConfigBufferSizes(
       uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);
   ```

   `out` et `ref` passent l'adresse d'une variable ; un tableau `DisplayConfigPathInfo[]` est passé comme pointeur vers son premier élément. `[assembly: DisableRuntimeMarshalling]` demande à .NET de transmettre les structures octet pour octet, sans conversion.
3. **Traduire les codes d'erreur** : `new Win32Exception(code).Message` donne le texte Windows dans la langue du système (équivalent de `HRESULT::message()`).

Différence avec Rust : en C#, appeler une fonction native ou lire une union n'exige pas de bloc `unsafe`. Seuls les pointeurs (ici la lecture des tampons de texte) en demandent un. La sûreté repose donc davantage sur la relecture : une erreur de taille ou d'ordre de champ dans une structure ne se voit qu'à l'exécution.

## Différences avec la version Rust

Fonctionnellement identique ; les tests reprennent ceux de Rust un par un. Écarts d'interface, liés à WinForms :

- Police Segoe UI (police de Windows 11) au lieu d'Ubuntu Light, intégrée à egui.
- Boutons à coins carrés (bordure plate WinForms) ; le bloc des écrans garde ses coins arrondis.
- La boîte « Configurer Télétravail » est une vraie fenêtre Windows : Échap et la croix valent Annuler, mais un clic à côté ne la ferme pas.
- Des noms d'écrans très longs sont coupés dans la boîte de configuration au lieu d'aller à la ligne.

Comme en Rust, les appels Windows sont synchrones : la fenêtre peut rester figée un court instant pendant une bascule.

## Références

- [QueryDisplayConfig](https://learn.microsoft.com/fr-fr/windows/win32/api/winuser/nf-winuser-querydisplayconfig), [SetDisplayConfig](https://learn.microsoft.com/fr-fr/windows/win32/api/winuser/nf-winuser-setdisplayconfig), [DisplayConfigGetDeviceInfo](https://learn.microsoft.com/fr-fr/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo)
- [Appels natifs avec LibraryImport](https://learn.microsoft.com/fr-fr/dotnet/standard/native-interop/pinvoke-source-generation)
- [Native AOT](https://learn.microsoft.com/fr-fr/dotnet/core/deploying/native-aot/)
- [Publication single-file](https://learn.microsoft.com/fr-fr/dotnet/core/deploying/single-file/overview)
