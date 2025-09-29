using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    public partial class FormGeneral : Form
    {
        // ===== Paleta =====
        private static readonly Color C_BG = Color.FromArgb(244, 247, 247);  // #F4F7F7
        private static readonly Color C_MID = Color.FromArgb(170, 207, 208);  // #AACFD0
        private static readonly Color C_ACCENT = Color.FromArgb(121, 168, 169);  // #79A8A9
        private static readonly Color C_PRIMARY = Color.FromArgb(31, 78, 95);   // #1F4E5F
        private static readonly Color C_TXT_DARK = Color.FromArgb(20, 30, 40);
        private static readonly Color C_TXT_DIM = Color.FromArgb(70, 84, 94);

        // ===== Tipografía (con fallback si no está instalada) =====
        private static Font MakeFont(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font("Aptos", size, style, GraphicsUnit.Point); }
            catch { return new Font("Segoe UI", size, style, GraphicsUnit.Point); }
        }

        // ===== Contenedores =====
        private Panel header;     // barra superior
        private Panel sidebar;    // columna de módulos
        private Panel content;    // área de trabajo

        // ===== Cabecera: controles =====
        private Label lblTitle;
        private Label lblUser;
        private Button btnLogout;

        public FormGeneral()
        {
            // Ventana
            Text = "Sistema de Control de Calificaciones";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 680);
            BackColor = C_PRIMARY;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.Dpi;

            // Layout raíz
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = C_PRIMARY,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); // header fijo
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // resto
            Controls.Add(root);

            // ====== Header ======
            header = new Panel { Dock = DockStyle.Fill, BackColor = C_PRIMARY };
            root.Controls.Add(header, 0, 0);

            // Título a la izquierda
            lblTitle = new Label
            {
                Text = "SISTEMA DE CONTROL DE CALIFICACIONES",
                AutoSize = false,
                Dock = DockStyle.Left,
                Width = 560,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.White,
                Padding = new Padding(24, 0, 0, 0)
            };
            header.Controls.Add(lblTitle);

            // Panel derecho (usuario + botón)
            var right = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 16, 24, 16),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            header.Controls.Add(right);

            // Etiqueta usuario/rol
            lblUser = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = MakeFont(11f),
                Text = BuildUserText(),   // "👤 Hola, Nombre (Rol)"
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 8, 12, 0)
            };
            right.Controls.Add(lblUser);

            // Botón Cerrar sesión
            btnLogout = new Button
            {
                Text = "Cerrar Sesión",
                AutoSize = true,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                ForeColor = C_PRIMARY,
                BackColor = Color.White,
                Padding = new Padding(12, 6, 12, 6),
                TabStop = false
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Cursor = Cursors.Hand;
            btnLogout.Click += BtnLogout_Click;
            btnLogout.Resize += (_, __) => SetRounded(btnLogout, 16);
            SetRounded(btnLogout, 16);
            right.Controls.Add(btnLogout);

            // ====== Cuerpo (sidebar + contenido) ======
            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                ColumnCount = 2,
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(body, 0, 1);

            // Sidebar (módulos)
            sidebar = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(16, 18, 8, 18)
            };
            body.Controls.Add(sidebar, 0, 0);

            // Contenido
            content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                Padding = new Padding(8, 18, 18, 18)
            };
            body.Controls.Add(content, 1, 0);

            BuildSidebar();        // crea grupos y botones
            BuildWelcomeCard();    // tarjeta placeholder al centro
        }

        // ===================== CABECERA =====================
        private string BuildUserText()
        {
            // Toma lo que tengas disponible de tu sesión.
            // Ajusta los nombres de propiedades según tu implementación real.
            string nombre = "Usuario";
            string rol = "Rol";

            try
            {
                // Ejemplos (cámbialos por tus propiedades reales)
                // nombre = SesionApp?.UsuarioNombre ?? nombre;
                // rol    = SesionApp?.UsuarioRol ?? rol;
            }
            catch { /* ignore */ }

            return $"👤 Hola, {nombre} ({rol})";
        }

        private void BtnLogout_Click(object sender, EventArgs e)
        {
            // Lógica de cierre de sesión + volver a login
            try { SesionApp.CerrarSesion(); } catch { /* opcional */ }
            foreach (Form f in Application.OpenForms)
            {
                if (f != this && f is FormLogin) { f.Show(); Close(); return; }
            }
            // Si no hay login abierto, crea uno
            var login = new FormLogin();
            login.Show();
            Close();
        }

        // ===================== SIDEBAR =====================
        private void BuildSidebar()
        {
            sidebar.Controls.Add(MakeGroupLabel("Módulos Principales"));
            sidebar.Controls.Add(MakeModuleButton("Inscripción de Alumnos", "🧑‍🎓", OnInscripcion));
            sidebar.Controls.Add(MakeModuleButton("Captura de Calificaciones", "📝", OnCaptura));
            sidebar.Controls.Add(MakeModuleButton("Lista por Grupo", "📋", OnListaGrupo));
            sidebar.Controls.Add(MakeModuleButton("Estadísticas", "📊", OnEstadisticas));

            sidebar.Controls.Add(new Panel { Height = 8, Dock = DockStyle.Top });

            sidebar.Controls.Add(MakeGroupLabel("Opciones Exclusivas del Director"));
            sidebar.Controls.Add(MakeModuleButton("Administrar Usuarios", "⚙️", OnAdminUsuarios));
            sidebar.Controls.Add(MakeModuleButton("Bitácora de Eventos", "🗒️", OnBitacora));
        }
        private Control MakeGroupLabel(string text)
        {
            var lbl = new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 28,
                Font = MakeFont(11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(70, 84, 94),
                Padding = new Padding(8, 4, 0, 0),
                BackColor = Color.Transparent
            };
            return WrapTop(lbl);
        }

        private Control MakeModuleButton(string text, string icon, EventHandler onClick)
        {
            var btn = new Button
            {
                Dock = DockStyle.Top,
                Height = 44,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.FromArgb(31, 78, 95),
                BackColor = Color.White,
                Font = MakeFont(10f),
                Text = $"{icon}  {text}",
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 6, 0, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(170, 207, 208);
            btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(121, 168, 169);
            btn.Click += onClick;
            btn.Resize += (_, __) => SetRounded(btn, 10);
            SetRounded(btn, 10);

            return WrapTop(btn);
        }

        // Envuelve un control para stack vertical (Dock=Top) y solo lo devuelve.
        private Panel WrapTop(Control c)
        {
            var host = new Panel
            {
                Dock = DockStyle.Top,
                Height = c.Height + 6,
                Padding = new Padding(0, 0, 8, 6),
                BackColor = Color.Transparent
            };
            c.Parent?.Controls.Remove(c);
            c.Dock = DockStyle.Fill;
            host.Controls.Add(c);
            return host;
        }

        // ===================== CONTENIDO (placeholder) =====================
        private void BuildWelcomeCard()
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20),
                Margin = new Padding(8)
            };
            SetRounded(card, 12);

            var title = new Label
            {
                Text = "Resumen Escolar",
                ForeColor = C_PRIMARY,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft
            };
            card.Controls.Add(title);

            var hint = new Label
            {
                Text = "Aquí irá el dashboard/resumen. Selecciona un módulo del menú izquierdo.",
                Font = MakeFont(11f),
                ForeColor = C_TXT_DIM,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };
            card.Controls.Add(hint);

            content.Controls.Add(card);
        }

        // ===================== Handlers de módulos =====================
        private void OnInscripcion(object sender, EventArgs e) => ShowToast("Inscripción de Alumnos");
        private void OnCaptura(object sender, EventArgs e) => ShowToast("Captura de Calificaciones");
        private void OnListaGrupo(object sender, EventArgs e) => ShowToast("Lista por Grupo");
        private void OnEstadisticas(object sender, EventArgs e) => ShowToast("Estadísticas");
        private void OnAdminUsuarios(object sender, EventArgs e) => ShowToast("Administrar Usuarios");
        private void OnBitacora(object sender, EventArgs e) => ShowToast("Bitácora de Eventos");


        private void ShowToast(string modulo)
        {
            // Aquí puedes abrir tu Form real. De momento, demo:
            MessageBox.Show($"Abrir módulo: {modulo}", "Navegación",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ===================== Utils =====================
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
    }
}
