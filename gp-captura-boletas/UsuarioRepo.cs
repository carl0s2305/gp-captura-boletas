using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace gp_captura_boletas
{
    public class UsuarioRepo
    {
        public static List<UsuarioDto> GetAll()
        {
            var list = new List<UsuarioDto>();
            using var cn = new SqlConnection(SesionApp.ConnStr);
            using var cmd = new SqlCommand(
                "SELECT UsuarioID, Usuario, NombreCompleto, Email, Rol FROM dbo.Usuario ORDER BY UsuarioID", cn);
            cn.Open();
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new UsuarioDto
                {
                    UsuarioID = rd.GetInt32(0),
                    Usuario = rd.GetString(1),
                    Nombre = rd.GetString(2),
                    Email = rd.IsDBNull(3) ? "" : rd.GetString(3),
                    Rol = rd.GetString(4)
                });
            }
            return list;
        }
        public static bool ExistsUsername(string usuario, int? excludeId = null)
        {
            using (var cn = new SqlConnection(SesionApp.ConnStr))
            using (var cmd = new SqlCommand(@"
SELECT 1
FROM dbo.Usuario
WHERE Usuario = @u AND (@id IS NULL OR UsuarioID <> @id);", cn))
            {
                cmd.Parameters.AddWithValue("@u", usuario);
                cmd.Parameters.AddWithValue("@id", (object)excludeId ?? DBNull.Value);
                cn.Open();
                var x = cmd.ExecuteScalar();
                return x != null;
            }
        }

        public static bool ExistsEmail(string email, int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            using (var cn = new SqlConnection(SesionApp.ConnStr))
            using (var cmd = new SqlCommand(@"
SELECT 1
FROM dbo.Usuario
WHERE Email = @e AND (@id IS NULL OR UsuarioID <> @id);", cn))
            {
                cmd.Parameters.AddWithValue("@e", email);
                cmd.Parameters.AddWithValue("@id", (object)excludeId ?? DBNull.Value);
                cn.Open();
                var x = cmd.ExecuteScalar();
                return x != null;
            }
        }

        public static int CountDirectors()
        {
            using (var cn = new SqlConnection(SesionApp.ConnStr))
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Usuario WHERE Rol='DIRECTOR';", cn))
            {
                cn.Open();
                return (int)cmd.ExecuteScalar();
            }
        }


        public static int Insert(string usuario, string passwordPlano, string nombre, string email, string rol)
        {
            using var cn = new SqlConnection(SesionApp.ConnStr);
            using var cmd = new SqlCommand(@"
INSERT INTO dbo.Usuario(Usuario, HashPassword, NombreCompleto, Email, Rol)
VALUES (@u, HASHBYTES('SHA2_256', @p), @n, @e, @r);
SELECT SCOPE_IDENTITY();", cn);
            cmd.Parameters.AddWithValue("@u", usuario);
            cmd.Parameters.AddWithValue("@p", passwordPlano);
            cmd.Parameters.AddWithValue("@n", nombre);
            cmd.Parameters.AddWithValue("@e", (object)email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@r", rol);
            cn.Open();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static void Update(int id, string usuario, string nombre, string email, string rol, string passwordPlanoOrNull)
        {
            using var cn = new SqlConnection(SesionApp.ConnStr);
            cn.Open();

            if (!string.IsNullOrWhiteSpace(passwordPlanoOrNull))
            {
                using var cmd = new SqlCommand(@"
UPDATE dbo.Usuario
   SET Usuario=@u, NombreCompleto=@n, Email=@e, Rol=@r, HashPassword=HASHBYTES('SHA2_256', @p)
 WHERE UsuarioID=@id;", cn);
                cmd.Parameters.AddWithValue("@u", usuario);
                cmd.Parameters.AddWithValue("@n", nombre);
                cmd.Parameters.AddWithValue("@e", (object)email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@r", rol);
                cmd.Parameters.AddWithValue("@p", passwordPlanoOrNull);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = new SqlCommand(@"
UPDATE dbo.Usuario
   SET Usuario=@u, NombreCompleto=@n, Email=@e, Rol=@r
 WHERE UsuarioID=@id;", cn);
                cmd.Parameters.AddWithValue("@u", usuario);
                cmd.Parameters.AddWithValue("@n", nombre);
                cmd.Parameters.AddWithValue("@e", (object)email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@r", rol);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public static void Delete(int id)
        {
            using var cn = new SqlConnection(SesionApp.ConnStr);
            using var cmd = new SqlCommand("DELETE FROM dbo.Usuario WHERE UsuarioID=@id;", cn);
            cmd.Parameters.AddWithValue("@id", id);
            cn.Open();
            cmd.ExecuteNonQuery();
        }
    }
}
