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
        public AtencionMacroHotel ObtenerAtencionMacro(long idAtencionMacro, UserProfileSessionData sesionUsuario)
        {
            try
            {
                AtencionMacroHotel atencionMacro = _hotelRepositorio.ObtenerAtencionMacro(idAtencionMacro);
                atencionMacro.FacturadoGlobal = atencionMacro.TieneFacturacion ? atencionMacro.FacturadoGlobal : (atencionMacro.Atenciones.Where(a => a.Importe > 0).Count() > 1);
                atencionMacro.Atenciones.ToList().ForEach(a => a.FacturadoGlobal = atencionMacro.FacturadoGlobal);
                if (atencionMacro.FacturadoGlobal)
                    atencionMacro.Atenciones.ToList().ForEach(a => a.Estados.AddRange(atencionMacro.Eventos));
                atencionMacro.Atenciones.ToList().ForEach(a => a.Estados = a.Estados.OrderBy(e => e.Fecha).ToList());
                atencionMacro.Atenciones.SelectMany(a => a.Huespedes).ToList().ForEach(h => h.EsTitular = h.JsonHuesdep != null && JsonConvert.DeserializeObject<JsonHuesped>(h.JsonHuesdep).estitular);
                atencionMacro.Atenciones.ToList().ForEach(a => a.Anotaciones = JsonConvert.DeserializeObject<List<Anotacion>>(a.AnotacionesJson));
                if (atencionMacro.HayImagenVoucherExtranet)
                    ObtenerImagenVoucherExtranet(atencionMacro, sesionUsuario);
                return atencionMacro;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener la atencion macro de hotel", e);
            }
        }
        public OperationResult EditarResponsableAtencionMacro(long idAtencionMacro, int idResponsable)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                var atenciones = _transaccionRepositorio.ObtenerTransacciones1DeTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencionMacro);
                var idAtenciones = new List<long> { idAtencionMacro };
                idAtenciones.AddRange(atenciones.Select(a => a.id).ToList());
                resultado = _hotelRepositorio.ActualizarActorNegocioExterno1DeTransacciones(idAtenciones, idResponsable);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar anular la atencion macro", e);
            }
        }
        public OperationResult ResolverEstadosAtencionMacro(long idAtencionMacro, int idNuevoEstado, string observacion, int[] idsEstadosValidos, UserProfileSessionData sesionUsuario)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                List<Estado_transaccion> estadosTransaccion = new List<Estado_transaccion>();
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var estado = _maestroRepositorio.ObtenerDetalle(idNuevoEstado);
                var atenciones = _transaccionRepositorio.ObtenerTransacciones1DeTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencionMacro);
                var numeroAtencionesPermitidas = atenciones.Where(a => idsEstadosValidos.Contains((int)a.id_estado_actual)).Count();
                if (atenciones.Count() != numeroAtencionesPermitidas)
                {
                    throw new LogicaException("Error al validar las atenciones y sus estados.");
                }
                foreach (var atencion in atenciones)
                {
                    estadosTransaccion.Add(new Estado_transaccion(atencion.id, sesionUsuario.Empleado.Id, idNuevoEstado, fechaActual, observacion));
                }
                resultado = _transaccionRepositorio.CrearEstadosDeTransaccionesAhora(estadosTransaccion);
                var estadoTransaccion = estadosTransaccion.First();
                estadoTransaccion.Detalle_maestro = estado;
                resultado.information = new EstadoAtencionHotel()
                {
                    Estados = new List<ItemEstado>() { new ItemEstado(estadoTransaccion) },
                    Facturado = atenciones.First().Evento_transaccion.Select(ev => ev.id_evento).Contains(MaestroSettings.Default.IdDetalleMaestroEstadoFacturado)
                };
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar resolver la creacion de estados de atencion macro", e);
            }
        }
        public OperationResult ConfirmarAtencionMacro(long idAtencionMacro, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoRegistrado };
                var idNuevoEstado = MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado;
                OperationResult resultado = ResolverEstadosAtencionMacro(idAtencionMacro, idNuevoEstado, observacion, idsEstadosValidos.ToArray(), sesionUsuario);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar confirmar la atencion macro", e);
            }
        }
        public OperationResult CheckInAtencionMacro(long idAtencionMacro, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado };
                var idNuevoEstado = MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn;
                OperationResult resultado = ResolverEstadosAtencionMacro(idAtencionMacro, idNuevoEstado, observacion, idsEstadosValidos.ToArray(), sesionUsuario);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar checkin la atencion macro", e);
            }
        }
        public OperationResult CheckOutAtencionMacro(long idAtencionMacro, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var atencionesHabitacion = _transaccionRepositorio.ObtenerTransacciones1DeTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencionMacro).ToList();
                var consumosPendientesFacturacion = _transaccionRepositorio.ObtenerTransacciones11DeTransacciones(atencionesHabitacion.Select(t => t.id).ToArray()).Where(c => c.id_tipo_transaccion == TransaccionSettings.Default.IdTipoTransaccionOrdenConsumoHabitacion && c.id_estado_actual == MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado).ToList();
                if (atencionesHabitacion.Where(ah => ah.indicador1).Count() != atencionesHabitacion.Count || consumosPendientesFacturacion.Where(ah => ah.indicador1).Count() != consumosPendientesFacturacion.Count)
                {
                    throw new LogicaException("Error al intentar checkout la atencion macro, debido a que no estan todas las atenciones y/o consumos facturados");
                }
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn, MaestroSettings.Default.IdDetalleMaestroEstadoEntradaCambiado };
                var idNuevoEstado = MaestroSettings.Default.IdDetalleMaestroEstadoCheckedOut;
                OperationResult resultado = ResolverEstadosAtencionMacro(idAtencionMacro, idNuevoEstado, observacion, idsEstadosValidos.ToArray(), sesionUsuario);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar checkout la atencion macro", e);
            }
        }
        public OperationResult AnularAtencionMacro(long idAtencionMacro, List<ComprobanteAtencion> comprobantes, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoRegistrado, MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado };
                var idNuevoEstado = MaestroSettings.Default.IdDetalleMaestroEstadoAnulado;
                List<Estado_transaccion> estadosTransaccion = new List<Estado_transaccion>();
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var estado = _maestroRepositorio.ObtenerDetalle(idNuevoEstado);
                var atencionMacro = _transaccionRepositorio.ObtenerTransaccion(idAtencionMacro);
                var atenciones = _transaccionRepositorio.ObtenerTransacciones1DeTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencionMacro);
                var numeroAtencionesPermitidas = atenciones.Where(a => idsEstadosValidos.Contains((int)a.id_estado_actual)).Count();
                if (atenciones.Count() != numeroAtencionesPermitidas)
                {
                    throw new LogicaException("Error al validar las atenciones y sus estados.");
                }
                foreach (var atencion in atenciones)
                {
                    estadosTransaccion.Add(new Estado_transaccion(atencion.id, sesionUsuario.Empleado.Id, idNuevoEstado, fechaActual, observacion));
                }
                atencionMacro.importe_total = 0;
                var resultado = _transaccionRepositorio.CrearEstadosDeTransaccionesAhoraActualizarTransaccion(estadosTransaccion, atencionMacro);
                //En el caso de que tenga facturacion invalidar o emitir nota de credito
                if (atencionMacro.enum1 != (int)ModoFacturacionHotel.NoEspecificado)
                {
                    ResolverAnulacionOperacionAtencionMacro(atencionMacro, atenciones.ToList(), comprobantes, observacion, sesionUsuario);
                }
                var estadoTransaccion = estadosTransaccion.First();
                estadoTransaccion.Detalle_maestro = estado;
                resultado.information = new EstadoAtencionHotel()
                {
                    Estados = new List<ItemEstado>() { new ItemEstado(estadoTransaccion) },
                    Facturado = atenciones.First().Evento_transaccion.Select(ev => ev.id_evento).Contains(MaestroSettings.Default.IdDetalleMaestroEstadoFacturado)
                };
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar anular la atencion macro", e);
            }
        }
        public void ResolverAnulacionOperacionAtencionMacro(Transaccion atencionMacro, List<Transaccion> atenciones, List<ComprobanteAtencion> comprobantes, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                for (int i = 0; i < comprobantes.Count; i++)
                {
                    OperationResult resultado = atencionMacro.enum1 == (int)ModoFacturacionHotel.Global ? _logicaOperacion.AnularOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].DarDeBaja, observacion, 0, sesionUsuario) : (comprobantes[i].Importe == comprobantes[i].MontoSoles ? _logicaOperacion.AnularOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].DarDeBaja, observacion, 0, sesionUsuario) : _logicaOperacion.DescuentoGlobalOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].MontoSoles, observacion, 0, sesionUsuario));
                    if (resultado.code_result != OperationResultEnum.Success)
                    {
                        throw new LogicaException("Se realizó correctamente la anulación de la atención de hotel, pero hubo un error al momento de la anulación del comprobante, por favor realizarlo manualmente o llamar a soporte.");
                    }
                }
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al realizar la anulacion de una operacion de hotel", e);
            }
        }

        public List<ComprobanteAtencion> ObtenerComprobantesDeAtencionMacro(long idAtencionMacro)
        {
            try
            {
                List<ComprobanteAtencion> comprobantes = new List<ComprobanteAtencion>();
                var atencionMacro = _transaccionRepositorio.ObtenerTransaccion(idAtencionMacro);
                if (atencionMacro.enum1 != (int)ModoFacturacionHotel.NoEspecificado)
                {
                    if (atencionMacro.enum1 == (int)ModoFacturacionHotel.Global)
                    {
                        var idsOrdenVenta = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion(idAtencionMacro).Where(t => t.id_tipo_transaccion == TransaccionSettings.Default.IdTipoTransaccionOrdenDeVenta && t.id_estado_actual == MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado).Select(ov => ov.id);
                        foreach (var idOrdenVenta in idsOrdenVenta)
                        {
                            OrdenDeVenta ordenDeVenta = new OrdenDeVenta(_transaccionRepositorio.ObtenerTransaccionInclusiveActoresYDetalleMaestroYEstado(idOrdenVenta));
                            var montoDescuento = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion(idOrdenVenta).Where(t => Diccionario.TiposDeTransaccionOrdenesDeOperacionesDeVentasSoloNotasDeCreditoYDebito.Contains(t.id_tipo_transaccion)).Sum(t => t.importe_total);
                            var montoHospedaje = ordenDeVenta.Detalles().Where(d => d.Producto.IdConceptoBasico == HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion).Sum(d => d.Importe);
                            comprobantes.Add(new ComprobanteAtencion
                            {
                                IdOrdenVenta = ordenDeVenta.Id,
                                SerieYNumeroComprobante = ordenDeVenta.Comprobante().SerieYNumero(),
                                Importe = ordenDeVenta.Total,
                                MontoHospedaje = montoHospedaje,
                                Descuento = montoDescuento,
                                PuedeDarDeBaja = ordenDeVenta.FechaEmision.AddDays(FacturacionElectronicaSettings.Default.PlazoEnDiasParaInvalidarComprobanteElectronico) >= DateTimeUtil.FechaActual() && montoDescuento == 0 && ordenDeVenta.Total == montoHospedaje,
                                IdTipoComprobante = ordenDeVenta.IdTipoComprobante
                            });
                        }
                    }
                    else
                    {
                        var atenciones = _transaccionRepositorio.ObtenerTransacciones1DeTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencionMacro).ToList();
                        foreach (var atencion in atenciones)
                        {
                            var idsOrdenVenta = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion(atencion.id).Where(t => t.id_tipo_transaccion == TransaccionSettings.Default.IdTipoTransaccionOrdenDeVenta && t.id_estado_actual == MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado).Select(ov => ov.id);
                            foreach (var idOrdenVenta in idsOrdenVenta)
                            {
                                OrdenDeVenta ordenDeVenta = new OrdenDeVenta(_transaccionRepositorio.ObtenerTransaccionInclusiveActoresYDetalleMaestroYEstado(idOrdenVenta));
                                var montoDescuento = _transaccionRepositorio.ObtenerTransacciones11DeTransaccion(idOrdenVenta).Where(t => Diccionario.TiposDeTransaccionOrdenesDeOperacionesDeVentasSoloNotasDeCreditoYDebito.Contains(t.id_tipo_transaccion)).Sum(t => t.importe_total);
                                var montoHospedaje = ordenDeVenta.Detalles().Where(d => d.Producto.IdConceptoBasico == HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion).Sum(d => d.Importe);
                                comprobantes.Add(new ComprobanteAtencion
                                {
                                    IdAtencion = atencion.id,
                                    IdOrdenVenta = ordenDeVenta.Id,
                                    SerieYNumeroComprobante = ordenDeVenta.Comprobante().SerieYNumero(),
                                    Importe = ordenDeVenta.Total,
                                    MontoHospedaje = montoHospedaje,
                                    Descuento = montoDescuento,
                                    PuedeDarDeBaja = ordenDeVenta.FechaEmision.AddDays(FacturacionElectronicaSettings.Default.PlazoEnDiasParaInvalidarComprobanteElectronico) >= DateTimeUtil.FechaActual() && montoDescuento == 0 && ordenDeVenta.Total == montoHospedaje,
                                    IdTipoComprobante = ordenDeVenta.IdTipoComprobante
                                });
                            }
                        }
                    }
                    comprobantes.RemoveAll(c => c.Diferencia == 0);
                    comprobantes.ForEach(c => c.DarDeBaja = c.PuedeDarDeBaja);
                }
                return comprobantes;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al realizar la obtencion de los comprobantes de anulacion de la atencion macro", e);
            }
        }
        public OperationResult RegistrarIncidenteAtencionMacro(long idAtencionMacro, bool esDevolucion, List<ComprobanteAtencion> comprobantes, string observacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var estadosActualesAtencionHabitacion = new List<EstadoAtencionHotel>();
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn, MaestroSettings.Default.IdDetalleMaestroEstadoEntradaCambiado };
                var idNuevoEvento = MaestroSettings.Default.IdDetalleMaestroEstadoIncidente;
                var eventosTransaccion = new List<Evento_transaccion>();
                var fechaActual = DateTimeUtil.FechaActual();
                var eventoIncidente = _maestroRepositorio.ObtenerDetalle(idNuevoEvento);
                var atencionMacro = _transaccionRepositorio.ObtenerTransaccion(idAtencionMacro);
                var atenciones = _transaccionRepositorio.ObtenerTransacciones1DeTransaccionInclusiveEstadoTransaccionDetalleMaestro(idAtencionMacro);
                var numeroAtencionesPermitidas = atenciones.Where(a => idsEstadosValidos.Contains((int)a.id_estado_actual)).Count();
                if (atenciones.Count() != numeroAtencionesPermitidas)
                {
                    throw new LogicaException("Error al validar el registro de incidentes de atencion macro.");
                }
                var eventoTransaccion = new Evento_transaccion(atencionMacro.id, sesionUsuario.Empleado.Id, idNuevoEvento, fechaActual, observacion);
                var resultado = _transaccionRepositorio.CrearEventoTransaccion(eventoTransaccion);
                foreach (var atencion in atenciones)
                {
                    var estadoActualAtencionHabitacion = new EstadoAtencionHotel { IdAuxiliar = atencion.id, Estados = new List<ItemEstado> { new ItemEstado(atencion.Estado_transaccion.Last()) } };
                    estadoActualAtencionHabitacion.Estados.Add(new ItemEstado(new Evento_transaccion(atencion.id, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoIncidente, fechaActual, observacion) { Detalle_maestro = eventoIncidente }));
                    estadosActualesAtencionHabitacion.Add(estadoActualAtencionHabitacion);
                }
                if (resultado.code_result == OperationResultEnum.Success)
                {
                    ResolverRegistroIncidenteAtencionMacro((int)resultado.information, esDevolucion, comprobantes, sesionUsuario);
                }
                resultado.information = estadosActualesAtencionHabitacion;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar registrar incidente en la atencion macro", e);
            }
        }
        public void ResolverRegistroIncidenteAtencionMacro(int idEventoTransaccion, bool esDevolucion, List<ComprobanteAtencion> comprobantes, UserProfileSessionData sesionUsuario)
        {
            try
            {
                for (int i = 0; i < comprobantes.Count; i++)
                {
                    var resultado = esDevolucion ? (comprobantes[i].Descuento == 0 ? _logicaOperacion.AnularOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].DarDeBaja, "Devolución Total", idEventoTransaccion, sesionUsuario) : _logicaOperacion.DescuentoGlobalOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].Diferencia, "Devolución Total", idEventoTransaccion, sesionUsuario)) : _logicaOperacion.DescuentoGlobalOperacionVenta(comprobantes[i].IdOrdenVenta, comprobantes[i].MontoSoles, "Descuento", idEventoTransaccion, sesionUsuario);
                    if (resultado.code_result != OperationResultEnum.Success)
                    {
                        throw new LogicaException("Se realizó correctamente el registro de incidente, pero hubo un error al momento de dar de baja / emitir nota de credito del comprobante, por favor realizarlo manualmente o llamar a soporte.");
                    }
                }
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar resolver el registro de incidente en atencion macro", e);
            }
        }
    }
}
