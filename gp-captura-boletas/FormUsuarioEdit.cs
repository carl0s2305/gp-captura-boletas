using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    public partial class FormUsuarioEdit : Form
    {
        private TextBox tbUsuario, tbNombre, tbEmail, tbPass, tbPass2;
        private ComboBox cbRol;
        private Button btnOk, btnCancel;

        public UsuarioDto Modelo { get; private set; }
        public string PasswordPlano { get; private set; } // null si no se cambia

        // ====== Paleta / Tipografía ======
        static readonly Color C_BG = Color.FromArgb(244, 247, 247); // #F4F7F7
        static readonly Color C_MID = Color.FromArgb(170, 207, 208); // #AACFD0
        static readonly Color C_ACCENT = Color.FromArgb(121, 168, 169); // #79A8A9
        static readonly Color C_PRIMARY = Color.FromArgb(31, 78, 95);    // #1F4E5F

        static Font Fx(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font("Berlin Sans FB", size, style); }
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

            // ===== Root: Título / Contenido / Botonera =====
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = C_BG,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(18)
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // título
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));     // contenido
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // botones
            Controls.Add(root);

            // ===== Título =====
            var lblTitle = new Label
            {
                Text = Text,
                ForeColor = C_PRIMARY,
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter
            };
            root.Controls.Add(lblTitle, 0, 0);

            // ===== Contenido centrado =====
            var contentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };
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
            tbEmail = MakeTextBox();

            cbRol = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = C_PRIMARY };
            if (existente == null) cbRol.Items.AddRange(new[] { "SECRETARIA" });
            else cbRol.Items.AddRange(new[] { "DIRECTOR", "SECRETARIA" });

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

            // ===== Filas (¡en el GRID, no en root!) =====
            AddRowToGrid(grid, "Usuario:", tbUsuario);
            AddRowToGrid(grid, "Nombre completo:", tbNombre);
            AddRowToGrid(grid, "Email:", tbEmail);
            AddRowToGrid(grid, "Rol:", cbRol);
            AddRowToGrid(grid, "Contraseña:", passPanel);
            AddRowToGrid(grid, "Confirmar contraseña:", confirmPanel);

            // Carga de edición
            if (existente != null)
            {
                tbUsuario.Text = existente.Usuario;
                tbNombre.Text = existente.Nombre;
                tbEmail.Text = existente.Email;
                cbRol.SelectedItem = existente.Rol;
                if (existente.Rol == "DIRECTOR") cbRol.Enabled = false;
            }
            else
            {
                cbRol.SelectedItem = "SECRETARIA";
            }

            // ===== Botonera abajo a la derecha =====
            var buttonsHost = new Panel { Dock = DockStyle.Fill, Height = 56 };
            root.Controls.Add(buttonsHost, 0, 2);

            var btnBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            buttonsHost.Controls.Add(btnBar);

            btnCancel = MakeOutlineButton("Cancelar");
            btnOk = MakeSolidButton("Guardar");
            btnBar.Controls.Add(btnCancel);
            btnBar.Controls.Add(new Panel { Width = 10 });
            btnBar.Controls.Add(btnOk);

            // Validación mínima de confirmación
            btnOk.Click += (_, __) =>
            {
                if (tbPass.Text != tbPass2.Text)
                {
                    MessageBox.Show("Las contraseñas no coinciden.", "Validación",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            btnCancel.Click += (_, __) => DialogResult = DialogResult.Cancel;

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void AddRowToGrid(TableLayoutPanel grid, string label, Control ctl)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            var lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = C_PRIMARY,
                Font = Fx(11f)
            };
            ctl.Dock = DockStyle.Fill; ctl.Margin = new Padding(4);
            grid.Controls.Add(lbl, 0, grid.RowCount);
            grid.Controls.Add(ctl, 1, grid.RowCount);
            grid.RowCount++;
        }


        // Helper
        private void AddRow(TableLayoutPanel tl, string label, Control ctl, int row)
        {
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tl.Controls.Add(new Label
            {
                Text = label,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(31, 78, 95)
            }, 0, row);
            ctl.Dock = DockStyle.Fill;
            tl.Controls.Add(ctl, 1, row);
        }


        // ===== Helpers de estilo =====
        private TextBox MakeTextBox(bool password = false)
        {
            var tb = new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = C_PRIMARY,
                Height = 28,
                Margin = new Padding(4),
                UseSystemPasswordChar = password
            };
            tb.GotFocus += (_, __) => tb.BackColor = Color.FromArgb(250, 253, 253);
            tb.LostFocus += (_, __) => tb.BackColor = Color.White;
            return tb;
        }

        private void AddRow(TableLayoutPanel grid, string label, Control ctl)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            var lbl = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = C_PRIMARY,
                Font = Fx(11)
            };
            ctl.Dock = DockStyle.Fill;
            ctl.Margin = new Padding(4, 4, 4, 4);

            grid.Controls.Add(lbl, 0, grid.RowCount);
            grid.Controls.Add(ctl, 1, grid.RowCount);
            grid.RowCount++;
        }

        private Button MakeSolidButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = false,
                Width = 110,
                Height = 36,
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderSize = 0;
            b.MouseEnter += (_, __) => b.BackColor = C_PRIMARY;
            b.MouseLeave += (_, __) => b.BackColor = C_ACCENT;
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
            b.FlatAppearance.BorderColor = C_MID;
            b.FlatAppearance.BorderSize = 1;
            b.MouseEnter += (_, __) => b.BackColor = Color.White;
            b.MouseLeave += (_, __) => b.BackColor = C_BG;
            return b;
        }

        private Control Spacer(int w) => new Panel { Width = w, Height = 1 };

        // (tu SetCue con SendMessage ya existente se mantiene)


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
    }
}
