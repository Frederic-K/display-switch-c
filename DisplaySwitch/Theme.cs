// Apparence de la fenêtre, d'après le design system « Display Switch » :
// un thème clair et neutre, une seule famille de police, pas d'icône.
// Les tailles sont en pixels logiques (à 100 %) ; WinForms les adapte à la mise à l'échelle.

namespace DisplaySwitch;

static class Theme
{
    // Couleurs.
    public static readonly Color Bg = Color.FromArgb(0xf8, 0xf8, 0xf8);
    public static readonly Color Surface = Color.FromArgb(0xff, 0xff, 0xff);
    public static readonly Color Border = Color.FromArgb(0xde, 0xde, 0xde);
    public static readonly Color BorderControl = Color.FromArgb(0x8c, 0x8c, 0x8c);
    public static readonly Color Ink = Color.FromArgb(0x1f, 0x1f, 0x1f);
    public static readonly Color InkMuted = Color.FromArgb(0x5c, 0x5c, 0x5c);
    static readonly Color Control = Color.FromArgb(0xff, 0xff, 0xff);
    static readonly Color ControlHover = Color.FromArgb(0xf0, 0xf0, 0xf0);
    static readonly Color ControlActive = Color.FromArgb(0xe4, 0xe4, 0xe4);
    public static readonly Color Success = Color.FromArgb(0x1e, 0x7b, 0x34);

    // Espacements, rayons et hauteurs.
    public const int Space1 = 4;
    public const int Space2 = 8;
    public const int Space3 = 12;
    public const int Space4 = 16;
    public const int RadiusLg = 8;
    public const int ControlLg = 36;
    public const int ControlSm = 28;
    public const int DotSize = 8;

    // Tailles de texte.
    public const float HeadingSize = 18;
    public const float BodySize = 13;
    public const float SmallSize = 12;
    public const float MonitorNameSize = 14;
    public const float ButtonLgSize = 14;
    public const float ButtonSize = 13;

    // Segoe UI : la police de Windows 11 (la version Rust utilise Ubuntu Light, intégrée à egui).
    const string FontFamily = "Segoe UI";

    // Police à une taille en pixels logiques, convertie en points (1 px = 0,75 pt à 100 %)
    // pour que WinForms l'agrandisse avec la mise à l'échelle de Windows.
    public static Font Font(float pixels) => new(FontFamily, pixels * 0.75f, GraphicsUnit.Point);

    // Police en pixels réels, pour le dessin à la main (scale = mise à l'échelle de l'écran).
    public static Font PixelFont(float pixels, float scale) => new(FontFamily, pixels * scale, GraphicsUnit.Pixel);

    // Bouton plat : repos, survol, pressé.
    public static Button Button(string text, float textSize)
    {
        var button = new Button
        {
            Text = text,
            Font = Font(textSize),
            ForeColor = Ink,
            BackColor = Control,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AutoEllipsis = true,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderColor = BorderControl;
        button.FlatAppearance.MouseOverBackColor = ControlHover;
        button.FlatAppearance.MouseDownBackColor = ControlActive;
        return button;
    }

    // Deux boutons de même largeur sur toute la ligne, séparés de Space2.
    public static TableLayoutPanel ButtonRow(int height, Button left, Button right)
    {
        var row = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Height = height,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Margin = Padding.Empty,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        right.Margin = new Padding(Space2, 0, 0, 0);
        row.Controls.Add(left, 0, 0);
        row.Controls.Add(right, 1, 0);
        return row;
    }

    // Texte simple, sans retour à la ligne automatique.
    public static Label Label(string text, float size, Color color) => new()
    {
        Text = text,
        Font = Font(size),
        ForeColor = color,
        AutoSize = true,
        Margin = Padding.Empty,
    };
}
