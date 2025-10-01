using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    public partial class FormLogin : Form
    {
        // ===== Ajustes base =====
        private const int InputH = 24;      // alto de inputs
        private const int CardPad = 24;     // padding interno del card
        private const int CardRadius = 16;

        // Límites de tamaños
        private const int CardMinW = 420;
        private const int CardMinH = 340;
        private const int CardMaxW = 900;
        private const int CardMaxH = 620;

        private const int ContentMinW = 360;
        private const int ContentMaxW = 700;

        // Breakpoint para cambiar a 1 columna
        private const int NarrowBreakpoint = 480;

        // Ancho objetivo del bloque (labels + inputs) al estar en 2 columnas
        private const int BlockWMin = 520;   // aumenta para inputs más largos
        private const int BlockWMax = 620;   // límite para pantallas grandes

        // Ancho de la columna de etiquetas en 2 columnas
        private const int LabelColW = 140;   // súbelo si quieres empujar más los inputs a la derecha

        // === estado del layout actual (para no rehacer si no cambia)
        private bool _isTwoColumns = true;

        // ===== Controles =====
        private Panel card;
        private Panel content;
        private Label heading;
        private TableLayoutPanel tl;

        private Label lblUser, lblPass;
        private TextBox tbUser, tbPass;
        private Button btnEye, btnEnter;

        // Control de intentos
        private const int MaxIntentos = 3;
        private int intentosRestantes = MaxIntentos;
        private string usuarioIntentoActual = null;

        // Filas (paneles) para inputs; las necesitamos para hacer span al cambiar columnas
        private Panel rowUser, rowPass;

        public FormLogin()
        {
            

            // Ventana
            Text = "Iniciar sesión";
            StartPosition = FormStartPosition.CenterScreen;

            // Tamaño fijo de la ventana (ajústalo a lo que te guste)
            ClientSize = new Size(820, 560);     // <-- tamaño al abrir
            FormBorderStyle = FormBorderStyle.FixedDialog; // no redimensionable
            MaximizeBox = false;
            MinimizeBox = false;                 // ponlo true si quieres permitir minimizar
            SizeGripStyle = SizeGripStyle.Hide;
            BackColor = Color.FromArgb(31, 78, 95);
            Font = new Font("Segoe UI", 10f);
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;


            // Card (resizable)
            card = new Panel
            {
                BackColor = Color.FromArgb(244, 247, 247),
                Padding = new Padding(CardPad)
            };
            Controls.Add(card);
            SetRounded(card, CardRadius);

            // Contenido centrable
            content = new Panel { Dock = DockStyle.None };
            card.Controls.Add(content);

            // Título
            heading = new Label
            {
                Text = "Iniciar sesión",
                Font = new Font("Segoe UI Semibold", 22f),
                ForeColor = Color.FromArgb(15, 23, 42),
                Dock = DockStyle.Top,
                Height = 72,
                TextAlign = ContentAlignment.MiddleCenter
            };
            content.Controls.Add(heading);

            // TableLayout
            tl = new TableLayoutPanel
            {
                AutoSize = true,
                Margin = new Padding(0, 8, 0, 0),
            };
            content.Controls.Add(tl);
            tl.Top = heading.Bottom;
            tl.Left = 0;

            // Usuario
            lblUser = MakeLabel("Usuario:");
            tbUser = MakeTextBox(singleLine: true);
            rowUser = WrapInput(tbUser);

            // Contraseña + ojo
            lblPass = MakeLabel("Contraseña:");
            tbPass = MakeTextBox(singleLine: true);   // single-line para que funcione password
            tbPass.UseSystemPasswordChar = true;       // oculta al iniciar

            btnEye = new Button
            {
                Text = "👁",
                FlatStyle = FlatStyle.Flat,
                Width = 32,
                Height = InputH,
                TabStop = false
            };
            btnEye.FlatAppearance.BorderSize = 0;
            btnEye.Click += (_, __) =>
            {
                tbPass.UseSystemPasswordChar = !tbPass.UseSystemPasswordChar;
                btnEye.Text = tbPass.UseSystemPasswordChar ? "👁" : "🙈"; // opcional
                tbPass.Focus();
            };

            // Envolver password + ojo (SOLO UNA VEZ)
            rowPass = WrapPassword(tbPass, btnEye);

            //Enter
            btnEnter = new Button
            {
                Text = "Entrar",
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,                 // <- importante
                BackColor = Color.FromArgb(31, 78, 95),
                ForeColor = Color.White,
                Height = 44,
                MinimumSize = new Size(0, 44),
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 22, 0, 0)
            };
            btnEnter.FlatAppearance.BorderSize = 0;
            btnEnter.FlatAppearance.MouseOverBackColor = btnEnter.BackColor; // evita “brincos”
            btnEnter.FlatAppearance.MouseDownBackColor = btnEnter.BackColor;

            // redondeo inicial y cada vez que cambie de tamaño
            btnEnter.HandleCreated += (_, __) => SetRounded(btnEnter, 15); // cuando ya existe el handle
            btnEnter.Resize += (_, __) => SetRounded(btnEnter, 15); // y cada vez que cambie de tamaño

            // Enter = Entrar (después de crear el botón)
            AcceptButton = btnEnter;

            // Placeholders nativos
            SetCue(tbUser, "Usuario");
            SetCue(tbPass, "Contraseña");

            // Estructura inicial (se rehace en Reflow)
            BuildTable(twoColumns: true);

            // Eventos de layout / responsive
            Load += (_, __) => Reflow();
            Resize += (_, __) => Reflow();
            card.Resize += (_, __) => { SetRounded(card, CardRadius); Reflow(); };

            // Tab order
            tbUser.TabIndex = 0;
            tbPass.TabIndex = 1;
            btnEnter.TabIndex = 10;

            btnEnter.Click += BtnEnter_Click;  // enlazar el handler
            this.AcceptButton = btnEnter;       // Enter del teclado ejecuta click

            tbUser.TextChanged += (_, __) =>
            {
                usuarioIntentoActual = tbUser.Text.Trim();
                intentosRestantes = MaxIntentos;
                btnEnter.Enabled = true;
            };
        }


        // ================== Responsive ==================
        private void Reflow()
        {
            // Tamaño objetivo del card
            int targetW = Clamp((int)(ClientSize.Width * 0.80), 540, 780);
            int targetH = Clamp((int)(ClientSize.Height * 0.80), 360, 560);

            card.Size = new Size(targetW, targetH);
            card.Left = (ClientSize.Width - card.Width) / 2;
            card.Top = (ClientSize.Height - card.Height) / 2;

            // Ancho útil del contenido
            int contentW = Clamp(card.ClientSize.Width - card.Padding.Horizontal, 420, 620);
            content.Width = contentW;

            bool twoColumns = contentW >= 480;

            // Re-construye SOLO si cambia el número de columnas o aún no está armado
            if (tl == null)
            {
                tl = new TableLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 0) };
                content.Controls.Add(tl);
                tl.Top = heading.Bottom;
                BuildTable(twoColumns);
                _isTwoColumns = twoColumns;
            }
            else if (twoColumns != _isTwoColumns)
            {
                BuildTable(twoColumns);
                _isTwoColumns = twoColumns;
            }

            UpdateTypography(card.Width);

            // Limitar ancho del bloque y recalcular
            int blockW = Clamp(content.Width, 520, 620);
            tl.MaximumSize = new Size(blockW, 0);
            tl.PerformLayout();

            // Centrar bloque y ajustar alturas
            MeasureAndCenterContent();
        }


        private void MeasureAndCenterContent()
        {
            Size pref = tl.PreferredSize;
            int blockH = heading.Height + pref.Height + 8;

            content.Height = blockH;

            // Compactar card: no dejes hueco enorme
            int desiredCardH = Clamp(blockH + CardPad * 2 + 24, CardMinH, CardMaxH);
            card.Height = Math.Min(card.Height, desiredCardH);

            content.Left = (card.ClientSize.Width - content.Width) / 2;
            content.Top = (card.ClientSize.Height - content.Height) / 2;

            card.Left = (ClientSize.Width - card.Width) / 2;
            card.Top = (ClientSize.Height - card.Height) / 2;

            // Centrar la TLP en el content
            int visibleW = Math.Min(tl.MaximumSize.Width, pref.Width);
            tl.Left = (content.Width - visibleW) / 2;
            tl.Top = heading.Bottom;
        }



        private void UpdateTypography(int cardWidth)    
        {
            // título con escalado suave (18–24pt)
            float headingSize = Clamp(cardWidth / 28f, 18f, 24f);
            if (Math.Abs(heading.Font.Size - headingSize) > 0.5f)
                heading.Font = new Font("Aptos", headingSize, GraphicsUnit.Point);

            // inputs compactos y legibles
            var inputFont = new Font("Aptos", 12f, GraphicsUnit.Point);   // 12pt
            tbUser.Font = inputFont;
            tbPass.Font = inputFont;

            if (tbUser.Height != InputH) tbUser.Height = InputH;
            if (tbPass.Height != InputH) tbPass.Height = InputH;
            btnEye.Height = InputH;
        }

        private void BuildTable(bool twoColumns)
        {
            tl.SuspendLayout();

            // Quitar (sin Dispose) por si ya estaban puestos
            Control[] keep = { lblUser, rowUser, lblPass, rowPass, btnEnter };
            foreach (var c in keep)
            {
                if (c.Parent == tl) tl.Controls.Remove(c);
            }

            // Reset de estilos (esto NO disposea controles)
            tl.ColumnStyles.Clear();
            tl.RowStyles.Clear();
            tl.ColumnCount = 0;

            if (twoColumns)
            {
                tl.ColumnCount = 2;
                tl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelColW));
                tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

                // Usuario
                tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                lblUser.TextAlign = ContentAlignment.MiddleRight;
                tl.Controls.Add(lblUser, 0, 0);
                tl.Controls.Add(rowUser, 1, 0);

                // Contraseña
                tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                lblPass.TextAlign = ContentAlignment.MiddleRight;
                tl.Controls.Add(lblPass, 0, 1);
                tl.Controls.Add(rowPass, 1, 1);

                // Botón centrado, a lo ancho (colSpan=2)
                tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                btnEnter.Dock = DockStyle.Top;
                btnEnter.Anchor = AnchorStyles.None;
                btnEnter.Margin = new Padding(0, 22, 0, 0);
                tl.Controls.Add(btnEnter, 0, 2);
                tl.SetColumnSpan(btnEnter, 2);
            }
            else
            {
                tl.ColumnCount = 1;
                tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

                tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                lblUser.TextAlign = ContentAlignment.MiddleLeft;
                tl.Controls.Add(lblUser, 0, 0);

                tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tl.Controls.Add(rowUser, 0, 1);

                tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                lblPass.TextAlign = ContentAlignment.MiddleLeft;
                tl.Controls.Add(lblPass, 0, 2);

                tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                tl.Controls.Add(rowPass, 0, 3);

                tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                btnEnter.Dock = DockStyle.Top;
                btnEnter.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                btnEnter.Margin = new Padding(0, 22, 0, 0);
                tl.Controls.Add(btnEnter, 0, 4);
            }

            tl.ResumeLayout(true);
        }

        // ================== Acción de login ==================
        private void BtnEnter_Click(object sender, EventArgs e)
        {
            var usuario = tbUser.Text.Trim();
            var password = tbPass.Text;

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Escribe usuario y contraseña.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Si cambia el usuario, resetea contador
            if (!string.Equals(usuarioIntentoActual, usuario, StringComparison.OrdinalIgnoreCase))
            {
                usuarioIntentoActual = usuario;
                intentosRestantes = MaxIntentos;
                btnEnter.Enabled = true;
            }

            bool existe;
            try
            {
                existe = SesionApp.UsuarioExiste(usuario);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!existe)
            {
                MessageBox.Show("El usuario no existe.", "Acceso denegado",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // No descontamos intentos si el usuario ni existe
                tbUser.Focus();
                tbUser.SelectAll();
                return;
            }

            // Existe: aplica política de 3 intentos
            if (intentosRestantes <= 0)
            {
                MessageBox.Show("Has superado el máximo de 3 intentos. Intenta más tarde o contacta al administrador.",
                    "Bloqueado", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                btnEnter.Enabled = false;
                return;
            }

            bool ok;
            try
            {
                ok = SesionApp.IniciarSesion(usuario, password);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (ok)
            {
                var frm = new FormGeneral();

                frm.FormClosed += (_, __) =>
                {
                    this.Show();
                    ResetLoginForm();
                    try { SesionApp.CerrarSesion(); } catch { }
                };

                frm.Show();
                Hide();
            }
            else
            {
                intentosRestantes--;
                var msg = (intentosRestantes > 0)
                    ? $"Contraseña incorrecta. Intentos restantes: {intentosRestantes}."
                    : "Contraseña incorrecta. Has agotado los 3 intentos.";
                MessageBox.Show(msg, "Acceso denegado", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                if (intentosRestantes <= 0)
                    btnEnter.Enabled = false;

                tbPass.Clear();
                tbPass.UseSystemPasswordChar = true;
                btnEye.Text = "👁";
                tbPass.Focus();
            }
        }

        // ================== UI helpers ==================
        private Label MakeLabel(string text) => new Label
        {
            Text = text,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Margin = new Padding(0, 0, 8, 0)
        };

        private TextBox MakeTextBox(bool singleLine = false) => new TextBox
        {
            Multiline = !singleLine,              // password single-line
            AutoSize = false,
            Height = InputH,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.None,
            Font = new Font("Segoe UI", 11f),     // antes 12f
            ForeColor = Color.FromArgb(30, 41, 59),
            BackColor = Color.White,
            TextAlign = HorizontalAlignment.Center // <-- centrado horizontal
        };
        private Panel WrapInput(TextBox tb)
        {
            var row = new Panel { Height = InputH + 8, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 8) };

            var underline = new Panel
            {
                BackColor = Color.FromArgb(226, 232, 240),
                Height = 2,
                Dock = DockStyle.Bottom
            };

            tb.Dock = DockStyle.Fill;
            tb.Margin = new Padding(0);

            row.Controls.Add(tb);
            row.Controls.Add(underline);
            return row;
        }

        private Panel WrapPassword(TextBox tb, Button eye)
        {
            var row = new Panel { Height = InputH + 8, Dock = DockStyle.Top, Margin = new Padding(0, 4, 0, 8) };

            var underline = new Panel
            {
                BackColor = Color.FromArgb(226, 232, 240),
                Height = 2,
                Dock = DockStyle.Bottom
            };

            tb.Dock = DockStyle.Fill;
            tb.Margin = new Padding(0, 0, eye.Width + 8, 0);  // deja espacio para el ojo

            eye.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            eye.Left = row.Width - eye.Width;
            eye.Top = 0;

            row.Resize += (_, __) => eye.Left = row.Width - eye.Width;

            row.Controls.Add(tb);
            row.Controls.Add(eye);
            row.Controls.Add(underline);
            eye.BringToFront();   // que no quede tapado

            return row;
        }

        private static int Clamp(int val, int min, int max) => Math.Max(min, Math.Min(max, val));
        private static float Clamp(float val, float min, float max)
            => Math.Max(min, Math.Min(max, val));

        private void SetRounded(Control ctrl, int radius)
        {
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
        private void ResetLoginForm()
        {
            tbUser.Clear();
            tbPass.Clear();
            tbPass.UseSystemPasswordChar = true;
            btnEye.Text = "👁";
            intentosRestantes = MaxIntentos;
            usuarioIntentoActual = null;
            btnEnter.Enabled = true;
            tbUser.Focus();
        }

    }
}
