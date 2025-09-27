using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Configuration;

namespace gp_captura_boletas
{

    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.FormClosing += (_, __) => SesionApp.CerrarSesion();
        }

        private void btnProbarConexion_Click(object sender, EventArgs e)
        {
            string cs = ConfigurationManager.ConnectionStrings["AzureSql"].ConnectionString;

            try
            {
                using (SqlConnection conn = new SqlConnection(cs))
                {
                    conn.Open();
                    MessageBox.Show("✅ Conexión exitosa a la base de datos en Azure",
                                    "Conexión",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Error al conectar: " + ex.Message,
                                "Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private void CerrarSesion()
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["AzureSql"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(cs))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("Sesion_Cerrar", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@UsuarioID", SesionApp.UsuarioID); // el ID guardado al iniciar
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // No bloquea la salida, solo loguea
                Console.WriteLine("Error al cerrar sesión: " + ex.Message);
            }
        }

    }
}
