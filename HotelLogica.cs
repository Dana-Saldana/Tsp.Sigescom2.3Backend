using System;
using System.Collections.Generic;
using System.Linq;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Generico;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel;
using Tsp.Sigescom.Modelo.Entidades;
using Tsp.Sigescom.Modelo.Entidades.Custom;
using Tsp.Sigescom.Modelo.Entidades.Exceptions;
using Tsp.Sigescom.Modelo.Entidades.Sesion;
using Tsp.Sigescom.Modelo.Interfaces.Datos;
using Tsp.Sigescom.Modelo.Interfaces.Datos.Actores;
using Tsp.Sigescom.Modelo.Interfaces.Datos.Almacen;
using Tsp.Sigescom.Modelo.Interfaces.Datos.Establecimientos;
using Tsp.Sigescom.Modelo.Interfaces.Logica;
using Tsp.Sigescom.Modelo.Interfaces.Negocio;
using Tsp.Sigescom.Modelo.Interfaces.Repositorio;
using Tsp.Sigescom.Modelo.Negocio.Almacen;

namespace Tsp.Sigescom.Logica.SigesHotel
{
    public partial class HotelLogica : IHotelLogica
    {
        private readonly IHotelRepositorio _hotelRepositorio;
        private readonly IActor_Repositorio _actor_Repositorio;
        private readonly IMaestroRepositorio _maestroRepositorio;
        private readonly IConceptoRepositorio _conceptoRepositorio;
        private readonly ITransaccionRepositorio _transaccionRepositorio;
        private readonly IConceptoLogica _logicaConcepto;
        private readonly IOperacionLogica _logicaOperacion;
        private readonly IActorNegocioLogica _logicaActorNegocio;
        private readonly IPermisos_Logica _permisos_Logica;
        private readonly ICodigosOperacion_Logica _codigosOperacion_Logica;
        private readonly IEstablecimiento_Repositorio _establecimientoDatos;
        private readonly ICentroDeAtencion_Logica _centroDeAtencionLogica;
        private readonly IPrecioRepositorio _precioRepositorio;
        private readonly IInventarioActual_Logica _inventarioActualLogica;


        public HotelLogica(IHotelRepositorio hotelRepositorio, IMaestroRepositorio maestroRepositorio, IConceptoRepositorio conceptoRepositorio, ITransaccionRepositorio transaccionRepositorio, IFacturacionRepositorio facturacionRepositorio, IPrecioRepositorio precioRepositorio, ICodigosOperacion_Logica codigosOperacion_Logica, IPermisos_Logica permisos_Logica, IActorNegocioLogica logicaActorNegocio, IEstablecimiento_Repositorio establecimientoDatos, ICentroDeAtencion_Logica centroDeAtencionLogica, IActor_Repositorio actor_Repositorio, IActorRepositorio actorRepositorio, IInventarioActual_Logica inventarioActualLogica)
        {
            _transaccionRepositorio = transaccionRepositorio;
            _conceptoRepositorio = conceptoRepositorio;
            _maestroRepositorio = maestroRepositorio;
            _hotelRepositorio = hotelRepositorio;
            _precioRepositorio = precioRepositorio;
            _codigosOperacion_Logica = codigosOperacion_Logica;
            _logicaOperacion = new OperacionLogica(_transaccionRepositorio, _maestroRepositorio, actorRepositorio, _conceptoRepositorio, facturacionRepositorio, _codigosOperacion_Logica, permisos_Logica, null, actor_Repositorio, null, null);
            _logicaActorNegocio = logicaActorNegocio;
            _logicaConcepto = new ConceptoLogica(_transaccionRepositorio, _conceptoRepositorio, _maestroRepositorio, _precioRepositorio, actorRepositorio, _logicaActorNegocio, inventarioActualLogica);
            _permisos_Logica = permisos_Logica;
            _establecimientoDatos = establecimientoDatos;
            _centroDeAtencionLogica = centroDeAtencionLogica;
            _actor_Repositorio = actor_Repositorio;
            _inventarioActualLogica = inventarioActualLogica;
        }

        #region TIPO CAMAS
        public List<ItemGenerico> ObtenerTipoCamas()
        {
            try
            {
                return _hotelRepositorio.ObtenerTiposCama().ToList();
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener tipo camas  de hotel", e);
            }
        }
        #endregion

        #region COMPLEMENTOS
        public OperationResult ActualizarComplemento(Complemento complemento)
        {
            try
            {
                return null;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        public OperationResult GuardarComplemento(Complemento complemento)
        {
            try
            {
                Detalle_maestro nuevoComplemento = new Detalle_maestro()
                {
                    nombre = complemento.Nombre,
                    codigo = complemento.Nombre,
                    valor = complemento.Nombre
                };
                foreach (var valor in complemento.Valores)
                {
                    var nuevoValorComplemento = new Detalle_maestro()
                    {
                        nombre = valor.Nombre,
                        codigo = valor.Nombre,
                        valor = valor.Nombre,
                    };
                    nuevoComplemento.Detalle_detalle_maestro1.Add(new Detalle_detalle_maestro() { Detalle_maestro1 = nuevoValorComplemento });
                    //nuevoComplemento.Detalle_detalle_maestro1.Add(nuevoComplemento);
                }
                return null;// _repositorioMaestro.GuardarDetalleMaestro(nuevoComplemento);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        #endregion

        #region HUESPED
        public ItemGenerico ObtenerUltimoMotivoViajeHuesped(int idHuesped)
        {
            try
            {
                ItemGenerico motivoViaje = null;
                if (idHuesped != ActorSettings.Default.IdClienteGenerico)
                {
                    motivoViaje = _hotelRepositorio.ObtenerUltimoMotivoViajeHuesped(idHuesped);
                }
                return motivoViaje;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener Habitaciones", e);
            }
        }
        public OperationResult AgregarHuesped(long idAtencion, int idActorComercial, int idMotivoViaje, bool esTitular)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                Actor_negocio_por_transaccion actorComercialPorTransaccion = new Actor_negocio_por_transaccion()
                {
                    id_transaccion = idAtencion,
                    id_actor_negocio = idActorComercial,
                    id_rol = HotelSettings.Default.IdRolHuesped,
                    id_detalle_maestro = idMotivoViaje,
                    extension_json = "{ estitular: \"" + esTitular.ToString().ToLower() + "\" }"
                };
                resultado = _hotelRepositorio.CrearActorNegocioPorTransaccion(actorComercialPorTransaccion);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar anular la atencion macro", e);
            }
        }
        public OperationResult CambiarTitularHuesped(int idHuespedCambiado, int idHuespedNuevoTitular)
        {
            try
            {
                OperationResult resultado = _hotelRepositorio.CambiarTitularHuesped(idHuespedCambiado, idHuespedNuevoTitular);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar anular la atencion macro", e);
            }
        }
        public OperationResult EliminarHuesped(int idHuesped)
        {
            try
            {
                OperationResult resultado = _hotelRepositorio.EliminarActorNegocioPorTransaccion(idHuesped);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar anular la atencion macro", e);
            }
        }
        #endregion

        
    }
}
