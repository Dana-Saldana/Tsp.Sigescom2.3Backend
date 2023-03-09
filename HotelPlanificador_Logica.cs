using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Facturacion;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Generico;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel.ModeloExtranet;
using Tsp.Sigescom.Modelo.Custom;
using Tsp.Sigescom.Modelo.Entidades;
using Tsp.Sigescom.Modelo.Entidades.Custom;
using Tsp.Sigescom.Modelo.Entidades.Custom.Partial;
using Tsp.Sigescom.Modelo.Entidades.Exceptions;
using Tsp.Sigescom.Modelo.Entidades.Sesion;
using Tsp.Sigescom.Modelo.Interfaces.Datos;
using Tsp.Sigescom.Modelo.Interfaces.Datos.Actores;
using Tsp.Sigescom.Modelo.Interfaces.Datos.Establecimientos;
using Tsp.Sigescom.Modelo.Interfaces.Logica;
using Tsp.Sigescom.Modelo.Interfaces.Negocio;
using Tsp.Sigescom.Modelo.Interfaces.Repositorio;

namespace Tsp.Sigescom.Logica.SigesHotel
{
    public partial class HotelLogica : IHotelLogica
    {
        public ReportePlanificador ObtenerReportePlanificador(int idEstablecimiento, int idActorNegocioQueTienePrecios)
        {
            try
            {
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var idsAmbientesSeleccionados = ObtenerAmbientesHotelPorEstablecimiento(idEstablecimiento).Select(a => a.Id).ToList();
                var idsTiposHabitacionSeleccionados = ObtenerTipoHabitaciones().Select(a => a.Id).ToList();
                var habitacionesSeleccionadas = _hotelRepositorio.ObtenerHabitacionesPlanificador(idsAmbientesSeleccionados.ToArray(), idsTiposHabitacionSeleccionados.ToArray(), idActorNegocioQueTienePrecios, fechaActual).ToList();
                ReportePlanificador reportePlanificador = new ReportePlanificador();
                reportePlanificador.Disponibles = habitacionesSeleccionadas.Where(hs => hs.Disponible).Count();
                reportePlanificador.Ocupadas = habitacionesSeleccionadas.Where(hs => hs.Ocupada).Count();
                reportePlanificador.PorIngresar = habitacionesSeleccionadas.Where(hs => hs.PorIngresar).Count();
                reportePlanificador.PorSalir = habitacionesSeleccionadas.Where(hs => hs.PorSalir).Count();
                reportePlanificador.EnLimpieza = habitacionesSeleccionadas.Where(hs => hs.EnLimpieza).Count();
                reportePlanificador.FechaActual = fechaActual;
                return reportePlanificador;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener reporte planificador", e);
            }
        }
        public Planificador ObtenerPlanificadorHabitaciones(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta, int idAmbiente, int idTipoHabitacion, int idActorNegocioQueTienePrecios)
        {
            try
            {
                Planificador planificadorHabitaciones = new Planificador();
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var idsAmbientesSeleccionados = (idAmbiente == 0) ? ObtenerAmbientesHotelPorEstablecimiento(idEstablecimiento).Select(a => a.Id).ToList() : new List<int>() { idAmbiente };
                var idsTiposHabitacionSeleccionados = (idTipoHabitacion == 0) ? ObtenerTipoHabitaciones().Select(a => a.Id).ToList() : new List<int>() { idTipoHabitacion };
                var habitacionesSeleccionadas = _hotelRepositorio.ObtenerHabitacionesPlanificador(idsAmbientesSeleccionados.ToArray(), idsTiposHabitacionSeleccionados.ToArray(), idActorNegocioQueTienePrecios, fechaActual).ToList();
                habitacionesSeleccionadas = habitacionesSeleccionadas.OrderBy(hs => hs.TipoHabitacion).ToList();
                foreach (var habitacion in habitacionesSeleccionadas)
                {
                    var estadosHabitacion = ObtenerEstadosHabitaciones(fechaDesde, fechaHasta, habitacion, fechaActual);
                    planificadorHabitaciones.HabitacionesEnPlanificador.Add(estadosHabitacion);
                }
                return planificadorHabitaciones;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener planificador de habitaciones", e);
            }
        }
        public HabitacionEnPlanificador ObtenerEstadosHabitaciones(DateTime fechaDesde, DateTime fechaHasta, HabitacionEnPlanificador habitacion, DateTime fechaActual)
        {
            try
            {
                List<EstadoHabitacionEnPlanificador> estadosHabitacion = new List<EstadoHabitacionEnPlanificador>();
                var fechaConsulta = fechaDesde;
                while (fechaConsulta <= fechaHasta)
                {
                    estadosHabitacion.Add(_hotelRepositorio.ObtenerEstadoHabitacionPlanificador(fechaConsulta, habitacion.Id, fechaActual));
                    fechaConsulta = fechaConsulta.Date.AddDays(1);
                }
                habitacion.EstadosHabitacion = estadosHabitacion;
                return habitacion;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener estados de habitaciones", e);
            }
        }
 
        public OperationResult CambiarEnLimpiezaDeHabitacion(int idHabitacion)
        {
            try
            {
                return _actor_Repositorio.InvertirIndicador1ActorNegocio(idHabitacion);
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar cambiar en limpieza de habitacion", e);
            }
        }

    }
}
