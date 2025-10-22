using Microsoft.Win32;
using PdfSharp.Fonts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace gp_captura_boletas
{
    internal static class Program
    {
        /// <summary>
        /// Punto de entrada principal para la aplicación.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (GlobalFontSettings.FontResolver is null)
                GlobalFontSettings.FontResolver = new AppFontResolver();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // App cerrando normal
            Application.ApplicationExit += (_, __) => SesionApp.CerrarSesion();

            // Cierre de sesión/apagado de Windows
            SystemEvents.SessionEnding += (_, __) => SesionApp.CerrarSesion();
            Application.Run(new FormLogin());
        }
    }
}
