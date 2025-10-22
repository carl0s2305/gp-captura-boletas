using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

#nullable enable

namespace gp_captura_boletas
{
    internal static class TextBoxExtensions
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, string lParam);

        public static void SetCueBanner(this TextBox tb, string text, bool showWhenFocused = false)
        {
            void Apply() => SendMessage(tb.Handle, EM_SETCUEBANNER, showWhenFocused ? 1 : 0, text);
            if (tb.IsHandleCreated) Apply();
            else tb.HandleCreated += (_, __) => Apply();
        }
    }
    public partial class FormInscripcion : Form
    {
        // ====== Paleta (alineada a tu app) ======
        private static readonly Color C_BG = Color.FromArgb(244, 247, 247);
        private static readonly Color C_ACCENT = Color.FromArgb(121, 168, 169);
        private static readonly Color C_PRIMARY = Color.FromArgb(31, 78, 95);
        private static readonly Color C_TEXT_DIM = Color.FromArgb(70, 84, 94);

        private readonly Timer _debounceTarjetas = new Timer { Interval = 250 };
        private readonly Timer _debounceLista = new Timer { Interval = 250 };
        private bool _subscribed;

        // ===== Vista tipo “lista mensual” en pantalla =====
        private Panel headerLista;                // encabezado con logo y textos
        private PictureBox pbEscudo;
        private Label lblEscNombre, lblEscDir1, lblEscDir2, lblEscTel, lblTituloLista;

        private readonly int[] _colPct = { 6, 23, 23, 28, 15, 5 }; // No, Pat, Mat, Nom, CURP, GEN

        // Para que el menú contextual siga funcionando
        private List<AlumnoListaDto> _lastAlumnos = new List<AlumnoListaDto>();

        // Encabezado de la escuela (ajústalo si ocupas)
        private const string ESC_NOMBRE = "ESCUELA PRIMARIA EMILIANO ZAPATA";
        private const string ESC_DIR1 = "Calle 16 de Septiembre No. 45, Col. Centro";
        private const string ESC_DIR2 = "C.P. 39300, Acapulco de Juárez, Guerrero";
        private const string ESC_TEL = "Tel. (744) 123-4567";

        private static Font MakeFont(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font("Aptos", size, style, GraphicsUnit.Point); }
            catch { return new Font("Segoe UI", size, style, GraphicsUnit.Point); }
        }

        private enum Rol { Director, Secretaria }
        private Rol RolActual => ObtenerRol();

        // ====== UI ======
        private Panel header;
        private RoundedButton btnAgregar, btnRefrescar, btnImprimir, btnVolver;
        private TextBox tbBuscar;
        private RoundedButton btnBuscar;
        private Label lblTituloModulo, lblProfesor, lblGrupo;

        // selector de grupos
        private Panel pnlSelectorGrupos;
        // lista de alumnos
        private Panel pnlLista;
        private DataGridView grid;

        private int? grupoSeleccionadoId;
        private string grupoSeleccionadoEtiqueta;
        private string profesorSeleccionadoNombre;

        public FormInscripcion()
        {
            Text = "Inscripción de Alumnos";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1020, 680);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;
            BackColor = C_BG;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = C_BG
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));  // header
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // contenido
            Controls.Add(root);

            // ===== Header (2 filas: título centrado + barra de acciones centrada) =====
            header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_PRIMARY,
                Padding = new Padding(12, 6, 12, 10)
            };
            root.Controls.Add(header, 0, 0);

            // Tabla para centrar: 3 columnas (50% | autosize | 50%), 2 filas
            var hdr = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = C_PRIMARY
            };
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));  // contenido centrado
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            hdr.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));   // título
            hdr.RowStyles.Add(new RowStyle(SizeType.Absolute, 60f));   // toolbar
            header.Controls.Add(hdr);

            // Fila 0: Título centrado
            lblTituloModulo = new Label
            {
                Text = "Inscripción de Alumnos",
                ForeColor = Color.White,
                AutoSize = true,
                Font = MakeFont(20f, FontStyle.Bold),
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 6, 0, 0)
            };
            hdr.Controls.Add(lblTituloModulo, 1, 0);

            // Fila 1: Toolbar centrada (con buscador “integrado”)
            var toolbar = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0),
                Padding = new Padding(0),
                Anchor = AnchorStyles.None
            };
            hdr.Controls.Add(toolbar, 1, 1);

            // --- Buscar (solo Director) como “pill” integrado ---
            if (RolActual == Rol.Director)
            {
                var searchGroup = CreateSearchGroup();     // <- crea panel con textbox + botón
                toolbar.Controls.Add(searchGroup);
            }

            // --- Botones de acción ---
            btnAgregar = new RoundedButton
            {
                Text = "Agregar Alumno",
                AutoSize = true,
                Height = 36,
                BackColor = Color.White,
                ForeColor = C_PRIMARY,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(10, 10, 10, 10)
            };
            btnAgregar.FlatAppearance.BorderSize = 0;
            btnAgregar.Click += (_, __) => AgregarAlumno();
            toolbar.Controls.Add(btnAgregar);

            // Botón Cargar/Actualizar (innecesario si refrescas al navegar) -> lo oculto
            btnRefrescar = new RoundedButton
            {
                Text = "Actualizar",
                AutoSize = true,
                Height = 36,
                BackColor = Color.White,
                ForeColor = C_PRIMARY,
                FlatStyle = FlatStyle.Flat,
                Visible = false,                              // <--- OCULTO
                Margin = new Padding(10, 10, 10, 10)
            };
            btnRefrescar.FlatAppearance.BorderSize = 0;
            btnRefrescar.Click += (_, __) => RefrescarLista();
            // toolbar.Controls.Add(btnRefrescar);           // <--- NO lo agregamos

            // Quitamos Imprimir por ahora (ni lo creamos ni lo agregamos)

            // Botón Volver (aparece al ver la lista)
            btnVolver = new RoundedButton
            {
                Text = "← Grupos",
                AutoSize = true,
                Height = 36,
                BackColor = Color.White,
                ForeColor = C_PRIMARY,
                FlatStyle = FlatStyle.Flat,
                Visible = false,
                Margin = new Padding(10, 10, 0, 10)
            };
            btnVolver.FlatAppearance.BorderSize = 0;
            btnVolver.Click += (_, __) => MostrarSelectorGrupos();
            toolbar.Controls.Add(btnVolver);

            // Línea sutil inferior
            header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(0, 0, 0, 40) });

            // Ajusta la altura del header (más alto para que no recorte)
            root.RowStyles[0].Height = 130;

            // ===== Contenido =====
            var content = new Panel { Dock = DockStyle.Fill, BackColor = C_BG };
            root.Controls.Add(content, 0, 1);

            // Selector de grupos
            pnlSelectorGrupos = new Panel { Dock = DockStyle.Fill, BackColor = C_BG, Padding = new Padding(18) };
            content.Controls.Add(pnlSelectorGrupos);

            // Lista
            pnlLista = new Panel { Dock = DockStyle.Fill, BackColor = C_BG, Padding = new Padding(18), Visible = false };
            content.Controls.Add(pnlLista);

            // ===== Encabezado estilo plantilla (una sola vez) =====
            headerLista = new Panel { Dock = DockStyle.Top, Height = 150, BackColor = Color.White };
            pnlLista.Controls.Add(headerLista);

            pbEscudo = new PictureBox
            {
                Image = Properties.Resources.escudo,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(18, 12),
                Size = new Size(80, 80)
            };
            headerLista.Controls.Add(pbEscudo);

            lblEscNombre = new Label
            {
                Text = ESC_NOMBRE,
                AutoSize = false,
                Location = new Point(110, 12),
                Size = new Size(760, 26),
                Font = MakeFont(16f, FontStyle.Bold),
                ForeColor = Color.Black
            };
            headerLista.Controls.Add(lblEscNombre);

            lblEscDir1 = new Label { Text = ESC_DIR1, AutoSize = false, Location = new Point(110, 40), Size = new Size(760, 18), Font = MakeFont(10.5f) };
            lblEscDir2 = new Label { Text = ESC_DIR2, AutoSize = false, Location = new Point(110, 58), Size = new Size(760, 18), Font = MakeFont(10.5f) };
            lblEscTel = new Label { Text = ESC_TEL, AutoSize = false, Location = new Point(110, 76), Size = new Size(760, 18), Font = MakeFont(10.5f) };
            headerLista.Controls.Add(lblEscDir1);
            headerLista.Controls.Add(lblEscDir2);
            headerLista.Controls.Add(lblEscTel);

            // Título centrado
            lblTituloLista = new Label
            {
                Text = "LISTA MENSUAL DEL GRUPO",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom,
                Height = 46,
                Font = MakeFont(16f, FontStyle.Bold)
            };
            headerLista.Controls.Add(lblTituloLista);

            // Cabecera de lista (grupo + profesor)
            var info = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = C_BG,
                Padding = new Padding(0)
            };
            lblGrupo = new Label { AutoSize = true, Font = MakeFont(12, FontStyle.Bold), ForeColor = C_PRIMARY, Text = "Grupo: -" };
            lblProfesor = new Label { AutoSize = true, Font = MakeFont(12), ForeColor = C_TEXT_DIM, Margin = new Padding(16, 6, 0, 0), Text = "Profesor: -" };
            info.Controls.Add(lblGrupo);
            info.Controls.Add(lblProfesor);
            pnlLista.Controls.Add(info);

            // Grid de alumnos
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoGenerateColumns = false
            };
            pnlLista.Controls.Add(grid);
            grid.BringToFront();

            // --- Bloquear redimensionamiento por el usuario ---
            grid.AllowUserToResizeColumns = false; // ancho de columnas
            grid.AllowUserToResizeRows = false;    // alto de filas
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;

            // Desactivar el auto-ajuste con doble click en los separadores
            grid.ColumnDividerDoubleClick += (_, e) => e.Handled = true;

            // Asegurar que ninguna columna sea "redimensionable"
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; // ya lo tienes
            grid.ColumnAdded += (_, e) => e.Column.Resizable = DataGridViewTriState.False;


            grid.Columns.Clear();
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235);
            grid.ColumnHeadersDefaultCellStyle.Font = MakeFont(10f, FontStyle.Bold);
            grid.ColumnHeadersHeight = 26;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            grid.GridColor = Color.Black;
            grid.DefaultCellStyle.Font = MakeFont(10f);
            grid.RowTemplate.Height = 22;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.RowHeadersVisible = false;
            grid.BorderStyle = BorderStyle.FixedSingle;

            foreach (DataGridViewColumn col in grid.Columns)
                col.Resizable = DataGridViewTriState.False;

            // Columnas (No., Paterno, Materno, Nombres, CURP, GEN.)
            var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "No.", DataPropertyName = "No", DefaultCellStyle = center });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "APELLIDO PATERNO", DataPropertyName = "Paterno" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "APELLIDO MATERNO", DataPropertyName = "Materno" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "NOMBRE(S)", DataPropertyName = "Nombre" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "CURP", DataPropertyName = "CURP" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "GEN.", DataPropertyName = "Gen", DefaultCellStyle = center });

            // Redimensiona por porcentaje al cambiar tamaño
            grid.Resize += (_, __) => ResizeMonthlyColumns();
            ResizeMonthlyColumns();

            // Menú contextual (según rol)
            var menu = new ContextMenuStrip();
            var miModificar = new ToolStripMenuItem("Modificar");
            var miEliminar = new ToolStripMenuItem("Eliminar");
            var miConsultar = new ToolStripMenuItem("Consultar");

            miModificar.Click += (_, __) => ModificarSeleccionado();
            miEliminar.Click += (_, __) => EliminarSeleccionado();
            miConsultar.Click += (_, __) => ConsultarSeleccionado();

            menu.Items.Add(miConsultar);
            if (RolActual == Rol.Director)
            {
                menu.Items.Add(miModificar);
                menu.Items.Add(miEliminar);
            }

            grid.ContextMenuStrip = menu;

            // Cargar selector al entrar
            CargarSelectorGrupos();

            // --- auto-refresco (debounce) ---
            _debounceTarjetas.Tick += (_, __) =>
            {
                _debounceTarjetas.Stop();
                CargarSelectorGrupos();
            };

            _debounceLista.Tick += (_, __) =>
            {
                _debounceLista.Stop();
                if (grupoSeleccionadoId.HasValue) RefrescarLista();
            };
        }
        private void ResizeMonthlyColumns()
        {
            if (grid.Columns.Count < 6) return;
            int w = grid.ClientSize.Width;
            for (int i = 0; i < _colPct.Length; i++)
                grid.Columns[i].Width = (int)Math.Round(w * _colPct[i] / 100.0);
        }

        // Modelo para la vista en pantalla (35 filas fijas)
        private sealed class MonthlyRow
        {
            public int No { get; set; }
            public string Paterno { get; set; } = "";
            public string Materno { get; set; } = "";
            public string Nombre { get; set; } = "";
            public string CURP { get; set; } = "";
            public string Gen { get; set; } = "";
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (!_subscribed)
            {
                AppEvents.DatosCambiaron += OnDatosCambiaron;
                _subscribed = true;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_subscribed)
                AppEvents.DatosCambiaron -= OnDatosCambiaron;
            base.OnFormClosed(e);
        }

        // Si vuelves a esta ventana desde otra, refresca por si cambió algo
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RequestRefreshTarjetas();
            RequestRefreshLista();
        }

        private void OnDatosCambiaron()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(OnDatosCambiaron)); return; }

            if (pnlSelectorGrupos.Visible) RequestRefreshTarjetas();
            if (pnlLista.Visible) RequestRefreshLista();
        }

        private void RequestRefreshTarjetas()
        {
            _debounceTarjetas.Stop();
            _debounceTarjetas.Start();
        }

        private void RequestRefreshLista()
        {
            if (!grupoSeleccionadoId.HasValue) return;
            _debounceLista.Stop();
            _debounceLista.Start();
        }

        private Panel CreateSearchGroup()
        {
            var box = new RoundedPanel
            {
                BackColor = Color.White,
                CornerRadius = 20,
                Height = 42,
                Width = 520,
                Padding = new Padding(14, 9, 8, 9),
                Margin = new Padding(0, 10, 12, 10),
                BorderColor = Color.FromArgb(210, 220, 224)
            };

            // TextBox sin borde, SIN Dock y con AutoSize=false para poder fijar altura
            tbBuscar = new TextBox
            {
                BorderStyle = BorderStyle.None,
                AutoSize = false,
                Font = MakeFont(10f)
            };
            tbBuscar.SetCueBanner("Buscar: CURP / Nombre / Apellidos");
            tbBuscar.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; BuscarGlobal(); } };

            // Botón a la derecha
            btnBuscar = new RoundedButton
            {
                Text = "Buscar",
                AutoSize = false,
                Width = 92,
                Height = 26,
                BackColor = C_PRIMARY,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0)
            };
            btnBuscar.FlatAppearance.BorderSize = 0;
            btnBuscar.Resize += (_, __) => SetRounded(btnBuscar, 13);
            btnBuscar.Click += (_, __) => BuscarGlobal();

            box.Controls.Add(tbBuscar);
            box.Controls.Add(btnBuscar);

            // Layout manual: centra verticalmente textbox y botón
            void LayoutSearchBox(object _, EventArgs __)
            {
                var pad = box.Padding;

                // Botón, centrado vertical en el pill
                int hBtn = btnBuscar.Height;
                btnBuscar.Left = box.ClientSize.Width - pad.Right - btnBuscar.Width;
                btnBuscar.Top = pad.Top + (box.ClientSize.Height - pad.Vertical - hBtn) / 2;

                // TextBox: ancho disponible hasta el botón, altura ideal según fuente
                int gap = 8;
                int availW = btnBuscar.Left - pad.Left - gap;
                int textH = TextRenderer.MeasureText("Ag", tbBuscar.Font).Height + 4;

                tbBuscar.Width = Math.Max(60, availW);
                tbBuscar.Height = textH;
                tbBuscar.Left = pad.Left;
                tbBuscar.Top = pad.Top + (box.ClientSize.Height - pad.Vertical - tbBuscar.Height) / 2;
            }

            box.Resize += LayoutSearchBox;
            LayoutSearchBox(null, EventArgs.Empty);

            // feedback de foco en el borde
            tbBuscar.GotFocus += (_, __) => { box.BorderColor = C_ACCENT; box.Invalidate(); };
            tbBuscar.LostFocus += (_, __) => { box.BorderColor = Color.FromArgb(210, 220, 224); box.Invalidate(); };

            return box;
        }
        // Redondear (local a este form)
        private static void SetRounded(Control ctrl, int radius)
        {
            if (radius <= 0) { ctrl.Region = null; return; }
            ctrl.Region?.Dispose();
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
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

        // ====== Roles (igual que en tu FormGeneral) ======
        private Rol ObtenerRol()
        {
            try
            {
                if (!string.IsNullOrEmpty(SesionApp.Rol))
                {
                    return SesionApp.Rol.Equals("DIRECTOR", StringComparison.OrdinalIgnoreCase)
                        ? Rol.Director : Rol.Secretaria;
                }
            }
            catch { }
            return Rol.Secretaria;
        }

        // ====== Selector de Grupos ======
        private static string EtiquetaGradoLetra(byte gradoId, string letra)
        {
            // 1->"1°", 2->"2°", ...
            return $"{gradoId}° {letra}";
        }

        private static Label MakeIconLabel()
        {
            // Icono simple de aula (emoji). Si prefieres MDL2, puedes usar un glyph y fuente especial.
            return new Label
            {
                Text = " 🏫",
                AutoSize = true,
                Font = new Font("Segoe UI Emoji", 26f, FontStyle.Regular),
                ForeColor = C_PRIMARY,
                Margin = new Padding(0, 0, 8, 0)
            };
        }
        private void CargarSelectorGrupos()
        {
            pnlSelectorGrupos.Controls.Clear();

            var grupos = GrupoRepo.GetGruposActivos(); // 6 esperados
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = C_BG,
                Padding = new Padding(6)
            };
            for (int c = 0; c < 3; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            for (int r = 0; r < 2; r++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            pnlSelectorGrupos.Controls.Add(grid);

            foreach (var g in grupos.OrderBy(x => x.GradoID))
            {
                var card = CrearCardGrupo(g);
                grid.Controls.Add(card);
            }
        }
        private Control CrearCardGrupo(GrupoSimpleDto g)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(12),
                Padding = new Padding(16),
                Cursor = Cursors.Hand,
                Tag = g
            };

            // Borde + hover
            card.Paint += (s, e) =>
            {
                var r = card.ClientRectangle; r.Width -= 1; r.Height -= 1;
                using var pen = new Pen(Color.FromArgb(220, 225, 230), 1f);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawRectangle(pen, r);
            };
            card.MouseEnter += (_, __) => card.BackColor = Color.FromArgb(248, 251, 252);
            card.MouseLeave += (_, __) => card.BackColor = Color.White;

            // Tabla 3 columnas (para centrar horizontal) y 6 filas:
            // 0: spacer 50%   1: icono   2: título   3: profesor   4: total   5: spacer 50%
            var col = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 6,
                BackColor = Color.Transparent
            };
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            col.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));   // centro
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));     // spacer arriba
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // icono
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // 1° A
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // profesor
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // alumnos
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));     // spacer abajo
            card.Controls.Add(col);

            // Icono
            var icon = MakeIconLabel();
            icon.Anchor = AnchorStyles.None;
            col.Controls.Add(icon, 1, 1);

            // “1° A”
            var lblTitulo = new Label
            {
                AutoSize = true,
                Text = $"{g.GradoID}° {g.Letra}",
                Font = MakeFont(22f, FontStyle.Bold),
                ForeColor = C_PRIMARY,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 0, 6)
            };
            lblTitulo.Anchor = AnchorStyles.None;
            col.Controls.Add(lblTitulo, 1, 2);

            // Profesor
            var prof = string.IsNullOrWhiteSpace(g.Profesor) ? "(Sin profesor)" : g.Profesor;
            var lblProf = new Label
            {
                AutoSize = true,
                Text = $"Profesor: {prof}",
                Font = MakeFont(11f),
                ForeColor = C_TEXT_DIM,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 2, 0, 8)
            };
            lblProf.Anchor = AnchorStyles.None;
            col.Controls.Add(lblProf, 1, 3);

            // Total alumnos
            var lblTotal = new Label
            {
                AutoSize = true,
                Text = $"Alumnos: {g.TotalAlumnos}",
                Font = MakeFont(11f, FontStyle.Bold),
                ForeColor = C_PRIMARY,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblTotal.Anchor = AnchorStyles.None;
            col.Controls.Add(lblTotal, 1, 4);

            // Para que no se desborden textos largos
            card.Resize += (_, __) =>
            {
                int maxw = Math.Max(60, card.ClientSize.Width - card.Padding.Horizontal - 24);
                lblTitulo.MaximumSize = new Size(maxw, 0);
                lblProf.MaximumSize = new Size(maxw, 0);
                lblTotal.MaximumSize = new Size(maxw, 0);
            };

            // Click en cualquier parte
            void abrir(object _1, EventArgs _2) => AbrirGrupo(g);
            card.Click += abrir;
            foreach (Control c in col.Controls) c.Click += abrir;

            return card;
        }
        private void AbrirGrupo(GrupoSimpleDto g)
        {
            grupoSeleccionadoId = g.GrupoID;
            grupoSeleccionadoEtiqueta = $"{g.GradoDesc} {g.Letra}";
            profesorSeleccionadoNombre = string.IsNullOrWhiteSpace(g.Profesor) ? "(Sin profesor)" : g.Profesor;

            // Actualiza encabezados existentes
            lblTituloLista.Text = $"LISTA MENSUAL DEL GRUPO {g.GradoID}° {g.Letra}".ToUpperInvariant();

            // Si mantienes el panel “info”, actualiza sus labels:
            lblGrupo.Text = "Grupo: " + grupoSeleccionadoEtiqueta;
            lblProfesor.Text = "Profesor: " + profesorSeleccionadoNombre;

            MostrarLista();
            RefrescarLista();
        }
        private void MostrarSelectorGrupos()
        {
            pnlLista.Visible = false;
            btnVolver.Visible = false;
            pnlSelectorGrupos.Visible = true;
            grupoSeleccionadoId = null;

            RequestRefreshTarjetas();
        }

        private void MostrarLista()
        {
            pnlSelectorGrupos.Visible = false;
            pnlLista.Visible = true;
            btnVolver.Visible = true;
        }

        // ====== Acciones Header ======
        private void AgregarAlumno()
        {
            using var dlg = new FormAlumnoEdit();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                // ya guardó internamente; refresca lista si estabas en un grupo
                if (grupoSeleccionadoId.HasValue) RefrescarLista();
            }
        }
        private void RefrescarLista()
        {
            if (grupoSeleccionadoId == null)
            {
                MessageBox.Show("Elige un grupo primero.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Ordena como quieras que aparezca
            var alumnos = AlumnoRepo.GetByGrupo(grupoSeleccionadoId.Value)
                                    .OrderBy(a => a.Paterno).ThenBy(a => a.Materno).ThenBy(a => a.Nombre)
                                    .ToList();

            _lastAlumnos = alumnos; // para menú contextual

            var rows = new List<MonthlyRow>();
            for (int i = 0; i < 35; i++)
            {
                var r = new MonthlyRow { No = i + 1 };
                if (i < alumnos.Count)
                {
                    var a = alumnos[i];
                    r.Paterno = a.Paterno ?? "";
                    r.Materno = a.Materno ?? "";
                    r.Nombre = a.Nombre ?? "";
                    r.CURP = a.CURP ?? "";
                    r.Gen = (a.Sexo ?? "").Trim();
                }
                rows.Add(r);
            }

            grid.DataSource = rows;
            ResizeMonthlyColumns();
        }
        private void ImprimirLista()
        {
            // Implementa aquí tu lógica de impresión o exportación (PDF/Excel)
            MessageBox.Show("Función de impresión pendiente de implementar.", "Imprimir",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BuscarGlobal()
        {
            if (RolActual != Rol.Director)
                return;

            string q = tbBuscar.Text?.Trim();
            if (string.IsNullOrWhiteSpace(q))
            {
                MessageBox.Show("Escribe algo para buscar (CURP / Nombre / Apellidos).",
                    "Búsqueda", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var resultados = AlumnoRepo.Buscar(q);
            using var gridRes = new FormResultadosBusqueda(resultados, puedeEditar: true);
            gridRes.OnAbrir += alumno =>
            {
                // abrir edición/consulta
                if (RolActual == Rol.Director) ModificarAlumno(alumno);
                else ConsultarAlumno(alumno);
            };
            gridRes.OnEliminar += alumno =>
            {
                if (RolActual == Rol.Director) EliminarAlumno(alumno.AlumnoID);
            };
            gridRes.ShowDialog(this);
        }

        // ====== Operaciones por selección en grid ======
        private AlumnoListaDto? GetSeleccionado()
        {
            int row = grid.CurrentCell?.RowIndex ?? -1;
            if (row < 0) return null;
            if (row >= _lastAlumnos.Count) return null; // fila vacía de las 35
            return _lastAlumnos[row];
        }

        private void ConsultarSeleccionado()
        {
            var a = GetSeleccionado();
            if (a == null) return;
            ConsultarAlumno(a);
        }

        private void ModificarSeleccionado()
        {
            if (RolActual != Rol.Director) return;
            var a = GetSeleccionado();
            if (a == null) return;
            ModificarAlumno(a);
        }

        private void EliminarSeleccionado()
        {
            if (RolActual != Rol.Director) return;
            var a = GetSeleccionado();
            if (a == null) return;
            EliminarAlumno(a.AlumnoID);
            RefrescarLista();
        }

        private void ConsultarAlumno(AlumnoListaDto a)
        {
            MessageBox.Show(
                $"CURP: {a.CURP}\nNombre: {a.Nombre} {a.Paterno} {a.Materno}\nSexo: {a.Sexo}\nProfesor: {a.Profesor}",
                "Consulta de Alumno",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ModificarAlumno(AlumnoListaDto a)
        {
            using var dlg = new MiniAlumnoEdit(a);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    AlumnoRepo.Update(dlg.Modelo);
                    RefrescarLista();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo modificar: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EliminarAlumno(int alumnoID)
        {
            if (RolActual != Rol.Director) return;
            if (MessageBox.Show("¿Eliminar este alumno de forma permanente?",
                "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    AlumnoRepo.Delete(alumnoID);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo eliminar: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    // =========================== Repositorios (SQL Server) ===========================

    internal static class Db
    {
        public static SqlConnection New()
        {
            // Ajusta el nombre de tu connectionString si es distinto
            var cs = ConfigurationManager.ConnectionStrings["LocalSql"]?.ConnectionString
                     ?? throw new InvalidOperationException("Falta la cadena de conexión 'cn' en App.config.");
            return new SqlConnection(cs);
        }
    }

    internal partial class GrupoRepo
    {
        public static List<GrupoSimpleDto> GetGruposActivos()
        {
            const string sql = @"
SELECT  g.GrupoID,
        g.GradoID,
        g.Letra,
        ISNULL(p.Nombre,'') AS Profesor,
        CASE g.GradoID
            WHEN 1 THEN '1° Primaria' WHEN 2 THEN '2° Primaria' WHEN 3 THEN '3° Primaria'
            WHEN 4 THEN '4° Primaria' WHEN 5 THEN '5° Primaria' WHEN 6 THEN '6° Primaria'
        END AS GradoDesc,
        COUNT(ag.AlumnoID) AS TotalAlumnos
FROM dbo.Grupo g
JOIN dbo.CicloEscolar c
  ON c.CicloID = g.CicloID AND c.Activo = 1
LEFT JOIN dbo.Profesor p
  ON p.ProfesorID = g.ProfesorID
LEFT JOIN dbo.AsignacionGrupo ag
  ON ag.GrupoID = g.GrupoID AND ag.CicloID = c.CicloID
GROUP BY g.GrupoID, g.GradoID, g.Letra, p.Nombre,
         CASE g.GradoID
            WHEN 1 THEN '1° Primaria' WHEN 2 THEN '2° Primaria' WHEN 3 THEN '3° Primaria'
            WHEN 4 THEN '4° Primaria' WHEN 5 THEN '5° Primaria' WHEN 6 THEN '6° Primaria'
         END
ORDER BY g.GradoID, g.Letra;";

            var list = new List<GrupoSimpleDto>();
            using var cn = Db.New();
            using var cmd = new SqlCommand(sql, cn);
            cn.Open();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new GrupoSimpleDto
                {
                    GrupoID = rd.GetInt32(0),
                    GradoID = rd.GetByte(1),
                    Letra = rd.GetString(2),
                    Profesor = rd.GetString(3),
                    GradoDesc = rd.GetString(4),
                    TotalAlumnos = rd.GetInt32(5)
                });
            }
            return list;
        }
    }

    internal partial class AlumnoRepo
    {
        public static int? CicloActivoId()
        {
            const string sql = "SELECT TOP 1 CicloID FROM dbo.CicloEscolar WHERE Activo = 1;";
            using var cn = Db.New();
            using var cmd = new SqlCommand(sql, cn);
            cn.Open();
            var obj = cmd.ExecuteScalar();
            if (obj == null || obj == DBNull.Value) return null;
            return Convert.ToInt32(obj);
        }

        public static List<AlumnoListaDto> GetByGrupo(int grupoId)
        {
            const string sql = @"
SELECT a.AlumnoID, a.CURP, a.Sexo, a.Paterno, a.Materno, a.Nombre,
       ISNULL(p.Nombre,'') AS Profesor, g.GrupoID
FROM dbo.AsignacionGrupo ag
JOIN dbo.Alumno a   ON a.AlumnoID = ag.AlumnoID
JOIN dbo.Grupo  g   ON g.GrupoID  = ag.GrupoID
LEFT JOIN dbo.Profesor p ON p.ProfesorID = g.ProfesorID
WHERE ag.GrupoID = @GrupoID
  AND ag.CicloID = (SELECT TOP 1 CicloID FROM dbo.CicloEscolar WHERE Activo = 1)
ORDER BY a.Paterno, a.Materno, a.Nombre;";

            var list = new List<AlumnoListaDto>();
            using var cn = Db.New();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@GrupoID", grupoId);
            cn.Open();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new AlumnoListaDto
                {
                    AlumnoID = rd.GetInt32(0),
                    CURP = rd.IsDBNull(1) ? "" : rd.GetString(1).Trim(),
                    Sexo = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Paterno = rd.IsDBNull(3) ? "" : rd.GetString(3),
                    Materno = rd.IsDBNull(4) ? "" : rd.GetString(4),
                    Nombre = rd.IsDBNull(5) ? "" : rd.GetString(5),
                    Profesor = rd.IsDBNull(6) ? "" : rd.GetString(6),
                    GrupoID = rd.GetInt32(7)
                });
            }
            return list;
        }

        public static List<AlumnoListaDto> Buscar(string query)
        {
            // Busca por CURP o texto en nombre/apellidos
            const string sql = @"
SELECT TOP 50 a.AlumnoID, a.CURP, a.Sexo, a.Paterno, a.Materno, a.Nombre,
       '' AS Profesor, NULL AS GrupoID
FROM dbo.Alumno a
WHERE a.CURP LIKE @q
   OR a.Nombre  LIKE @q
   OR a.Paterno LIKE @q
   OR a.Materno LIKE @q
ORDER BY a.Paterno, a.Materno, a.Nombre;";

            var list = new List<AlumnoListaDto>();
            using var cn = Db.New();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@q", "%" + query + "%");
            cn.Open();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new AlumnoListaDto
                {
                    AlumnoID = rd.GetInt32(0),
                    CURP = rd.IsDBNull(1) ? "" : rd.GetString(1).Trim(),
                    Sexo = rd.IsDBNull(2) ? "" : rd.GetString(2),
                    Paterno = rd.IsDBNull(3) ? "" : rd.GetString(3),
                    Materno = rd.IsDBNull(4) ? "" : rd.GetString(4),
                    Nombre = rd.IsDBNull(5) ? "" : rd.GetString(5),
                });
            }
            return list;
        }

        public static int Insert(AlumnoEditModel m)
        {
            const string sql = @"
INSERT INTO dbo.Alumno (CURP, Nombre, Paterno, Materno, Sexo, FechaNac)
VALUES (@CURP, @Nombre, @Paterno, @Materno, @Sexo, @FechaNac);
SELECT SCOPE_IDENTITY();";
            using var cn = Db.New();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@CURP", (object?)m.CURP ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nombre", m.Nombre);
            cmd.Parameters.AddWithValue("@Paterno", (object?)m.Paterno ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Materno", (object?)m.Materno ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Sexo", (object?)m.Sexo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FechaNac", (object?)m.FechaNac ?? DBNull.Value);
            cn.Open();
            AppEvents.RaiseDatosCambiaron();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static void Update(AlumnoEditModel m)
        {
            const string sql = @"
UPDATE dbo.Alumno
   SET CURP=@CURP, Nombre=@Nombre, Paterno=@Paterno, Materno=@Materno, Sexo=@Sexo, FechaNac=@FechaNac
 WHERE AlumnoID=@AlumnoID;";
            using var cn = Db.New();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@AlumnoID", m.AlumnoID);
            cmd.Parameters.AddWithValue("@CURP", (object?)m.CURP ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Nombre", m.Nombre);
            cmd.Parameters.AddWithValue("@Paterno", (object?)m.Paterno ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Materno", (object?)m.Materno ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Sexo", (object?)m.Sexo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FechaNac", (object?)m.FechaNac ?? DBNull.Value);
            cn.Open();
            cmd.ExecuteNonQuery();

            AppEvents.RaiseDatosCambiaron();
        }

        public static void Delete(int alumnoID)
        {
            using var cn = Db.New();
            cn.Open();
            using var tx = cn.BeginTransaction();

            // Primero bajas de grupo del ciclo activo (por integridad)
            var cmd1 = new SqlCommand(@"
DELETE FROM dbo.AsignacionGrupo
WHERE AlumnoID=@AlumnoID
  AND CicloID = (SELECT TOP 1 CicloID FROM dbo.CicloEscolar WHERE Activo = 1);", cn, tx);
            cmd1.Parameters.AddWithValue("@AlumnoID", alumnoID);
            cmd1.ExecuteNonQuery();

            // Luego eliminar alumno
            var cmd2 = new SqlCommand("DELETE FROM dbo.Alumno WHERE AlumnoID=@AlumnoID;", cn, tx);
            cmd2.Parameters.AddWithValue("@AlumnoID", alumnoID);
            cmd2.ExecuteNonQuery();
            AppEvents.RaiseDatosCambiaron();
            tx.Commit();
        }

        public static void AsignarAGrupo(int alumnoID, int grupoID, int cicloID)
        {
            const string sql = @"
IF NOT EXISTS (SELECT 1 FROM dbo.AsignacionGrupo WHERE AlumnoID=@A AND CicloID=@C)
BEGIN
    INSERT INTO dbo.AsignacionGrupo(AlumnoID, GrupoID, CicloID, FechaAsignacion)
    VALUES (@A, @G, @C, CAST(GETDATE() AS DATE));
END
ELSE
BEGIN
    UPDATE dbo.AsignacionGrupo SET GrupoID=@G WHERE AlumnoID=@A AND CicloID=@C;
END";
            using var cn = Db.New();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@A", alumnoID);
            cmd.Parameters.AddWithValue("@G", grupoID);
            cmd.Parameters.AddWithValue("@C", cicloID);
            cn.Open();
            cmd.ExecuteNonQuery();
            AppEvents.RaiseDatosCambiaron();
        }
    }

    // =========================== DTOs / Modelos ===========================

    internal sealed class GrupoSimpleDto
    {
        public int GrupoID { get; set; }
        public byte GradoID { get; set; }
        public string Letra { get; set; }
        public string Profesor { get; set; }
        public string GradoDesc { get; set; }
        public int TotalAlumnos { get; set; }
    }

    internal sealed class AlumnoListaDto
    {
        public int __idx { get; set; }         // número en la lista
        public int AlumnoID { get; set; }
        public string CURP { get; set; }
        public string Sexo { get; set; }
        public string Paterno { get; set; }
        public string Materno { get; set; }
        public string Nombre { get; set; }
        public string Profesor { get; set; }
        public int GrupoID { get; set; }
    }

    public sealed class AlumnoEditModel
    {
        public int? AlumnoID { get; set; }
        public string CURP { get; set; }
        public string Nombre { get; set; }
        public string Paterno { get; set; }
        public string Materno { get; set; }
        public string Sexo { get; set; }      // 'M' / 'F' u otro esquema
        public DateTime? FechaNac { get; set; }
    }

    // =========================== Mini diálogo de edición (rápido) ===========================
    internal sealed class MiniAlumnoEdit : Form
    {
        public AlumnoEditModel Modelo { get; private set; }

        private TextBox tbCurp, tbNombre, tbPaterno, tbMaterno, tbSexo;
        private DateTimePicker dpNac;

        public MiniAlumnoEdit(AlumnoListaDto init = null)
        {
            Text = init == null ? "Agregar Alumno" : "Modificar Alumno";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(480, 360);
            AutoScaleMode = AutoScaleMode.Dpi;
            Modelo = new AlumnoEditModel();

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 6 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            root.Controls.Add(new Label { Text = "CURP:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            tbCurp = new TextBox { Dock = DockStyle.Fill, MaxLength = 18 };
            root.Controls.Add(tbCurp, 1, 0);

            root.Controls.Add(new Label { Text = "Nombre:", AutoSize = true }, 0, 1);
            tbNombre = new TextBox { Dock = DockStyle.Fill };
            root.Controls.Add(tbNombre, 1, 1);

            root.Controls.Add(new Label { Text = "Apellido Paterno:", AutoSize = true }, 0, 2);
            tbPaterno = new TextBox { Dock = DockStyle.Fill };
            root.Controls.Add(tbPaterno, 1, 2);

            root.Controls.Add(new Label { Text = "Apellido Materno:", AutoSize = true }, 0, 3);
            tbMaterno = new TextBox { Dock = DockStyle.Fill };
            root.Controls.Add(tbMaterno, 1, 3);

            root.Controls.Add(new Label { Text = "Sexo (M/F):", AutoSize = true }, 0, 4);
            tbSexo = new TextBox { Dock = DockStyle.Left, Width = 60, MaxLength = 1 };
            root.Controls.Add(tbSexo, 1, 4);

            root.Controls.Add(new Label { Text = "Fecha Nac.:", AutoSize = true }, 0, 5);
            dpNac = new DateTimePicker { Dock = DockStyle.Left, Width = 160, Format = DateTimePickerFormat.Short, ShowCheckBox = true };
            root.Controls.Add(dpNac, 1, 5);

            var panelBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50 };
            var btnOk = new Button { Text = "Guardar", AutoSize = true };
            var btnCancel = new Button { Text = "Cancelar", AutoSize = true };
            btnOk.Click += (_, __) =>
            {
                if (string.IsNullOrWhiteSpace(tbNombre.Text))
                {
                    MessageBox.Show("El nombre es obligatorio.", "Validación",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Modelo.AlumnoID = init?.AlumnoID;
                Modelo.CURP = string.IsNullOrWhiteSpace(tbCurp.Text) ? null : tbCurp.Text.Trim();
                Modelo.Nombre = tbNombre.Text.Trim();
                Modelo.Paterno = string.IsNullOrWhiteSpace(tbPaterno.Text) ? null : tbPaterno.Text.Trim();
                Modelo.Materno = string.IsNullOrWhiteSpace(tbMaterno.Text) ? null : tbMaterno.Text.Trim();
                Modelo.Sexo = string.IsNullOrWhiteSpace(tbSexo.Text) ? null : tbSexo.Text.Trim().ToUpperInvariant();
                Modelo.FechaNac = dpNac.Checked ? dpNac.Value.Date : (DateTime?)null;

                DialogResult = DialogResult.OK;
            };
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;
            panelBtns.Controls.Add(btnOk);
            panelBtns.Controls.Add(btnCancel);
            Controls.Add(panelBtns);

            // Pre-carga si es edición
            if (init != null)
            {
                tbCurp.Text = init.CURP;
                tbNombre.Text = init.Nombre;
                tbPaterno.Text = init.Paterno;
                tbMaterno.Text = init.Materno;
                tbSexo.Text = init.Sexo;
            }
        }
    }

    // =========================== Resultados de Búsqueda ===========================
    internal sealed class FormResultadosBusqueda : Form
    {
        public event Action<AlumnoListaDto> OnAbrir;
        public event Action<AlumnoListaDto> OnEliminar;

        public FormResultadosBusqueda(IEnumerable<AlumnoListaDto> data, bool puedeEditar)
        {
            Text = "Resultados de búsqueda";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(720, 480);
            AutoScaleMode = AutoScaleMode.Dpi;

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AutoGenerateColumns = false
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "CURP", DataPropertyName = "CURP", Width = 150 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Género", DataPropertyName = "Sexo", Width = 70 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Paterno", DataPropertyName = "Paterno", Width = 140 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Materno", DataPropertyName = "Materno", Width = 140 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Nombre", DataPropertyName = "Nombre", Width = 200 });
            grid.DataSource = data.ToList();

            var menu = new ContextMenuStrip();
            var miAbrir = new ToolStripMenuItem("Abrir / Modificar");
            var miEliminar = new ToolStripMenuItem("Eliminar");
            miAbrir.Enabled = puedeEditar;
            miEliminar.Enabled = puedeEditar;
            miAbrir.Click += (_, __) =>
            {
                if (grid.CurrentRow?.DataBoundItem is AlumnoListaDto a) OnAbrir?.Invoke(a);
            };
            miEliminar.Click += (_, __) =>
            {
                if (grid.CurrentRow?.DataBoundItem is AlumnoListaDto a) OnEliminar?.Invoke(a);
            };
            menu.Items.Add(miAbrir);
            menu.Items.Add(miEliminar);
            grid.ContextMenuStrip = menu;

            grid.CellDoubleClick += (_, __) =>
            {
                if (grid.CurrentRow?.DataBoundItem is AlumnoListaDto a) OnAbrir?.Invoke(a);
            };

            Controls.Add(grid);
        }
    }

    // Panel redondeado con borde suave (para el buscador)
    sealed class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; } = 18;
        public Color BorderColor { get; set; } = Color.FromArgb(210, 220, 224);
        public int BorderThickness { get; set; } = 1;

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            using var path = BuildPath();
            Region = new Region(path);   // recorta la forma
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor, BorderThickness);
            using var path = BuildPath();
            e.Graphics.DrawPath(pen, path);       // dibuja el borde
        }

        private System.Drawing.Drawing2D.GraphicsPath BuildPath()
        {
            var r = ClientRectangle; r.Inflate(-1, -1);
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            int d = CornerRadius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

}
