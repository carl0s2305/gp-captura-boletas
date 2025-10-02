using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    public partial class FormUsuarios : Form
    {
        private DataGridView grid;
        private BindingSource bs;
        private Button btnAdd, btnEdit, btnDel;

        public FormUsuarios()
        {
            Text = "Administrar Usuarios";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(820, 520);
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

            // Centro
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,

                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(230, 230, 230),
                EnableHeadersVisualStyles = false
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 78, 95);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Aptos", 10f, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(30, 30, 30);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(170, 207, 208);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 30, 30);
            grid.ColumnHeadersHeight = 34;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            root.Controls.Add(grid, 1, 0);

            // Columnas
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UsuarioID", HeaderText = "ID", Width = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Usuario", HeaderText = "Usuario", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Nombre", HeaderText = "Nombre", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Rol", HeaderText = "Rol", Width = 110 });

            bs = new BindingSource();
            grid.DataSource = bs;

            Load += (_, __) => Refrescar();
            btnAdd.Click += (_, __) => Agregar();
            btnEdit.Click += (_, __) => ModificarSeleccionado();
            btnDel.Click += (_, __) => EliminarSeleccionado();
            grid.CellDoubleClick += (_, __) => ModificarSeleccionado();
        }

        private UsuarioDto Seleccionado()
        {
            return bs.Current as UsuarioDto;
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
