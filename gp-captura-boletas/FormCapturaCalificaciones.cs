using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PdfSharp.Drawing.Layout;

namespace gp_captura_boletas
{

    sealed class AppFontResolver : IFontResolver
    {
        const string FaceRegular = "App.NotoSans#R";
        const string FaceBold = "App.NotoSans#B";

        public string DefaultFontName => "Noto Sans";

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var fam = familyName?.Trim().ToLowerInvariant() ?? "";
            if (fam == "aptos" || fam == "segoe ui" || fam == "noto sans" || fam == "segoeui")
                return new FontResolverInfo(isBold ? FaceBold : FaceRegular);

            // fallback
            return new FontResolverInfo(isBold ? FaceBold : FaceRegular);
        }

        public byte[] GetFont(string faceName)
        {
            string resName = faceName == FaceBold
                ? "gp_captura_boletas.Assets.NotoSans-Bold.ttf"
                : "gp_captura_boletas.Assets.NotoSans-Regular.ttf";

            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName)
                         ?? throw new InvalidOperationException("Recurso no encontrado: " + resName);
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }
    static class BoletaColors
    {
        public static readonly XColor PF_BG = XColor.FromArgb(217, 224, 241); // rgba(217,224,241)
        public static readonly XColor Prom = PF_BG;          // usar el mismo en la columna PF de PERIODOS
        public static readonly XColor InaPF = PF_BG;          // usarlo también en el PF de inasistencias

        public static readonly XColor BG = XColor.FromArgb(244, 247, 247);
        public static readonly XColor Primary = XColor.FromArgb(31, 78, 95);
        public static readonly XColor Accent = XColor.FromArgb(121, 168, 169);
        public static readonly XColor TextDim = XColor.FromArgb(70, 84, 94);
        public static readonly XColor GridLine = XColor.FromArgb(200, 206, 210);
        public static readonly XColor HeadGray = XColor.FromArgb(235, 235, 235);

        // Bloques de materias como en la imagen (puedes ajustar)
        public static readonly XColor BloqueLenguajes = XColor.FromArgb(201, 222, 243);
        public static readonly XColor BloqueMatTec = XColor.FromArgb(246, 199, 255);
        public static readonly XColor BloqueCiencias = XColor.FromArgb(231, 255, 214);
        public static readonly XColor BloqueCivica = XColor.FromArgb(255, 233, 207);
        public static readonly XColor BloqueFisica = XColor.FromArgb(216, 246, 255);
    }

    // === REEMPLAZA COMPLETO BoletaPdfBuilder POR ESTA VERSIÓN ===
    // ===================  CALCO DEL PDF  ===================
    // === REEMPLAZA COMPLETO BoletaPdfBuilder POR ESTA VERSIÓN ===
    public sealed class BoletaPdfBuilder
    {
        public enum CampoFA
        {
            Lenguajes,
            SaberesYPensamientoCientifico,
            EticaNaturalezaYSociedad,
            DeLoHumanoYLoComunitario
        }
        public sealed class MateriaPlan
        {
            public int MateriaID { get; set; }
            public string Nombre { get; set; } = "";
            public CampoFA Campo { get; set; }
        }

        private static CampoFA ParseCampo(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            if (s.StartsWith("lenguaj")) return CampoFA.Lenguajes;
            if (s.StartsWith("saberes") || s.Contains("cient")) return CampoFA.SaberesYPensamientoCientifico;
            if (s.StartsWith("ética") || s.StartsWith("etica") || s.Contains("naturaleza") || s.Contains("sociedad"))
                return CampoFA.EticaNaturalezaYSociedad;
            // “De lo Humano y lo Comunitario”
            return CampoFA.DeLoHumanoYLoComunitario;
        }

        internal static List<MateriaPlan> ObtenerMateriasPorGrado(IDbConnection cn, byte grado)
        {
            var list = new List<MateriaPlan>();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
        SELECT m.MateriaID, m.Nombre, m.CampoFormacionAcademica
        FROM dbo.PlanEstudios pe
        JOIN dbo.Materia m ON m.MateriaID = pe.MateriaID
        WHERE pe.GradoID = @g
        ORDER BY m.MateriaID;"; // el orden final lo imponemos luego
            var p = cmd.CreateParameter(); p.ParameterName = "@g"; p.Value = grado; cmd.Parameters.Add(p);
            using var rd = cmd.ExecuteReader();
            while (rd.Read())
            {
                list.Add(new MateriaPlan
                {
                    MateriaID = rd.GetInt32(0),
                    Nombre = rd.IsDBNull(1) ? "" : rd.GetString(1),
                    Campo = ParseCampo(rd.IsDBNull(2) ? "" : rd.GetString(2))
                });
            }
            return list;
        }
        private static readonly Dictionary<CampoFA, string[]> OrdenPorCampoBase = new()
        {
            [CampoFA.Lenguajes] = new[] { "ESPAÑOL", "INGLÉS", "INGLES", "ARTES" },
            [CampoFA.SaberesYPensamientoCientifico] = new[] { "MATEMÁTICAS", "MATEMATICAS", "TECNOLOGÍA", "TECNOLOGIA", "CONOCIMIENTO DEL MEDIO" },
            [CampoFA.EticaNaturalezaYSociedad] = new[] { "CIENCIAS NATURALES", "FORMACIÓN CÍVICA Y ÉTICA", "FORMACION CIVICA Y ETICA" },
            [CampoFA.DeLoHumanoYLoComunitario] = new[] { "EDUCACIÓN FÍSICA", "EDUCACION FISICA" }
        };

        private static int IndexByPrefix(string nombre, string[] orden)
        {
            string up = (nombre ?? "").ToUpperInvariant();
            for (int i = 0; i < orden.Length; i++)
                if (up.StartsWith(orden[i])) return i;
            return int.MaxValue; // al final si no coincide
        }

        internal static List<MateriaPlan> OrdenarMaterias(byte grado, List<MateriaPlan> sinOrden)
        {
            // 1) Agrupamos por campo con el orden de campos fijo
            var ordenCampos = new[] {
        CampoFA.Lenguajes,
        CampoFA.SaberesYPensamientoCientifico,
        CampoFA.EticaNaturalezaYSociedad,
        CampoFA.DeLoHumanoYLoComunitario
    };

            // 2) Para cada campo aplicamos su orden interno
            var output = new List<MateriaPlan>();
            foreach (var campo in ordenCampos)
            {
                var ordenInterno = OrdenPorCampoBase[campo];
                var itemsCampo = sinOrden.Where(m => m.Campo == campo).ToList();

                // Filtro por grado (Conocimiento del Medio ~ 1–3 ; Ciencias Naturales ~ 4–6)
                itemsCampo = itemsCampo.Where(m =>
                {
                    var n = m.Nombre.ToUpperInvariant();
                    if (n.StartsWith("CONOCIMIENTO DEL MEDIO")) return grado <= 3;
                    if (n.StartsWith("CIENCIAS NATURALES")) return grado >= 4;
                    return true;
                }).ToList();

                itemsCampo.Sort((a, b) =>
                {
                    int ia = IndexByPrefix(a.Nombre, ordenInterno);
                    int ib = IndexByPrefix(b.Nombre, ordenInterno);
                    int cmp = ia.CompareTo(ib);
                    return (cmp != 0) ? cmp : string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase);
                });

                output.AddRange(itemsCampo);
            }
            return output;
        }
        static class P

        {
            public static readonly XColor PF_BG = XColor.FromArgb(217, 224, 241); // rgba(217,224,241)

            public static readonly XColor Grid = XColor.FromArgb(170, 176, 182);
            public static readonly XColor HeadGray = XColor.FromArgb(229, 233, 236);

            // —— Nuevos colores solicitados ——
            public static readonly XColor M_DIAG = XColor.FromArgb(208, 206, 206);   // rgba(208,206,206)
            public static readonly XColor M_P1 = XColor.FromArgb(248, 202, 172);   // rgba(248,202,172)
            public static readonly XColor M_P2 = XColor.FromArgb(255, 230, 153);   // rgba(255,230,153)
            public static readonly XColor M_P3 = XColor.FromArgb(197, 223, 180);   // rgba(197,223,180)

            public static readonly XColor Per1 = XColor.FromArgb(248, 202, 172);
            public static readonly XColor Per2 = XColor.FromArgb(255, 230, 153);
            public static readonly XColor Per3 = XColor.FromArgb(197, 223, 180);

            // Puedes conservar el color del Promedio Final o dejarlo neutro
            public static readonly XColor Prom = XColor.FromArgb(121, 168, 169);

            public static readonly XColor Ina_P1 = Per1;
            public static readonly XColor Ina_P2 = Per2;
            public static readonly XColor Ina_P3 = Per3;
            public static readonly XColor InaCalif = XColor.FromArgb(255, 224, 177);
            public static readonly XColor InaPF = XColor.FromArgb(197, 220, 232);

            // Bandas de materias
            public static readonly XColor AzLeng = XColor.FromArgb(201, 222, 243);
            public static readonly XColor Magenta = XColor.FromArgb(246, 199, 255);
            public static readonly XColor VerdeMed = XColor.FromArgb(231, 255, 214);
            public static readonly XColor Crema = XColor.FromArgb(255, 233, 207);
            public static readonly XColor Cian = XColor.FromArgb(216, 246, 255);

            public static XSolidBrush BrushCampo(CampoFA c) => c switch
            {
                CampoFA.Lenguajes => new XSolidBrush(XColor.FromArgb(221, 235, 247)),
                CampoFA.SaberesYPensamientoCientifico => new XSolidBrush(XColor.FromArgb(255, 153, 255)),
                CampoFA.EticaNaturalezaYSociedad => new XSolidBrush(XColor.FromArgb(255, 230, 153)),
                _ => new XSolidBrush(XColor.FromArgb(204, 255, 51))
            };
        }

        private readonly string escuela, seccion, clave, gradoGrupo, listaNo, ciclo, alumno;
        private readonly IList<MateriaPlan> materias;
        private readonly Image logo;

        private static readonly string[] MESES = { "DIAG", "SEP", "OCT", "NO/DI", "ENE", "FEB", "MAR", "ABR", "MAY", "JUN" };

        public BoletaPdfBuilder(
    string escuela, string seccion, string clave, string gradoGrupo,
    string listaNo, string ciclo, string alumno,
    IList<MateriaPlan> materias, Image logo = null)
        {
            this.escuela = escuela; this.seccion = seccion; this.clave = clave;
            this.gradoGrupo = gradoGrupo; this.listaNo = listaNo; this.ciclo = ciclo;
            this.alumno = alumno; this.materias = materias ?? Array.Empty<MateriaPlan>();
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
            // si quieres el “nudge” vertical, no recortes la altura:
            var r = new XRect(rect.X, rect.Y + 1, rect.Width, rect.Height - 2);
            tf.DrawString(text, font, brush, r, XStringFormats.TopLeft);
        }
        // Estima la altura necesaria para envolver un párrafo en 'maxWidth' usando el ancho de texto medido.
        static double AltoParrafo(XGraphics g, string txt, XFont f, double maxWidth, double lineHeight = 10)
        {
            if (string.IsNullOrWhiteSpace(txt)) return lineHeight;
            var words = txt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string line = "";
            int lines = 1;
            foreach (var w in words)
            {
                string test = string.IsNullOrEmpty(line) ? w : line + " " + w;
                // Nota: MeasureString no envuelve, sólo usamos el ancho para “simular” saltos
                if (g.MeasureString(test, f).Width > maxWidth)
                {
                    lines++;
                    line = w;
                }
                else line = test;
            }
            // pequeño margen arriba/abajo
            return lines * lineHeight + 6;
        }
        static string LetraDe(string gg)
        {
            if (string.IsNullOrEmpty(gg)) return "";
            int i1 = gg.IndexOf('"'); if (i1 < 0) return "";
            int i2 = gg.IndexOf('"', i1 + 1); if (i2 < 0) return "";
            return gg.Substring(i1 + 1, i2 - i1 - 1);
        }
        static string GradoTexto(byte n) => n switch
        {
            1 => "PRIMERO",
            2 => "SEGUNDO",
            3 => "TERCERO",
            4 => "CUARTO",
            5 => "QUINTO",
            _ => "SEXTO"
        };

        public void Generar(string path)
        {
            // ——— Preparación

            // Tipografías más pequeñas y consistentes
            const double FS_HDR = 7.2; // títulos de encabezados grises
            const double FS_MESES = 6.6; // meses rotados
            const double FS_PER = 6.2; // etiquetas de periodos
            const double FS_MAT = 8.0; // nombres de materias
            const double FS_BANDA = 3.0; // texto en bandas verticales
            const double FS_LABEL = 8.5; // otras etiquetas

            byte grado = 1;
            var digits = new string(gradoGrupo.Where(char.IsDigit).ToArray());
            if (byte.TryParse(digits, out var gParsed)) grado = gParsed;

            var mats = materias; // ya viene ordenada y filtrada por grado
            int nRows = mats.Count;

            var doc = new PdfDocument();
            var page = doc.AddPage(); page.Size = PageSize.Letter;
            var g = XGraphics.FromPdfPage(page); g.SmoothingMode = XSmoothingMode.AntiAlias;

            // ——— Márgenes/medidas
            double ML = 40, MT = 36;
            double W = page.Width - 2 * ML;
            double x = ML, y = MT;
            var pen = new XPen(P.Grid, 0.7);

            // ============= ENCABEZADO SENCILLO (como la segunda imagen) =============
            // Dibuja logo a la izquierda (si hay)
            double logoW = 70, logoH = 70;
            if (logo != null)
            {
                using var bmp = new Bitmap(logo);
                using var ms = new MemoryStream(); bmp.Save(ms, ImageFormat.Png); ms.Position = 0;
                using var img = XImage.FromStream(ms);
                g.DrawImage(img, x, y, logoW, logoH);
            }
            // Bloque de texto a la derecha del logo
            double tx = x + (logo != null ? (logoW + 18) : 0);
            double line = 14;

            // Línea 1: Nombre de la escuela (negrita, sin barras ni cajas)
            g.DrawString(string.IsNullOrWhiteSpace(escuela) ? "NOMBRE DE LA ESCUELA" : escuela,
                         F(13.5, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx, y + 12));
            double yy = y + 28;

            // Fila: SECCIÓN | CLAVE
            g.DrawString("SECCIÓN:", F(9.8, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx, yy));
            g.DrawString(string.IsNullOrWhiteSpace(seccion) ? "-" : seccion,
                         F(9.8), XBrushes.Black, new XPoint(tx + 60, yy));
            g.DrawString("CLAVE:", F(9.8, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx + 220, yy));
            g.DrawString(string.IsNullOrWhiteSpace(clave) ? "-" : clave,
                         F(9.8), XBrushes.Black, new XPoint(tx + 270, yy));
            yy += line;

            // Fila: GRADO Y GRUPO
            string letra = LetraDe(gradoGrupo);
            g.DrawString("GRADO Y GRUPO:", F(9.8, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx, yy));
            g.DrawString($"{GradoTexto(grado)} \"{letra}\"", F(9.8), XBrushes.Black, new XPoint(tx + 95, yy));
            yy += line;

            // Fila: NO. DE LISTA | CICLO ESCOLAR
            g.DrawString("NO. DE LISTA:", F(9.8, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx, yy));
            g.DrawString(string.IsNullOrWhiteSpace(listaNo) ? "-" : listaNo,
                         F(9.8), XBrushes.Black, new XPoint(tx + 80, yy));
            g.DrawString("CICLO ESCOLAR:", F(9.8, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx + 220, yy));
            g.DrawString(string.IsNullOrWhiteSpace(ciclo) ? "-" : ciclo,
                         F(9.8), XBrushes.Black, new XPoint(tx + 320, yy));
            yy += line;

            // Fila: ALUMNO (A):
            g.DrawString("ALUMNO (A):", F(9.8, XFontStyleEx.Bold), XBrushes.Black, new XPoint(tx, yy));
            g.DrawString(string.IsNullOrWhiteSpace(alumno) ? "-" : alumno.ToUpperInvariant(),
                         F(10.5), XBrushes.Black, new XPoint(tx + 80, yy));
            yy += line;

            // Separador tenue inferior (opcional, sin cajas de color)
            g.DrawLine(new XPen(P.Grid, 0.8), x, yy + 6, x + W, yy + 6);
            y = yy + 14;
            // =================== FIN ENCABEZADO SENCILLO ===================

            // ——— Tablas grandes
            double rightGap = 16;
            double periodsW = 120;
            double leftW = page.Width - 2 * ML - periodsW - rightGap;

            double hdrH = 26;
            double rowH = 20;               // <- súbelo aquí (lo usaremos para la altura del bloque izq.)
            double camposPct = 0.37;

            // CAMPOS: cuadro alto (hdr + fila de meses)
            var rCampos = new XRect(x, y, leftW * camposPct, hdrH + rowH);

            // CALIFICACIONES MENSUALES: solo barra gris superior
            var rMeses = new XRect(rCampos.Right, y, leftW - rCampos.Width, hdrH);

            double[] parts = { .24, .24, .24 };   // sólo 3 columnas dentro de PERIODOS
            double w123 = periodsW * (parts[0] + parts[1] + parts[2]);

            var rPerHdrPeriodos = new XRect(rMeses.Right + rightGap, y, w123, hdrH);
            var rPerPF = new XRect(rPerHdrPeriodos.Right, y, periodsW - w123, hdrH + rowH);


            // Marco del bloque PERIODOS (título + fila de 1er/2do/3ro)
            var rPeriodosBox = new XRect(rPerHdrPeriodos.X, y, w123, hdrH + rowH);
            g.DrawRectangle(XBrushes.White, rPerHdrPeriodos);
            g.DrawRectangle(pen, rPerHdrPeriodos);
            g.DrawRectangle(pen, rPeriodosBox);

            // Altura total del bloque de calificaciones: encabezados (hdrH+rowH) + filas (nRows*rowH) + PROM. MENSUAL (rowH)
            double bodyH = (hdrH + rowH) + nRows * rowH + rowH;

            // Cuadro izquierdo: CAMPO + CALIFICACIONES MENSUALES (sin PERIODOS)
            // Cuadros separados a la izquierda
            var boxCampos = new XRect(x, y, rCampos.Width, bodyH);                 // solo la banda de CAMPO + filas
            var boxMeses = new XRect(rCampos.Right, y, leftW - rCampos.Width, bodyH); // solo la zona de meses

            g.DrawRectangle(pen, boxCampos);
            g.DrawRectangle(pen, boxMeses);

            // Cuadro derecho: PERIODOS (1er, 2do, 3ro + PF)
            var rightBox = new XRect(rPerHdrPeriodos.X, y, periodsW, bodyH);
            g.DrawRectangle(pen, rightBox);

            // Columna única de PF (alto doble) con el nuevo color
            g.DrawRectangle(new XSolidBrush(P.PF_BG), rPerPF);
            g.DrawRectangle(pen, rPerPF);
            Rotar(g, rPerPF.X + rPerPF.Width / 2, rPerPF.Y + rPerPF.Height / 2, -90, () =>
            {
                var rr = new XRect(-rPerPF.Height / 2 + 1, -rPerPF.Width / 2 + 1, rPerPF.Height - 2, rPerPF.Width - 2);
                DrawMultiline(g, "PROMEDIO\nFINAL", F(6.8, XFontStyleEx.Bold), XBrushes.Black, rr, XParagraphAlignment.Center);
            });



            // Textos de las barras superiores
            DrawMultiline(g, "CAMPOS DE\nFORMACIÓN ACADÉMICA",
                          F(7.2, XFontStyleEx.Bold), XBrushes.Black, rCampos, XParagraphAlignment.Center);

            DrawMultiline(g, "CALIFICACIONES MENSUALES",
                          F(7.2, XFontStyleEx.Bold), XBrushes.Black, rMeses, XParagraphAlignment.Center);

            // “PERIODOS” SOLO sobre 1er–3ro
            DrawMultiline(g, "P  E  R  I  O  D  O  S",
                          F(7.2, XFontStyleEx.Bold), XBrushes.Black, rPerHdrPeriodos, XParagraphAlignment.Center);

            // Cabecera de meses
            double cellMesW = rMeses.Width / MESES.Length;
            // Cabecera de meses (debajo de rMeses)
            double yMes = rMeses.Bottom;

            for (int i = 0; i < MESES.Length; i++)
            {
                var rm = new XRect(rMeses.X + i * (rMeses.Width / MESES.Length), yMes, (rMeses.Width / MESES.Length), rowH);

                // Colores: DIAG especial, meses por trimestre 1/2/3
                XColor bg;
                if (i == 0) bg = P.M_DIAG;
                else
                {
                    int pIdx = (i >= 1 && i <= 3) ? 1 : (i >= 4 && i <= 6) ? 2 : 3;
                    bg = (pIdx == 1) ? P.M_P1 : (pIdx == 2) ? P.M_P2 : P.M_P3;
                }

                g.DrawRectangle(new XSolidBrush(bg), rm);

                Rotar(g, rm.X + rm.Width / 2, rm.Y + rm.Height / 2, -90, () =>
                {
                    g.DrawString(MESES[i], F(6.6, XFontStyleEx.Bold), XBrushes.Black,
                        new XRect(-rm.Height / 2 + 1, -rm.Width / 2 + 1, rm.Height - 2, rm.Width - 2), XStringFormats.Center);
                });

                g.DrawRectangle(pen, rm);
            }
            // Cabecera vertical PERÍODOS
            var perRowY = rPerHdrPeriodos.Bottom;   // misma altura que yMes + rowH
            var perRowH = rowH;

            // Dibuja 1er, 2do, 3ro, Prom. Final (con colores de los trimestres)
            string[] pLbl = { "1er", "2do", "3ro", "Prom.\nFinal" };
            XColor[] pCol = { P.Per1, P.Per2, P.Per3, P.Prom };

            double xp = rPerHdrPeriodos.X;  // arranca desde el inicio de periodos
            for (int i = 0; i < 3; i++)
            {
                double ww = periodsW * parts[i];
                var c = new XRect(xp, perRowY, ww, perRowH);
                g.DrawRectangle(new XSolidBrush(pCol[i]), c);
                g.DrawRectangle(pen, c);

                Rotar(g, c.X + c.Width / 2, c.Y + c.Height / 2, -90, () =>
                {
                    var rr = new XRect(-c.Height / 2 + 1, -c.Width / 2 + 1, c.Height - 2, c.Width - 2);
                    DrawMultiline(g, pLbl[i], F(6.2, XFontStyleEx.Bold), XBrushes.Black, rr, XParagraphAlignment.Center);
                });

                xp += ww;
            }

            // Cuerpo (materias)
            double yRows = rPerHdrPeriodos.Bottom + rowH;
            double bandaW = 24;
            double colNombresW = rCampos.Width;

            // Agrupamos consecutivos por campo (en el orden ya aplicado)
            int iRow = 0;
            while (iRow < nRows)
            {
                var campo = mats[iRow].Campo;
                int start = iRow;
                while (iRow < nRows && mats[iRow].Campo == campo) iRow++;
                int count = iRow - start;
                double h = count * rowH;

                var banda = new XRect(x, yRows + start * rowH, bandaW, h);
                g.DrawRectangle(P.BrushCampo(campo), banda);
                g.DrawRectangle(pen, banda);

                string rotulo = campo switch
                {
                    CampoFA.Lenguajes => "LENGUAJES",
                    CampoFA.SaberesYPensamientoCientifico => "SABERES\nY PENS.\nCIEN.",
                    CampoFA.EticaNaturalezaYSociedad => "ÉTICA,\nNATU\nY SOC.",
                    _ => "DE LO\nHUM\nY COMUN."
                };
                Rotar(g, banda.X + banda.Width / 2, banda.Y + banda.Height / 2, -90, () =>
                {
                    var rr = new XRect(-banda.Height / 2 + 2, -banda.Width / 2 + 2, banda.Height - 4, banda.Width - 4);
                    DrawMultiline(g, rotulo, F(FS_BANDA, XFontStyleEx.Bold), XBrushes.Black, rr, XParagraphAlignment.Center);
                });
            }
            double pfW = periodsW - w123;
            for (int i = 0; i < nRows; i++)
            {
                double yR = yRows + i * rowH;

                var cNom = new XRect(x + bandaW, yR, colNombresW - bandaW, rowH);
                g.DrawRectangle(P.BrushCampo(mats[i].Campo), cNom);
                g.DrawRectangle(pen, cNom);

                // Muestra nombre tal cual (puedes quitar “ I/II/III/IV” si quieres)
                g.DrawString(mats[i].Nombre.ToUpperInvariant(), F(FS_MAT), XBrushes.Black, new XRect(cNom.X + 6, cNom.Y + 4, cNom.Width - 8, cNom.Height - 8), XStringFormats.TopLeft);

                // columnas de meses (blancas)
                for (int m = 0; m < MESES.Length; m++)
                {
                    var c = new XRect(rMeses.X + m * cellMesW, yR, cellMesW, rowH);
                    g.DrawRectangle(XBrushes.White, c);
                    g.DrawRectangle(pen, c);
                }
                // subcolumnas de períodos (blancas)
                double xpRow = rPerHdrPeriodos.X;
                for (int k = 0; k < parts.Length; k++)
                {
                    double ww = periodsW * parts[k];
                    var c = new XRect(xpRow, yR, ww, rowH);
                    g.DrawRectangle(XBrushes.White, c);
                    g.DrawRectangle(pen, c);
                    xpRow += ww;
                }

                // Columna única de PF (alineada con rPerPF)
                var cPF = new XRect(rPerHdrPeriodos.X + w123, yR, pfW, rowH);
                g.DrawRectangle(XBrushes.White, cPF);
                g.DrawRectangle(pen, cPF);
            }

            var prom = new XRect(x, yRows + nRows * rowH, leftW, rowH);
            g.DrawRectangle(XBrushes.White, prom); g.DrawRectangle(pen, prom);
            g.DrawString("PROM. MENSUAL", F(9, XFontStyleEx.Bold), XBrushes.Black,
                         new XRect(prom.X + 30, prom.Y + 4, 140, prom.Height - 8), XStringFormats.TopLeft);

            // meses (igual que las filas)
            for (int m = 0; m < MESES.Length; m++)
            {
                var c = new XRect(rMeses.X + m * cellMesW, prom.Y, cellMesW, rowH);
                g.DrawRectangle(XBrushes.White, c);
                g.DrawRectangle(pen, c);
            }

            // 3 periodos
            double xp2 = rPerHdrPeriodos.X;
            for (int k = 0; k < parts.Length; k++)
            {
                double ww = periodsW * parts[k];
                var c = new XRect(xp2, prom.Y, ww, rowH);
                g.DrawRectangle(XBrushes.White, c);
                g.DrawRectangle(pen, c);
                xp2 += ww;
            }

            // PF único
            var cPFprom = new XRect(rPerHdrPeriodos.X + w123, prom.Y, pfW, rowH);
            g.DrawRectangle(XBrushes.White, cPFprom);
            g.DrawRectangle(pen, cPFprom);

            // =================== INASISTENCIAS ===================
            double yIna = prom.Bottom + 14;

            // Misma altura que las filas de materias
            double inaRowH = rowH;

            // Título con el MISMO ancho que los recuadros de la materia (banda+nombre)
            var inaTit = new XRect(x, yIna + inaRowH, rMeses.X - x, inaRowH);
            g.DrawRectangle(XBrushes.White, inaTit);
            g.DrawRectangle(pen, inaTit);
            g.DrawString("INASISTENCIAS", F(9, XFontStyleEx.Bold), XBrushes.Black, inaTit, XStringFormats.Center);

            // Encabezado de meses (mismo ancho y celda que la grilla de meses)
            string[] mesC = { "D", "S", "O", "N/D", "E", "F", "M", "A", "M", "J" };
            double xx = rMeses.X; // arranca al inicio exacto de los meses
            for (int i = 0; i < mesC.Length; i++)
            {
                var rc = new XRect(xx, yIna, cellMesW, inaRowH);

                // Colores: D diagnóstico; 1er: S,O,N/D ; 2º: E,F,M ; 3º: A,M,J
                XColor bg;
                if (i == 0) bg = P.M_DIAG;                 // D
                else if (i >= 1 && i <= 3) bg = P.Ina_P1;  // S,O,N/D
                else if (i >= 4 && i <= 6) bg = P.Ina_P2;  // E,F,M
                else bg = P.Ina_P3;                        // A,M,J

                g.DrawRectangle(new XSolidBrush(bg), rc);
                g.DrawRectangle(pen, rc);
                g.DrawString(mesC[i], F(8, XFontStyleEx.Bold), XBrushes.Black, rc, XStringFormats.Center);

                xx += cellMesW;
            }

            // Fila inferior en blanco para capturar inasistencias (misma medida)
            double yInaVals = yIna + inaRowH;
            xx = rMeses.X;
            for (int i = 0; i < mesC.Length; i++)
            {
                var rc = new XRect(xx, yInaVals, cellMesW, inaRowH);
                g.DrawRectangle(XBrushes.White, rc);
                g.DrawRectangle(pen, rc);
                xx += cellMesW;
            }

            // Separación antes de CALIF. debajo de PERÍODOS
            double yCalif = yIna;

            // Encabezados CALIF. 1er/2º/3er y PF con sus colores, alineados a PERÍODOS
            double calW1 = periodsW * parts[0];
            double calW2 = periodsW * parts[1];
            double calW3 = periodsW * parts[2];
            double calWpf = periodsW - (calW1 + calW2 + calW3);

            (double w, XColor col, string txt)[] calHeads = {
    (calW1, P.Per1, "CALIF."),
    (calW2, P.Per2, "CALIF."),
    (calW3, P.Per3, "CALIF."),
    (calWpf, P.PF_BG, "PF")
};

            xx = rPerHdrPeriodos.X;
            foreach (var (w, col, txt) in calHeads)
            {
                var rc = new XRect(xx, yCalif, w, inaRowH);
                g.DrawRectangle(new XSolidBrush(col), rc);
                g.DrawRectangle(pen, rc);
                g.DrawString(txt, F(8, XFontStyleEx.Bold), XBrushes.Black, rc, XStringFormats.Center);
                xx += w;
            }

            // Renglón en blanco para capturar las calificaciones por periodo
            double yCalifVals = yIna + inaRowH;
            xx = rPerHdrPeriodos.X;
            foreach (var (w, _, __) in calHeads)
            {
                var rc = new XRect(xx, yCalifVals, w, inaRowH);
                g.DrawRectangle(XBrushes.White, rc);
                g.DrawRectangle(pen, rc);
                xx += w;
            }

            // =================== NIVELES DE DESEMPEÑO ===================
            // arranca después del renglón de captura de CALIF. (+espaciado)
            double yNivStart = yCalifVals + inaRowH + 14;

            var nivHdr = new XRect(x, yNivStart, W, 18);
            g.DrawRectangle(new XSolidBrush(P.HeadGray), nivHdr);
            g.DrawRectangle(pen, nivHdr);
            g.DrawString("NIVELES DE DESEMPEÑO", F(9, XFontStyleEx.Bold), XBrushes.Black, nivHdr, XStringFormats.CenterLeft);

            string[] niveles =
            {
    "NIVEL I  = EQUIVALE A 5|El estudiante tiene carencias fundamentales en valores y principios para desarrollar una convivencia sana y pacífica, dentro y fuera del aula.",
    "NIVEL II = EQUIVALE A 6 Y 7|El estudiante tiene dificultades para demostrar valores y principios para desarrollar una convivencia sana y pacífica, dentro y fuera del aula.",
    "NIVEL III = EQUIVALE A 8 Y 9|El estudiante ha demostrado los valores y principios para desarrollar una convivencia sana y pacífica, dentro y fuera del aula.",
    "NIVEL IV = EQUIVALE A 10|El estudiante ha demostrado los valores y principios para desarrollar una convivencia sana y pacífica, dentro y fuera del aula."
};

            double wIzq = W * 0.26;           // un poco más ancho para el rotulado
            double yyN = nivHdr.Bottom;
            var fontTituloNivel = F(8, XFontStyleEx.Bold);
            var fontDescNivel = F(8);

            // Recuadros por nivel, con altura dinámica según el texto envuelto
            foreach (var nv in niveles)
            {
                var partsTxt = nv.Split('|');
                var c1 = new XRect(x, yyN, wIzq, 1);           // altura la calculamos después
                var c2 = new XRect(x + wIzq, yyN, W - wIzq, 1);

                // Altura necesaria del párrafo derecho (texto largo)
                double altoDerecha = AltoParrafo(g, partsTxt[1], fontDescNivel, c2.Width - 8, lineHeight: 10);
                // Deja un mínimo decente para que el rótulo izquierdo respire
                double filaN = Math.Max(26, Math.Ceiling(altoDerecha + 6)); // colchón extr
                // Re-define celdas con la altura final
                c1 = new XRect(c1.X, c1.Y, c1.Width, filaN);
                c2 = new XRect(c2.X, c2.Y, c2.Width, filaN);

                g.DrawRectangle(XBrushes.White, c1); g.DrawRectangle(pen, c1);
                g.DrawRectangle(XBrushes.White, c2); g.DrawRectangle(pen, c2);

                // Izquierda: rótulo (corto) sin wrap
                g.DrawString(partsTxt[0], fontTituloNivel, XBrushes.Black, c1, XStringFormats.CenterLeft);

                // Derecha: descripción con wrap real (usa nuestro DrawMultiline → XTextFormatter)
                var rtext = new XRect(c2.X + 4, c2.Y + 2, c2.Width - 8, c2.Height - 4);
                DrawMultiline(g, partsTxt[1], fontDescNivel, XBrushes.Black, rtext, XParagraphAlignment.Left);

                yyN += filaN;
            }
            g.DrawLine(pen, x, yyN, x + W, yyN);

            double yFirmas = yyN + 10; // ← esto sigue igual, ahora parte debajo de NIVELES

            // Firmas
            string[] m1 = { "AGOSTO DIAGNOSTICO", "SEPTIEMBRE", "OCTUBRE", "NOV/ DIC", "ENERO" };
            string[] m2 = { "FEBRERO", "MARZO", "ABRIL", "MAYO", "JUNIO" };
            double gapCol = 24, colW = (W - gapCol) / 2.0;

            void Col(string[] ms, double xc)
            {
                var t = new XRect(xc, yFirmas, colW, 18);
                g.DrawRectangle(new XSolidBrush(P.HeadGray), t); g.DrawRectangle(pen, t);
                g.DrawString("MES                                   FIRMA DEL PADRE O TUTOR", F(8, XFontStyleEx.Bold), XBrushes.Black, t, XStringFormats.CenterLeft);

                double yy = t.Bottom;
                foreach (var m in ms)
                {
                    var rm = new XRect(xc, yy, colW * 0.5, 22);
                    var rf = new XRect(rm.Right, yy, colW - rm.Width, 22);
                    g.DrawRectangle(XBrushes.White, rm); g.DrawRectangle(pen, rm);
                    g.DrawRectangle(XBrushes.White, rf); g.DrawRectangle(pen, rf);
                    g.DrawString(m, F(8, XFontStyleEx.Bold), XBrushes.Black, new XRect(rm.X + 4, rm.Y + 3, rm.Width - 8, rm.Height - 6), XStringFormats.TopLeft);
                    g.DrawString("______________________________", F(8), XBrushes.Black, rf, XStringFormats.Center);
                    yy += 22;
                }
            }
            Col(m1, x);
            Col(m2, x + colW + gapCol);

            doc.Save(path);
        }
    }
    public partial class FormCapturaCalificaciones : Form
    {
        // ====== Paleta ======
        private static readonly Color C_BG = Color.FromArgb(244, 247, 247);
        private static readonly Color C_ACCENT = Color.FromArgb(121, 168, 169);
        private static readonly Color C_PRIMARY = Color.FromArgb(31, 78, 95);
        private static readonly Color C_TEXTDIM = Color.FromArgb(70, 84, 94);
        private List<BoletaPdfBuilder.MateriaPlan> _materiasPlan = new();

        private static Font Fx(float s, FontStyle st = FontStyle.Regular)
        {
            try { return new Font("Aptos", s, st); }
            catch { return new Font("Segoe UI", s, st); }
        }

        // ====== Estado ======
        private Panel pnlCards;          // vista 1
        private Panel pnlCaptura;        // vista 2

        // Encabezado captura
        private Label lblTituloFormato;
        private PictureBox pbEscudo;
        private Label lblEscNombre, lblGrupo, lblMaestro, lblCiclo;

        // Selector trimestre/meses
        private Button btnT1, btnT2, btnT3;
        private FlowLayoutPanel mesesStrip;   // meses (siempre debajo de trimestres)
        private FlowLayoutPanel trimesStrip;  // trimestres (siempre arriba)
        private Button _btnVolver;

        // Grid principal
        private DataGridView grid;

        private int? _grupoIdSel;
        private byte _gradoIdSel;
        private string _letraSel = "";
        private string _profSel = "";
        private List<string> _materias = new();
        private List<AlumnoListaDto> _alumnos = new();

        private enum Trimestre { T1 = 1, T2 = 2, T3 = 3 }
        private Trimestre _tri = Trimestre.T1;

        private Label _mesActivo = null;

        public FormCapturaCalificaciones()
        {
            InitializeComponent();

            Text = "Captura de Calificaciones";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1060, 720);
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96)); // header alto
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // Botón volver visible sólo en la vista de captura
            _btnVolver = new Button
            {
                Text = "← Grupos",
                AutoSize = true,
                Height = 30,
                BackColor = Color.White,
                ForeColor = C_PRIMARY,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(10, 10, 0, 10),
                Visible = false
            };
            _btnVolver.FlatAppearance.BorderSize = 0;

            // Navegación
            _btnVolver.Click += (_, __) => MostrarCards();

            // ====== Header ======
            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = C_PRIMARY,
                Padding = new Padding(12, 6, 12, 10)
            };
            root.Controls.Add(header, 0, 0);

            // header layout
            var hdr = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = C_PRIMARY
            };
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));   // título centrado ocupa 2 columnas visualmente
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            hdr.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // botón a la derecha
            header.Controls.Add(hdr);

            // Título centrado (col 0, fila 0) con ColumnSpan=2
            var lblTituloModulo = new Label
            {
                Text = "Captura de Calificaciones",
                ForeColor = Color.White,
                AutoSize = true,
                Font = Fx(20f, FontStyle.Bold),
                Anchor = AnchorStyles.None,
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(0, 6, 0, 0)
            };
            hdr.Controls.Add(lblTituloModulo, 0, 0);
            hdr.SetColumnSpan(lblTituloModulo, 2);

            // Botón volver en la 3a columna (derecha)
            _btnVolver.Text = "Grupos →";
            _btnVolver.Visible = false;
            _btnVolver.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            hdr.Controls.Add(_btnVolver, 2, 0);

            header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(0, 0, 0, 40) });

            // ====== Contenido ======
            var content = new Panel { Dock = DockStyle.Fill, BackColor = C_BG };
            root.Controls.Add(content, 0, 1);

            // Vista 1: cards de grupos
            pnlCards = new Panel { Dock = DockStyle.Fill, BackColor = C_BG, Padding = new Padding(18) };
            content.Controls.Add(pnlCards);

            // Vista 2: formato de captura
            pnlCaptura = new Panel { Dock = DockStyle.Fill, BackColor = C_BG, Padding = new Padding(18), Visible = false };
            content.Controls.Add(pnlCaptura);

            // ====== Encabezado del formato ======
            var top = new Panel { Dock = DockStyle.Top, Height = 160, BackColor = Color.White };
            pnlCaptura.Controls.Add(top);

            pbEscudo = new PictureBox
            {
                Image = Properties.Resources.escudo, // tu recurso
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(18, 12),
                Size = new Size(86, 86)
            };
            top.Controls.Add(pbEscudo);

            lblEscNombre = new Label
            {
                Text = "ESCUELA PRIMARIA EMILIANO ZAPATA",
                AutoSize = false,
                Location = new Point(110, 12),
                Size = new Size(760, 26),
                Font = Fx(13f, FontStyle.Bold),
                ForeColor = Color.Black
            };
            top.Controls.Add(lblEscNombre);

            lblTituloFormato = new Label
            {
                Text = "ASIGNACIÓN DE CALIFICACIONES",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom,
                Height = 46,
                Font = Fx(16f, FontStyle.Bold)
            };
            top.Controls.Add(lblTituloFormato);

            // Sub-encabezado con grupo, maestro, ciclo
            var info = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = C_BG,
                Padding = new Padding(0)
            };
            lblGrupo = new Label { AutoSize = true, Font = Fx(12, FontStyle.Bold), ForeColor = C_PRIMARY, Text = "Grupo: -" };
            lblMaestro = new Label { AutoSize = true, Font = Fx(12), ForeColor = C_TEXTDIM, Margin = new Padding(16, 6, 0, 0), Text = "Maestro: -" };
            lblCiclo = new Label { AutoSize = true, Font = Fx(12), ForeColor = C_TEXTDIM, Margin = new Padding(16, 6, 0, 0), Text = "Ciclo: Actual" };
            info.Controls.Add(lblGrupo);
            info.Controls.Add(lblMaestro);
            info.Controls.Add(lblCiclo);
            pnlCaptura.Controls.Add(info);

            // === IMPORTANTE: primero agrego MESES y luego TRIMESTRES,
            // pero como Dock=Top el ÚLTIMO agregado queda ARRIBA.
            // Así garantizamos: TRIMESTRES ARRIBA, MESES ABAJO. ===

            // Strip de meses (debajo)
            mesesStrip = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = C_BG,
                Padding = new Padding(0, 0, 0, 8)
            };
            pnlCaptura.Controls.Add(mesesStrip);

            // Selector de trimestres (arriba)
            trimesStrip = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 56,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = C_BG,
                Padding = new Padding(0, 8, 0, 8)
            };
            pnlCaptura.Controls.Add(trimesStrip);

            btnT1 = MakePill("1° TRIMESTRE");
            btnT2 = MakePill("2° TRIMESTRE");
            btnT3 = MakePill("3° TRIMESTRE");
            btnT1.Click += (_, __) => CambiarTrimestre(Trimestre.T1);
            btnT2.Click += (_, __) => CambiarTrimestre(Trimestre.T2);
            btnT3.Click += (_, __) => CambiarTrimestre(Trimestre.T3);
            trimesStrip.Controls.AddRange(new Control[] { btnT1, btnT2, btnT3 });

            // Grid
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,                     // visual por ahora
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoGenerateColumns = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            pnlCaptura.Controls.Add(grid);


            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 235, 235);
            grid.ColumnHeadersDefaultCellStyle.Font = Fx(8f, FontStyle.Regular);
            grid.RowTemplate.Height = 28;
            grid.GridColor = Color.FromArgb(210, 216, 220);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 239, 237);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(249, 251, 252);

            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            grid.ColumnHeadersHeight = 56; // más alto (ajusta 46–56 a tu gusto)
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ScrollBars = ScrollBars.Vertical; // evita scroll horizontal (forzamos a que quepa)

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            grid.ColumnHeadersHeight = 64;                         // más alto (prueba 56–72)
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(0, 6, 0, 6); // aire arriba/abajo

            grid.AllowUserToResizeColumns = false;
            grid.AllowUserToResizeRows = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;


            // Columnas “fijas” de alumno (con pesos para Fill)
            // Columnas “fijas” (2) + botones (3)  -> AutoSizeColumnsMode = Fill
            var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };

            var colNo = new DataGridViewTextBoxColumn
            {
                HeaderText = "No.",
                DataPropertyName = "No",
                FillWeight = 5,              // más angosto
                MinimumWidth = 50,
                DefaultCellStyle = center
            };
            var colAlumno = new DataGridViewTextBoxColumn
            {
                HeaderText = "ALUMNO",
                DataPropertyName = "Alumno",
                FillWeight = 35,             // ancho generoso para ver nombre completo
                MinimumWidth = 220,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(6, 0, 0, 0)
                }
            };

            grid.Columns.Add(colNo);
            grid.Columns.Add(colAlumno);

            // Botones por alumno (más compactos)
            grid.Columns.Add(new DataGridViewButtonColumn
            {
                HeaderText = "",
                Text = "✎",
                UseColumnTextForButtonValue = true,
                FillWeight = 6,
                MinimumWidth = 50
            });
            grid.Columns.Add(new DataGridViewButtonColumn
            {
                HeaderText = "",
                Text = "⟲",
                UseColumnTextForButtonValue = true,
                FillWeight = 6,
                MinimumWidth = 50
            });
            grid.Columns.Add(new DataGridViewButtonColumn
            {
                HeaderText = "",
                Text = "🖨",
                UseColumnTextForButtonValue = true,
                FillWeight = 7,
                MinimumWidth = 54
            });

            grid.CellClick += Grid_CellClick;

            // Navegación

            // Cargar tarjetas al entrar
            CargarCards();
            CambiarTrimestre(Trimestre.T1); // pinta meses iniciales
        }

        // ====== UI helpers ======
        private Button MakePill(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 36,
                BackColor = Color.White,
                ForeColor = C_PRIMARY,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(8, 6, 8, 6),
                Padding = new Padding(14, 6, 14, 6)
            };
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(210, 220, 224);
            return b;
        }
        private List<AlumnoListaDto> CargarAlumnosPorGrupo(int grupoId)
        {
            // 1) Intenta por el repositorio actual
            var list = AlumnoRepo.GetByGrupo(grupoId)?.ToList() ?? new List<AlumnoListaDto>();
            if (list.Count > 0) return list;

            // 2) Fallback directo a BD (mismo orden que usas en las cards)
            try
            {
                using (var cn = Db.New())
                {
                    cn.Open();
                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = @"
                        SELECT a.nombre, a.apellido_paterno, a.apellido_materno
                        FROM dbo.Alumno AS a
                        WHERE a.Activo = 1 AND a.id_grupo = @id
                        ORDER BY a.apellido_paterno, a.apellido_materno, a.nombre;";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@id";
                        p.Value = grupoId;
                        cmd.Parameters.Add(p);

                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                list.Add(new AlumnoListaDto
                                {
                                    Nombre = rd.IsDBNull(0) ? "" : rd.GetString(0),
                                    Paterno = rd.IsDBNull(1) ? "" : rd.GetString(1),
                                    Materno = rd.IsDBNull(2) ? "" : rd.GetString(2)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // útil para depurar sin romper la UI
                MessageBox.Show("Fallback de alumnos falló:\n" + ex.Message, "BD", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return list;
        }
        private Label MonthBadge(string text)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(6, 8, 6, 6),
                Padding = new Padding(10, 6, 10, 6),
                Font = Fx(10.5f, FontStyle.Bold),
                ForeColor = C_PRIMARY,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            lbl.Click += (s, e) => SetMesActivo(lbl);
            return lbl;
        }

        // ====== Vista 1: Tarjetas de grupos ======
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

                var grupos = GrupoRepo.GetGruposActivos();
                foreach (var g in grupos.OrderBy(x => x.GradoID))
                {
                    var card = CrearCardGrupo(g);
                    layout.Controls.Add(card);
                }
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
                    using (var pen = new Pen(Color.FromArgb(220, 225, 230), 1f))
                    {
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        e.Graphics.DrawRectangle(pen, r);
                    }
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

                var icon = new Label { Text = " 🏫", AutoSize = true, Font = new Font("Segoe UI Emoji", 26f), ForeColor = C_PRIMARY, Margin = new Padding(0, 0, 8, 0), Anchor = AnchorStyles.None };
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
                    Text = $"Profesor: {prof}",
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

                void abrir(object _1, EventArgs _2) => AbrirGrupo(g);
                card.Click += abrir;
                foreach (Control c in col.Controls) c.Click += abrir;

                return card;
            }

            private void MostrarCards()
            {
                _btnVolver.Visible = false;

                pnlCaptura.Visible = false;
                pnlCards.Visible = true;
                _grupoIdSel = null;
                grid.DataSource = null;
            }

            // ====== Vista 2: Captura ======
            private void AbrirGrupo(GrupoSimpleDto g)
            {
                _grupoIdSel = g.GrupoID;
                _gradoIdSel = g.GradoID;
                _letraSel = g.Letra;
                _profSel = string.IsNullOrWhiteSpace(g.Profesor) ? "(Sin profesor)" : g.Profesor;

                _btnVolver.Visible = true;

                lblGrupo.Text = $"Grupo: {g.GradoDesc} {g.Letra}";
                lblMaestro.Text = $"Maestro: {_profSel}";
                lblCiclo.Text = "Ciclo: Actual";

            // 1) Materias del grado desde BD
            List<BoletaPdfBuilder.MateriaPlan> materiasPlan;
            using (var cn = Db.New())
            {
                cn.Open();
                var sinOrden = BoletaPdfBuilder.ObtenerMateriasPorGrado(cn, _gradoIdSel);
                materiasPlan = BoletaPdfBuilder.OrdenarMaterias(_gradoIdSel, sinOrden);
            }
            _materiasPlan = materiasPlan;
            // 2) (si necesitas para el grid, puedes derivar solo los nombres)
            _materias = materiasPlan.Select(m => m.Nombre.ToUpperInvariant()).ToList();
            RebuildMateriaColumns();

            // Alumnos del grupo
            _alumnos = AlumnoRepo.GetByGrupo(g.GrupoID)
                                      .OrderBy(a => a.Paterno).ThenBy(a => a.Materno).ThenBy(a => a.Nombre)
                                      .ToList();

                // Alumnos del grupo (con fallback)
                _alumnos = CargarAlumnosPorGrupo(g.GrupoID)
                           .OrderBy(a => a.Paterno).ThenBy(a => a.Materno).ThenBy(a => a.Nombre)
                           .ToList();

                // Columnas de materias
                RebuildMateriaColumns();

                // ===== SOLO FILAS HASTA DONDE HAY ALUMNOS y NUMERACIÓN DESDE 12 =====
                var rows = new List<RowAlumno>();
            for (int i = 0; i < _alumnos.Count; i++)
            {
                var a = _alumnos[i];
                // En AbrirGrupo(..), cuando construyes 'rows':
                rows.Add(new RowAlumno
                {
                    No = i + 1,
                    Alumno = $"{a.Paterno} {a.Materno} {a.Nombre}".Trim(),
                        Promedio = "" // o "—"

                });
                }
                grid.SuspendLayout();
                grid.DataSource = null;
                grid.DataSource = rows;
                grid.ClearSelection();
                grid.ResumeLayout();
                grid.Refresh();

                if (rows.Count == 0)
                {
                    MessageBox.Show(
                        "No se encontraron alumnos para este grupo.\n" +
                        $"GrupoID: {_grupoIdSel}, {g.GradoDesc} {g.Letra}\n" +
                        "Verifica datos Activo/id_grupo en dbo.Alumno.",
                        "Sin alumnos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                pnlCards.Visible = false;
                pnlCaptura.Visible = true;
                _btnVolver.Visible = true;
                grid.BringToFront();
            }
        // ===== helper de color para las columnas del grid =====
        private static Color ColorCampo(BoletaPdfBuilder.CampoFA c) => c switch
        {
            BoletaPdfBuilder.CampoFA.Lenguajes => Color.FromArgb(221, 235, 247), // azul claro
            BoletaPdfBuilder.CampoFA.SaberesYPensamientoCientifico => Color.FromArgb(255, 153, 255), // magenta pastel (Mat/Tec/Ciencias)
            BoletaPdfBuilder.CampoFA.EticaNaturalezaYSociedad => Color.FromArgb(255, 230, 153), // crema (Cívica/Ética)
            BoletaPdfBuilder.CampoFA.DeLoHumanoYLoComunitario => Color.FromArgb(204, 255, 51), // cian (Ed. Física)
            _ => Color.FromArgb(235, 235, 235)
        };

        private void RebuildMateriaColumns()
        {
            int idxFijas = 2;      // [0]=No., [1]=ALUMNO
            int botonesFinales = 3;

            // Limpia cualquier columna de materias previa
            while (grid.Columns.Count > (idxFijas + botonesFinales))
                grid.Columns.RemoveAt(idxFijas);

            // Pesos base para Fill
            float wNo = 6f;
            float wAlumno = 40f;
            float wBtnsCapt = 6f, wBtnsLimp = 6f, wBtnsImp = 7f;
            float wReservado = wNo + wAlumno + (wBtnsCapt + wBtnsLimp + wBtnsImp); // 65
            float wMaterias = 100f - wReservado;

            // Asegura los pesos de las dos primeras columnas (ya creadas previamente)
            grid.Columns[0].FillWeight = wNo;      // No.
            grid.Columns[1].FillWeight = wAlumno;  // ALUMNO

            // ——— NUEVO: construir columnas según _materiasPlan (orden ya “de boleta”) ———
            int n = _materiasPlan?.Count ?? 0;
            float wPorMateria = (n > 0) ? Math.Max(3.5f, wMaterias / n) : 0f;

            var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };

            foreach (var mp in _materiasPlan)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = mp.Nombre.ToUpperInvariant()
                                         .Replace("  ", " ")
                                         .Replace("CÍVICA Y", "CÍVICA\nY")
                                         .Replace("CIVICA Y", "CIVICA\nY"),
                    ReadOnly = true,
                    DefaultCellStyle = center,
                    FillWeight = wPorMateria,
                    MinimumWidth = 60
                };

                // Encabezado con color por CAMPO (que coincide con el PDF)
                var hdrStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    WrapMode = DataGridViewTriState.True,
                    Font = Fx(7.4f, FontStyle.Regular),
                    BackColor = ColorCampo(mp.Campo),
                    ForeColor = Color.Black,
                    SelectionBackColor = ColorCampo(mp.Campo),
                    SelectionForeColor = Color.Black
                };
                col.HeaderCell.Style = hdrStyle;

                // Inserta antes de los 3 botones de acción
                grid.Columns.Insert(grid.Columns.Count - botonesFinales, col);
            }
            // === COLUMNA PROMEDIO (después de materias, antes de los botones) ===
            var colProm = new DataGridViewTextBoxColumn
            {
                HeaderText = "PROMEDIO",
                DataPropertyName = "Promedio",     // enlaza con RowAlumno.Promedio
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = Fx(8f, FontStyle.Regular)
                },
                FillWeight = 8f,
                MinimumWidth = 70
            };
            // Inserta justo antes de los 3 botones de acción
            grid.Columns.Insert(grid.Columns.Count - botonesFinales, colProm);

            // Ajusta los tres botones
            int colCap = grid.Columns.Count - 3;
            int colLim = grid.Columns.Count - 2;
            int colImp = grid.Columns.Count - 1;

            grid.Columns[colCap].FillWeight = wBtnsCapt; grid.Columns[colCap].MinimumWidth = 44;
            grid.Columns[colLim].FillWeight = wBtnsLimp; grid.Columns[colLim].MinimumWidth = 44;
            grid.Columns[colImp].FillWeight = wBtnsImp; grid.Columns[colImp].MinimumWidth = 52;

            foreach (DataGridViewColumn c in grid.Columns)
                c.Resizable = DataGridViewTriState.False;

            // Importante para que se vean los colores de header
            grid.EnableHeadersVisualStyles = false;
            grid.Refresh();
        }
        private void CambiarTrimestre(Trimestre t)
            {
                _tri = t;

                btnT1.BackColor = (t == Trimestre.T1) ? C_ACCENT : Color.White;
                btnT2.BackColor = (t == Trimestre.T2) ? C_ACCENT : Color.White;
                btnT3.BackColor = (t == Trimestre.T3) ? C_ACCENT : Color.White;

                btnT1.ForeColor = btnT1.BackColor == Color.White ? C_PRIMARY : Color.White;
                btnT2.ForeColor = btnT2.BackColor == Color.White ? C_PRIMARY : Color.White;
                btnT3.ForeColor = btnT3.BackColor == Color.White ? C_PRIMARY : Color.White;

                mesesStrip.Controls.Clear();
                switch (t)
                {
                    case Trimestre.T1:
                        mesesStrip.Controls.Add(MonthBadge("Septiembre"));
                        mesesStrip.Controls.Add(MonthBadge("Octubre"));
                        mesesStrip.Controls.Add(MonthBadge("Noviembre/Diciembre"));
                        break;
                    case Trimestre.T2:
                        mesesStrip.Controls.Add(MonthBadge("Enero"));
                        mesesStrip.Controls.Add(MonthBadge("Febrero"));
                        mesesStrip.Controls.Add(MonthBadge("Marzo"));
                        break;
                    case Trimestre.T3:
                        mesesStrip.Controls.Add(MonthBadge("Abril"));
                        mesesStrip.Controls.Add(MonthBadge("Mayo"));
                        mesesStrip.Controls.Add(MonthBadge("Junio"));
                        break;
                }
            // Selecciona el primero por defecto
            if (mesesStrip.Controls.Count > 0 && mesesStrip.Controls[0] is Label first)
                SetMesActivo(first);
        }

        // ====== Data model para grid ======
            private sealed class RowAlumno
            {
                public int No { get; set; }
                public string Alumno { get; set; } = "";
            // NUEVO: para poder consultar en BD el promedio del alumno
            public int AlumnoId { get; set; }

            // NUEVO: columna que se mostrará en el grid
            public string Promedio { get; set; } = "";
        }

            // ====== Acciones por alumno (maqueta) ======
            private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0) return;
                int colCap = grid.Columns.Count - 3;
                int colLimp = grid.Columns.Count - 2;
                int colImp = grid.Columns.Count - 1;
                var materiasOrdenadas = _materiasPlan;

            if (e.ColumnIndex == colCap)
                {
                    var a = AlumnoDeFila(e.RowIndex);
                    MessageBox.Show($"[Capturar] {NombreCompleto(a)}\nTrimestre: {_tri}\n(Maqueta visual)", "Captura", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (e.ColumnIndex == colLimp)
                {
                    var a = AlumnoDeFila(e.RowIndex);
                    MessageBox.Show($"[Limpiar] Calificaciones de {NombreCompleto(a)}\n(Maqueta visual)", "Limpiar", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            else if (e.ColumnIndex == colImp)
            {
                var a = AlumnoDeFila(e.RowIndex);
                var alumnoNombre = (a == null) ? "(sin alumno)" : $"{a.Paterno} {a.Materno} {a.Nombre}".Trim();

                var pdf = new BoletaPdfBuilder(
                    escuela: "ESCUELA PRIMARIA EMILIANO ZAPATA",
                    seccion: "PRIMARIA",
                    clave: "01DBT0172Z",
                    gradoGrupo: $"{_gradoIdSel}°  \"{_letraSel}\"",
                    listaNo: $"{e.RowIndex + 1}",
                    ciclo: "2025–2026",
                    alumno: alumnoNombre,
                    materias: materiasOrdenadas,
                    logo: Properties.Resources.escudo
                );

                using (var sfd = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = $"Boleta_{alumnoNombre}.pdf" })
                {
                    if (sfd.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            pdf.Generar(sfd.FileName);

                            var psi = new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true };
                            System.Diagnostics.Process.Start(psi);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error al generar PDF:\n\n" + ex, "PDF",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }

        }
        private void SetMesActivo(Label lbl)
        {
            if (_mesActivo != null)
            {
                _mesActivo.BackColor = Color.White;
                _mesActivo.ForeColor = C_PRIMARY;
            }
            lbl.BackColor = C_ACCENT;
            lbl.ForeColor = Color.White;
            _mesActivo = lbl;
        }

        private AlumnoListaDto? AlumnoDeFila(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _alumnos.Count) return null;
            return _alumnos[rowIndex];
        }

        private static string NombreCompleto(AlumnoListaDto? a)
        {
            if (a == null) return "(sin alumno)";
            return $"{a.Nombre} {a.Paterno} {a.Materno}".Trim();
        }
    }
}