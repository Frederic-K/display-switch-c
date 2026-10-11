// Appels aux API Windows d'affichage, et structures Windows correspondantes.
// C'est le seul fichier du programme qui contient du code `unsafe`.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Les structures sont transmises telles quelles à Windows, octet pour octet,
// sans conversion par .NET : c'est le comportement le plus simple et compatible Native AOT.
[assembly: DisableRuntimeMarshalling]

namespace DisplaySwitch;

// Identifiant d'un adaptateur graphique (LUID). Un `record struct` sait se comparer avec ==.
readonly record struct Luid(uint LowPart, int HighPart);

// Côté source d'un chemin d'affichage (DISPLAYCONFIG_PATH_SOURCE_INFO).
struct DisplayConfigPathSourceInfo
{
    public Luid AdapterId;
    public uint Id;
    // Union Windows : sans QDC_VIRTUAL_MODE_AWARE, elle contient l'index du mode source.
    public uint ModeInfoIdx;
    public uint StatusFlags;
}

// Côté cible (le moniteur) d'un chemin d'affichage (DISPLAYCONFIG_PATH_TARGET_INFO).
struct DisplayConfigPathTargetInfo
{
    public Luid AdapterId;
    public uint Id;
    public uint ModeInfoIdx;
    public uint OutputTechnology;
    public uint Rotation;
    public uint Scaling;
    public uint RefreshRateNumerator;
    public uint RefreshRateDenominator;
    public uint ScanLineOrdering;
    // BOOL Windows : entier 32 bits, 0 = faux.
    public int TargetAvailable;
    public uint StatusFlags;
}

// Un chemin source → cible (DISPLAYCONFIG_PATH_INFO).
struct DisplayConfigPathInfo
{
    public DisplayConfigPathSourceInfo SourceInfo;
    public DisplayConfigPathTargetInfo TargetInfo;
    public uint Flags;
}

// Mode source : résolution et position sur le bureau (DISPLAYCONFIG_SOURCE_MODE).
struct DisplayConfigSourceMode
{
    public uint Width;
    public uint Height;
    public uint PixelFormat;
    public int PositionX;
    public int PositionY;
}

// Un mode d'affichage (DISPLAYCONFIG_MODE_INFO), 64 octets.
// À partir de l'octet 16, Windows range une union : mode cible, mode source ou image du bureau.
// Seul le mode source est décrit ici ; il n'a de sens que si InfoType vaut « source ».
[StructLayout(LayoutKind.Explicit, Size = 64)]
struct DisplayConfigModeInfo
{
    [FieldOffset(0)] public uint InfoType;
    [FieldOffset(4)] public uint Id;
    [FieldOffset(8)] public Luid AdapterId;
    [FieldOffset(16)] public DisplayConfigSourceMode SourceMode;
}

// Demande du nom d'un moniteur (DISPLAYCONFIG_TARGET_DEVICE_NAME), en-tête compris.
unsafe struct DisplayConfigTargetDeviceName
{
    // En-tête DISPLAYCONFIG_DEVICE_INFO_HEADER.
    public uint Type;
    public uint Size;
    public Luid AdapterId;
    public uint Id;
    // Réponse de Windows.
    public uint Flags;
    public uint OutputTechnology;
    public ushort EdidManufactureId;
    public ushort EdidProductCodeId;
    public uint ConnectorInstance;
    public fixed char MonitorFriendlyDeviceName[64];
    public fixed char MonitorDevicePath[128];
}

// Chemins et modes lus ensemble : les index de mode des chemins
// ne sont valables que dans le tableau Modes de la même lecture.
record Snapshot(DisplayConfigPathInfo[] Paths, DisplayConfigModeInfo[] Modes);

// Valider seulement, ou appliquer réellement une configuration.
enum DisplayAction
{
    Validate,
    Apply,
}

static partial class Win
{
    // Constantes Windows (wingdi.h, winuser.h, winerror.h).
    public const uint DisplayConfigPathActive = 0x1;
    const uint QdcAllPaths = 0x1;
    const uint SdcTopologyExtend = 0x4;
    const uint SdcUseSuppliedDisplayConfig = 0x20;
    const uint SdcValidate = 0x40;
    const uint SdcApply = 0x80;
    const uint PathModeIdxInvalid = 0xFFFFFFFF;
    const uint ModeInfoTypeSource = 1;
    const uint DeviceInfoGetTargetName = 2;
    const int ErrorSuccess = 0;
    const int ErrorInsufficientBuffer = 122;

    // Nombre de lectures tentées si la configuration change entre deux appels.
    const int QueryAttempts = 3;

    // Déclarations des fonctions de user32.dll. [LibraryImport] fait générer le code
    // d'appel à la compilation (pas de réflexion), ce qui le rend compatible Native AOT.
    [LibraryImport("user32.dll")]
    private static partial int GetDisplayConfigBufferSizes(
        uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [LibraryImport("user32.dll")]
    private static partial int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        nint currentTopologyId);

    [LibraryImport("user32.dll")]
    private static partial int SetDisplayConfig(
        uint numPathArrayElements,
        DisplayConfigPathInfo[]? pathArray,
        uint numModeInfoArrayElements,
        DisplayConfigModeInfo[]? modeInfoArray,
        uint flags);

    [LibraryImport("user32.dll")]
    private static partial int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName requestPacket);

    static uint Flag(DisplayAction action) => action == DisplayAction.Apply ? SdcApply : SdcValidate;

    // Transformer un code d'erreur Windows en exception au message lisible (traduit par Windows).
    static void CheckCode(int code, string call)
    {
        if (code != ErrorSuccess)
        {
            string message = new Win32Exception(code).Message;
            throw new InvalidOperationException($"{call} a échoué : {message} (code {code})");
        }
    }

    // Lire tous les chemins d'affichage, actifs ou non, avec leurs modes.
    // Recommencer si Windows signale que la capacité a changé entre les deux appels.
    public static Snapshot QueryAllPaths()
    {
        for (int attempt = 0; attempt < QueryAttempts; attempt++)
        {
            CheckCode(
                GetDisplayConfigBufferSizes(QdcAllPaths, out uint pathCount, out uint modeCount),
                "GetDisplayConfigBufferSizes");

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];

            // Chaque tableau contient exactement le nombre d'éléments annoncé à Windows.
            int code = QueryDisplayConfig(QdcAllPaths, ref pathCount, paths, ref modeCount, modes, 0);
            if (code == ErrorInsufficientBuffer)
            {
                continue;
            }
            CheckCode(code, "QueryDisplayConfig");

            // Windows indique combien d'éléments il a réellement remplis.
            return new Snapshot(paths[..(int)pathCount], modes[..(int)modeCount]);
        }

        throw new InvalidOperationException("La configuration des écrans change en permanence. Réessayez.");
    }

    // Index du mode source d'un chemin, ou null s'il n'y en a pas.
    public static uint? SourceModeIndex(DisplayConfigPathInfo path)
    {
        uint index = path.SourceInfo.ModeInfoIdx;
        return index == PathModeIdxInvalid ? null : index;
    }

    // Mode source (résolution et position sur le bureau) désigné par un index.
    public static DisplayConfigSourceMode? SourceMode(DisplayConfigModeInfo[] modes, uint index)
    {
        if (index >= modes.Length || modes[index].InfoType != ModeInfoTypeSource)
        {
            return null;
        }
        // InfoType vient d'être vérifié : l'union contient bien un mode source.
        return modes[index].SourceMode;
    }

    // Nom lisible et chemin de périphérique du moniteur relié à une cible.
    public static unsafe (string Name, string DevicePath) MonitorIdentity(DisplayConfigPathTargetInfo target)
    {
        var request = new DisplayConfigTargetDeviceName
        {
            Type = DeviceInfoGetTargetName,
            Size = (uint)sizeof(DisplayConfigTargetDeviceName),
            AdapterId = target.AdapterId,
            Id = target.Id,
        };

        // Windows remplit la structure complète, dont la taille est indiquée dans Size.
        CheckCode(DisplayConfigGetDeviceInfo(ref request), "DisplayConfigGetDeviceInfo");

        return (
            Utf16ToString(request.MonitorFriendlyDeviceName, 64),
            Utf16ToString(request.MonitorDevicePath, 128));
    }

    // Convertir un tampon de texte Windows (UTF-16 terminé par un zéro) en string.
    static unsafe string Utf16ToString(char* text, int capacity)
    {
        var span = new ReadOnlySpan<char>(text, capacity);
        int end = span.IndexOf('\0');
        return new string(end < 0 ? span : span[..end]);
    }

    // Valider ou appliquer exactement les chemins et modes fournis.
    public static void SetSuppliedConfig(
        DisplayConfigPathInfo[] paths, DisplayConfigModeInfo[] modes, DisplayAction action)
    {
        int code = SetDisplayConfig(
            (uint)paths.Length, paths, (uint)modes.Length, modes,
            SdcUseSuppliedDisplayConfig | Flag(action));
        CheckCode(code, "SetDisplayConfig");
    }

    // Valider ou appliquer la dernière topologie étendue mémorisée par Windows.
    public static void SetExtendTopology(DisplayAction action)
    {
        // Aucun tableau transmis : Windows utilise sa propre base de configuration.
        CheckCode(SetDisplayConfig(0, null, 0, null, SdcTopologyExtend | Flag(action)), "SetDisplayConfig");
    }
}
