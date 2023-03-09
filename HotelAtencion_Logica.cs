using Newtonsoft.Json;
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
using Tsp.Sigescom.Modelo.Interfaces.Negocio;

namespace Tsp.Sigescom.Logica.SigesHotel
{
    public partial class HotelLogica : IHotelLogica
    {
        public OperationResult GuardarAnotacion(AtencionHotel atencion)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                DateTime fechaActual = DateTimeUtil.FechaActual();
                atencion.Anotaciones.Add(new Anotacion(fechaActual.ToString("dd/MM/yyyy HH:mm tt"), atencion.Anotacion));
                var comentario = JsonConvert.SerializeObject(atencion.Anotaciones);
                if(comentario.Length > 500)
                {
                    throw new LogicaException("Se superó el máximo de anotaciones.");
                }
                resultado = _hotelRepositorio.ActualizarComentarioTransaccion(atencion.Id, comentario);
                resultado.information = atencion;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar guardar la anotación", e);
            }
        }
        public OperationResult EditarFechaAtencion(AtencionHotel atencion)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                DateTime fechaIngreso = DateTime.Parse(atencion.FechaIngreso.ToString("dd/MM/yyyy"));
                DateTime fechaSalida = DateTime.Parse(atencion.FechaSalida.ToString("dd/MM/yyyy"));
                if (!_hotelRepositorio.ObtenerDisponibilidadHabitacion(atencion.Id, atencion.Habitacion.Id, fechaIngreso, fechaSalida))
                {
                    throw new LogicaException("La habitación se encuentra reservada u ocupada en las fechas seleccionadas");
                }
                atencion.Importe = atencion.Noches * atencion.PrecioUnitario;
                var atencionMacroBD = _transaccionRepositorio.ObtenerTransaccionPadre(atencion.Id);
                var atencionBD = _transaccionRepositorio.ObtenerTransaccion(atencion.Id);
                atencionMacroBD.importe_total = atencionMacroBD.importe_total - atencionBD.importe_total + atencion.Importe;
                atencionBD.cantidad1 = atencion.Noches;
                atencionBD.importe1 = atencion.PrecioUnitario;
                atencionBD.importe_total = atencion.Importe;
                atencionBD.fecha_inicio = fechaIngreso;
                atencionBD.fecha_fin = fechaSalida;
                resultado = _transaccionRepositorio.ActualizarTransacciones(new List<Transaccion>() { atencionMacroBD, atencionBD });
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar anular la atencion macro", e);
            }
        }
        public OperationResult CambiarHabitacionAtencion(AtencionHotel atencion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                var fechaActual = DateTimeUtil.FechaActual();
                var atencionBD = _transaccionRepositorio.ObtenerTransaccion(atencion.Id);
                if (atencion.EstadoActual.Id == MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn)
                {
                    Transaccion atencionCambioAtencion = atencionBD.CloneTransaccion();
                    atencionCambioAtencion.id_comprobante = atencionBD.id_comprobante;
                    atencionCambioAtencion.id_actor_negocio_interno = atencion.Habitacion.Id;
                    atencionCambioAtencion.fecha_registro_sistema = fechaActual;
                    atencionCambioAtencion.fecha_inicio = DateTime.Parse(atencion.FechaIngreso.ToString("dd/MM/yyyy"));
                    atencionCambioAtencion.fecha_fin = DateTime.Parse(atencion.FechaSalida.ToString("dd/MM/yyyy"));
                    atencionCambioAtencion.cantidad1 = atencion.Noches;
                    atencionCambioAtencion.importe1 = atencion.PrecioUnitario;
                    atencionCambioAtencion.importe_total = atencion.Importe;
                    atencionCambioAtencion.enum1 = atencionBD.enum1;
                    atencionCambioAtencion.indicador1 = atencionBD.indicador1;
                    atencionCambioAtencion.indicador2 = atencionBD.indicador2;
                    atencionCambioAtencion.id_transaccion_referencia = atencion.Id;
                    atencionCambioAtencion.Estado_transaccion.Add(new Estado_transaccion(sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoEntradaCambiado, fechaActual, "Entrada por cambio de habitacion"));
                    foreach (var actorNegocioTipoTransaccion in atencionBD.Actor_negocio_por_transaccion)
                    {
                        atencionCambioAtencion.Actor_negocio_por_transaccion.Add(new Actor_negocio_por_transaccion { id_actor_negocio = actorNegocioTipoTransaccion.id_actor_negocio, id_detalle_maestro = actorNegocioTipoTransaccion.id_detalle_maestro, id_rol = actorNegocioTipoTransaccion.id_rol, extension_json = actorNegocioTipoTransaccion.extension_json });
                    }

                    atencionBD.fecha_fin = DateTime.Parse(atencion.FechaIngreso.ToString("dd/MM/yyyy"));
                    atencionBD.cantidad1 -= atencionCambioAtencion.cantidad1;
                    atencionBD.importe_total -= atencionCambioAtencion.importe_total;

                    Estado_transaccion estadoCambiadoSalida = new Estado_transaccion(atencionBD.id, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoSalidaCambiado, fechaActual, "Salida por cambio de habitacion");

                    var consumosPendientesFacturacion = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion(atencion.Id).Where(c => c.id_tipo_transaccion == TransaccionSettings.Default.IdTipoTransaccionOrdenConsumoHabitacion && c.id_estado_actual == MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado && !c.indicador1).ToList();
                    atencionCambioAtencion.Transaccion11 = new List<Transaccion>();
                    foreach (var consumo in consumosPendientesFacturacion)
                    {
                        consumo.id_actor_negocio_interno1 = atencion.Habitacion.Id;
                        atencionCambioAtencion.Transaccion11.Add(consumo);
                    }
                    resultado = _transaccionRepositorio.CrearTransaccionActualizarTransaccionCrearEstadoTransaccion(atencionCambioAtencion, atencionBD, estadoCambiadoSalida);
                }
                else
                {
                    atencionBD.id_actor_negocio_interno = atencion.Habitacion.Id;
                    resultado = _transaccionRepositorio.ActualizarTransaccion(atencionBD);
                }
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar cambiar la habitacion de atencion", e);
            }
        }
        public OperationResult ResolverEstadoAtencion(long idAtencion, int idNuevoEstado, string observacion, int[] idsEstadosValidos, UserProfileSessionData sesionUsuario)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var estado = _maestroRepositorio.ObtenerDetalle(idNuevoEstado);
                var atencion = _transaccionRepositorio.ObtenerTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencion);
                if (!idsEstadosValidos.Contains((int)atencion.id_estado_actual))
                {
                    throw new LogicaException("Error al validar la atencion y su estado.");
                }
                Estado_transaccion estadoTransaccion = new Estado_transaccion(atencion.id, sesionUsuario.Empleado.Id, idNuevoEstado, fechaActual, observacion);
                resultado = _transaccionRepositorio.CrearEstadoDeTransaccionAhora(estadoTransaccion);
                estadoTransaccion.Detalle_maestro = estado;
                resultado.information = new EstadoAtencionHotel()
                {
                    Estados = new List<ItemEstado>() { new ItemEstado(estadoTransaccion) },
                    Facturado = atencion.Evento_transaccion.Select(ev => ev.id_evento).Contains(MaestroSettings.Default.IdDetalleMaestroEstadoFacturado)
                };
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar resolver la creacion de estado de atencion", e);
            }
        }
        public OperationResult ConfirmarAtencion(long idAtencion, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoRegistrado };
                var idNuevoEstado = MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado;
                OperationResult resultado = ResolverEstadoAtencion(idAtencion, idNuevoEstado, observacion, idsEstadosValidos.ToArray(), sesionUsuario);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar confirmar la atencion", e);
            }
        }
        public OperationResult CheckInAtencion(long idAtencion, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado };
                var idNuevoEstado = MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn;
                OperationResult resultado = ResolverEstadoAtencion(idAtencion, idNuevoEstado, observacion, idsEstadosValidos.ToArray(), sesionUsuario);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar checkin la atencion", e);
            }
        }
        public OperationResult CheckOutAtencion(long idAtencion, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var atencionHabitacion = _transaccionRepositorio.ObtenerTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencion);
                var consumosPendientesFacturacion = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion(idAtencion).Where(c => c.id_tipo_transaccion == TransaccionSettings.Default.IdTipoTransaccionOrdenConsumoHabitacion && c.id_estado_actual == MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado).ToList();
                if ((!atencionHabitacion.indicador1) || consumosPendientesFacturacion.Where(ah => ah.indicador1).Count() != consumosPendientesFacturacion.Count)
                {
                    throw new LogicaException("Error al intentar checkout la atencion, debido a que no estan todas las atenciones y/o consumos facturados");
                }
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn, MaestroSettings.Default.IdDetalleMaestroEstadoEntradaCambiado };
                var idNuevoEstado = MaestroSettings.Default.IdDetalleMaestroEstadoCheckedOut;
                OperationResult resultado = ResolverEstadoAtencion(idAtencion, idNuevoEstado, observacion, idsEstadosValidos.ToArray(), sesionUsuario);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar checkout la atencion", e);
            }
        }
        public OperationResult AnularAtencion(long idAtencion, long idAtencionMacro, List<ComprobanteAtencion> comprobantes, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoRegistrado, MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado };
                var idNuevoEstado = MaestroSettings.Default.IdDetalleMaestroEstadoAnulado;
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var estado = _maestroRepositorio.ObtenerDetalle(idNuevoEstado);
                var atencionMacro = _transaccionRepositorio.ObtenerTransaccion(idAtencionMacro);
                var atencion = _transaccionRepositorio.ObtenerTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencion);
                if (!idsEstadosValidos.Contains((int)atencion.id_estado_actual))
                {
                    throw new LogicaException("Error al validar la atencion y su estado.");
                }
                Estado_transaccion estadoTransaccion = new Estado_transaccion(atencion.id, sesionUsuario.Empleado.Id, idNuevoEstado, fechaActual, observacion);
                atencionMacro.importe_total -= atencion.importe_total;
                var resultado = _transaccionRepositorio.CrearEstadoDeTransaccionAhoraActualizarTransaccion(estadoTransaccion, atencionMacro);
                //En el caso de que tenga facturacion invalidar o emitir nota de credito
                if (atencion.enum1 != (int)ModoFacturacionHotel.NoEspecificado)
                {
                    ResolverAnulacionOperacionAtencion(atencionMacro, atencion, comprobantes, observacion, sesionUsuario);
                }
                estadoTransaccion.Detalle_maestro = estado;
                resultado.information = new EstadoAtencionHotel()
                {
                    Estados = new List<ItemEstado>() { new ItemEstado(estadoTransaccion) },
                    Facturado = atencion.Evento_transaccion.Select(ev => ev.id_evento).Contains(MaestroSettings.Default.IdDetalleMaestroEstadoFacturado)
                };
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar anular la atencion", e);
            }
        }
        public OperationResult ResolverAnulacionOperacionAtencion(Transaccion atencionMacro, Transaccion atencion, List<ComprobanteAtencion> comprobantes, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                for (int i = 0; i < comprobantes.Count; i++)
                {
                    OperationResult resultado = atencion.enum1 == (int)ModoFacturacionHotel.Individual ? _logicaOperacion.AnularOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].DarDeBaja, observacion, 0, sesionUsuario) : (comprobantes[i].Importe == comprobantes[i].MontoSoles ? _logicaOperacion.AnularOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].DarDeBaja, observacion, 0, sesionUsuario) : _logicaOperacion.DescuentoGlobalOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].MontoSoles, observacion, 0, sesionUsuario));
                    if (resultado.code_result != OperationResultEnum.Success)
                    {
                        throw new LogicaException("Se realizó correctamente la anulación de la atención de hotel, pero hubo un error al momento de la anulación del comprobante, por favor realizarlo manualmente o llamar a soporte.");
                    }
                }
                return new OperationResult(OperationResultEnum.Success);
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al realizar la anulacion de una operacion de hotel", e);
            }
        }
        public List<ComprobanteAtencion> ObtenerComprobantesDeAtencion(long idAtencionMacro, long idAtencion)
        {
            try
            {
                List<ComprobanteAtencion> comprobantes = new List<ComprobanteAtencion>();
                var atencion = _transaccionRepositorio.ObtenerTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencion);
                if (atencion.enum1 != (int)ModoFacturacionHotel.NoEspecificado)
                {
                    var idAtencionReferenciado = atencion.enum1 == (int)ModoFacturacionHotel.Global ? idAtencionMacro : idAtencion;
                    var idsOrdenVentaPrincipal = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion(idAtencionReferenciado).Where(t => t.id_tipo_transaccion == TransaccionSettings.Default.IdTipoTransaccionOrdenDeVenta && t.id_estado_actual == MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado).Select(ov => ov.id);
                    var idsOrdenVentaSecundario = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion3DeTransaccion(idAtencionReferenciado).Where(t => t.id_tipo_transaccion == TransaccionSettings.Default.IdTipoTransaccionOrdenDeVenta && t.id_estado_actual == MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado).Select(ov => ov.id);
                    var idsOrdenVenta = idsOrdenVentaPrincipal == null ? (idsOrdenVentaSecundario ?? null) : (idsOrdenVentaSecundario == null ? idsOrdenVentaPrincipal : idsOrdenVentaPrincipal.Union(idsOrdenVentaSecundario));
                    foreach (var idOrdenVenta in idsOrdenVenta)
                    {
                        OrdenDeVenta ordenDeVenta = new OrdenDeVenta(_transaccionRepositorio.ObtenerTransaccionInclusiveActoresYDetalleMaestroYEstado(idOrdenVenta));
                        var montoDescuento = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion(idOrdenVenta).Where(t => Diccionario.TiposDeTransaccionOrdenesDeOperacionesDeVentasSoloNotasDeCreditoYDebito.Contains(t.id_tipo_transaccion)).Sum(t => t.importe_total);
                        var montoHospedaje = ordenDeVenta.Detalles().Where(d => d.Producto.IdConceptoBasico == HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion).Sum(d => d.Importe);
                        comprobantes.Add(new ComprobanteAtencion
                        {
                            IdAtencion = idAtencion,
                            IdOrdenVenta = ordenDeVenta.Id,
                            SerieYNumeroComprobante = ordenDeVenta.Comprobante().SerieYNumero(),
                            Importe = ordenDeVenta.Total,
                            MontoHospedaje = montoHospedaje,
                            Descuento = montoDescuento,
                            PuedeDarDeBaja = ordenDeVenta.FechaEmision.AddDays(FacturacionElectronicaSettings.Default.PlazoEnDiasParaInvalidarComprobanteElectronico) >= DateTimeUtil.FechaActual() && montoDescuento == 0 && ordenDeVenta.Total == montoHospedaje,
                            IdTipoComprobante = ordenDeVenta.IdTipoComprobante
                        });
                    }
                    comprobantes.RemoveAll(c => c.Diferencia == 0);
                    comprobantes.ForEach(c => c.DarDeBaja = c.PuedeDarDeBaja);
                }
                return comprobantes;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al realizar la obtencion de los comprobantes de anulacion de atencion", e);
            }
        }
        public OperationResult RegistrarIncidenteAtencion(long idAtencion, long idAtencionMacro, bool esDevolucion, List<ComprobanteAtencion> comprobantes, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn, MaestroSettings.Default.IdDetalleMaestroEstadoEntradaCambiado };
                var idNuevoEvento = MaestroSettings.Default.IdDetalleMaestroEstadoIncidente;
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var eventoIncidente = _maestroRepositorio.ObtenerDetalle(idNuevoEvento);
                var atencionMacro = _transaccionRepositorio.ObtenerTransaccion(idAtencionMacro);
                var atencion = _transaccionRepositorio.ObtenerTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencion);
                if (!idsEstadosValidos.Contains((int)atencion.id_estado_actual))
                {
                    throw new LogicaException("Error al validar la atencion y su estado.");
                }
                var estadoActualAtencionHabitacion = new EstadoAtencionHotel
                {
                    IdAuxiliar = idAtencion,
                    Estados = new List<ItemEstado>() { new ItemEstado(atencion.Estado_transaccion.Last()) }
                };
                Evento_transaccion eventoTransaccion = new Evento_transaccion(atencion.id, sesionUsuario.Empleado.Id, idNuevoEvento, fechaActual, observacion);
                var resultado = _transaccionRepositorio.CrearEventoTransaccion(eventoTransaccion);
                if (resultado.code_result == OperationResultEnum.Success)
                {
                    ResolverRegistroIncidenteAtencion((int)resultado.information, esDevolucion, comprobantes, sesionUsuario);
                }
                eventoTransaccion.Detalle_maestro = eventoIncidente;
                estadoActualAtencionHabitacion.Estados.Add(new ItemEstado(eventoTransaccion));
                resultado.information = estadoActualAtencionHabitacion;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar registrar incidente en atencion", e);
            }
        }
        public OperationResult ResolverRegistroIncidenteAtencion(int idEventoTransaccion, bool esDevolucion, List<ComprobanteAtencion> comprobantes, UserProfileSessionData sesionUsuario)
        {
            try
            {
                for (int i = 0; i < comprobantes.Count; i++)
                {
                    OperationResult resultado = esDevolucion ? ((comprobantes[i].Descuento == 0 && comprobantes[i].Importe == comprobantes[i].MontoHospedaje) ? _logicaOperacion.AnularOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].DarDeBaja, "Devolución Total", idEventoTransaccion, sesionUsuario) : _logicaOperacion.DescuentoGlobalOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].Diferencia, "Devolución Total", idEventoTransaccion, sesionUsuario)) : _logicaOperacion.DescuentoGlobalOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].MontoSoles, "Descuento", idEventoTransaccion, sesionUsuario);
                    if (resultado.code_result != OperationResultEnum.Success)
                    {
                        throw new LogicaException("Se realizó correctamente el registro de incidente, pero hubo un error al momento de dar de baja / emitir nota de credito del comprobante, por favor realizarlo manualmente o llamar a soporte.");
                    }
                }
                return new OperationResult(OperationResultEnum.Success);
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar resolver el registro de incidente en atencion", e);
            }
        }
    }
}
