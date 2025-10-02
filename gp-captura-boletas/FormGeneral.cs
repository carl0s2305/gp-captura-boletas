using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    public partial class FormGeneral : Form
    {
        // ===== Paleta =====
        private static readonly Color C_BG = Color.FromArgb(244, 247, 247);  // #F4F7F7
        private static readonly Color C_ACCENT = Color.FromArgb(121, 168, 169);  // #79A8A9
        private static readonly Color C_PRIMARY = Color.FromArgb(31, 78, 95);     // #1F4E5F
        private static readonly Color C_TXT_DIM = Color.FromArgb(70, 84, 94);

        private static Font MakeFont(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font("Aptos", size, style, GraphicsUnit.Point); }
            catch { return new Font("Segoe UI", size, style, GraphicsUnit.Point); }
        }

        // ===== Layout raíz =====
        private Panel header;   // barra superior
        private Panel canvas;   // aquí pondremos los tiles

        // ===== Cabecera =====
        private Label lblTitle;
        private Label lblUser;
        private Button btnLogout;

        // ===== Config. rol =====
        private enum Rol { Director, Secretaria }
        private Rol RolActual => ObtenerRol();  // Lee de tu sesión

        public FormGeneral()
        {
            // Ventana
            Text = "Sistema de Control de Calificaciones";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1020, 700);
            BackColor = C_PRIMARY;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            // ===== Layout raíz (TopBar + Canvas) =====
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = C_PRIMARY,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96)); // alto de barra
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // ===== Barra superior única =====
            var topBar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(31, 78, 95)
            };
            root.Controls.Add(topBar, 0, 0);

            // Logo a la izquierda (se hace más alto con la barra)
            var picLogo = new PictureBox
            {
                Image = Properties.Resources.escudo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Left,
                Width = 96,
                Margin = new Padding(12, 8, 12, 8)
            };
            topBar.Controls.Add(picLogo);

            topBar.SizeChanged += (_, __) =>
            {
                // deja 8px arriba y abajo para que respire
                int altoDisponible = topBar.Height - 16;
                picLogo.Width = Math.Max(72, altoDisponible);
            };

            // ===== Panel derecho: botón centrado arriba + usuario centrado abajo =====
            var rightStack = new TableLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 280,                     // ancho fijo para no invadir el título
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(16, 8, 16, 8)
            };
            rightStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f)); // fila del botón
            rightStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f)); // fila del usuario
            topBar.Controls.Add(rightStack);

            // contenedor para CENTRAR el botón dentro de su celda
            // Contenedor del botón
            var pnlBtn = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            pnlBtn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pnlBtn.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlBtn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // Contenedor del usuario
            var pnlUser = new TableLayoutPanel { Dock = DockStyle.Fill };
            pnlUser.ColumnCount = 3;
            pnlUser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pnlUser.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlUser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            rightStack.Controls.Add(pnlBtn, 0, 0);

            // Botón "Cerrar sesión" (centrado)
            btnLogout = new Button
            {
                Text = "Cerrar Sesión",
                AutoSize = true,
                Height = 32,
                FlatStyle = FlatStyle.Flat,
                ForeColor = C_PRIMARY,
                BackColor = Color.White,
                Padding = new Padding(12, 4, 12, 4),
                TabStop = false,
                Cursor = Cursors.Hand
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Resize += (_, __) => SetRounded(btnLogout, 16);
            btnLogout.Click += BtnLogout_Click;
            SetRounded(btnLogout, 16);
            pnlBtn.Controls.Add(btnLogout, 1, 0);


            // centrado real del botón
            pnlBtn.Resize += (_, __) =>
            {
                btnLogout.Left = (pnlBtn.ClientSize.Width - btnLogout.Width) / 2;
                btnLogout.Top = (pnlBtn.ClientSize.Height - btnLogout.Height) / 2;
            };

            // contenedor para CENTRAR el usuario dentro de su celda
            rightStack.Controls.Add(pnlUser, 0, 1);

            // Usuario (centrado)
            lblUser = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = MakeFont(11f),
                Text = $"👤 {ObtenerNombre()} ({RolActual})",
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlUser.Controls.Add(lblUser, 1, 0);

            // centrado real del label
            pnlUser.Resize += (_, __) =>
            {
                lblUser.Left = (pnlUser.ClientSize.Width - lblUser.Width) / 2;
                lblUser.Top = (pnlUser.ClientSize.Height - lblUser.Height) / 2;
            };


            // Centro: título y subtítulo (entre el logo y el panel derecho)
            var titlePanel = new Panel { Dock = DockStyle.Fill };
            topBar.Controls.Add(titlePanel);

            var lblTitulo = new Label
            {
                Text = "Sistema de Control de Calificaciones",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 20f),
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };
            titlePanel.Controls.Add(lblTitulo);

            var lblSubtitulo = new Label
            {
                Text = "Escuela Primaria Emiliano Zapata",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12f, FontStyle.Italic),
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleCenter
            };
            titlePanel.Controls.Add(lblSubtitulo);

            // Asegurar que el subtítulo quede debajo del título
            titlePanel.Controls.SetChildIndex(lblSubtitulo, 0);

            // ===== Canvas (grid de módulos) =====
            canvas = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(24)
            };
            root.Controls.Add(canvas, 0, 1);

            // Construir tiles según rol
            ConstruirTilesSegunRol();
        }

        // ==================== Rol y usuario (ajusta a tu sesión real) ====================
        private Rol ObtenerRol()
        {
            try
            {
                if (!string.IsNullOrEmpty(SesionApp.Rol))
                {
                    return SesionApp.Rol.Equals("DIRECTOR", StringComparison.OrdinalIgnoreCase)
                        ? Rol.Director
                        : Rol.Secretaria;
                }
            }
            catch { }
            return Rol.Secretaria; // fallback
        }

        private string ObtenerNombre()
        {
            try
            {
                return !string.IsNullOrEmpty(SesionApp.Nombre)
                    ? SesionApp.Nombre
                    : SesionApp.Usuario ?? "Usuario";
            }
            catch { }
            return "Usuario";
        }


        // ==================== Construcción de tiles ====================
        private void ConstruirTilesSegunRol()
        {
            canvas.SuspendLayout();
            canvas.Controls.Clear();

            // 6 opciones “máximas”
            var items = new List<(string texto, string icono, EventHandler onClick)>
            {
                ("Inscripción de Alumnos", "🧑‍🎓", OnInscripcion),
                ("Captura de Calificaciones", "📝", OnCaptura),
                ("Lista por Grupo", "📋", OnListaGrupo),
                ("Estadísticas", "📊", OnEstadisticas),
                ("Administrar Usuarios", "⚙️", OnAdminUsuarios),   // exclusivas del Director
                ("Bitácora de Eventos", "🗒️", OnBitacora)          // exclusivas del Director
            };

            if (RolActual == Rol.Secretaria)
            {
                // Filtra las exclusivas
                items = items
                    .Where(it => it.texto != "Administrar Usuarios" && it.texto != "Bitácora de Eventos")
                    .ToList();
            }

            // Grid responsive: hasta 3 columnas; con 4 se arma 2x2, con 6 se arma 3x2, etc.
            var cols = (items.Count <= 4) ? 2 : 3;
            var rows = (int)Math.Ceiling(items.Count / (double)cols);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                ColumnCount = cols,
                RowCount = rows,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            for (int c = 0; c < cols; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
            for (int r = 0; r < rows; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            canvas.Controls.Add(grid);

            foreach (var it in items)
            {
                var tile = CrearTile(it.texto, it.icono, it.onClick);
                grid.Controls.Add(tile);
            }

            canvas.ResumeLayout();
        }

        private Control CrearTile(string texto, string iconoEmojiIgnorado, EventHandler onClick)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(18),
                Padding = new Padding(18),
                MinimumSize = new Size(240, 160)
            };
            panel.Resize += (_, __) => SetRounded(panel, 14);
            panel.MouseEnter += (_, __) => panel.BackColor = Blend(Color.White, C_ACCENT, 0.08);
            panel.MouseLeave += (_, __) => panel.BackColor = Color.White;
            panel.Click += onClick;

            // Columna: ícono (centrado) | título (centrado, multilínea) | espacio | botón (centrado)
            var col = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // icono
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // título
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // spacer
            col.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));  // botón
            panel.Controls.Add(col);

            // Ícono vectorial (MDL2)
            var lblIcon = new Label
            {
                Text = GetGlyph(texto),
                AutoSize = true,
                Dock = DockStyle.Top,
                Font = IconFont(28f),
                ForeColor = C_PRIMARY,
                Margin = new Padding(0, 0, 0, 6),
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = true
            };
            lblIcon.Anchor = AnchorStyles.Top;
            lblIcon.Click += onClick;
            col.Controls.Add(lblIcon, 0, 0);

            // Título centrado con word-wrap
            var lblText = new Label
            {
                Text = texto,
                AutoSize = true,
                MaximumSize = new Size(int.MaxValue, 0),
                Dock = DockStyle.Top,
                Font = MakeFont(13f, FontStyle.Bold),
                ForeColor = C_PRIMARY,
                Margin = new Padding(0, 0, 0, 6),
                TextAlign = ContentAlignment.MiddleCenter,
                UseCompatibleTextRendering = true
            };
            lblText.Anchor = AnchorStyles.Top;
            panel.SizeChanged += (_, __) =>
            {
                int w = Math.Max(10, panel.ClientSize.Width - panel.Padding.Horizontal - 12);
                lblText.MaximumSize = new Size(w, 0);
            };
            lblText.Click += onClick;
            col.Controls.Add(lblText, 0, 1);

            // Spacer relleno
            var spacer = new Panel { Dock = DockStyle.Fill };
            spacer.Click += onClick;
            col.Controls.Add(spacer, 0, 2);

            // Botón centrado y con ancho relativo
            var btn = new Button
            {
                Text = "Abrir",
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = C_PRIMARY,
                Cursor = Cursors.Hand,
                Height = 36,
                Width = 220,                // tamaño base
                Margin = new Padding(0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            btn.Resize += (_, __) => SetRounded(btn, 10);

            // Host para centrar el botón dentro de la fila
            var btnHost = new Panel { Dock = DockStyle.Fill };
            btnHost.Controls.Add(btn);
            btn.Anchor = AnchorStyles.None; // verdadero centrado
            btnHost.Resize += (_, __) =>
            {
                int target = Math.Min(260, btnHost.ClientSize.Width - 24);
                btn.Width = Math.Max(140, target);
                btn.Left = (btnHost.ClientSize.Width - btn.Width) / 2;
                btn.Top = (btnHost.ClientSize.Height - btn.Height) / 2;
            };
            col.Controls.Add(btnHost, 0, 3);

            return panel;
        }

        // ==================== Navegación (conserva tus handlers reales) ====================
        private void OnInscripcion(object sender, EventArgs e) => Abrir("Inscripción de Alumnos");
        private void OnCaptura(object sender, EventArgs e) => Abrir("Captura de Calificaciones");
        private void OnListaGrupo(object sender, EventArgs e) => Abrir("Lista por Grupo");
        private void OnEstadisticas(object sender, EventArgs e) => Abrir("Estadísticas");
        private void OnAdminUsuarios(object sender, EventArgs e)
        {
            using var frm = new FormUsuarios();
            frm.ShowDialog(this); // modal sobre el principal
        }
        private void OnBitacora(object sender, EventArgs e) => Abrir("Bitácora de Eventos");

        private void Abrir(string modulo)
        {
            // Reemplaza esto por abrir tus Forms reales
            MessageBox.Show($"Abrir módulo: {modulo}", "Navegación",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ==================== Logout ====================
        private void BtnLogout_Click(object sender, EventArgs e)
        {
            try { SesionApp.CerrarSesion(); } catch { }
            Close();
        }

        // ==================== Utils ====================
        private static void SetRounded(Control ctrl, int radius)
        {
            if (radius <= 0) { ctrl.Region = null; return; }
            ctrl.Region?.Dispose();
            using (var path = new GraphicsPath())
            {
                int d = radius * 2;
                path.AddArc(0, 0, d, d, 180, 90);
                path.AddArc(ctrl.Width - d, 0, d, d, 270, 90);
                path.AddArc(ctrl.Width - d, ctrl.Height - d, d, d, 0, 90);
                path.AddArc(0, ctrl.Height - d, d, d, 90, 90);
                path.CloseFigure();
                ctrl.Region = new Region(path);
            }
        }

        private static Color Blend(Color a, Color b, double t)
        {
            byte Lerp(byte x, byte y) => (byte)(x + (y - x) * t);
            return Color.FromArgb(
                Lerp(a.A, b.A),
                Lerp(a.R, b.R),
                Lerp(a.G, b.G),
                Lerp(a.B, b.B));
        }

        private static Font IconFont(float size)
        {
            // Windows 10+ trae "Segoe MDL2 Assets"
            try { return new Font("Segoe MDL2 Assets", size, FontStyle.Regular); }
            catch { return MakeFont(size); } // fallback
        }

        private static string GetGlyph(string modulo)
        {
            switch (modulo)
            {
                case "Inscripción de Alumnos": return "\uE77B"; // Contact
                case "Captura de Calificaciones": return "\uE104"; // Edit
                case "Lista por Grupo": return "\uE14C"; // Bulleted list
                case "Estadísticas": return "\uE9D2"; // Area chart
                case "Administrar Usuarios": return "\uE713"; // Settings (gear)
                case "Bitácora de Eventos": return "\uE12D"; // Calendar
                default: return "\uE10F"; // Info (fallback)
            }
        }

    }
}
