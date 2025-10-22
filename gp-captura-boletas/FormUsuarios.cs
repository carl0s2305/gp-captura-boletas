using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    sealed class UserCard : Panel
    {
        public UsuarioDto Data { get; }
        public event Action<UserCard> OnSelected;
        public event Action<UserCard> OnOpen;

        private Label lblNombre, lblUsuario, lblRol;
        private Panel badge;
        private bool _selected;

        // Estilo
        private static readonly Color CBorder = Color.FromArgb(220, 225, 230);
        private static readonly Color CHover = Color.FromArgb(248, 251, 252);
        private static readonly Color CSel = Color.FromArgb(170, 207, 208);
        private static readonly Color CCard = Color.White;
        private static readonly Color CRol = Color.FromArgb(31, 78, 95);

        public bool IsSelected
        {
            get => _selected;
            set { _selected = value; Invalidate(); }
        }

        public UserCard(UsuarioDto data)
        {
            DoubleBuffered = true;
            Data = data;

            Width = 280;
            Height = 120;
            BackColor = CCard;
            Cursor = Cursors.Hand;

            Padding = new Padding(12, 10, 12, 10);
            Margin = new Padding(10);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            // ===== Contenido centrado en columna =====
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // nombre
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // usuario
            host.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // badge
            Controls.Add(host);

            // Nombre (centrado y en negritas)
            lblNombre = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 28,
                Text = string.IsNullOrWhiteSpace(data.Nombre) ? "(Sin nombre)" : data.Nombre,
                Font = new Font("Aptos", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            host.Controls.Add(lblNombre, 0, 0);

            // Usuario (centrado) — “nombre del user”
            lblUsuario = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 22,
                Text = "Usuario: " + data.Usuario,   // aquí está el "nombre de user"
                ForeColor = Color.FromArgb(70, 70, 70),
                TextAlign = ContentAlignment.MiddleCenter
            };
            host.Controls.Add(lblUsuario, 0, 1);

            // Badge Rol (centrado)
            badge = new Panel
            {
                Height = 26,
                Width = 140,
                BackColor = Color.FromArgb(235, 241, 243),
                Margin = new Padding(0, 6, 0, 0)
            };
            lblRol = new Label
            {
                AutoSize = true,
                Text = data.Rol,
                ForeColor = CRol,
                Font = new Font("Aptos", 9.5f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            // Centramos el label dentro del badge usando un FlowLayout
            var badgeFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = new Padding(8, 4, 8, 4)
            };
            badgeFlow.Controls.Add(lblRol);
            badge.Controls.Add(badgeFlow);

            // Contenedor para centrar el badge horizontalmente
            var badgeHost = new Panel { Dock = DockStyle.Top, Height = badge.Height + 8 };
            badge.Parent = badgeHost;
            badgeHost.Controls.Add(badge);
            badge.Anchor = AnchorStyles.Top;
            // centrado dinámico
            badgeHost.Resize += (_, __) => badge.Left = (badgeHost.ClientSize.Width - badge.Width) / 2;

            host.Controls.Add(badgeHost, 0, 2);

            // ===== Eventos (click en TODO el panel) =====
            WireClicksRecursive(this);       // <- esto asegura click en cualquier zona
            MouseEnter += (_, __) => { if (!IsSelected) { BackColor = CHover; Invalidate(); } };
            MouseLeave += (_, __) => { if (!IsSelected) { BackColor = CCard; Invalidate(); } };
        }

        // Engancha Click / DoubleClick a todos los hijos recursivamente
        private void WireClicksRecursive(Control root)
        {
            root.Click += (_, __) => OnSelected?.Invoke(this);
            root.DoubleClick += (_, __) => OnOpen?.Invoke(this);
            foreach (Control c in root.Controls)
                WireClicksRecursive(c);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var pen = new Pen(IsSelected ? CSel : CBorder, IsSelected ? 2f : 1f);
            var r = ClientRectangle; r.Width -= 1; r.Height -= 1;
            g.DrawRectangle(pen, r);
        }
    }
    public partial class FormUsuarios : Form
    {
        private FlowLayoutPanel cards;
        private BindingSource bs;
        private Button btnAdd, btnEdit, btnDel;

        private UserCard selectedCard;

        public FormUsuarios()
        {
            Text = "Administrar Usuarios";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(890, 520);
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            this.Font = new Font("Aptos", 10f, FontStyle.Regular);

            // ===== Contenedor padre (2 filas: header y contenido) =====
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.White
            };
            outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));     // header fijo
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));     // contenido
            Controls.Add(outer);

            // ===== Encabezado verde =====
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(31, 78, 95) };
            outer.Controls.Add(header, 0, 0);

            header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(0, 0, 0, 40) });

            var lblHeader = new Label
            {
                Text = "Usuarios",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Aptos", 14f, FontStyle.Bold)
            };
            header.Controls.Add(lblHeader);

            // ===== Contenido (2 columnas: acciones + grilla) =====
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.White
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            outer.Controls.Add(root, 0, 1);

            // Lado izquierdo: acciones
            var left = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(16),
                AutoScroll = true,
                BackColor = Color.FromArgb(244, 247, 247) // mismo tono que tu app
            };
            root.Controls.Add(left, 0, 0);

            btnAdd = new Button { Text = "➕  Agregar Usuario", Width = 180, Height = 36 };
            btnEdit = new Button { Text = "✏️  Modificar Usuario", Width = 180, Height = 36 };
            btnDel = new Button { Text = "🗑️  Eliminar Usuario", Width = 180, Height = 36 };
            left.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDel });



            // Centro: contenedor de tarjetas
            cards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.White,
                Padding = new Padding(16),
                Margin = Padding.Empty
            };
            root.Controls.Add(cards, 1, 0);

            bs = new BindingSource();

            Load += (_, __) => Refrescar();
            btnAdd.Click += (_, __) => Agregar();
            btnEdit.Click += (_, __) => ModificarSeleccionado();
            btnDel.Click += (_, __) => EliminarSeleccionado();
        }

        private UsuarioDto Seleccionado()
        {
            return selectedCard?.Data;
        }

        private void Refrescar()
        {
            List<UsuarioDto> data;
            try { data = UsuarioRepo.GetAll(); }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar usuarios: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            bs.DataSource = data;
            RenderCards(data);
        }

        private void RenderCards(IEnumerable<UsuarioDto> data)
        {
            cards.SuspendLayout();
            cards.Controls.Clear();
            selectedCard = null;

            foreach (var u in data)
            {
                var card = new UserCard(u);
                card.Margin = new Padding(10);
                card.OnSelected += c =>
                {
                    // limpiar selección previa
                    if (selectedCard != null && selectedCard != c)
                        selectedCard.IsSelected = false;
                    selectedCard = c;
                    selectedCard.IsSelected = true;
                };
                card.OnOpen += _ => ModificarSeleccionado();  // doble click
                cards.Controls.Add(card);
            }

            cards.ResumeLayout(true);
        }

        private void Agregar()
        {
            var dlg = new FormUsuarioEdit();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    // Unicidad
                    if (UsuarioRepo.ExistsUsername(dlg.Modelo.Usuario, null))
                    {
                        MessageBox.Show("El nombre de usuario ya está en uso.", "Validación",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Regla: NO crear DIRECTOR (el form ya lo impide, doble seguro)
                    if (dlg.Modelo.Rol == "DIRECTOR")
                    {
                        MessageBox.Show("No está permitido crear usuarios con rol DIRECTOR.", "Política",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    UsuarioRepo.Insert(dlg.Modelo.Usuario,
                                       dlg.PasswordPlano,
                                       dlg.Modelo.Nombre,
                                       dlg.Modelo.Rol);
                    Refrescar();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo agregar: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ModificarSeleccionado()
        {
            var sel = Seleccionado();
            if (sel == null)
            {
                MessageBox.Show("Selecciona un usuario.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dlg = new FormUsuarioEdit(sel);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    // Unicidad (excluyendo al propio ID)
                    if (UsuarioRepo.ExistsUsername(dlg.Modelo.Usuario, sel.UsuarioID))
                    {
                        MessageBox.Show("El nombre de usuario ya está en uso.", "Validación",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Si el seleccionado es DIRECTOR, mantener rol director (el diálogo ya lo bloquea)
                    if (sel.Rol == "DIRECTOR" && dlg.Modelo.Rol != "DIRECTOR")
                    {
                        MessageBox.Show("No puedes cambiar el rol de un DIRECTOR.", "Política",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    UsuarioRepo.Update(sel.UsuarioID,
                                       dlg.Modelo.Usuario, dlg.Modelo.Nombre, dlg.Modelo.Rol,
                                       dlg.PasswordPlano); // null = no cambiar pass
                    Refrescar();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo modificar: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void EliminarSeleccionado()
        {
            var sel = Seleccionado();
            if (sel == null)
            {
                MessageBox.Show("Selecciona un usuario.", "Aviso",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // No auto-eliminarse
            if (sel.UsuarioID == SesionApp.UsuarioID)
            {
                MessageBox.Show("No puedes eliminar tu propio usuario.", "Política",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // No eliminar DIRECTOR
            if (sel.Rol == "DIRECTOR")
            {
                MessageBox.Show("No está permitido eliminar usuarios con rol DIRECTOR.", "Política",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show($"¿Eliminar al usuario '{sel.Usuario}'?",
                                "Confirmar eliminación",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                try
                {
                    UsuarioRepo.Delete(sel.UsuarioID);
                    Refrescar();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo eliminar: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
