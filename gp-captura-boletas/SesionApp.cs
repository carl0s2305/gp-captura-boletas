using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace gp_captura_boletas
{
    public static class SesionApp
    {
        public static int UsuarioID { get; private set; }
        public static string Usuario { get; private set; }
        public static string Rol { get; private set; }
        public static string Nombre { get; private set; }

        private static string ConnectionString =>
            ConfigurationManager.ConnectionStrings["LocalSql"].ConnectionString;

        // SesionApp.cs
        public static string ConnStr => ConfigurationManager.ConnectionStrings["LocalSql"].ConnectionString;

        // Inicia sesión validando en la BD
        public static bool IniciarSesion(string usuario, string password)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("spLogin_Validar", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Usuario", usuario);
                        cmd.Parameters.AddWithValue("@Password", password);

                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                UsuarioID = rdr.GetInt32(rdr.GetOrdinal("UsuarioID"));
                                Rol = rdr.GetString(rdr.GetOrdinal("Rol"));
                                Nombre = rdr.GetString(rdr.GetOrdinal("NombreCompleto"));
                                Usuario = usuario;
                                return true;
                            }
                            else
                            {
                                return false; // Usuario o contraseña incorrectos
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al iniciar sesión: " + ex.Message);
            }
        }

        // Cierra sesión llamando a procedimiento almacenado
        public static void CerrarSesion()
        {
            if (UsuarioID <= 0) return;

            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("Sesion_Finalizar", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@UsuarioID", UsuarioID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch { /* no romper el cierre */ }
            finally
            {
                UsuarioID = 0; Usuario = null; Rol = null; Nombre = null;
            }
        }

        public static bool UsuarioExiste(string usuario)
        {
            try
            {
                using (var conn = new SqlConnection(ConnectionString))
                using (var cmd = new SqlCommand("SELECT 1 FROM dbo.Usuario WHERE Usuario = @u", conn))
                {
                    cmd.Parameters.AddWithValue("@u", usuario);
                    conn.Open();
                    var r = cmd.ExecuteScalar();
                    return r != null;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error al verificar usuario: " + ex.Message);
            }
        }

    }
}
