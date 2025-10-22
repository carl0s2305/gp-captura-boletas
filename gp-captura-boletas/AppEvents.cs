using System;

namespace gp_captura_boletas
{
    internal static class AppEvents
    {
        // Se dispara cuando cambian datos que afectan tarjetas/listas
        public static event Action? DatosCambiaron;

        public static void RaiseDatosCambiaron()
            => DatosCambiaron?.Invoke();
    }
}
