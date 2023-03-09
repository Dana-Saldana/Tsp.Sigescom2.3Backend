using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo.Interfaces.Negocio;

namespace Tsp.Sigescom.Logica.SigesHotel
{
    public class HotelUtilitario_Logica : IHotelUtilitario_Logica
    {
        public int ObtenerNumeroNoches(DateTime fechaInicio, DateTime fechaFin)
        {
            int numeroNoches = (fechaFin.Date - fechaInicio.Date).Days;
            DateTime fechaHoteleraInicio = fechaInicio.Date.AddHours(12);
            DateTime fechaHoteleraFin = fechaFin.Date.AddHours(12);
            if ((fechaInicio - fechaHoteleraInicio).TotalMinutes + HotelSettings.Default.ToleranciaEnMinutosParaChecking < 0)
            {
                numeroNoches++;
            }
            if ((fechaFin - fechaHoteleraFin).TotalMinutes - HotelSettings.Default.ToleranciaEnMinutosParaChecking > 0)
            {
                numeroNoches++;
            }
            return numeroNoches;
        }
    }
}
