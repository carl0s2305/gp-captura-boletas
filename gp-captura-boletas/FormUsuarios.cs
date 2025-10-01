using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // Lado izquierdo: acciones
            var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16), AutoScroll = true };
            root.Controls.Add(left, 0, 0);

            btnAdd = new Button { Text = "➕  Agregar Usuario", Width = 180, Height = 36 };
            btnEdit = new Button { Text = "✏️  Modificar Usuario", Width = 180, Height = 36 };
            btnDel = new Button { Text = "🗑️  Eliminar Usuario", Width = 180, Height = 36 };
            left.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnDel });

            // Centro: grilla
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false
            };
            root.Controls.Add(grid, 1, 0);

            // Columnas
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UsuarioID", HeaderText = "ID", Width = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Usuario", HeaderText = "Usuario", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Nombre", HeaderText = "Nombre", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Email", HeaderText = "Email", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 45 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Rol", HeaderText = "Rol", Width = 110 });

            bs = new BindingSource();
            grid.DataSource = bs;

            Load += (_, __) => Refrescar();

            // Acciones
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
                    if (UsuarioRepo.ExistsEmail(dlg.Modelo.Email, null))
                    {
                        MessageBox.Show("El correo ya está en uso.", "Validación",
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
                                       dlg.PasswordPlano, // ya validada
                                       dlg.Modelo.Nombre,
                                       dlg.Modelo.Email,
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
                    if (UsuarioRepo.ExistsEmail(dlg.Modelo.Email, sel.UsuarioID))
                    {
                        MessageBox.Show("El correo ya está en uso.", "Validación",
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
                                       dlg.Modelo.Usuario, dlg.Modelo.Nombre, dlg.Modelo.Email, dlg.Modelo.Rol,
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
