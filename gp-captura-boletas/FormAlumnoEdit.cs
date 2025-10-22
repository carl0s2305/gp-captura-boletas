    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.SqlClient;
    using System.Drawing;
    using System.Globalization;
    using System.Net;
    using System.Net.Mail;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    #nullable enable


    namespace gp_captura_boletas
    {
        public partial class FormAlumnoEdit : Form
        {
            // Estado derivado del CURP
            private int? _gradoCalc;             // 1..6
            private string? _gradoNombreCalc;    // "Primer año", etc.

            // Anti-doble envío + rate-limit
            private bool _isSendingCode;
            private string? _lastCodeEmail;
            private DateTime? _lastCodeSentUtc;
            private string? _lastCodeValue;
            private static readonly TimeSpan CODE_COOLDOWN = TimeSpan.FromMinutes(1);

            // ===== Paleta =====
            static readonly Color C_BG = Color.FromArgb(244, 247, 247);
            static readonly Color C_PRIMARY = Color.FromArgb(31, 78, 95);
            static readonly Color C_ACCENT = Color.FromArgb(121, 168, 169);
            static Font Fx(float s, FontStyle st = FontStyle.Regular)
            { try { return new Font("Aptos", s, st); } catch { return new Font("Segoe UI", s, st); } }

        // ===== Reglas =====
            private static readonly Regex RxSoloLetras = new Regex(@"^(?=.{1,200}$)[\p{L}]+(?:\s[\p{L}]+)*$", RegexOptions.Compiled);
            private static readonly Regex RxEmail = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
            private static readonly Regex RxCurp = new Regex(
                @"^[A-Z][AEIOUX][A-Z]{2}\d{2}(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])[HM][A-Z]{2}[B-DF-HJ-NP-TV-Z]{3}[0-9A-Z]\d$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // ===== Controles =====
            private ErrorProvider _err;

            // Stepper
            private Panel stepAlumno, stepTutor;
            private Button btnBack, btnNextSave;

            // Alumno
            private TextBox tbCURP, tbNombre, tbPaterno, tbMaterno;
            private DateTimePicker dpNac;
            private ComboBox cbSexo;

            // Tutor
            private TextBox tbTNombre, tbTel, tbEmail;

            // Estado
            private int? _alumnoIdSiEdicion;

            public FormAlumnoEdit(AlumnoEditModel init = null)
            {
                Text = init == null ? "Agregar Alumno" : "Modificar Alumno";
                StartPosition = FormStartPosition.CenterParent;
                MinimumSize = new Size(680, 540);
                ClientSize = new Size(720, 560);
                BackColor = C_BG;
                Font = Fx(11f);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = MinimizeBox = false;

                _alumnoIdSiEdicion = init?.AlumnoID;

                _err = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

                // ===== Root (3 filas) =====
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 3,
                    ColumnCount = 1,
                    BackColor = C_BG
                };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));     // header
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));     // contenido
                root.RowStyles.Add(new RowStyle(SizeType.AutoSize));         // botones
                Controls.Add(root);

                // ===== Header =====
                var header = new Panel { Dock = DockStyle.Fill, BackColor = C_PRIMARY };
                header.Controls.Add(new Label
                {
                    Text = "Inscripción de Alumno",
                    Dock = DockStyle.Fill,
                    ForeColor = Color.White,
                    Font = Fx(16, FontStyle.Bold),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(0, 0, 0, 40) });
                root.Controls.Add(header, 0, 0);

                // ===== Contenido (steps centrados) =====
                var contentHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18) };
                root.Controls.Add(contentHost, 0, 1);

                // Step Alumno
                stepAlumno = CrearStepAlumno();
                // Step Tutor
                stepTutor = CrearStepTutor();

                contentHost.Controls.Add(stepAlumno);
                contentHost.Controls.Add(stepTutor);
                stepAlumno.Dock = stepTutor.Dock = DockStyle.Fill;
                stepAlumno.Visible = true;
                stepTutor.Visible = false;

                // ===== Barra de acciones =====
                var actions = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 3,
                    Padding = new Padding(18, 8, 18, 12),
                    BackColor = C_BG
                };
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                root.Controls.Add(actions, 0, 2);

                btnBack = new Button
                {
                    Text = "← Anterior",
                    AutoSize = false,
                    Width = 120,
                    Height = 36,
                    BackColor = C_BG,
                    ForeColor = C_PRIMARY,
                    FlatStyle = FlatStyle.Flat
                };
                btnBack.FlatAppearance.BorderColor = C_PRIMARY;
                btnBack.FlatAppearance.BorderSize = 1;
                btnBack.Enabled = false;
                btnBack.Click += (_, __) => MostrarAlumno();

                btnNextSave = new Button
                {
                    Text = "Siguiente →",
                    AutoSize = false,
                    Width = 120,
                    Height = 36,
                    BackColor = C_PRIMARY,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnNextSave.FlatAppearance.BorderSize = 0;
                btnNextSave.Click += BtnNextSave_Click;

                actions.Controls.Add(new Panel(), 0, 0);
                actions.Controls.Add(btnBack, 1, 0);
                actions.Controls.Add(btnNextSave, 2, 0);

                // Precarga si edición (solo muestra, no cambiamos fecha/sexo si no viene CURP)
                if (init != null)
                {
                    tbCURP.Text = init.CURP ?? "";
                    tbNombre.Text = init.Nombre ?? "";
                    tbPaterno.Text = init.Paterno ?? "";
                    tbMaterno.Text = init.Materno ?? "";
                    if (init.FechaNac.HasValue) { dpNac.Value = init.FechaNac.Value; dpNac.Enabled = false; }
                    if (!string.IsNullOrWhiteSpace(init.Sexo))
                    {
                        cbSexo.SelectedItem = init.Sexo.ToUpperInvariant().StartsWith("M") ? "M" : "H";
                        cbSexo.Enabled = false;
                    }
                }

                AcceptButton = btnNextSave;
                CancelButton = btnBack;
            }

            private bool TryDerivarGrado(DateTime fechaNac, out int grado, out string nombre)
            {
                // Mapa por AÑO DE NACIMIENTO
                var y = fechaNac.Year;
                switch (y)
                {
                    case 2019: grado = 1; nombre = "Primer año"; return true;
                    case 2018: grado = 2; nombre = "Segundo año"; return true;
                    case 2017: grado = 3; nombre = "Tercer año"; return true;
                    case 2016: grado = 4; nombre = "Cuarto año"; return true;
                    case 2015: grado = 5; nombre = "Quinto año"; return true;
                    case 2014: grado = 6; nombre = "Sexto año"; return true;
                    default: grado = 0; nombre = ""; return false;
                }
            }
            private async Task EnviarCodigoAsync(string email, string code)
            {
                if (_isSendingCode) return; 
                _isSendingCode = true;
                btnNextSave.Enabled = false;

                try
                {
                    await MailSender.SendAsync(email, "Código de verificación",
                        $"Tu código para registrar tutor es: {code}");
                }
                finally
                {
                    btnNextSave.Enabled = true;
                    _isSendingCode = false;
                }
            }

            // =================== STEP 1: ALUMNO ===================
            private Panel CrearStepAlumno()
            {
                var host = new Panel { BackColor = C_BG };

                var grid = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Dock = DockStyle.Top,
                    ColumnCount = 2,
                    BackColor = C_BG,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };

                // ancho fijo para que no se vaya demasiado a la derecha
                const int W_LABEL = 220;  // etiquetas
                const int W_INPUT = 420;  // zona de entradas (ajústalo a tu gusto)

                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, W_LABEL));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, W_INPUT));

                // Centrado horizontal del bloque completo
                host.Controls.Add(grid);
                host.Resize += (_, __) =>
                {
                    int total = W_LABEL + W_INPUT;
                    grid.Left = Math.Max(0, (host.ClientSize.Width - total) / 2);
                    grid.Top = 12;
                };

                // ----- Controles -----
                tbCURP = MakeTextBox(); SetCue(tbCURP, "CURP (18 caracteres)");
                tbNombre = MakeTextBox(); SetCue(tbNombre, "Nombre(s)");
                tbPaterno = MakeTextBox(); SetCue(tbPaterno, "Apellido paterno");
                tbMaterno = MakeTextBox(); SetCue(tbMaterno, "Apellido materno");

                // Longitudes
                tbCURP.MaxLength = 18;
                tbNombre.MaxLength = 30;
                tbPaterno.MaxLength = 30;
                tbMaterno.MaxLength = 30;

                // Forzar a mayúsculas mientras teclean
                tbNombre.CharacterCasing = CharacterCasing.Upper;
                tbPaterno.CharacterCasing = CharacterCasing.Upper;
                tbMaterno.CharacterCasing = CharacterCasing.Upper;

                // CURP: mayúsculas + solo letra/dígito + no pasar de 18
                tbCURP.CharacterCasing = CharacterCasing.Upper;

                tbCURP.KeyPress += (s, e) =>
                {
                    if (!char.IsControl(e.KeyChar) && !char.IsLetterOrDigit(e.KeyChar)) { e.Handled = true; return; }
                    // si no hay selección y ya llegó al tope, bloquear
                    if (!char.IsControl(e.KeyChar) && tbCURP.SelectionLength == 0 && tbCURP.TextLength >= tbCURP.MaxLength)
                        e.Handled = true;
                };
                // Si pegan algo más largo, recortar
                tbCURP.TextChanged += (_, __) =>
                {
                    if (tbCURP.TextLength > tbCURP.MaxLength)
                    {
                        int pos = tbCURP.SelectionStart;
                        tbCURP.Text = tbCURP.Text.Substring(0, tbCURP.MaxLength);
                        tbCURP.SelectionStart = Math.Min(pos, tbCURP.TextLength);
                    }
                };

                dpNac = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 160, Anchor = AnchorStyles.Left, Enabled = false };
                cbSexo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80, Anchor = AnchorStyles.Left, Enabled = false };
                cbSexo.Items.AddRange(new[] { "H", "M" });

                AddRow(grid, "CURP:", tbCURP);
                AddRow(grid, "Nombre(s):", tbNombre);
                AddRow(grid, "Apellido paterno:", tbPaterno);
                AddRow(grid, "Apellido materno:", tbMaterno);
                AddRow(grid, "Fecha de nacimiento:", dpNac);
                AddRow(grid, "Sexo:", cbSexo);

                // Entradas válidas
                tbNombre.KeyPress += SoloLetras_KeyPress;
                tbPaterno.KeyPress += SoloLetras_KeyPress;
                tbMaterno.KeyPress += SoloLetras_KeyPress;

                // Validaciones live
                tbCURP.Leave += (_, __) => ValidarYDerivarCurp();
                tbNombre.Leave += (_, __) => ValidarNombreMin(tbNombre, 3);
                tbPaterno.Leave += (_, __) => ValidarNombreMin(tbPaterno, 3);
                tbMaterno.Leave += (_, __) => ValidarNombreMin(tbMaterno, 3);


            return host;
            }
            // Cambia a "HM" si tu tabla permite H/M; deja "FM" si permite F/M.
            private const string DB_SEXO_SET = "FM";
            private static string MapSexoForDb(char curpSexo)
            {
                curpSexo = char.ToUpperInvariant(curpSexo); // 'H' o 'M' según CURP
                if (DB_SEXO_SET == "FM")
                    return (curpSexo == 'H') ? "M" : "F";   // H->M (Masculino), M->F (Femenino)
                else
                    return (curpSexo == 'H') ? "H" : "M";   // ya coincide con H/M
            }


            private Panel CrearStepTutor()
            {
                var host = new Panel { BackColor = C_BG };

                var grid = new TableLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Dock = DockStyle.Top,
                    ColumnCount = 2,
                    BackColor = C_BG,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };

                // ancho fijo para que no se vaya demasiado a la derecha
                const int W_LABEL = 220;  // etiquetas
                const int W_INPUT = 420;  // zona de entradas (ajústalo a tu gusto)

                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, W_LABEL));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, W_INPUT));

                // Centrado horizontal del bloque completo
                host.Controls.Add(grid);
                host.Resize += (_, __) =>
                {
                    int total = W_LABEL + W_INPUT;
                    grid.Left = Math.Max(0, (host.ClientSize.Width - total) / 2);
                    grid.Top = 12;
                };

                tbTNombre = MakeTextBox(); SetCue(tbTNombre, "Nombre del padre/madre/tutor");
                tbTel = MakeTextBox(); SetCue(tbTel, "Teléfono (solo dígitos)");
                tbEmail = MakeTextBox(); SetCue(tbEmail, "Correo electrónico");

                // Longitudes recomendadas
                tbTNombre.MaxLength = 50;
                tbTel.MaxLength = 10;
                tbEmail.MaxLength = 100;

                tbTNombre.CharacterCasing = CharacterCasing.Upper;
                // Recomendado para email:
                tbEmail.CharacterCasing = CharacterCasing.Lower;

                // Validaciones de tecleo
                tbTNombre.KeyPress += SoloLetras_KeyPress;
                tbTel.KeyPress += (s, e) =>
                {
                    if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
                    if (!char.IsControl(e.KeyChar) && tbTel.SelectionLength == 0 && tbTel.TextLength >= tbTel.MaxLength)
                        e.Handled = true;
                };

                AddRow(grid, "Nombre del tutor:", tbTNombre);
                AddRow(grid, "Teléfono:", tbTel);
                AddRow(grid, "Correo:", tbEmail);

                tbTNombre.KeyPress += SoloLetras_KeyPress;
                tbTel.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };

                tbTNombre.Leave += (_, __) => ValidarNombreMin(tbTNombre, 3);
                tbTel.Leave += (_, __) => ValidarTelefono();
                tbEmail.Leave += (_, __) => _err.SetError(tbEmail, (tbEmail.Text.Trim().Length == 0 || !RxEmail.IsMatch(tbEmail.Text.Trim())) ? "Correo inválido." : null);

                return host;
            }

            // =================== Navegación / Guardado ===================
            private void MostrarAlumno()
            {
                stepAlumno.Visible = true;
                stepTutor.Visible = false;
                btnBack.Enabled = false;
                btnNextSave.Text = "Siguiente →";
            }

            private void MostrarTutor()
            {
                stepAlumno.Visible = false;
                stepTutor.Visible = true;
                btnBack.Enabled = true;
                btnNextSave.Text = "Guardar";
            }

            private async void BtnNextSave_Click(object sender, EventArgs e)
            {
                if (stepAlumno.Visible)
                {
                    // Validar alumno
                    if (!ValidarYDerivarCurp()) return;
                    if (!ValidarNombreMin(tbNombre, 3)) return;
                    if (!ValidarNombreMin(tbPaterno, 3)) return;
                    if (!ValidarNombreMin(tbMaterno, 3)) return;
                // Duplicado de CURP
                try
                {
                        bool existe = await System.Threading.Tasks.Task.Run(() => AlumnoRepo.ExistsCURP(tbCURP.Text.Trim()));
                        if (existe)
                        {
                            _err.SetError(tbCURP, "Esta CURP ya existe en el sistema.");
                            tbCURP.Focus(); return;
                        }
                    }
                    catch { /* si falla la consulta, no bloquees aquí */ }

                    MostrarTutor();
                    return;
                }

            // === Paso Tutor ===
                if (!ValidarNombreMin(tbTNombre, 3)) return;
                if (!ValidarTelefono()) return;
                if (string.IsNullOrWhiteSpace(tbEmail.Text) || !RxEmail.IsMatch(tbEmail.Text.Trim()))
                { _err.SetError(tbEmail, "Correo inválido."); tbEmail.Focus(); return; }

                // ➊ Resolver duplicados y/o verificar con código
                var res = await ResolverTutorDuplicadoYVerificarAsync();
                if (res == null) return; // canceló o falló verificación
                var (esExistente, tutorIdExistente) = res.Value;

                try
                {
                    int alumnoId = GuardarAlumnoYVinculo(esExistente ? tutorIdExistente : (int?)null);
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("No se pudo guardar: " + ex.Message, "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            private bool ValidarTelefono()
            {
                // Deja solo dígitos por si pegaron con espacios o guiones
                string t = Regex.Replace(tbTel.Text ?? "", @"\D", "");
                tbTel.Text = t; // normaliza en el control

                // 10 dígitos exactos
                if (t.Length != 10)
                {
                    _err.SetError(tbTel, "Debe tener exactamente 10 dígitos.");
                    tbTel.Focus(); return false;
                }

                // En MX los números no deben iniciar con 0 o 1
                if (t[0] < '2')
                {
                    _err.SetError(tbTel, "El número no puede iniciar con 0 o 1.");
                    tbTel.Focus(); return false;
                }

                // Evita todos iguales: 0000000000, 1111111111, etc.
                if (Regex.IsMatch(t, @"^(\d)\1{9}$"))
                {
                    _err.SetError(tbTel, "Número inválido (dígitos repetidos).");
                    tbTel.Focus(); return false;
                }

                // Evita secuencias obvias
                if (t is "0123456789" or "1234567890" or "9876543210" or "0987654321")
                {
                    _err.SetError(tbTel, "Número inválido (secuencia).");
                    tbTel.Focus(); return false;
                }   
                if (t is "1231231234" or "1112223333" or "5555555555")
                {
                    _err.SetError(tbTel, "Número inválido (patrón no permitido).");
                    tbTel.Focus(); return false;
                }

                _err.SetError(tbTel, null);
                return true;
            }


            private async Task<(bool esExistente, int tutorId)?> ResolverTutorDuplicadoYVerificarAsync()
            {
                string tel = tbTel.Text.Trim();
                string emailLow = tbEmail.Text.Trim().ToLowerInvariant();

                using var cn = Db.New();
                cn.Open();
                using var tx = cn.BeginTransaction();

                var hitEmail = TutorRepo.FindByEmail(cn, tx, emailLow);
                var hitTel = TutorRepo.FindByTelefono(cn, tx, tel);

                if (hitEmail != null || hitTel != null)
                {
                    var hit = hitEmail ?? hitTel;
                    string por = hitEmail != null ? "correo" : "teléfono";
                    var ans = MessageBox.Show(
                        $"Ese {por} ya está registrado para:\n\n{hit!.Nombre}\n\n¿Deseas ligar al alumno con ese tutor?",
                        "Tutor existente", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    tx.Commit();
                    if (ans == DialogResult.Yes) return (true, hit.TutorID);
                    return null;
                }

                tx.Commit();

                // ========= Envío con rate-limit =========
                string code;
                var now = DateTime.UtcNow;
                int initialCountdown = 0;

                if (_lastCodeValue != null &&
                    string.Equals(_lastCodeEmail, emailLow, StringComparison.OrdinalIgnoreCase) &&
                    _lastCodeSentUtc.HasValue &&
                    now - _lastCodeSentUtc.Value < CODE_COOLDOWN)
                {
                    // Reusar el mismo código y mostrar cuánto falta para poder reenviar
                    code = _lastCodeValue;
                    initialCountdown = (int)Math.Ceiling((CODE_COOLDOWN - (now - _lastCodeSentUtc.Value)).TotalSeconds);
                    MessageBox.Show($"Ya enviamos un código a {emailLow}. " +
                                    $"Puedes reenviar otro en {initialCountdown} segundos.",
                                    "Espera para reenviar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    code = GenerarCodigo6();
                    try
                    {
                        await EnviarCodigoAsync(emailLow, code);
                        _lastCodeValue = code;
                        _lastCodeEmail = emailLow;
                        _lastCodeSentUtc = DateTime.UtcNow;
                        initialCountdown = (int)CODE_COOLDOWN.TotalSeconds;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("No se pudo enviar el código de verificación por correo.\n" + ex.Message,
                            "Comunicación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return null;
                    }
                }

                // ========= Diálogo moderno con countdown y "Reenviar" =========
                while (true)
                {
                    using var dlg = new VerifyCodeDialog2(emailLow, initialCountdown);
                    var dr = dlg.ShowDialog(this);

                    if (dr == DialogResult.Cancel)
                        return null;

                    if (dr == DialogResult.OK)
                    {
                        if (string.Equals(dlg.CodigoIngresado, code, StringComparison.Ordinal))
                            return (false, 0); // verificado, se creará nuevo tutor
                        MessageBox.Show("El código no coincide.", "Verificación",
                                         MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        continue;
                    }

                    if (dr == DialogResult.Retry) // pidió reenviar y el diálogo ya desbloqueó el botón
                    {
                        // respeta cooldown (por si abren múltiples diálogos)
                        now = DateTime.UtcNow;
                        if (_lastCodeSentUtc.HasValue && now - _lastCodeSentUtc.Value < CODE_COOLDOWN)
                        {
                            initialCountdown = (int)Math.Ceiling((CODE_COOLDOWN - (now - _lastCodeSentUtc.Value)).TotalSeconds);
                            continue;
                        }

                        code = GenerarCodigo6();
                        try
                        {
                            await EnviarCodigoAsync(emailLow, code);
                            _lastCodeValue = code;
                            _lastCodeEmail = emailLow;
                            _lastCodeSentUtc = DateTime.UtcNow;
                            initialCountdown = (int)CODE_COOLDOWN.TotalSeconds;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("No se pudo reenviar el código.\n" + ex.Message,
                                "Comunicación", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return null;
                        }

                        // vuelve a mostrar el diálogo desde 60s
                        continue;
                    }
                }
            }

            private static string GenerarCodigo6()
            {
                var rnd = new Random();
                return rnd.Next(100000, 999999).ToString();
            }


            private int GuardarAlumnoYVinculo(int? tutorIdExistente = null)
            {
                using var cn = Db.New();
                cn.Open();
                using var tx = cn.BeginTransaction();

                // Normalización previa
                string curpUp = tbCURP.Text.Trim().ToUpperInvariant();
                string nombreUp = tbNombre.Text.Trim().ToUpperInvariant();
                string paternoUp = tbPaterno.Text.Trim().ToUpperInvariant();
                string maternoUp = tbMaterno.Text.Trim().ToUpperInvariant();
                char curpSexo = char.ToUpperInvariant(tbCURP.Text.Trim()[10]);
                string sexoUp = MapSexoForDb(curpSexo);
                DateTime fecha = dpNac.Value.Date;

                string tutorNomUp = tbTNombre.Text.Trim().ToUpperInvariant();
                string tel = tbTel.Text.Trim();
                string emailLow = tbEmail.Text.Trim().ToLowerInvariant();

                int alumnoId;

                if (_alumnoIdSiEdicion.HasValue)
                {
                    using var cmdU = new SqlCommand(@"
    UPDATE dbo.Alumno
       SET CURP=@CURP, Nombre=@Nombre, Paterno=@Paterno, Materno=@Materno, Sexo=@Sexo, FechaNac=@FechaNac
     WHERE AlumnoID=@ID;", cn, tx);

                    cmdU.Parameters.AddWithValue("@CURP", curpUp);
                    cmdU.Parameters.AddWithValue("@Nombre", nombreUp);
                    cmdU.Parameters.AddWithValue("@Paterno", paternoUp);
                    cmdU.Parameters.AddWithValue("@Materno", maternoUp);
                    cmdU.Parameters.AddWithValue("@Sexo", sexoUp);
                    cmdU.Parameters.AddWithValue("@FechaNac", fecha);
                    cmdU.Parameters.AddWithValue("@ID", _alumnoIdSiEdicion.Value);

                    cmdU.ExecuteNonQuery();
                    alumnoId = _alumnoIdSiEdicion.Value;
                }
                else
                {
                    using var cmdI = new SqlCommand(@"
    SET NOCOUNT ON;
    INSERT INTO dbo.Alumno (CURP,Nombre,Paterno,Materno,Sexo,FechaNac)
    VALUES (@CURP,@Nombre,@Paterno,@Materno,@Sexo,@FechaNac);
    SELECT CAST(SCOPE_IDENTITY() AS int);", cn, tx);

                    cmdI.Parameters.AddWithValue("@CURP", curpUp);
                    cmdI.Parameters.AddWithValue("@Nombre", nombreUp);
                    cmdI.Parameters.AddWithValue("@Paterno", paternoUp);
                    cmdI.Parameters.AddWithValue("@Materno", maternoUp);
                    cmdI.Parameters.AddWithValue("@Sexo", sexoUp);
                    cmdI.Parameters.AddWithValue("@FechaNac", fecha);

                    alumnoId = (int)cmdI.ExecuteScalar();   // devuelve int por el CAST
                }

                // Tutor
                int tutorId;
                if (tutorIdExistente.HasValue)
                {
                    tutorId = tutorIdExistente.Value; // ligar al existente
                }
                else
                {
                    // crear SI NO existe (por robustez si dos pantallas guardan a la vez)
                    tutorId = TutorRepo.GetOrCreateByEmailOTelefono(cn, tx, emailLow, tel, tutorNomUp);
                };

                // Vincular
                AlumnoTutorRepo.Link(cn, tx, alumnoId, tutorId);

                // Asegura grado derivado (por si viniste sin pasar por ValidarYDerivarCurp)
                if (!_gradoCalc.HasValue || string.IsNullOrEmpty(_gradoNombreCalc))
                {
                    if (!TryDerivarGrado(dpNac.Value.Date, out var g, out var nom))
                        throw new InvalidOperationException("No se pudo derivar el grado desde la fecha.");
                    _gradoCalc = g; _gradoNombreCalc = nom;
                }

                // Buscar grupo por grado
                System.Diagnostics.Debug.WriteLine($"[Asignación] Grado a usar = {_gradoCalc}");
                var grupo = GrupoRepo.FindByGrado(cn, tx, _gradoCalc.Value);
                if (grupo == null)
                    throw new InvalidOperationException($"No hay grupo para grado {_gradoCalc}.");
                System.Diagnostics.Debug.WriteLine($"[Asignación] Grupo elegido = {grupo.GrupoID} ({grupo.Nombre})");


                // Asignar alumno→grupo
                AlumnoGrupoRepo.Assign(cn, tx, alumnoId, grupo.GrupoID);

                // Guarda para mostrar luego
                int grupoIdAsignado = grupo.GrupoID;
                string grupoNombreAsignado = grupo.Nombre;

                tx.Commit();

                AppEvents.RaiseDatosCambiaron();

                // Mostrar grupo y materias (solo consulta; fuera de la transacción)
                try
                {
                    using var cnShow = Db.New();
                    cnShow.Open();
                    var materias = MateriaRepo.ListByGrado(cnShow, _gradoCalc!.Value);
                    string lista = materias.Count == 0 ? "(sin materias configuradas)" : string.Join("\n• ", materias);
                    MessageBox.Show(
                        $"Alumno asignado a: {_gradoNombreCalc} — Grupo: {grupoNombreAsignado}\n\nMaterias:\n• {lista}",
                        "Asignación de grupo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch { /* si falla la lista, no bloquees el alta */ }

                return alumnoId;
            }

            // =================== Validaciones ===================
            private bool ValidarYDerivarCurp()
            {
                string curp = tbCURP.Text.Trim().ToUpperInvariant();

                if (curp.Length != 18 || !RxCurp.IsMatch(curp))
                {
                    _err.SetError(tbCURP, "CURP inválida (revisa formato).");
                    tbCURP.Focus(); return false;
                }

                // Derivar fecha: posiciones 5-10 (yyMMdd) -> [4..9]
                string yy = curp.Substring(4, 2);
                string mm = curp.Substring(6, 2);
                string dd = curp.Substring(8, 2);

                int y = int.Parse(yy);
                int m = int.Parse(mm);
                int d = int.Parse(dd);

                // Inferir siglo
                int fullY = (y <= DateTime.Now.Year % 100 ? 2000 + y : 1900 + y);

                DateTime fecha;
                try
                {
                    fecha = new DateTime(fullY, m, d);
                }
                catch
                {
                    _err.SetError(tbCURP, "Fecha inválida derivada de CURP.");
                    tbCURP.Focus(); return false;
                }

                // Asignar fecha y sexo a los controles
                dpNac.Value = fecha;
                cbSexo.SelectedItem = (curp[10] == 'H') ? "H" : "M";

                // Ahora sí, derivar grado usando la fecha correcta
                if (!TryDerivarGrado(fecha, out var grado, out var nombre))
                {
                    _err.SetError(tbCURP, "Solo se aceptan alumnos nacidos entre 2014 y 2019.");
                    tbCURP.Focus(); return false;
                }

                _gradoCalc = grado;
                _gradoNombreCalc = nombre;

                System.Diagnostics.Debug.WriteLine(
        $"[CURP] Fecha={dpNac.Value:yyyy-MM-dd} => Grado={_gradoCalc} ({_gradoNombreCalc})");

                _err.SetError(tbCURP, null);
                return true;
            }

            private bool ValidarNombre(TextBox tb, bool permitirVacio = false)
            {
                string t = tb.Text.Trim();
                if (t.Length == 0)
                {
                    if (permitirVacio) { _err.SetError(tb, null); return true; }
                    _err.SetError(tb, "Obligatorio."); tb.Focus(); return false;
                }
                if (!RxSoloLetras.IsMatch(t))
                {
                    _err.SetError(tb, "Solo letras y espacios.");
                    tb.Focus(); return false;
                }
                _err.SetError(tb, null);
                return true;
            }
        // Normaliza espacios: recorta extremos y comprime espacios múltiples a uno
        private static string NormalizeSpaces(string s) =>
            Regex.Replace((s ?? "").Trim(), @"\s{2,}", " ");

        // Valida texto de nombres con mínimo (minLen) y máximo (usa tb.MaxLength)
        private bool ValidarNombreMin(TextBox tb, int minLen, bool permitirVacio = false)
        {
            string t = NormalizeSpaces(tb.Text);

            if (t.Length == 0)
            {
                if (permitirVacio) { _err.SetError(tb, null); return true; }
                _err.SetError(tb, "Obligatorio.");
                tb.Focus(); return false;
            }

            if (t.Length < minLen)
            {
                _err.SetError(tb, $"Debe tener al menos {minLen} caracteres.");
                tb.Focus(); return false;
            }

            if (t.Length > tb.MaxLength)
            {
                _err.SetError(tb, $"Máximo {tb.MaxLength} caracteres.");
                tb.Focus(); return false;
            }

            if (!RxSoloLetras.IsMatch(t))
            {
                _err.SetError(tb, "Solo letras y un espacio entre palabras.");
                tb.Focus(); return false;
            }

            // Si pasó, escribe el texto normalizado en el control
            tb.Text = t;
            _err.SetError(tb, null);
            return true;
        }
        private void SoloLetras_KeyPress(object? sender, KeyPressEventArgs e)
            {
            char ch = e.KeyChar;
            if (char.IsControl(ch)) return;
            if (!(char.IsLetter(ch) || char.IsWhiteSpace(ch))) { e.Handled = true; return; }

            // Evita dos espacios seguidos
            if (char.IsWhiteSpace(ch) && sender is TextBox tb)
            {
                int pos = tb.SelectionStart;
                string txt = tb.Text;
                if (pos > 0 && pos <= txt.Length && (pos == txt.Length ? txt.EndsWith(" ") : txt[pos - 1] == ' '))
                    e.Handled = true;
            }

        }

            // =================== Helpers UI ===================
            private TextBox MakeTextBox()
            {
                var tb = new TextBox
                {
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    Height = 28,
                    Margin = new Padding(4),
                    AutoSize = false
                };
                tb.GotFocus += (_, __) => tb.BackColor = Color.FromArgb(250, 253, 253);
                tb.LostFocus += (_, __) => tb.BackColor = Color.White;
                _err.SetIconAlignment(tb, ErrorIconAlignment.MiddleRight);
                _err.SetIconPadding(tb, -28);
                return tb;
            }

            private void AddRow(TableLayoutPanel grid, string label, Control ctl)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                var lbl = new Label
                {
                    Text = label,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = Fx(11)
                };
                grid.Controls.Add(lbl, 0, grid.RowCount);

                // Para entradas de texto sí llenamos; para DTP/Combo los dejamos a la izquierda
                if (ctl is DateTimePicker || ctl is ComboBox)
                {
                    ctl.Anchor = AnchorStyles.Left;
                    // tamaño cómodo:
                    if (ctl is DateTimePicker dtp) dtp.Width = 160;
                    if (ctl is ComboBox cb) cb.Width = 80;
                }
                else
                {
                    ctl.Dock = DockStyle.Fill;   // TextBox llenan la columna
                }

                grid.Controls.Add(ctl, 1, grid.RowCount);
                grid.RowCount++;
            }

            // Cue banner nativo
            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
            private const int EM_SETCUEBANNER = 0x1501;
            private void SetCue(TextBox tb, string placeholder)
            {
                if (tb.IsHandleCreated) SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
                else tb.HandleCreated += (s, e) => SendMessage(tb.Handle, EM_SETCUEBANNER, (IntPtr)1, placeholder);
            }


        }

        // =================== Repos mínimos para Tutor / vínculo ===================

        internal static class TutorRepo
        {
            internal sealed class TutorHit
            {
                public int TutorID { get; set; }
                public string Nombre { get; set; } = "";
            }

            // Buscar por email
            public static TutorHit? FindByEmail(SqlConnection cn, SqlTransaction tx, string email)
            {
                using var cmd = new SqlCommand(
                    "SELECT TOP 1 TutorID, Nombre FROM dbo.Tutor WHERE Email=@E;", cn, tx);
                cmd.Parameters.AddWithValue("@E", email);
                using var rd = cmd.ExecuteReader();
                if (!rd.Read()) return null;
                return new TutorHit { TutorID = rd.GetInt32(0), Nombre = rd.GetString(1) };
            }

            // Buscar por teléfono
            public static TutorHit? FindByTelefono(SqlConnection cn, SqlTransaction tx, string telefono)
            {
                using var cmd = new SqlCommand(
                    "SELECT TOP 1 TutorID, Nombre FROM dbo.Tutor WHERE Telefono=@T;", cn, tx);
                cmd.Parameters.AddWithValue("@T", telefono);
                using var rd = cmd.ExecuteReader();
                if (!rd.Read()) return null;
                return new TutorHit { TutorID = rd.GetInt32(0), Nombre = rd.GetString(1) };
            }

            // Crear si no existe (checa email o teléfono)
            public static int GetOrCreateByEmailOTelefono(SqlConnection cn, SqlTransaction tx,
                string email, string telefono, string nombre)
            {
                // Prioriza email
                var hitE = FindByEmail(cn, tx, email);
                if (hitE != null) return hitE.TutorID;

                var hitT = FindByTelefono(cn, tx, telefono);
                if (hitT != null) return hitT.TutorID;

                using var cmdIns = new SqlCommand(@"
    SET NOCOUNT ON;
    INSERT INTO dbo.Tutor(Nombre, Telefono, Email)
    VALUES(@N,@T,@E);
    SELECT CAST(SCOPE_IDENTITY() AS int);", cn, tx);
                cmdIns.Parameters.AddWithValue("@N", nombre);
                cmdIns.Parameters.AddWithValue("@T", (object?)telefono ?? DBNull.Value);
                cmdIns.Parameters.AddWithValue("@E", (object?)email ?? DBNull.Value);
                return Convert.ToInt32(cmdIns.ExecuteScalar());
            }

            public static bool ExistsEmail(string email)
            {
                const string sql = "SELECT 1 FROM dbo.Tutor WHERE Email = @E;";
                using var cn = Db.New();
                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@E", email);
                cn.Open();
                var o = cmd.ExecuteScalar();
                return o != null && o != DBNull.Value;
            }

            // Usado dentro de transacción externa
            public static int GetOrCreateByEmail(SqlConnection cn, SqlTransaction tx, string email, string nombre, string telefono)
            {
                // 1) existe?
                var cmdSel = new SqlCommand("SELECT TutorID FROM dbo.Tutor WHERE Email=@E;", cn, tx);
                cmdSel.Parameters.AddWithValue("@E", email);
                var o = cmdSel.ExecuteScalar();
                if (o != null && o != DBNull.Value) return Convert.ToInt32(o);

                // 2) crear
                var cmdIns = new SqlCommand(@"
    INSERT INTO dbo.Tutor(Nombre, Telefono, Email)
    VALUES(@N,@T,@E);
    SELECT SCOPE_IDENTITY();", cn, tx);
                cmdIns.Parameters.AddWithValue("@N", nombre);
                cmdIns.Parameters.AddWithValue("@T", (object?)telefono ?? DBNull.Value);
                cmdIns.Parameters.AddWithValue("@E", email);
                return Convert.ToInt32(cmdIns.ExecuteScalar());
            }
        }

        internal static class AlumnoTutorRepo
        {
            public static void Link(SqlConnection cn, SqlTransaction tx, int alumnoId, int tutorId)
            {
                using var cmd = new SqlCommand(@"
    IF OBJECT_ID(N'dbo.AlumnoTutor', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS(SELECT 1 FROM dbo.AlumnoTutor WHERE AlumnoID=@A AND TutorID=@T)
            INSERT INTO dbo.AlumnoTutor(AlumnoID, TutorID) VALUES(@A, @T);
    END
    ELSE
    BEGIN
        -- Fallback: columna TutorID en Alumno
        IF COL_LENGTH('dbo.Alumno','TutorID') IS NOT NULL
            UPDATE dbo.Alumno SET TutorID=@T WHERE AlumnoID=@A;
        ELSE
            THROW 50001, 'No hay tabla AlumnoTutor ni columna Alumno.TutorID.', 1;
    END", cn, tx);

                cmd.Parameters.AddWithValue("@A", alumnoId);
                cmd.Parameters.AddWithValue("@T", tutorId);
                cmd.ExecuteNonQuery();
            }
        }

        // Extensión que ya tienes; añado ExistsCURP
        internal partial class AlumnoRepo
        {
            public static bool ExistsCURP(string curp)
            {
                const string sql = "SELECT 1 FROM dbo.Alumno WHERE CURP=@C;";
                using var cn = Db.New();
                using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@C", curp);
                cn.Open();
                var o = cmd.ExecuteScalar();
                return o != null && o != DBNull.Value;
            }
        }

        internal class VerifyCodeDialog : Form
        {
            private TextBox tbCode;
            public string CodigoIngresado => tbCode.Text.Trim();

            public VerifyCodeDialog(string email, string? telefono = null)
            {
                Text = "Verificación de tutor";
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(420, 160);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = MinimizeBox = false;

                var lbl = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 60,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = string.IsNullOrWhiteSpace(telefono)
                           ? $"Enviamos un código a:\n{email}\nEscríbelo para continuar."
                           : $"Enviamos un código a:\n{email} y {telefono}\nEscríbelo para continuar."
                };
                Controls.Add(lbl);

                tbCode = new TextBox { Dock = DockStyle.Top, Margin = new Padding(16), MaxLength = 6, TextAlign = HorizontalAlignment.Center };
                tbCode.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
                Controls.Add(tbCode);

                var panelBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10) };
                var ok = new Button { Text = "Aceptar", DialogResult = DialogResult.OK, Width = 90 };
                var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 90 };
                panelBtns.Controls.Add(ok);
                panelBtns.Controls.Add(cancel);
                Controls.Add(panelBtns);

                AcceptButton = ok;
                CancelButton = cancel;
            }
        }
        internal static class MailSender
        {
            static readonly string Host = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
            static readonly int Port = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out var p) ? p : 587;
            static readonly bool UseSsl = bool.TryParse(ConfigurationManager.AppSettings["SmtpUseSsl"], out var b) ? b : true;
            static readonly string User = ConfigurationManager.AppSettings["SmtpUser"] ?? "";
            static readonly string Pass = ConfigurationManager.AppSettings["SmtpPass"] ?? "";
            static readonly string From = ConfigurationManager.AppSettings["SmtpFrom"] ?? User;

            public static async Task SendAsync(string to, string subject, string body, bool isHtml = false)
            {
                if (string.IsNullOrWhiteSpace(User) || string.IsNullOrWhiteSpace(Pass))
                    throw new InvalidOperationException("SMTP no configurado. Revisa App.config (SmtpUser/SmtpPass).");

                using var smtp = new SmtpClient(Host, Port)
                {
                    EnableSsl = UseSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(User, Pass),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                using var msg = new MailMessage(From, to, subject, body) { IsBodyHtml = isHtml };
                await smtp.SendMailAsync(msg);
            }
        }

        internal static class SmsSender
        {
            public static Task SendAsync(string toPhoneE164, string message)
            {
                // TODO: integra Twilio/u otro proveedor
                // Ejemplo Twilio:
                // TwilioClient.Init("SID", "TOKEN");
                // return MessageResource.CreateAsync(new CreateMessageOptions(new PhoneNumber(toPhoneE164)){ Body = message, From = new PhoneNumber("+1XXX...")});
                return Task.CompletedTask;
            }
        }

        internal static class SqlDebug
        {
            public static string Describe(SqlCommand cmd)
            {
                var sb = new StringBuilder();
                sb.AppendLine("/* ===== SQL ===== */");
                sb.AppendLine(cmd.CommandText.Trim());

                foreach (SqlParameter p in cmd.Parameters)
                    sb.AppendLine($"-- {p.ParameterName} = {FormatValue(p.Value)}");

                return sb.ToString();
            }

            public static void Log(SqlCommand cmd)
            {
                var dump = Describe(cmd);
                System.Diagnostics.Debug.WriteLine(dump);
            }

            // Útil si quieres envolver la ejecución y que te muestre el SQL cuando falla
            public static T Exec<T>(Func<T> go, SqlCommand cmd)
            {
                var dump = Describe(cmd);
                System.Diagnostics.Debug.WriteLine(dump);
                try { return go(); }
                catch (Exception ex)
                {
                    MessageBox.Show(dump + "\n\n" + ex.Message, "SQL ERROR",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw;
                }
            }

            private static string FormatValue(object? v)
            {
                if (v == null || v == DBNull.Value) return "NULL";
                return v switch
                {
                    string s => "N'" + s.Replace("'", "''") + "'",
                    DateTime dt => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "'",
                    bool b => b ? "1" : "0",
                    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture) ?? "NULL",
                    _ => v.ToString() ?? "NULL"
                };
            }
        }
        internal static class CicloEscolarRepo
        {
            public static int? GetCicloActualId(SqlConnection cn, SqlTransaction tx)
            {
                const string sql = @"
    DECLARE @id int,
            @hasActivo  bit = CASE WHEN OBJECT_ID(N'dbo.CicloEscolar', N'U') IS NULL THEN 0
                                   WHEN EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.CicloEscolar') AND name = 'Activo')  THEN 1 ELSE 0 END,
            @hasVigente bit = CASE WHEN OBJECT_ID(N'dbo.CicloEscolar', N'U') IS NULL THEN 0
                                   WHEN EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.CicloEscolar') AND name = 'Vigente') THEN 1 ELSE 0 END,
            @hasFi      bit = CASE WHEN OBJECT_ID(N'dbo.CicloEscolar', N'U') IS NULL THEN 0
                                   WHEN EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.CicloEscolar') AND name = 'FechaInicio') THEN 1 ELSE 0 END,
            @hasFf      bit = CASE WHEN OBJECT_ID(N'dbo.CicloEscolar', N'U') IS NULL THEN 0
                                   WHEN EXISTS(SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.CicloEscolar') AND name = 'FechaFin')   THEN 1 ELSE 0 END;

    IF OBJECT_ID(N'dbo.CicloEscolar', N'U') IS NULL
    BEGIN
        SELECT @id = NULL;
    END
    ELSE IF @hasActivo = 1
        EXEC sp_executesql N'SELECT TOP 1 @o = CicloID FROM dbo.CicloEscolar WHERE Activo = 1 ORDER BY CicloID DESC',
                           N'@o int OUTPUT', @o=@id OUTPUT;
    ELSE IF @hasVigente = 1
        EXEC sp_executesql N'SELECT TOP 1 @o = CicloID FROM dbo.CicloEscolar WHERE Vigente = 1 ORDER BY CicloID DESC',
                           N'@o int OUTPUT', @o=@id OUTPUT;
    ELSE IF @hasFi = 1 AND @hasFf = 1
        EXEC sp_executesql N'SELECT TOP 1 @o = CicloID FROM dbo.CicloEscolar WHERE GETDATE() BETWEEN FechaInicio AND FechaFin ORDER BY CicloID DESC',
                           N'@o int OUTPUT', @o=@id OUTPUT;
    ELSE
        EXEC sp_executesql N'SELECT TOP 1 @o = CicloID FROM dbo.CicloEscolar ORDER BY CicloID DESC',
                           N'@o int OUTPUT', @o=@id OUTPUT;

    SELECT @id;";
                using var cmd = new SqlCommand(sql, cn, tx);
                var o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? (int?)null : Convert.ToInt32(o);
            }
        }

        internal partial class GrupoRepo
        {
            internal sealed class GrupoHit
            {
                public int GrupoID { get; set; }
                public string Nombre { get; set; } = "";
            }

            public static GrupoHit? FindByGrado(SqlConnection cn, SqlTransaction tx, int grado)
            {
                // 0) Tu esquema: Grupo(GradoID, Letra)
                try
                {
                    using var c0 = new SqlCommand(
                        "SELECT TOP 1 GrupoID, ISNULL(Letra, CAST(GrupoID AS nvarchar(20))) AS Nombre " +
                        "FROM dbo.Grupo WHERE GradoID=@G ORDER BY GrupoID;", cn, tx);
                    c0.Parameters.AddWithValue("@G", grado);
                    SqlDebug.Exec(() => 0, c0);
                    using var rd0 = c0.ExecuteReader();
                    if (rd0.Read()) return new GrupoHit { GrupoID = rd0.GetInt32(0), Nombre = rd0.GetString(1) };
                }
                catch (SqlException) { }

                // 1) Alternativa: Grupo(Grado)
                try
                {
                    using var c1 = new SqlCommand(
                        "SELECT TOP 1 GrupoID, ISNULL(Letra, CAST(GrupoID AS nvarchar(20))) AS Nombre " +
                        "FROM dbo.Grupo WHERE Grado=@G ORDER BY GrupoID;", cn, tx);
                    c1.Parameters.AddWithValue("@G", grado);
                    SqlDebug.Exec(() => 0, c1);
                    using var rd1 = c1.ExecuteReader();
                    if (rd1.Read()) return new GrupoHit { GrupoID = rd1.GetInt32(0), Nombre = rd1.GetString(1) };
                }
                catch (SqlException) { }

                // 2) Alternativa: Grupo(GradoID) + Grado(Numero|Grado|Nivel|Num)
                try
                {
                    string colG = FirstExistingCol(cn, tx, "dbo.Grado", "Numero", "Grado", "Nivel", "Num") ?? "";
                    if (!string.IsNullOrEmpty(colG))
                    {
                        using var c2 = new SqlCommand($@"
    SELECT TOP 1 g.GrupoID, ISNULL(g.Letra, CAST(g.GrupoID AS nvarchar(20))) AS Nombre
    FROM dbo.Grupo g
    JOIN dbo.Grado gr ON gr.GradoID = g.GradoID
    WHERE gr.{colG} = @G
    ORDER BY g.GrupoID;", cn, tx);
                        c2.Parameters.AddWithValue("@G", grado);
                        SqlDebug.Exec(() => 0, c2);
                        using var rd2 = c2.ExecuteReader();
                        if (rd2.Read()) return new GrupoHit { GrupoID = rd2.GetInt32(0), Nombre = rd2.GetString(1) };
                    }
                }
                catch (SqlException) { }

                // Nada de "TOP 1" sin filtro:
                return null;
            }

            private static string? FirstExistingCol(SqlConnection cn, SqlTransaction tx, string table, params string[] candidates)
            {
                foreach (var col in candidates)
                {
                    using var cmd = new SqlCommand(
                        "SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(@t) AND name = @c;", cn, tx);
                    cmd.Parameters.AddWithValue("@t", table);
                    cmd.Parameters.AddWithValue("@c", col);
                    var ok = cmd.ExecuteScalar();
                    if (ok != null) return col;
                }
                return null;
            }
        }

        internal static class AlumnoGrupoRepo
        {
            public static void Assign(SqlConnection cn, SqlTransaction tx, int alumnoId, int grupoId)
            {
                // ¿existe AsignacionGrupo?
                bool tieneAsignacion = ObjectExists(cn, tx, "dbo.AsignacionGrupo");

                if (tieneAsignacion)
                {
                    int? cicloId = CicloEscolarRepo.GetCicloActualId(cn, tx);
                    if (!cicloId.HasValue)
                        throw new InvalidOperationException("No hay Ciclo Escolar activo/actual para asignar (CicloID). Configúralo en dbo.CicloEscolar.");

                    using var cmd = new SqlCommand(@"
    IF NOT EXISTS(SELECT 1 FROM dbo.AsignacionGrupo WHERE AlumnoID=@A AND CicloID=@C)
        INSERT INTO dbo.AsignacionGrupo(AlumnoID, GrupoID, CicloID) VALUES(@A, @G, @C);
    ELSE
        UPDATE dbo.AsignacionGrupo SET GrupoID=@G WHERE AlumnoID=@A AND CicloID=@C;", cn, tx);

                    cmd.Parameters.AddWithValue("@A", alumnoId);
                    cmd.Parameters.AddWithValue("@G", grupoId);
                    cmd.Parameters.AddWithValue("@C", cicloId.Value);

                    System.Diagnostics.Debug.WriteLine($"[SQL] Upsert AsignacionGrupo AlumnoID={alumnoId} GrupoID={grupoId} CicloID={cicloId}");

                    cmd.ExecuteNonQuery();
                    return;
                }

                // Fallback a AlumnoGrupo (tu otra tabla puente)
                if (ObjectExists(cn, tx, "dbo.AlumnoGrupo"))
                {
                    using var cmd2 = new SqlCommand(@"
    IF NOT EXISTS(SELECT 1 FROM dbo.AlumnoGrupo WHERE AlumnoID=@A)
        INSERT INTO dbo.AlumnoGrupo(AlumnoID, GrupoID) VALUES(@A, @G);
    ELSE
        UPDATE dbo.AlumnoGrupo SET GrupoID=@G WHERE AlumnoID=@A;", cn, tx);

                    cmd2.Parameters.AddWithValue("@A", alumnoId);
                    cmd2.Parameters.AddWithValue("@G", grupoId);
                    cmd2.ExecuteNonQuery();
                    return;
                }

                throw new InvalidOperationException("No existe AsignacionGrupo ni AlumnoGrupo en la BD.");
            }

            private static bool ObjectExists(SqlConnection cn, SqlTransaction tx, string name)
            {
                using var cmd = new SqlCommand("SELECT 1 WHERE OBJECT_ID(@n,'U') IS NOT NULL", cn, tx);
                cmd.Parameters.AddWithValue("@n", name);
                var o = cmd.ExecuteScalar();
                return o != null && o != DBNull.Value;
            }
        }
        internal static class MateriaRepo
        {
            public static List<string> ListByGrado(SqlConnection cn, int grado)
            {
                var list = new List<string>();

                // A) PlanEstudios(GradoID) + Materia.Nombre
                try
                {
                    using var cA = new SqlCommand(@"
    SELECT m.Nombre
    FROM dbo.PlanEstudios p
    JOIN dbo.Materia m ON m.MateriaID = p.MateriaID
    WHERE p.GradoID = @G
    ORDER BY m.Nombre;", cn);
                    cA.Parameters.AddWithValue("@G", grado);
                    SqlDebug.Exec(() => 0, cA);
                    using var rdA = cA.ExecuteReader();
                    while (rdA.Read()) list.Add(rdA.GetString(0));
                    if (list.Count > 0) return list;
                }
                catch (SqlException) { }

                // B) PlanEstudios + Grado(Numero) + Materia.Nombre
                try
                {
                    using var c2 = new SqlCommand(@"
    SELECT m.Nombre
    FROM dbo.PlanEstudios p
    JOIN dbo.Materia m ON m.MateriaID = p.MateriaID
    JOIN dbo.Grado   g ON g.GradoID   = p.GradoID
    WHERE g.Numero = @G
    ORDER BY m.Nombre;", cn);
                    c2.Parameters.AddWithValue("@G", grado);
                    SqlDebug.Exec(() => 0, c2);
                    using var rd = c2.ExecuteReader();
                    while (rd.Read()) list.Add(rd.GetString(0));
                    if (list.Count > 0) return list;
                }
                catch (SqlException) { }

                // C) Último recurso: cualquier columna de texto existente
                try
                {
                    var col = FirstExistingCol(cn, null, "dbo.Materia", "Nombre", "Descripcion", "Materia");
                    string expr = col ?? "CAST(MateriaID AS nvarchar(20))";
                    string sql = $"SELECT {expr} FROM dbo.Materia ORDER BY 1;";
                    using var c3 = new SqlCommand(sql, cn);
                    SqlDebug.Exec(() => 0, c3);
                    using var rd = c3.ExecuteReader();
                    while (rd.Read()) list.Add(rd.GetString(0));
                }
                catch (SqlException) { }

                return list;
            }

            private static string? FirstExistingCol(SqlConnection cn, SqlTransaction? tx, string table, params string[] candidates)
            {
                foreach (var col in candidates)
                {
                    using var cmd = new SqlCommand(
                        "SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(@t) AND name = @c;", cn, tx);
                    cmd.Parameters.AddWithValue("@t", table);
                    cmd.Parameters.AddWithValue("@c", col);
                    var ok = cmd.ExecuteScalar();
                    if (ok != null) return col;
                }
                return null;
            }
        }
        internal sealed class VerifyCodeDialog2 : Form
        {
            private readonly TextBox tbCode;
            private readonly Button btnOk;
            private readonly Button btnCancel;
            private readonly LinkLabel lnkResend;
            private readonly Label lblInfo;
            private readonly System.Windows.Forms.Timer tmr;
            private int secondsLeft;

            public string CodigoIngresado => tbCode.Text.Trim();

            public VerifyCodeDialog2(string email, int secondsUntilResend = 60)
            {
                Text = "Verificación por correo";
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(460, 210);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = MinimizeBox = false;
                BackColor = Color.White;

                var title = new Label
                {
                    Text = "Ingresa el código de verificación",
                    Dock = DockStyle.Top,
                    Height = 34,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font(SystemFonts.DialogFont, FontStyle.Bold)
                };
                Controls.Add(title);

                lblInfo = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 44,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Color.DimGray,
                    Text = $"Te enviamos un código a:\n{MaskEmail(email)}"
                };
                Controls.Add(lblInfo);

                tbCode = new TextBox
                {
                    Dock = DockStyle.Top,
                    MaxLength = 6,
                    Height = 40,
                    TextAlign = HorizontalAlignment.Center,
                    Font = new Font("Consolas", 20f, FontStyle.Bold),
                    Margin = new Padding(18)
                };
                tbCode.KeyPress += (s, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
                tbCode.TextChanged += (s, e) => btnOk.Enabled = tbCode.TextLength == 6;
                Controls.Add(tbCode);

                var panelBottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 50, ColumnCount = 3, Padding = new Padding(10) };
                panelBottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                panelBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                panelBottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                Controls.Add(panelBottom);

                lnkResend = new LinkLabel
                {
                    Text = "",
                    AutoSize = true,
                    LinkBehavior = LinkBehavior.HoverUnderline,
                    Enabled = false
                };
                lnkResend.LinkClicked += (s, e) => { DialogResult = DialogResult.Retry; Close(); };
                panelBottom.Controls.Add(lnkResend, 0, 0);

                btnOk = new Button { Text = "Confirmar", DialogResult = DialogResult.OK, Enabled = false, Width = 100, Height = 30 };
                btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Width = 100, Height = 30 };
                panelBottom.Controls.Add(btnCancel, 1, 0);
                panelBottom.Controls.Add(btnOk, 2, 0);

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                secondsLeft = Math.Max(0, secondsUntilResend);
                tmr = new System.Windows.Forms.Timer { Interval = 1000 };
                tmr.Tick += (_, __) => Tick();
                Tick(); // pinta de una vez
                tmr.Start();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) tmr?.Dispose();
                base.Dispose(disposing);
            }

            private void Tick()
            {
                if (secondsLeft > 0)
                {
                    lnkResend.Text = $"Reenviar código ({secondsLeft}s)";
                    lnkResend.Enabled = false;
                    secondsLeft--;
                }
                else
                {
                    lnkResend.Text = "Reenviar código";
                    lnkResend.Enabled = true;
                    tmr.Stop();
                }
            }

            private static string MaskEmail(string email)
            {
                try
                {
                    var at = email.IndexOf('@');
                    if (at <= 1) return email;
                    var name = email.Substring(0, at);
                    var dom = email.Substring(at);
                    if (name.Length <= 2) return name[0] + "****" + dom;
                    return name.Substring(0, 2) + "****" + dom;
                }
                catch { return email; }
            }
        }

    }
