// Fenêtre WinForms : état des écrans, Télétravail, Personnel, Actualiser et Configurer.
// L'apparence (couleurs, tailles, espacements) vient de Theme.cs.
// Contrairement à egui, WinForms garde les contrôles d'un affichage à l'autre :
// on modifie leurs propriétés, et Windows ne redessine que ce qui a changé.

namespace DisplaySwitch;

using static Theme;

sealed class MainForm : Form
{
    const string WindowTitle = "Display Switch";
    // Taille intérieure : 420 pixels de large, hauteur prévue pour deux écrans et un message.
    static readonly Size WindowSize = new(420, 300);
    // Taille intérieure de la boîte « Configurer Télétravail » : 340 de contenu plus les marges.
    static readonly Size DialogSize = new(372, 186);

    // Écrans lus à la dernière actualisation, ou message d'erreur de lecture.
    List<Display> displays = [];
    string? displaysError;

    readonly Panel monitorPanel;
    readonly Label messageLabel;

    public MainForm()
    {
        SuspendLayout();
        // Les tailles ci-dessous sont données pour 96 ppp (100 %) ; WinForms les adapte à l'écran.
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = WindowTitle;
        ClientSize = WindowSize;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Bg;
        ForeColor = InkMuted;
        Font = Theme.Font(BodySize);

        // Bloc des écrans, dessiné à la main dans PaintMonitors.
        monitorPanel = new Panel
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 0, 0, Space3),
        };
        monitorPanel.Paint += (_, e) => PaintMonitors(e);
        monitorPanel.Resize += (_, _) => UpdateMonitorPanel();
        monitorPanel.DpiChangedAfterParent += (_, _) => UpdateMonitorPanel();

        // Ligne d'état centrée ; vide, elle garde sa place pour que rien ne bouge.
        messageLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopCenter,
            Margin = new Padding(0, 0, 0, Space3),
        };

        Button telework = Theme.Button("Télétravail", ButtonLgSize);
        Button personal = Theme.Button("Personnel", ButtonLgSize);
        Button refresh = Theme.Button("Actualiser", ButtonSize);
        Button configure = Theme.Button("Configurer Télétravail…", ButtonSize);
        telework.Click += (_, _) => RunMode(settings => Switch.DisableSecondary(settings, dryRun: false));
        personal.Click += (_, _) => RunMode(Switch.EnableSecondary);
        refresh.Click += (_, _) => RefreshDisplays();
        configure.Click += (_, _) => StartConfiguration();

        TableLayoutPanel modeButtons = ButtonRow(ControlLg, telework, personal);
        modeButtons.Margin = new Padding(0, 0, 0, Space3);

        Label title = Theme.Label(WindowTitle, HeadingSize, Ink);
        title.Margin = new Padding(0, 0, 0, Space3);

        // Les blocs s'empilent de haut en bas ; la ligne d'état prend la place restante.
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(Space4),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (SizeType rowSize in new[] { SizeType.AutoSize, SizeType.AutoSize, SizeType.AutoSize, SizeType.Percent, SizeType.AutoSize })
        {
            layout.RowStyles.Add(new RowStyle(rowSize, 100));
        }
        layout.Controls.Add(title);
        layout.Controls.Add(monitorPanel);
        layout.Controls.Add(modeButtons);
        layout.Controls.Add(messageLabel);
        layout.Controls.Add(ButtonRow(ControlSm, refresh, configure));
        Controls.Add(layout);

        ResumeLayout();
    }

    // Après la mise à l'échelle de WinForms : taille minimale, puis première lecture des écrans.
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = Size;
        RefreshDisplays();
    }

    void RefreshDisplays()
    {
        try
        {
            displays = Displays.ReadDisplays().Displays;
            displaysError = null;
        }
        catch (InvalidOperationException error)
        {
            displays = [];
            displaysError = error.Message;
        }
        UpdateMonitorPanel();
    }

    // Résultat de la dernière action : information, ou erreur préfixée.
    void ShowResult(Func<string> action)
    {
        try
        {
            messageLabel.Text = action();
        }
        catch (InvalidOperationException error)
        {
            messageLabel.Text = $"Erreur : {error.Message}";
        }
        RefreshDisplays();
    }

    // Lire la configuration, lancer le mode demandé, puis relire les écrans.
    void RunMode(Func<Settings, string> mode) => ShowResult(() => mode(Config.Load()));

    // Proposer les rôles détectés dans Windows, puis enregistrer si l'utilisateur confirme.
    void StartConfiguration()
    {
        Settings proposal;
        try
        {
            proposal = Switch.DetectTeleworkConfig();
        }
        catch (InvalidOperationException error)
        {
            messageLabel.Text = $"Erreur : {error.Message}";
            RefreshDisplays();
            return;
        }

        RefreshDisplays();
        if (ConfirmConfiguration(this, proposal))
        {
            ShowResult(() => SaveConfiguration(proposal));
        }
    }

    // Revérifier Windows au moment d'enregistrer : les rôles ont pu changer entre-temps.
    // Deux records sont égaux quand tous leurs champs le sont.
    static string SaveConfiguration(Settings proposal)
    {
        if (Switch.DetectTeleworkConfig() != proposal)
        {
            throw new InvalidOperationException(
                "La configuration Windows a changé. Rouvrez « Configurer Télétravail… ».");
        }
        Config.Save(proposal);
        return "Configuration Télétravail enregistrée.";
    }

    // Adapter la hauteur du bloc des écrans à son contenu, puis le redessiner.
    void UpdateMonitorPanel()
    {
        monitorPanel.Height = LayoutMonitors(null, monitorPanel.Width);
        monitorPanel.Invalidate();
    }

    // Dessiner d'abord dans une image en mémoire, puis l'afficher d'un coup :
    // le bloc apparaît toujours complet, sans scintillement.
    void PaintMonitors(PaintEventArgs e)
    {
        using BufferedGraphics buffer = BufferedGraphicsManager.Current.Allocate(e.Graphics, monitorPanel.ClientRectangle);
        buffer.Graphics.Clear(Bg);
        LayoutMonitors(buffer.Graphics, monitorPanel.Width);
        buffer.Render(e.Graphics);
    }

    // Placer le contenu du bloc des écrans, le dessiner si `graphics` est fourni,
    // et renvoyer la hauteur nécessaire. Une seule fonction pour mesurer et dessiner.
    int LayoutMonitors(Graphics? graphics, int width)
    {
        // Le dessin se fait en pixels réels : tout est multiplié par la mise à l'échelle.
        float scale = monitorPanel.DeviceDpi / 96f;
        int Scaled(int pixels) => (int)Math.Round(pixels * scale);

        using Font nameFont = PixelFont(MonitorNameSize, scale);
        using Font bodyFont = PixelFont(BodySize, scale);
        using Font smallFont = PixelFont(SmallSize, scale);
        const TextFormatFlags SingleLine = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;

        int left = Scaled(Space3);
        int right = width - Scaled(Space3);
        int y = Scaled(Space2);

        if (graphics is not null)
        {
            // Fond blanc à coins arrondis et bordure claire.
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = new RectangleF(0.5f, 0.5f, width - 1, monitorPanel.Height - 1);
            var radius = new SizeF(Scaled(RadiusLg), Scaled(RadiusLg));
            using var surface = new SolidBrush(Surface);
            using var border = new Pen(Border);
            graphics.FillRoundedRectangle(surface, bounds, radius);
            graphics.DrawRoundedRectangle(border, bounds, radius);
        }

        if (displaysError is not null || displays.Count == 0)
        {
            string text = displaysError is null
                ? "Aucun écran disponible."
                : $"Lecture des écrans impossible : {displaysError}";
            const TextFormatFlags Wrapped = TextFormatFlags.NoPadding | TextFormatFlags.WordBreak;
            Size size = TextRenderer.MeasureText(text, bodyFont, new Size(right - left, 0), Wrapped);
            if (graphics is not null)
            {
                TextRenderer.DrawText(graphics, text, bodyFont, new Rectangle(left, y, right - left, size.Height), InkMuted, Wrapped);
            }
            return y + size.Height + Scaled(Space2);
        }

        int nameHeight = TextRenderer.MeasureText("Ag", nameFont, Size.Empty, TextFormatFlags.NoPadding).Height;
        int detailHeight = TextRenderer.MeasureText("Ag", bodyFont, Size.Empty, TextFormatFlags.NoPadding).Height;
        int gap = Scaled(Space1);
        int rowHeight = nameHeight + gap + detailHeight;

        for (int index = 0; index < displays.Count; index++)
        {
            Display display = displays[index];

            // Trait de séparation entre deux écrans.
            if (index > 0)
            {
                y += Scaled(Space2);
                if (graphics is not null)
                {
                    using var line = new Pen(Border);
                    graphics.DrawLine(line, left, y, right, y);
                }
                y += Scaled(Space2);
            }

            // État à droite : point vert « Actif » ou anneau gris « Inactif », toujours avec son mot.
            string state = display.Active ? "Actif" : "Inactif";
            Size stateSize = TextRenderer.MeasureText(state, smallFont, Size.Empty, TextFormatFlags.NoPadding);
            int stateLeft = right - stateSize.Width;
            int dotSize = Scaled(DotSize);
            int dotLeft = stateLeft - gap - dotSize;
            int textRight = dotLeft - Scaled(Space2);

            if (graphics is not null)
            {
                int center = y + rowHeight / 2;
                TextRenderer.DrawText(graphics, state, smallFont, new Point(stateLeft, center - stateSize.Height / 2), InkMuted, TextFormatFlags.NoPadding);
                var dot = new Rectangle(dotLeft, center - dotSize / 2, dotSize, dotSize);
                if (display.Active)
                {
                    using var fill = new SolidBrush(Success);
                    graphics.FillEllipse(fill, dot);
                }
                else
                {
                    using var ring = new Pen(BorderControl);
                    graphics.DrawEllipse(ring, dot);
                }

                // Nom et détail à gauche.
                TextRenderer.DrawText(graphics, Displays.DisplayName(display), nameFont,
                    new Rectangle(left, y, textRight - left, nameHeight), Ink, SingleLine);
                TextRenderer.DrawText(graphics, Detail(display), bodyFont,
                    new Rectangle(left, y + nameHeight + gap, textRight - left, detailHeight), InkMuted, SingleLine);
            }

            y += rowHeight;
        }

        return y + Scaled(Space2);
    }

    // Détail d'un écran sous son nom : résolution et rôle.
    static string Detail(Display display)
    {
        if (display.Active && display.Resolution is (var width, var height))
        {
            string role = display.Primary ? "Principal" : "Secondaire";
            return $"{width} × {height} — {role}";
        }
        return "Résolution indisponible";
    }

    // Boîte modale « Configurer Télétravail » : true pour Enregistrer, false pour Annuler.
    // Échap et la croix de fermeture valent Annuler.
    static bool ConfirmConfiguration(IWin32Window owner, Settings proposal)
    {
        // Même construction que la fenêtre principale : tailles pour 96 ppp, adaptées par WinForms
        // à la reprise de la mise en page (ResumeLayout).
        using var dialog = new Form();
        dialog.SuspendLayout();
        dialog.AutoScaleDimensions = new SizeF(96F, 96F);
        dialog.AutoScaleMode = AutoScaleMode.Dpi;
        dialog.Text = WindowTitle;
        dialog.ClientSize = DialogSize;
        dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
        dialog.MaximizeBox = false;
        dialog.MinimizeBox = false;
        dialog.ShowInTaskbar = false;
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.BackColor = Surface;
        dialog.ForeColor = InkMuted;
        dialog.Font = Theme.Font(BodySize);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Padding = new Padding(Space4),
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Titre, puis lignes de texte avec un espace après certaines.
        (string Text, float Size, Color Color, int SpaceAfter)[] lines =
        [
            ("Configurer Télétravail", HeadingSize, Ink, Space2),
            ($"Écran à conserver : {proposal.PrimaryMonitor}", BodySize, InkMuted, 0),
            ($"Écran à désactiver : {proposal.SecondaryMonitor}", BodySize, InkMuted, Space2),
            ("Ces réglages seront utilisés par le bouton Télétravail.", BodySize, InkMuted, 0),
            ("Aucun changement d'affichage ne sera appliqué.", BodySize, InkMuted, Space3),
        ];
        foreach (var (text, size, color, spaceAfter) in lines)
        {
            Label label = Theme.Label(text, size, color);
            label.Margin = new Padding(0, 0, 0, spaceAfter);
            content.Controls.Add(label);
        }

        Button save = Theme.Button("Enregistrer", ButtonSize);
        Button cancel = Theme.Button("Annuler", ButtonSize);
        save.DialogResult = DialogResult.OK;
        cancel.DialogResult = DialogResult.Cancel;
        dialog.CancelButton = cancel;
        content.Controls.Add(ButtonRow(ControlSm, save, cancel));
        dialog.Controls.Add(content);
        dialog.ResumeLayout();

        return dialog.ShowDialog(owner) == DialogResult.OK;
    }
}
