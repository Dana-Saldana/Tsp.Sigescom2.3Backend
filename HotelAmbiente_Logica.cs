using System;
using System.Collections.Generic;
using System.Linq;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Generico;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel;
using Tsp.Sigescom.Modelo.Entidades;
using Tsp.Sigescom.Modelo.Entidades.Exceptions;
using Tsp.Sigescom.Modelo.Interfaces.Negocio;
namespace Tsp.Sigescom.Logica.SigesHotel
{
    public partial class HotelLogica : IHotelLogica
    {
        public List<ItemGenerico> ObtenerAmbientesVigentesPorEstablecimientoSimplificado(int idEstablecimiento)
        {
            try
            {
                return _hotelRepositorio.ObtenerAmbientesVigentesPorEstablecimientoSimplificado(idEstablecimiento).ToList();
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener ambientes de hotel", e);
            }
        }
        public List<AmbienteHotel> ObtenerAmbientesHotelPorEstablecimiento(int idEstablecimiento)
        {
            try
            {
                return _hotelRepositorio.ObtenerAmbientesHotelPorEstablecimiento(idEstablecimiento).ToList();
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener ambientes", e);
            }
        }
        private Actor_negocio GenerarAmbienteActorNegocio(AmbienteHotel ambiente)
        {
            try
            {
                DateTime fechaActual = DateTimeUtil.FechaActual();
                DateTime fechaFin = fechaActual.AddYears(ActorSettings.Default.vigenciaEnAnyosPorDefectoDeActorDeNegocioEntidadInterna);
                //Crear el actor de negocio
                Actor_negocio _habitacionActorNegocio = new Actor_negocio(HotelSettings.Default.IdRolAmbienteHotel, fechaActual, fechaFin, "", ambiente.EsVigente, ambiente.Establecimiento.Id, false, "");

                //Crear al actor
                Actor actor = new Actor(HotelSettings.Default.IdDetalleMaestroDocumentoIdentidadAmbienteHotel, fechaActual, ambiente.Codigo, ambiente.Nombre, "", "", HotelSettings.Default.IdTipoActorAmbienteHotel, ActorSettings.Default.IdFotoActorPorDefecto, HotelSettings.Default.IdClaseActorAmbienteHotel, HotelSettings.Default.IdEstadoLegalAmbienteHotel, "", "", "");
                //Asignar el actor al vehiculo
                _habitacionActorNegocio.Actor = actor;
                return _habitacionActorNegocio;
            }
            catch (Exception e)
            {

                throw new LogicaException("Error al intentar generar actor negocio habitacion", e);
            }
        }
        private void EstablecerIdActorNegocioParaEditar(Actor_negocio ambienteActorNegocio, AmbienteHotel ambiente)
        {
            try
            {
                ambienteActorNegocio.id = ambiente.Id;
                ambienteActorNegocio.id_actor = ambiente.IdActor;
                ambienteActorNegocio.Actor.id = ambiente.IdActor;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar establecer el id de actor negociio habitacion", e);
            }
        }
        public OperationResult CrearAmbiente(AmbienteHotel ambiente)
        {
            try
            {
                if (_hotelRepositorio.ExisteNombreAmbienteEnEstablecimiento(ambiente.Nombre, ambiente.Establecimiento.Id))
                {
                    throw new LogicaException("Error al intentar crear el ambiente, el nombre de ambiente ya existe.");
                }
                var ambienteActorNegocio = GenerarAmbienteActorNegocio(ambiente);
                var resultado = _actor_Repositorio.CrearActorNegocio(ambienteActorNegocio);
                //Conseguir datos luego de guardar
                ambiente.Id = ambienteActorNegocio.id;
                ambiente.IdActor = ambienteActorNegocio.id_actor;
                resultado.information = ambiente;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar registrar AMBIENTE", e);
            }
        }
        public OperationResult EditarAmbiente(AmbienteHotel ambiente)
        {
            try
            {
                if (_hotelRepositorio.ExisteNombreAmbienteEnEstablecimientoExceptoAmbiente(ambiente.Nombre, ambiente.Establecimiento.Id, ambiente.Id))
                {
                    throw new LogicaException("Error al intentar editar el ambiente, el nombre de ambiente ya existe.");
                }
                var habitacionActorNegocio = GenerarAmbienteActorNegocio(ambiente);
                EstablecerIdActorNegocioParaEditar(habitacionActorNegocio, ambiente);
                var resultado = _actor_Repositorio.ActualizarActorNegocioYIdActorNegocioPadre(habitacionActorNegocio);
                resultado.information = ambiente;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar registrar AMBIENTE", e);
            }
        }
        public OperationResult CambiarVigenciaDelAmbienteHotel(int idAmbiente)
        {
            try
            {
                if (_hotelRepositorio.ExisteHabitacionesVigentesAmbiente(idAmbiente))
                {
                    throw new LogicaException("existe habitaciones en vigencia para este ambiente");
                }
                else
                {
                    return _actor_Repositorio.InvertirEsVigenteActorNegocio(idAmbiente);
                }
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar cambiar vigencia de habitacion", e);
            }
        }
    }
}
