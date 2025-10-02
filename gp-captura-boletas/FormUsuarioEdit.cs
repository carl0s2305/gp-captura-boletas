using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    public partial class FormUsuarioEdit : Form
    {
        private readonly bool _esEdicion;
        private Label lblUsuarioError;



        // ===== Validación en vivo =====
        private readonly int? _idEdicion;
        private ErrorProvider _err;
        private Label _lblPwd;
        private Timer _debounceUser;


        private TextBox tbUsuario, tbNombre, tbPass, tbPass2;
        private ComboBox cbRol;
        private Button btnOk, btnCancel;

        public UsuarioDto Modelo { get; private set; }
        public string PasswordPlano { get; private set; }

        // ====== Paleta / Tipografía ======
        static readonly Color C_BG = Color.FromArgb(244, 247, 247); // #F4F7F7
        static readonly Color C_MID = Color.FromArgb(170, 207, 208); // #AACFD0
        static readonly Color C_ACCENT = Color.FromArgb(121, 168, 169); // #79A8A9
        static readonly Color C_PRIMARY = Color.FromArgb(31, 78, 95);    // #1F4E5F

        static Font Fx(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font("Aptos", size, style); }
            catch { return new Font("Segoe UI", size, style); }
        }

        public FormUsuarioEdit(UsuarioDto existente = null)
        {
            Text = existente == null ? "Agregar usuario" : "Modificar usuario";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(560, 440);
            ClientSize = new Size(560, 440);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            BackColor = C_BG;
            Font = Fx(11f);

            _esEdicion = (existente != null);
            _idEdicion = existente?.UsuarioID;

            // ErrorProvider
            _err = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink, Icon = SystemIcons.Warning };

            // Timers de debounce (ms)
            _debounceUser = new Timer { Interval = 400 };
            _debounceUser.Tick += (_, __) => { _debounceUser.Stop(); ChecarUsuarioAsync(); };

            // ===== Root: Título / Contenido / Botonera =====
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0) // sin padding para que la franja ocupe todo el ancho
            };
            // Fila 0: franja fija / Fila 1: contenido / Fila 2: botones
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            // ===== Franja de título =====
            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_PRIMARY // #1F4E5F
            };
            root.Controls.Add(header, 0, 0);

            // línea sutil inferior
            header.Controls.Add(new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = Color.FromArgb(0, 0, 0, 40)
            });

            // título del formulario, centrado
            var lblTitle = new Label
            {
                Text = Text, // "Agregar usuario" o "Modificar usuario"
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = Fx(14f, FontStyle.Bold),
                ForeColor = Color.White,
                Padding = new Padding(8, 0, 8, 0)
            };
            header.Controls.Add(lblTitle);

            // ===== Contenido centrado =====
            var contentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
            root.Controls.Add(contentHost, 0, 1);

            var grid = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                BackColor = C_BG,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); // etiquetas
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // controles
            contentHost.Controls.Add(grid);
            grid.Anchor = AnchorStyles.Top;
            contentHost.Resize += (_, __) =>
            {
                grid.Left = Math.Max(0, (contentHost.ClientSize.Width - grid.PreferredSize.Width) / 2);
                grid.Top = 0;
            };

            // ===== Controles =====
            tbUsuario = MakeTextBox();
            tbNombre = MakeTextBox();

            cbRol = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.Black };
            if (!_esEdicion)
            {
                // Creación: solo SECRETARIA
                cbRol.Items.AddRange(new[] { "SECRETARIA/O" });
                cbRol.SelectedIndex = 0;
            }
            else
            {
                // Edición: si era DIRECTOR, no permitir cambiar; si era SECRETARIA, seguir siéndolo.
                if (existente.Rol == "DIRECTOR")
                {
                    cbRol.Items.Add("DIRECTOR");
                    cbRol.SelectedIndex = 0;
                    cbRol.Enabled = false;
                }
                else
                {
                    cbRol.Items.Add("SECRETARIA/O");
                    cbRol.SelectedIndex = 0;
                }
            }

            tbPass = MakeTextBox(true); SetCue(tbPass, "Contraseña");
            tbPass2 = MakeTextBox(true); SetCue(tbPass2, "Confirmar contraseña");

            // Ojos para mostrar/ocultar
            var passPanel = new Panel { Dock = DockStyle.Fill };
            var eye1 = new Button { Text = "👁", Width = 34, FlatStyle = FlatStyle.Flat };
            eye1.FlatAppearance.BorderSize = 0;
            eye1.Click += (_, __) => tbPass.UseSystemPasswordChar = !tbPass.UseSystemPasswordChar;
            passPanel.Controls.Add(tbPass); tbPass.Dock = DockStyle.Fill;
            passPanel.Controls.Add(eye1); eye1.Dock = DockStyle.Right;

            var confirmPanel = new Panel { Dock = DockStyle.Fill };
            var eye2 = new Button { Text = "👁", Width = 34, FlatStyle = FlatStyle.Flat };
            eye2.FlatAppearance.BorderSize = 0;
            eye2.Click += (_, __) => tbPass2.UseSystemPasswordChar = !tbPass2.UseSystemPasswordChar;
            confirmPanel.Controls.Add(tbPass2); tbPass2.Dock = DockStyle.Fill;
            confirmPanel.Controls.Add(eye2); eye2.Dock = DockStyle.Right;

            // ===== Filas (¡en el GRID) =====
            AddRowToGrid(grid, "Usuario:", tbUsuario);
            AddRowToGrid(grid, "Nombre completo:", tbNombre);
            AddRowToGrid(grid, "Rol:", cbRol);
            AddRowToGrid(grid, "Contraseña:", passPanel);
            AddRowToGrid(grid, "Confirmar contraseña:", confirmPanel);

            // Mensaje de error debajo del Usuario
            lblUsuarioError = new Label
            {
                ForeColor = Color.Red,
                AutoSize = true,
                Dock = DockStyle.Fill,
                Visible = false,
                Font = Fx(9f, FontStyle.Italic)
            };
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(lblUsuarioError, 1, grid.RowCount);
            grid.RowCount++;

            // Limpiar avisos al teclear
            tbUsuario.TextChanged += (_, __) =>
            {
                ClearUsuarioError();
                _debounceUser.Stop();
                _debounceUser.Start();
            };

            // Carga de edición
            if (existente != null)
            {
                tbUsuario.Text = existente.Usuario;
                tbNombre.Text = existente.Nombre;
                cbRol.SelectedItem = existente.Rol;
                if (existente.Rol == "DIRECTOR") cbRol.Enabled = false;
            }
            else
            {
                cbRol.SelectedItem = "SECRETARIA/O";
            }

            // ===== Botonera abajo a la derecha =====
            var actionBar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                Padding = new Padding(18, 8, 18, 12), // margen izq/der para que no se corte
                BackColor = C_BG
            };

            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actionBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            root.Controls.Add(actionBar, 0, 2);

            btnCancel = MakeOutlineButton("Cancelar");
            btnOk = MakeSolidButton("Guardar");

            // márgenes entre botones
            btnCancel.Margin = new Padding(0, 0, 10, 0);
            btnOk.Margin = new Padding(0);

            // agrega (el expansor vacío ocupa la izquierda)
            actionBar.Controls.Add(new Panel(), 0, 0);
            actionBar.Controls.Add(btnCancel, 1, 0);
            actionBar.Controls.Add(btnOk, 2, 0);

            // (opcional) asegura tamaño mínimo en escalados altos
            btnCancel.MinimumSize = new Size(110, 36);
            btnOk.MinimumSize = new Size(110, 36);

            // Validación de confirmación
            btnOk.Click += (_, __) =>
            {
                // al inicio del Click:
                ClearUsuarioError();

                string usuario = (tbUsuario.Text ?? "").Trim();
                string nombre = (tbNombre.Text ?? "").Trim();
                string rol = cbRol.SelectedItem as string;
                string p1 = tbPass.Text;
                string p2 = tbPass2.Text;

                // Validaciones 
                if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(rol))
                {
                    MessageBox.Show("Usuario, Nombre y Rol son obligatorios.", "Validación",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Validaciones de contraseña
                if (!_esEdicion)
                {
                    if (string.IsNullOrEmpty(p1) || string.IsNullOrEmpty(p2))
                    {
                        MessageBox.Show("Escribe y confirma la contraseña.", "Validación",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (p1 != p2)
                    {
                        MessageBox.Show("Las contraseñas no coinciden.", "Validación",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (!PasswordFuerte(p1, out var msg))
                    {
                        MessageBox.Show("Contraseña inválida: " + msg, "Seguridad",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                // Duplicados en BD
                bool dupU = false;
                try { dupU = UsuarioRepo.ExistsUsername(usuario, _idEdicion); } catch { }

                if (dupU)
                {
                    lblUsuarioError.Text = "Este nombre de usuario ya está en uso.";
                    lblUsuarioError.Visible = true;
                    tbUsuario.BackColor = Color.MistyRose;
                    tbUsuario.Focus();
                    return;  // aquí se corta, no cierra el formulario
                }

                // Si pasó todas las validaciones: asigna modelo
                Modelo ??= new UsuarioDto();
                Modelo.Usuario = usuario;
                Modelo.Nombre = nombre;
                Modelo.Rol = rol;
                PasswordPlano = (!_esEdicion) ? p1 : (string.IsNullOrWhiteSpace(p1) ? null : p1);

                DialogResult = DialogResult.OK; // Solo aquí se cierra
            };

            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // ===== Checklist de contraseña (fila debajo de confirmación) =====
            _lblPwd = new Label
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = Color.FromArgb(190, 60, 60), // rojo por defecto
                Padding = new Padding(0, 6, 0, 6),
                Visible = false // oculto de inicio
            };
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grid.Controls.Add(_lblPwd, 0, grid.RowCount);
            grid.SetColumnSpan(_lblPwd, 2);
            grid.RowCount++;

            tbPass.TextChanged += (_, __) => ActualizarChecklistPassword();
            tbPass2.TextChanged += (_, __) => ActualizarChecklistPassword();

            tbUsuario.Leave += (_, __) => ChecarUsuarioAsync();

            ActualizarChecklistPassword(); // estado inicial correcto
        }

        private void MarcarError(Control c, string msg)
        {
            _err.SetError(c, msg);
            c.BackColor = Color.MistyRose;
        }
        private void LimpiarError(Control c)
        {
            _err.SetError(c, null);
            c.BackColor = Color.White;
        }
        private void ActualizarChecklistPassword()
        {
            string p1 = tbPass.Text ?? "";
            string p2 = tbPass2.Text ?? "";

            bool okLen = p1.Length >= 8;
            bool okUpp = Regex.IsMatch(p1, "[A-Z]");
            bool okLow = Regex.IsMatch(p1, "[a-z]");
            bool okNum = Regex.IsMatch(p1, "[0-9]");
            bool okSpc = Regex.IsMatch(p1, "[^A-Za-z0-9]");
            bool okEq = string.IsNullOrEmpty(p1) && string.IsNullOrEmpty(p2) ? false : (p1 == p2);

            bool anyTyped = p1.Length > 0 || p2.Length > 0;
            bool allOk = okLen && okUpp && okLow && okNum && okSpc && okEq;

            // Mostrar solo si el usuario está escribiendo y aún falta algo
            _lblPwd.Visible = anyTyped && !allOk;

            string Mark(bool v, string t) => $"{(v ? "✔" : "✖")} {t}";
            _lblPwd.Text =
                Mark(okLen, "8+ car.") + "   " +
                Mark(okUpp, "Mayús.") + "   " +
                Mark(okLow, "Minús.") + Environment.NewLine +
                Mark(okNum, "Número") + "   " +
                Mark(okSpc, "Símbolo") + "   " +
                Mark(okEq, "Coinciden");

            _lblPwd.ForeColor = allOk ? Color.FromArgb(30, 120, 70) : Color.FromArgb(190, 60, 60);
        }

        private async void ChecarUsuarioAsync()
        {
            string u = (tbUsuario.Text ?? "").Trim();
            if (u.Length == 0) { LimpiarError(tbUsuario); return; }

            try
            {
                bool existe = await System.Threading.Tasks.Task.Run(() => UsuarioRepo.ExistsUsername(u, _idEdicion));
                if (existe) MarcarError(tbUsuario, "Este nombre de usuario ya está en uso.");
                else LimpiarError(tbUsuario);
            }
            catch {}
        }
        private void AddRowToGrid(TableLayoutPanel grid, string label, Control ctl)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.Black,
                Font = Fx(11f)
            };
            ctl.Dock = DockStyle.Fill; ctl.Margin = new Padding(4);
            grid.Controls.Add(lbl, 0, grid.RowCount);
            grid.Controls.Add(ctl, 1, grid.RowCount);
            grid.RowCount++;
        }

        // ===== Helpers de estilo =====
        private TextBox MakeTextBox(bool password = false)
        {
            var tb = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Color.Black,
                Height = 28,
                Margin = new Padding(4),
                UseSystemPasswordChar = password
            };
            tb.GotFocus += (_, __) => tb.BackColor = Color.FromArgb(250, 253, 253);
            tb.LostFocus += (_, __) => tb.BackColor = Color.White;
            return tb;
        }
        private Button MakeSolidButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = false,
                Width = 110,
                Height = 36,
                BackColor = C_PRIMARY,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (_, __) => b.BackColor = C_ACCENT;
            b.MouseLeave += (_, __) => b.BackColor = C_PRIMARY;
            return b;
        }

        private Button MakeOutlineButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = false,
                Width = 110,
                Height = 36,
                BackColor = C_BG,
                ForeColor = C_PRIMARY,
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderColor = C_PRIMARY;
            b.FlatAppearance.BorderSize = 1;
            b.MouseEnter += (_, __) => b.BackColor = Color.White;
            b.MouseLeave += (_, __) => b.BackColor = C_BG;
            return b;
        }
        // Validador de fuerza
        private bool PasswordFuerte(string p, out string mensaje)
        {
            if (p == null) p = "";
            if (p.Length < 8) { mensaje = "mínimo 8 caracteres."; return false; }
            if (!Regex.IsMatch(p, "[A-Z]")) { mensaje = "debe tener al menos una mayúscula."; return false; }
            if (!Regex.IsMatch(p, "[a-z]")) { mensaje = "debe tener al menos una minúscula."; return false; }
            if (!Regex.IsMatch(p, "[0-9]")) { mensaje = "debe tener al menos un número."; return false; }
            if (!Regex.IsMatch(p, "[^A-Za-z0-9]")) { mensaje = "debe tener al menos un carácter especial."; return false; }
            mensaje = null; return true;
        }

        // Cue banner nativo
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        private const int EM_SETCUEBANNER = 0x1501;
        private void SetCue(TextBox tb, string placeholder)
        {
            if (tb.IsHandleCreated)
                SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
            else
                tb.HandleCreated += (s, e) => SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
        }
        private void ClearUsuarioError()
        {
            lblUsuarioError.Visible = false;
            lblUsuarioError.Text = string.Empty;
            tbUsuario.BackColor = Color.White;
            _err.SetError(tbUsuario, null); // por si aún usas ErrorProvider
        }
    }
}
