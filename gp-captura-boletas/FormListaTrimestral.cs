using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PdfSharp.Fonts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    // ————————————————————————————————————————————————————————————————
    //  FORM: LISTA TRIMESTRAL (cards de 6 grupos → elegir trimestre → PDF)
    // ————————————————————————————————————————————————————————————————
    public partial class FormListaTrimestral : Form
    {
        // Paleta UI (igual que el otro módulo)
        private static readonly Color C_BG = Color.FromArgb(244, 247, 247);
        private static readonly Color C_ACCENT = Color.FromArgb(121, 168, 169);
        private static readonly Color C_PRIMARY = Color.FromArgb(31, 78, 95);
        private static readonly Color C_TEXTDIM = Color.FromArgb(70, 84, 94);

        private const int CICLO_ACTUAL = 2025;

        private static Font Fx(float s, FontStyle st = FontStyle.Regular)
        {
            try { return new Font("Aptos", s, st); }
            catch { return new Font("Segoe UI", s, st); }
        }

        // Estado
        private Panel pnlCards;
        private Button _btnVolver;
        private Label lblHeader;

        public FormListaTrimestral()
        {
            InitializeComponent();

            Text = "Listas Trimestrales";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1024, 640);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = C_BG;
            DoubleBuffered = true;

            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new AppFontResolver();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = C_BG
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // Header
            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_PRIMARY,
                Padding = new Padding(12, 10, 12, 10)
            };
            root.Controls.Add(header, 0, 0);

            var hdr = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = C_PRIMARY
            };
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.Controls.Add(hdr);

            lblHeader = new Label
            {
                Text = "Listas Trimestrales",
                ForeColor = Color.White,
                AutoSize = true,
                Font = Fx(20f, FontStyle.Bold),
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleCenter
            };
            hdr.Controls.Add(lblHeader, 0, 0);
            hdr.SetColumnSpan(lblHeader, 2);

            _btnVolver = new Button
            {
                Text = "Actualizar grupos",
                AutoSize = true,
                Height = 30,
                BackColor = Color.White,
                ForeColor = C_PRIMARY,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(10, 8, 0, 8),
            };
            _btnVolver.FlatAppearance.BorderSize = 0;
            _btnVolver.Click += (_, __) => CargarCards();
            hdr.Controls.Add(_btnVolver, 2, 0);

            header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(0, 0, 0, 40) });

            // Cards
            pnlCards = new Panel { Dock = DockStyle.Fill, BackColor = C_BG, Padding = new Padding(18) };
            root.Controls.Add(pnlCards, 0, 1);

            // Cargar al entrar
            CargarCards();
        }

        private void CargarCards()
        {
            pnlCards.Controls.Clear();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = C_BG,
                Padding = new Padding(6)
            };
            for (int c = 0; c < 3; c++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            for (int r = 0; r < 2; r++) layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            pnlCards.Controls.Add(layout);

            var grupos = GrupoRepo.GetGruposActivos().OrderBy(g => g.GradoID).ToList();

            foreach (var g in grupos)
                layout.Controls.Add(CrearCardGrupo(g));
        }

        private Control CrearCardGrupo(GrupoSimpleDto g)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Margin = new Padding(12),
                Padding = new Padding(16),
                Cursor = Cursors.Hand,
                Tag = g
            };

            card.Paint += (s, e) =>
            {
                var r = card.ClientRectangle; r.Width -= 1; r.Height -= 1;
                using var pen = new Pen(Color.FromArgb(220, 225, 230), 1f);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawRectangle(pen, r);
            };
            card.MouseEnter += (_, __) => card.BackColor = Color.FromArgb(248, 251, 252);
            card.MouseLeave += (_, __) => card.BackColor = Color.White;

            var col = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 6,
                BackColor = Color.Transparent
            };
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            col.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            col.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            card.Controls.Add(col);

            var icon = new Label { Text = " 📝", AutoSize = true, Font = new Font("Segoe UI Emoji", 26f), ForeColor = C_PRIMARY, Margin = new Padding(0, 0, 8, 0), Anchor = AnchorStyles.None };
            col.Controls.Add(icon, 1, 1);

            var lblTitulo = new Label
            {
                AutoSize = true,
                Text = $"{g.GradoID}° {g.Letra}",
                Font = Fx(22f, FontStyle.Bold),
                ForeColor = C_PRIMARY,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 0, 0, 6),
                Anchor = AnchorStyles.None
            };
            col.Controls.Add(lblTitulo, 1, 2);

            var prof = string.IsNullOrWhiteSpace(g.Profesor) ? "(Sin profesor)" : g.Profesor;
            var lblProf = new Label
            {
                AutoSize = true,
                Text = $"Maestra/ro: {prof}",
                Font = Fx(11f),
                ForeColor = C_TEXTDIM,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 2, 0, 8),
                Anchor = AnchorStyles.None
            };
            col.Controls.Add(lblProf, 1, 3);

            var lblTotal = new Label
            {
                AutoSize = true,
                Text = $"Alumnos: {g.TotalAlumnos}",
                Font = Fx(11f, FontStyle.Bold),
                ForeColor = C_PRIMARY,
                TextAlign = ContentAlignment.MiddleCenter,
                Anchor = AnchorStyles.None
            };
            col.Controls.Add(lblTotal, 1, 4);

            void abrir(object _1, EventArgs _2) => ElegirTrimestreYGenerar(g);
            card.Click += abrir;
            foreach (Control c in col.Controls) c.Click += abrir;

            return card;
        }

        private void ElegirTrimestreYGenerar(GrupoSimpleDto g)
        {
            // Diálogo rápido para elegir trimestre (SIN año)
            using var dlg = new Form
            {
                Text = "Elegir trimestre",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ClientSize = new Size(320, 150)
            };

            var rb1 = new RadioButton { Text = "1.º Trimestre (Sep–Nov/Dic)", Checked = true, Left = 16, Top = 16, AutoSize = true };
            var rb2 = new RadioButton { Text = "2.º Trimestre (Ene–Mar)", Left = 16, Top = 44, AutoSize = true };
            var rb3 = new RadioButton { Text = "3.º Trimestre (Abr–Jun)", Left = 16, Top = 72, AutoSize = true };

            var ok = new Button { Text = "Generar", DialogResult = DialogResult.OK, Left = 160, Top = 104, Width = 140 };
            dlg.Controls.AddRange(new Control[] { rb1, rb2, rb3, ok });
            dlg.AcceptButton = ok;

            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            int tri = rb1.Checked ? 1 : rb2.Checked ? 2 : 3;
            int anio = CICLO_ACTUAL; // ← FIJO EN 2025

            // Materias por grado (ordenadas como en tu módulo)
            List<BoletaPdfBuilder.MateriaPlan> materiasPlan;
            using (var cn = Db.New())
            {
                cn.Open();
                var sinOrden = BoletaPdfBuilder.ObtenerMateriasPorGrado(cn, g.GradoID);
                materiasPlan = BoletaPdfBuilder.OrdenarMaterias(g.GradoID, sinOrden);
            }

            // Alumnos
            var alumnos = AlumnoRepo.GetByGrupo(g.GrupoID)
                                    .OrderBy(a => a.Paterno).ThenBy(a => a.Materno).ThenBy(a => a.Nombre)
                                    .ToList();

            // Guardar PDF
            using var sfd = new SaveFileDialog
            {
                Filter = "PDF|*.pdf",
                FileName = $"ListaTrimestral_{g.GradoID}°{g.Letra}_T{tri}_{anio}.pdf"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var pdf = new ListaTrimestralPdfBuilder(
                    escuela: "ESCUELA PRIMARIA EMILIANO ZAPATA",
                    maestro: string.IsNullOrWhiteSpace(g.Profesor) ? "(Sin profesor)" : g.Profesor,
                    gradoGrupo: $"{g.GradoDesc} \"{g.Letra}\"",
                    ciclo: $"{anio}",           // ← 2025 fijo
                    trimestre: tri,
                    materias: materiasPlan,
                    alumnos: alumnos,
                    logo: Properties.Resources.escudo
                );
                pdf.Generar(sfd.FileName);

                var psi = new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al generar PDF:\n\n" + ex, "PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }

    // ————————————————————————————————————————————————————————————————
    //  GENERADOR PDF: LISTA TRIMESTRAL (hoja horizontal)
    // ————————————————————————————————————————————————————————————————
    internal sealed class ListaTrimestralPdfBuilder
    {
        // Reuso enum y clase MateriaPlan del builder anterior:
        public enum CampoFA
        {
            Lenguajes,
            SaberesYPensamientoCientifico,
            EticaNaturalezaYSociedad,
            DeLoHumanoYLoComunitario
        }
        public sealed class Alum
        {
            public string Nombre { get; set; } = "";
            public string Paterno { get; set; } = "";
            public string Materno { get; set; } = "";
            public string Sexo { get; set; } = "";  // si no tienes el campo, se deja vacío
            public string NombreCompleto => $"{Paterno} {Materno} {Nombre}".Trim();
        }

        // Colores (idénticos a tu módulo)
        static class P
        {
            public static readonly XColor Grid = XColor.FromArgb(170, 176, 182);
            public static readonly XColor HeadGray = XColor.FromArgb(255, 255, 255);

            // bandas materias (como antes)
            public static readonly XColor AzLeng = XColor.FromArgb(221, 235, 247);
            public static readonly XColor Magenta = XColor.FromArgb(255, 153, 255);
            public static readonly XColor VerdeMed = XColor.FromArgb(255, 153, 255); // ciencias / mat-tec
            public static readonly XColor Crema = XColor.FromArgb(255, 230, 153); // cívica y ética
            public static readonly XColor Cian = XColor.FromArgb(204, 255, 51); // ed. física

            // “PROMEDIO” (amarillo suave del mock)
            public static readonly XColor Promedio = XColor.FromArgb(255, 246, 137);
        }

        private readonly string escuela, maestro, gradoGrupo, ciclo;
        private readonly int trimestre;
        private readonly IList<BoletaPdfBuilder.MateriaPlan> materias;
        private readonly IList<AlumnoListaDto> alumnos;
        private readonly Image logo;
        double MedirAncho(XGraphics g, string texto, XFont f)
        {
            // PdfSharp mide en puntos; esto da el ancho sin saltos de línea
            return g.MeasureString(texto, f).Width;
        }
        public ListaTrimestralPdfBuilder(
            string escuela, string maestro, string gradoGrupo, string ciclo, int trimestre,
            IList<BoletaPdfBuilder.MateriaPlan> materias,
            IList<AlumnoListaDto> alumnos,
            Image logo = null)
        {
            this.escuela = escuela;
            this.maestro = maestro;
            this.gradoGrupo = gradoGrupo;
            this.ciclo = ciclo;
            this.trimestre = trimestre;
            this.materias = materias ?? Array.Empty<BoletaPdfBuilder.MateriaPlan>();
            this.alumnos = alumnos ?? Array.Empty<AlumnoListaDto>();
            this.logo = logo;
        }

        XFont F(double size, XFontStyleEx style = XFontStyleEx.Regular) =>
            new XFont("Noto Sans", size, style,
                new XPdfFontOptions(PdfFontEncoding.Unicode, PdfFontEmbedding.Always));

        static void Rotar(XGraphics g, double cx, double cy, double deg, Action body)
        {
            g.Save(); g.TranslateTransform(cx, cy); g.RotateTransform(deg); body(); g.Restore();
        }
        static void DrawMultiline(XGraphics g, string text, XFont font, XBrush brush, XRect rect, XParagraphAlignment align)
        {
            var tf = new XTextFormatter(g) { Alignment = align };
            tf.DrawString(text, font, brush, rect, XStringFormats.TopLeft);
        }

        private static (string[], string etiquetaTri) MesesDelTrimestre(int t) => t switch
        {
            1 => (new[] { "SEP", "OCT", "NOV/DIC" }, "1º TRIMESTRE"),
            2 => (new[] { "ENE", "FEB", "MAR" }, "2º TRIMESTRE"),
            _ => (new[] { "ABR", "MAY", "JUN" }, "3º TRIMESTRE")
        };

        private static XSolidBrush BrushCampo(BoletaPdfBuilder.CampoFA c) => c switch
        {
            BoletaPdfBuilder.CampoFA.Lenguajes => new XSolidBrush(P.AzLeng),
            BoletaPdfBuilder.CampoFA.SaberesYPensamientoCientifico => new XSolidBrush(P.Magenta),
            BoletaPdfBuilder.CampoFA.EticaNaturalezaYSociedad => new XSolidBrush(P.Crema),
            _ => new XSolidBrush(P.Cian)
        };

        private static string TituloCampo(BoletaPdfBuilder.CampoFA c) => c switch
        {
            BoletaPdfBuilder.CampoFA.Lenguajes => "LENGUAJES",
            BoletaPdfBuilder.CampoFA.SaberesYPensamientoCientifico => "SABERES Y PENS. MAT",
            BoletaPdfBuilder.CampoFA.EticaNaturalezaYSociedad => "ÉTICA, NAT Y SOC.",
            _ => "HUMANO Y COM."
        };

        // Construye la secuencia de columnas de materias para:
        //  - 3 veces (mes1, mes2, mes3)
        //  - 1 vez agrupadas por campo (bloque “trimestral”)
        private List<(string header, XBrush bg)> ConstruirBloquesColumnas(byte grado)
        {
            // materias ya vienen ordenadas como en tu módulo
            var list = new List<(string, XBrush)>();

            // 3 meses → repetir materias en el mismo orden; después añadir PROMEDIO
            var (meses, _) = MesesDelTrimestre(trimestre);
            foreach (var _m in meses)
            {
                foreach (var m in materias)
                    list.Add((m.Nombre.ToUpperInvariant(), BrushCampo(m.Campo)));
                list.Add(("PROMEDIO", new XSolidBrush(P.Promedio))); // columna amarilla por mes
            }

            // Bloque “trimestral” → por campo (bandas) + PROMEDIO + “TRIMESTRAL” rotado al extremo
            foreach (var m in materias)
                list.Add((m.Nombre.ToUpperInvariant(), BrushCampo(m.Campo)));
            list.Add(("PROMEDIO", new XSolidBrush(P.Promedio))); // promedio trimestral

            return list;
        }

        public void Generar(string path)
        {
            // ====== Parámetros base ======
            const double ML = 36, MT = 28;     // márgenes
            double altoHeader = 38;            // alto de encabezado de materias (se recalcula)
            double altoCampo = 18;            // alto de la banda de Campo (se recalcula)
            const double altoRow = 22;

            // columnas fijas de la izquierda
            double cNo = 22;
            double cSexo = 18;
            double cNombre = 200;

            // columnas de materias
            double cMateria = 22;
            double cPromMes = 22;

            // ====== Documento y gráficos ======
            var doc = new PdfDocument();
            var page = doc.AddPage();
            page.Orientation = PageOrientation.Landscape;
            var g = XGraphics.FromPdfPage(page);
            g.SmoothingMode = XSmoothingMode.AntiAlias;
            var pen = new XPen(P.Grid, 0.7);

            // Márgenes base (deben existir antes de las funciones locales que los usan)
            double x = ML, y = MT;

            // Fuentes
            XFont FHeader(double s, XFontStyleEx st = XFontStyleEx.Regular) => F(s, st);
            var fMat = FHeader(6.2, XFontStyleEx.Regular);
            var fCampo = FHeader(6.8, XFontStyleEx.Bold);
            var fProm = FHeader(8.0, XFontStyleEx.Bold);

            // Utilidades locales
            double MedirAncho(string texto, XFont fnt) => g.MeasureString(texto, fnt).Width;

            // Mes arriba, fuera de la tabla y centrado sobre la sección
            // Mes arriba, fuera de la tabla y centrado sobre la sección
            void EncabezadoMesFuera(double xInicio, double anchoSeccion, double yTexto, string mes)
            {
                var etiq = "MES:";
                var rojo = new XSolidBrush(XColor.FromArgb(157, 33, 33));
                var fEtiq = FHeader(8.0);
                var fMes = FHeader(8.8, XFontStyleEx.Bold);

                double w1 = MedirAncho(etiq, fEtiq);
                double w2 = MedirAncho(mes, fMes);
                double total = w1 + 4 + w2;

                double cx = xInicio + anchoSeccion / 2.0;
                double x0 = cx - total / 2.0;

                g.DrawString(etiq, fEtiq, XBrushes.Black, new XPoint(x0, yTexto));
                g.DrawString(mes, fMes, rojo, new XPoint(x0 + w1 + 4, yTexto));
            }


            void TextoVerticalCentrado(XRect rc, string texto, XFont font)
            {
                Rotar(g, rc.X + rc.Width / 2.0, rc.Y + rc.Height / 2.0, -90, () =>
                {
                    var rr = new XRect(-rc.Height / 2.0 + 2, -rc.Width / 2.0 + 2, rc.Height - 4, rc.Width - 4);
                    g.DrawString(texto, font, XBrushes.Black, rr, XStringFormats.Center);
                });
            }

            void HeaderMateriaVerticalOneLine(XRect rc, string texto, XBrush bg)
            {
                g.DrawRectangle(bg, rc); g.DrawRectangle(pen, rc);
                TextoVerticalCentrado(rc, texto, fMat);
            }

            void BandaCampoVertical(XRect rc, string titulo, XBrush bg)
            {
                g.DrawRectangle(bg, rc); g.DrawRectangle(pen, rc);
                TextoVerticalCentrado(rc, titulo, fCampo);
            }

            // ====== Columnas dinámicas ======
            byte grado = ExtraerGrado(gradoGrupo);
            var cols = ConstruirBloquesColumnas(grado); // 3×(materias+PROM) + (materias) + PROM

            // Alturas para que NADA se parta (vertical en 1 línea)
            double maxMat = 0, maxCampo = 0;
            foreach (var (h, _) in cols) maxMat = Math.Max(maxMat, MedirAncho(h.ToUpperInvariant(), fMat));
            foreach (var c in materias.Select(m => m.Campo).Distinct())
                maxCampo = Math.Max(maxCampo, MedirAncho(TituloCampo(c), fCampo));
            altoHeader = Math.Max(altoHeader, maxMat + 10);
            altoCampo = Math.Max(altoCampo, maxCampo + 8);

            // Ancho requerido por materias
            double anchoMaterias = 0;
            foreach (var (h, _) in cols)
                anchoMaterias += (h == "PROMEDIO") ? cPromMes : cMateria;

            // Tamaño de página justo para que quepa todo (sin barra "TRIMESTRAL")
            double anchoNecesario = cNo + cSexo + cNombre + anchoMaterias + 12; // respiro
            page.Width = XUnit.FromPoint(ML * 2 + anchoNecesario);
            page.Height = XUnit.FromPoint(612); // ~8.5 in

            double W = page.Width - 2 * ML, H = page.Height - 2 * MT;

            // ====== Header superior ======
            double logoW = 72, logoH = 72;
            if (logo != null)
            {
                using var bmp = new Bitmap(logo);
                using var ms = new MemoryStream(); bmp.Save(ms, ImageFormat.Png); ms.Position = 0;
                using var img = XImage.FromStream(ms);
                g.DrawImage(img, x, y, logoW, logoH);
            }

            double tx = x + (logo != null ? (logoW + 14) : 0);
            g.DrawString("NOMBRE DE LA ESCUELA", FHeader(9.8, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx, y + 10));
            g.DrawString(string.IsNullOrWhiteSpace(escuela) ? "—" : escuela, FHeader(12.5, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx + 170, y + 10));

            g.DrawString("DOCENTE:", FHeader(9.2, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx, y + 28));
            g.DrawString(string.IsNullOrWhiteSpace(maestro) ? "—" : maestro, FHeader(9.2), XBrushes.Black, new XPoint(tx + 70, y + 28));

            g.DrawString("GRADO Y GRUPO:", FHeader(9.2, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx, y + 44));
            g.DrawString(string.IsNullOrWhiteSpace(gradoGrupo) ? "—" : gradoGrupo, FHeader(9.2), XBrushes.Black, new XPoint(tx + 120, y + 44));

            var (mesesTri, etiquetaTri) = MesesDelTrimestre(trimestre);
            g.DrawString("TRIMESTRE", FHeader(9.2, XFontStyleEx.Bold), XBrushes.Black, new XPoint(W + ML - 210, y + 10));
            g.DrawString($"{etiquetaTri}   {ciclo}", FHeader(10.5, XFontStyleEx.Bold), XBrushes.Black, new XPoint(W + ML - 210, y + 28));

            // Separador: línea de cabecera + carril para los rótulos "MES"
            double yHeaderLine = y + 80;                 // línea horizontal bajo el bloque superior
            g.DrawLine(pen, x, yHeaderLine, x + W, yHeaderLine);

            double yMesTexto = yHeaderLine + 10;          // rótulos "MES" van 6 pt debajo de la línea
            y = yHeaderLine + 22;                        // tabla inicia 22 pt debajo de la línea

            // ====== Encabezados fijos (suman banda Campo + header materias) ======
            double altoHeaderTotal = altoCampo + altoHeader;

            var rNo = new XRect(x, y, cNo, altoHeaderTotal);
            var rSexo = new XRect(rNo.Right, y, cSexo, altoHeaderTotal);
            var rNombre = new XRect(rSexo.Right, y, cNombre, altoHeaderTotal);

            g.DrawRectangle(new XSolidBrush(P.HeadGray), rNo); g.DrawRectangle(pen, rNo);
            g.DrawRectangle(new XSolidBrush(P.HeadGray), rSexo); g.DrawRectangle(pen, rSexo);
            g.DrawRectangle(new XSolidBrush(P.HeadGray), rNombre); g.DrawRectangle(pen, rNombre);

            // “No. LISTA” vertical
            TextoVerticalCentrado(rNo, "No. LISTA", FHeader(8.4, XFontStyleEx.Bold));
            // “SEXO” vertical
            TextoVerticalCentrado(rSexo, "SEXO", FHeader(8.4, XFontStyleEx.Bold));
            // Nombre horizontal
            DrawMultiline(g, "NOMBRE DEL ALUMNO", FHeader(9.2, XFontStyleEx.Bold), XBrushes.Black, rNombre, XParagraphAlignment.Center);

            // ====== Encabezados por meses y bloque trimestral ======
            int nRows = Math.Max(4, alumnos.Count);
            int tamBloque = materias.Count + 1; // materias + PROMEDIO
            int startTriIdx = 3 * tamBloque;      // índice del bloque trimestral
            double xCol = rNombre.Right;

            // --- función que dibuja (BANDA Campo + headers materia) para un rango de índices en 'cols' ---
            void DibujarSeccionMaterias(int idxIni, int idxFinExclusivo, string rotuloMes, bool mostrarRotuloMes)
            {
                double xInicioSeccion = xCol;

                // 1) Banda superior por Campo (no cubre PROMEDIO)
                // Secuencia de campos según orden de 'materias'
                var camposOrden = materias.Select(m => m.Campo).Distinct().ToList();
                int cursor = idxIni;
                int materiasEnSeccion = idxFinExclusivo - idxIni;
                int materiasEnCampos = materias.Count; // siempre antes de PROMEDIO
                double anchoSeccion = materias.Count * cMateria + cPromMes;

                // 1) (Opcional) rótulo del mes, FUERA y CENTRADO
                if (mostrarRotuloMes && !string.IsNullOrEmpty(rotuloMes))
                    EncabezadoMesFuera(xInicioSeccion, anchoSeccion, yMesTexto, rotuloMes);

                double xCampo = xCol;
                for (int i = 0; i < camposOrden.Count; i++)
                {
                    int countCampo = materias.Count(m => m.Campo.Equals(camposOrden[i]));
                    double wCampo = countCampo * cMateria;
                    var rcCampo = new XRect(xCampo, y, wCampo, altoCampo);
                    var bgCampo = BrushCampo(camposOrden[i]);
                    BandaCampoVertical(rcCampo, TituloCampo(camposOrden[i]), bgCampo);
                    xCampo += wCampo;
                }

                // 3) Encabezados de materias (vertical), bajo la banda de Campo
                double yHeader = y + altoCampo;
                for (int i = idxIni; i < idxFinExclusivo; i++)
                {
                    var (h, bg) = cols[i];
                    double w = (h == "PROMEDIO") ? cPromMes : cMateria;
                    var rc = new XRect(xCol, yHeader, w, altoHeader);
                    HeaderMateriaVerticalOneLine(rc, h.ToUpperInvariant(), bg);
                    xCol = rc.Right;
                }
            }

            // MES 1 (idx 0..tamBloque-1)
            DibujarSeccionMaterias(0, tamBloque, mesesTri[0], true);
            // MES 2 (idx tamBloque..2*tamBloque-1)
            DibujarSeccionMaterias(tamBloque, 2 * tamBloque, mesesTri[1], true);
            // MES 3
            DibujarSeccionMaterias(2 * tamBloque, 3 * tamBloque, mesesTri[2], true);
            // BLOQUE TRIMESTRAL (idx startTriIdx..cols.Count-1)
            double xTriStart = xCol;
            DibujarSeccionMaterias(startTriIdx, cols.Count, "", false);

            // ====== Filas de alumnos ======
            double yRow = y + altoHeaderTotal;

            // grupos por Campo (para unión en el bloque trimestral)
            var gruposCampo = materias
                .GroupBy(m => m.Campo)
                .Select(gp => new { Campo = gp.Key, Count = gp.Count() })
                .ToList();

            for (int i = 0; i < nRows; i++)
            {
                var a = (i < alumnos.Count) ? alumnos[i] : null;

                var rn = new XRect(x, yRow, cNo, altoRow);
                var rs = new XRect(rn.Right, yRow, cSexo, altoRow);
                var rnom = new XRect(rs.Right, yRow, cNombre, altoRow);

                g.DrawRectangle(XBrushes.White, rn); g.DrawRectangle(pen, rn);
                g.DrawRectangle(XBrushes.White, rs); g.DrawRectangle(pen, rs);
                g.DrawRectangle(XBrushes.White, rnom); g.DrawRectangle(pen, rnom);

                g.DrawString((i + 1).ToString(), FHeader(8.6, XFontStyleEx.Bold), XBrushes.Black, rn, XStringFormats.Center);
                if (a != null) g.DrawString(a.Sexo ?? "", FHeader(8.6), XBrushes.Black, rs, XStringFormats.Center);
                if (a != null) g.DrawString($"{a.Paterno} {a.Materno} {a.Nombre}".Trim(), FHeader(8.6), XBrushes.Black,
                                            new XRect(rnom.X + 4, rnom.Y + 4, rnom.Width - 8, rnom.Height - 8), XStringFormats.TopLeft);

                // --- Meses (1..3): PROMEDIO fosforescente ---
                double xx = rnom.Right;
                for (int k = 0; k < startTriIdx; k++)
                {
                    var (h, _) = cols[k];
                    double w = (h == "PROMEDIO") ? cPromMes : cMateria;
                    var c = new XRect(xx, yRow, w, altoRow);
                    var fill = (h == "PROMEDIO") ? new XSolidBrush(P.Promedio) : XBrushes.White;
                    g.DrawRectangle(fill, c); g.DrawRectangle(pen, c);
                    xx += w;
                }

                // --- Trimestral: celdas unidas por Campo + PROMEDIO fosforescente ---
                double xxTri = xTriStart;
                foreach (var gcampo in gruposCampo)
                {
                    double wGrupo = gcampo.Count * cMateria;
                    var cGrupo = new XRect(xxTri, yRow, wGrupo, altoRow);
                    g.DrawRectangle(XBrushes.White, cGrupo);
                    g.DrawRectangle(pen, cGrupo);
                    xxTri += wGrupo;
                }
                // PROMEDIO trimestral
                var cPromTri = new XRect(xxTri, yRow, cPromMes, altoRow);
                g.DrawRectangle(new XSolidBrush(P.Promedio), cPromTri);
                g.DrawRectangle(pen, cPromTri);

                yRow += altoRow;
            }

            // ====== Fila de PROMEDIO (##) ======
            var rProm = new XRect(x, yRow, cNo + cSexo + cNombre, altoRow);
            g.DrawRectangle(XBrushes.White, rProm); g.DrawRectangle(pen, rProm);
            g.DrawString("PROMEDIO", FHeader(8.6, XFontStyleEx.Bold), XBrushes.Black, rProm, XStringFormats.Center);

            // Meses (## en columnas PROMEDIO)
            double xxp = rProm.Right;
            for (int k = 0; k < startTriIdx; k++)
            {
                var (h, _) = cols[k];
                double w = (h == "PROMEDIO") ? cPromMes : cMateria;
                var c = new XRect(xxp, yRow, w, altoRow);
                var fill = (h == "PROMEDIO") ? new XSolidBrush(P.Promedio) : XBrushes.White;
                g.DrawRectangle(fill, c); g.DrawRectangle(pen, c);
                if (h == "PROMEDIO") g.DrawString("##", FHeader(8.6, XFontStyleEx.Bold), XBrushes.Black, c, XStringFormats.Center);
                xxp += w;
            }

            // Trimestral unido + PROMEDIO trimestral (##)
            double xxpTri = xTriStart;
            foreach (var gcampo in gruposCampo)
            {
                double wGrupo = gcampo.Count * cMateria;
                var cGrupo = new XRect(xxpTri, yRow, wGrupo, altoRow);
                g.DrawRectangle(XBrushes.White, cGrupo);
                g.DrawRectangle(pen, cGrupo);
                xxpTri += wGrupo;
            }
            var cPromTriF = new XRect(xxpTri, yRow, cPromMes, altoRow);
            g.DrawRectangle(new XSolidBrush(P.Promedio), cPromTriF);
            g.DrawRectangle(pen, cPromTriF);
            g.DrawString("##", FHeader(8.6, XFontStyleEx.Bold), XBrushes.Black, cPromTriF, XStringFormats.Center);

            // ====== Guardar ======
            doc.Save(path);
        }

        private static byte ExtraerGrado(string gradoGrupo)
        {
            var digits = new string((gradoGrupo ?? "").Where(char.IsDigit).ToArray());
            return byte.TryParse(digits, out var g) ? g : (byte)1;
        }
    }
}
