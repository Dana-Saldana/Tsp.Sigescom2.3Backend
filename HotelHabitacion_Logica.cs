using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Generico;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel;
using Tsp.Sigescom.Modelo.Entidades;
using Tsp.Sigescom.Modelo.Entidades.Custom.Partial;
using Tsp.Sigescom.Modelo.Entidades.Exceptions;
using Tsp.Sigescom.Modelo.Interfaces.Negocio;

namespace Tsp.Sigescom.Logica.SigesHotel
{
    public partial class HotelLogica : IHotelLogica
    {
        public OperationResult CambiarEsVigenteHabitacion(int id)
        {
            try
            {
                if (_hotelRepositorio.ExisteAtencionesHabitacion(id))
                {
                    throw new LogicaException("Error al cambiar el estado de la habitacion, tienes atenciones de habitacion.");
                }
                return _actor_Repositorio.InvertirEsVigenteActorNegocio(id);
            }
            catch (Exception e)
            {
                throw new LogicaException("Error en la logica Cambiar es vigente Habitacion", e);
            }
        }
        public Habitacion ObtenerHabitacion(int id)
        {
            try
            {
                Habitacion habitacion = _hotelRepositorio.ObtenerHabitacion(id);
                habitacion.Camas = JsonConvert.DeserializeObject<List<ItemGenerico>>(habitacion.InformacionCamas);
                return habitacion;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener Habitaciones", e);
            }
        }
        public List<HabitacionBandeja> ObtenerHabitacionesBandeja(int idestablecimiento)
        {
            try
            {
                return _hotelRepositorio.ObtenerHabitacionesBandeja(idestablecimiento).ToList();
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener Habitaciones", e);
            }
        }
        public string ConvertirCamaString(List<ItemGenerico> camas)
        {
            string cadenaCamas = "";
            foreach (var item in camas)
            {
                cadenaCamas = $"{cadenaCamas}{item.Valor}x{item.Nombre} + ";
            }
            return cadenaCamas;
        }
        public string ConvertirCamasJsonArray(List<ItemGenerico> camas)
        {
            string cadenaCamas = "";
            foreach (var item in camas)
            {
                cadenaCamas = cadenaCamas + "{Id:" + item.Id + ", Nombre:\"" + item.Nombre + "\", Valor:" + item.Valor + " },";
            }
            return "[" + cadenaCamas + "]";
        }
        private Actor_negocio GenerarActorNegocio(Habitacion habitacion)
        {
            try
            {
                DateTime fechaActual = DateTimeUtil.FechaActual();
                DateTime fechaFin = fechaActual.AddYears(ActorSettings.Default.vigenciaEnAnyosPorDefectoDeActorDeNegocioEntidadInterna);
                //Crear el actor de negocio
                Actor_negocio _habitacionActorNegocio = new Actor_negocio(HotelSettings.Default.IdRolHabitacion, fechaActual, fechaFin, "", habitacion.EsVigente, habitacion.Ambiente.Id, true, "")
                {
                    id_concepto_negocio = habitacion.TipoHabitacion.Id
                };
                //Crear al actor
                Actor actor = new Actor(HotelSettings.Default.IdDetalleMaestroDocumentoIdentidadHabitacion, fechaActual, habitacion.CodigoHabitacion, "", ConvertirCamasJsonArray(habitacion.Camas), habitacion.Anexo, HotelSettings.Default.IdTipoActorHabitacion,
                    ActorSettings.Default.IdFotoActorPorDefecto, HotelSettings.Default.IdClaseActorHabitacion, HotelSettings.Default.IdEstadoLegalHabitacion, "", "", "");
                //Asignar el actor al vehiculo
                _habitacionActorNegocio.Actor = actor;
                return _habitacionActorNegocio;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar generar actor negocio habitacion", e);
            }
        }
        private void EstablecerIdActorNegocioParaEditar(Actor_negocio habitacionActorNegocio, Habitacion habitacion)
        {
            try
            {
                habitacionActorNegocio.id = habitacion.Id;
                habitacionActorNegocio.id_actor = habitacion.IdActor;
                habitacionActorNegocio.Actor.id = habitacion.IdActor;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar establecer el id de actor negociio habitacion", e);
            }
        }
        public OperationResult CrearHabitacion(Habitacion habitacion)
        {
            try
            {
                habitacion.EsVigente = true;
                if (_hotelRepositorio.ExisteCodigoHabitacionEnEstablecimiento(habitacion.CodigoHabitacion, habitacion.Ambiente.Establecimiento.Id))
                {
                    throw new LogicaException("Error al intentar crear la habitacion, el codigo de habitación ya existe.");
                }
                var habitacionActorNegocio = GenerarActorNegocio(habitacion);
                var resultado = _actor_Repositorio.CrearActorNegocio(habitacionActorNegocio);
                //Conseguir datos luego de guardar
                habitacion.Id = habitacionActorNegocio.id;
                habitacion.IdActor = habitacionActorNegocio.id_actor;
                resultado.information = habitacion;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar registrar habitacion", e);
            }
        }
        public OperationResult EditarHabitacion(Habitacion habitacion)
        {
            try
            {
                if (_hotelRepositorio.ExisteCodigoHabitacionEnEstablecimientoExceptoHabitacion(habitacion.CodigoHabitacion, habitacion.Ambiente.Establecimiento.Id, habitacion.Id))
                {
                    throw new LogicaException("Error al intentar editar la habitacion, el codigo de habitación ya existe.");
                }
                var habitacionActorNegocio = GenerarActorNegocio(habitacion);
                EstablecerIdActorNegocioParaEditar(habitacionActorNegocio, habitacion);
                var resultado = _actor_Repositorio.ActualizarActorNegocioIncluidoActor(habitacionActorNegocio);
                resultado.information = habitacion;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar editar habitacion", e);
            }
        }
        public List<Habitacion> ObtenerHabitacionesDisponibles(int idTipoHabitacion, DateTime fechaDesde, DateTime fechaHasta, int idEstablecimiento, int idAmbiente, int idActorNegocioQueTienePrecios)
        {
            try
            {
                List<Habitacion> habitacionesDisponibles;
                if (idAmbiente == 0)
                {
                    habitacionesDisponibles = _hotelRepositorio.ObtenerHabitacionDisponiblesPorEstablecimientoConPrecio(idTipoHabitacion, fechaDesde, fechaHasta, idEstablecimiento, idActorNegocioQueTienePrecios).ToList();
                }
                else
                {
                    habitacionesDisponibles = _hotelRepositorio.ObtenerHabitacionDisponibles(idTipoHabitacion, fechaDesde, fechaHasta, idAmbiente, idActorNegocioQueTienePrecios).ToList();
                }
                foreach (var habitacionDisponible in habitacionesDisponibles)
                {
                    if (habitacionDisponible.TipoHabitacion.Precios.Count() > 0)
                    {
                        var precioNormal = habitacionDisponible.TipoHabitacion.Precios.SingleOrDefault(p => p.IdTarifa == MaestroSettings.Default.IdDetalleMaestroTarifaNormal);
                        if (precioNormal != null)
                        {
                            var preciosHabitacion = habitacionDisponible.TipoHabitacion.Precios.ToList().Where(p => p.Id != precioNormal.Id);
                            var nuevosPrecios = new List<Precio_Concepto_Negocio_Comercial>() { precioNormal };
                            nuevosPrecios.AddRange(preciosHabitacion);
                            habitacionDisponible.TipoHabitacion.Precios = nuevosPrecios;
                        }
                    }
                }
                return habitacionesDisponibles;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener Habitaciones", e);
            }
        }
        public Habitacion ObtenerHabitacionDisponible(int idHabitacion, int idActorNegocioQueTienePrecios)
        {
            try
            {
                Habitacion habitacionDisponible = _hotelRepositorio.ObtenerHabitacionDisponible(idHabitacion, idActorNegocioQueTienePrecios);
                return habitacionDisponible;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener una habitacion", e);
            }
        }
        public List<Habitacion> BuscarHabitacionesDisponiblesConPrecio(int idTipoHabitacion, DateTime fechaDesde, DateTime fechaHasta, int idEstablecimiento, int idActorNegocioQueTienePrecios)
        {
            try
            {
                List<Habitacion> habitacionesDisponibles = _hotelRepositorio.ObtenerHabitacionDisponiblesPorEstablecimientoConPrecio(idTipoHabitacion, fechaDesde, fechaHasta, idEstablecimiento, idActorNegocioQueTienePrecios).ToList();
                return habitacionesDisponibles;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar buscar habitaciones disponibles", e);
            }
        }
        public List<Habitacion> BuscarHabitacionesDisponibles(int idTipoHabitacion, DateTime fechaDesde, DateTime fechaHasta, int idEstablecimiento)
        {
            try
            {
                List<Habitacion> habitacionesDisponibles = _hotelRepositorio.ObtenerHabitacionDisponiblesPorEstablecimiento(idTipoHabitacion, fechaDesde, fechaHasta, idEstablecimiento).ToList();
                return habitacionesDisponibles;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar buscar habitaciones disponibles", e);
            }
        }
    }
}
