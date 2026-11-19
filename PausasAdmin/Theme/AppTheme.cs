using System;
using System.Drawing;

namespace PausasAdmin.Theme;

/// <summary>
/// Paleta centralizada de colores y fuentes corporativas para toda la aplicación de administración.
/// </summary>
public static class AppTheme
{
    // === Fondos ===
    public static readonly Color SidebarBg       = Color.FromArgb(30, 41, 59);    // #1E293B  Slate 800
    public static readonly Color MainBg          = Color.FromArgb(248, 250, 252);  // #F8FAFC  Slate 50
    public static readonly Color CardBg          = Color.FromArgb(255, 255, 255);  // #FFFFFF
    public static readonly Color CardBorder      = Color.FromArgb(226, 232, 240);  // #E2E8F0  Slate 200
    public static readonly Color InputBg         = Color.FromArgb(248, 250, 252);  // #F8FAFC
    public static readonly Color InputBorder     = Color.FromArgb(203, 213, 225);  // #CBD5E1  Slate 300

    // === Texto ===
    public static readonly Color TextPrimary     = Color.FromArgb(15, 23, 42);     // #0F172A  Slate 900
    public static readonly Color TextSecondary   = Color.FromArgb(100, 116, 139);  // #64748B  Slate 500
    public static readonly Color TextOnDark      = Color.FromArgb(248, 250, 252);  // #F8FAFC
    public static readonly Color TextMuted       = Color.FromArgb(148, 163, 184);  // #94A3B8  Slate 400

    // === Acciones ===
    public static readonly Color Accent          = Color.FromArgb(37, 99, 235);    // #2563EB  Blue 600
    public static readonly Color AccentHover     = Color.FromArgb(29, 78, 216);    // #1D4ED8  Blue 700
    public static readonly Color Danger          = Color.FromArgb(220, 38, 38);    // #DC2626  Red 600
    public static readonly Color DangerHover     = Color.FromArgb(185, 28, 28);    // #B91C1C  Red 700
    public static readonly Color Success         = Color.FromArgb(22, 163, 74);    // #16A34A  Green 600
    public static readonly Color SuccessLight    = Color.FromArgb(220, 252, 231);  // #DCFCE7  Green 100
    public static readonly Color Warning         = Color.FromArgb(234, 179, 8);    // #EAB308  Yellow 500
    public static readonly Color WarningLight    = Color.FromArgb(254, 249, 195);  // #FEF9C3  Yellow 100

    // === Sidebar navigation ===
    public static readonly Color SidebarItemHover  = Color.FromArgb(51, 65, 85);   // #334155  Slate 700
    public static readonly Color SidebarItemActive = Color.FromArgb(37, 99, 235);  // #2563EB  Blue 600

    // === Fuentes ===
    public static readonly Font TitleFont        = new("Segoe UI", 16f, FontStyle.Bold);
    public static readonly Font SubtitleFont     = new("Segoe UI", 14f, FontStyle.Bold);
    public static readonly Font HeadingFont      = new("Segoe UI", 12f, FontStyle.Bold);
    public static readonly Font BodyFont         = new("Segoe UI", 10f, FontStyle.Regular);
    public static readonly Font BodyBoldFont     = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font SmallFont        = new("Segoe UI", 9f, FontStyle.Regular);
    public static readonly Font SmallBoldFont    = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font ButtonFont       = new("Segoe UI", 9.5f, FontStyle.Bold);

    /// <summary>
    /// Aplica el estilo plano corporativo a un botón de acción primaria.
    /// </summary>
    public static void EstilizarBotonPrimario(System.Windows.Forms.Button btn)
    {
        btn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Accent;
        btn.ForeColor = TextOnDark;
        btn.Font = ButtonFont;
        btn.Cursor = System.Windows.Forms.Cursors.Hand;
        btn.Height = 36;

        btn.MouseEnter += (s, e) => btn.BackColor = AccentHover;
        btn.MouseLeave += (s, e) => btn.BackColor = Accent;
    }

    /// <summary>
    /// Aplica el estilo plano corporativo a un botón de peligro / eliminación.
    /// </summary>
    public static void EstilizarBotonPeligro(System.Windows.Forms.Button btn)
    {
        btn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        btn.BackColor = Danger;
        btn.ForeColor = TextOnDark;
        btn.Font = ButtonFont;
        btn.Cursor = System.Windows.Forms.Cursors.Hand;
        btn.Height = 36;

        btn.MouseEnter += (s, e) => btn.BackColor = DangerHover;
        btn.MouseLeave += (s, e) => btn.BackColor = Danger;
    }

    /// <summary>
    /// Aplica el estilo plano corporativo a un botón secundario.
    /// </summary>
    public static void EstilizarBotonSecundario(System.Windows.Forms.Button btn)
    {
        btn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = CardBorder;
        btn.FlatAppearance.BorderSize = 1;
        btn.BackColor = CardBg;
        btn.ForeColor = TextPrimary;
        btn.Font = ButtonFont;
        btn.Cursor = System.Windows.Forms.Cursors.Hand;
        btn.Height = 36;

        btn.MouseEnter += (s, e) => btn.BackColor = MainBg;
        btn.MouseLeave += (s, e) => btn.BackColor = CardBg;
    }

    /// <summary>
    /// Estiliza un DataGridView con la paleta corporativa y formato moderno sin parpadeos.
    /// </summary>
    public static void EstilizarDataGridView(System.Windows.Forms.DataGridView dgv)
    {
        dgv.BorderStyle = System.Windows.Forms.BorderStyle.None;
        dgv.BackgroundColor = CardBg;
        dgv.GridColor = CardBorder;
        dgv.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
        dgv.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        dgv.MultiSelect = false;
        dgv.ReadOnly = true;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.AllowUserToResizeRows = false;
        dgv.RowHeadersVisible = false;
        dgv.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
        dgv.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        dgv.ColumnHeadersHeight = 40;

        dgv.EnableHeadersVisualStyles = false;
        dgv.ColumnHeadersDefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle
        {
            BackColor = MainBg,
            ForeColor = TextSecondary,
            Font = SmallBoldFont,
            SelectionBackColor = MainBg,
            SelectionForeColor = TextSecondary,
            Padding = new System.Windows.Forms.Padding(8, 0, 0, 0),
            Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft
        };

        dgv.DefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle
        {
            BackColor = CardBg,
            ForeColor = TextPrimary,
            Font = BodyFont,
            SelectionBackColor = Color.FromArgb(219, 234, 254), // Blue 100
            SelectionForeColor = TextPrimary,
            Padding = new System.Windows.Forms.Padding(8, 4, 4, 4)
        };

        dgv.AlternatingRowsDefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle
        {
            BackColor = MainBg,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(219, 234, 254),
            SelectionForeColor = TextPrimary
        };

        dgv.RowTemplate.Height = 38;

        // DoubleBuffered para evitar parpadeo
        typeof(System.Windows.Forms.DataGridView)
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(dgv, true);
    }
}
