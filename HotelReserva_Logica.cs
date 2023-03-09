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
        public List<ReservaBandeja> ObtenerReservaBandeja(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<ReservaBandeja> reservas = _hotelRepositorio.ObtenerReservaBandeja(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                reservas.ForEach(r => r.Responsable = r.Responsable.Replace("|", " "));
                return reservas;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener Habitaciones", e);
            }
        }
        public OperationResult ConfirmarReserva(AtencionMacroHotel reserva, UserProfileSessionData sesionUsuario)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                Transaccion atencionDeHotel = reserva.Atenciones.Count() > 1 ? GenerarTransaccionAtencionMacro(reserva, sesionUsuario, MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado, "Confirmado al guardar reserva por mostrador") : GenerarTransaccionAtencion(reserva, sesionUsuario, MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado, "Confirmado al guardar reserva por mostrador");
                if (reserva.FacturadoGlobal)
                {
                    reserva.Comprobante.TransaccionCreacion = atencionDeHotel;
                    CompletarDatosGeneralesDeVenta(reserva.Comprobante, sesionUsuario);
                    reserva.Comprobante.Orden.Detalles = GenerarDetallesDeVentaAtencion(reserva);
                    resultado = _logicaOperacion.ConfirmarVentaIntegrada(ModoOperacionEnum.PorMostrador, sesionUsuario, reserva.Comprobante);
                }
                else
                {
                    resultado = _transaccionRepositorio.CrearTransaccion(atencionDeHotel);
                }
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar guardar la reserva", e);
            }
        }
        public OperationResult CheckInReserva(AtencionMacroHotel reserva, UserProfileSessionData sesionUsuario)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                Transaccion atencionDeHotel = reserva.Atenciones.Count() > 1 ? GenerarTransaccionAtencionMacro(reserva, sesionUsuario, MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn, "Checked In al guardar reserva por mostrador") : GenerarTransaccionAtencion(reserva, sesionUsuario, MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn, "Checked In al guardar reserva por mostrador");
                if (reserva.FacturadoGlobal)
                {
                    reserva.Comprobante.TransaccionCreacion = atencionDeHotel;
                    CompletarDatosGeneralesDeVenta(reserva.Comprobante, sesionUsuario);
                    reserva.Comprobante.Orden.Detalles = GenerarDetallesDeVentaAtencion(reserva);
                    resultado = _logicaOperacion.ConfirmarVentaIntegrada(ModoOperacionEnum.PorMostrador, sesionUsuario, reserva.Comprobante);
                }
                else
                {
                    resultado = _transaccionRepositorio.CrearTransaccion(atencionDeHotel);
                }
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar guardar la reserva", e);
            }
        }
        public void CompletarDatosGeneralesDeVenta(DatosVentaIntegrada comprobante, UserProfileSessionData sesionUsuario)
        {
            ///Agregamos la transaccion origen a la transaccion 
            comprobante.Orden.PuntoDeVenta = new ItemGenerico { Id = sesionUsuario.IdCentroDeAtencionSeleccionado, Nombre = sesionUsuario.CentroDeAtencionSeleccionado.Nombre };
            comprobante.Orden.Vendedor = new ItemGenerico { Id = sesionUsuario.Empleado.Id };
            comprobante.Orden.NumeroBolsasDePlastico = 0;
            comprobante.MovimientoAlmacen = new DatosMovimientoDeAlmacen { HayComprobanteDeSalidaDeMercaderia = false };
        }
        public Transaccion GenerarTransaccionAtencionMacro(AtencionMacroHotel atencionMacro, UserProfileSessionData sesionUsuario, int idEstado, string observacionEstado)
        {
            try
            {
                var fechaActual = DateTimeUtil.FechaActual();
                var atencionDeHotel = ConvertirAtencionMacroEnTransaccion(atencionMacro, sesionUsuario, fechaActual);
                if (atencionMacro.FacturadoGlobal)
                {
                    atencionDeHotel.Evento_transaccion.Add(new Evento_transaccion(sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoFacturado, fechaActual.AddSeconds(1), "Facturado al confirmar"));
                }
                foreach (var atencion in atencionMacro.Atenciones)
                {
                    var atencionDeHabitacion = ConvertirAtencionEnTransaccion(atencionMacro, atencion, sesionUsuario, fechaActual, atencionDeHotel);
                    if (atencion.Huespedes != null)
                    {
                        foreach (var huesped in atencion.Huespedes)
                        {
                            atencionDeHabitacion.Actor_negocio_por_transaccion.Add(new Actor_negocio_por_transaccion()
                            {
                                id_actor_negocio = huesped.Id,
                                id_rol = HotelSettings.Default.IdRolHuesped,
                                id_detalle_maestro = huesped.MotivoDeViaje.Id,
                                extension_json = "{ estitular: \"" + huesped.EsTitular.ToString().ToLower() + "\" }"
                            });
                        }
                    }
                    atencionDeHabitacion.Estado_transaccion.Add(new Estado_transaccion(sesionUsuario.Empleado.Id, idEstado, fechaActual, observacionEstado));
                    atencionDeHotel.Transaccion1.Add(atencionDeHabitacion);
                }
                if (atencionMacro.HayImagenVoucherExtranet)
                {
                    atencionDeHotel.Traza_pago.Add(new Traza_pago(atencionMacro.IdMedioPagoExtranet, "Pago realizado desde la extranet", MaestroSettings.Default.IdDetalleMaestroEntidadBancariaPorDefecto)
                    {
                        extension_json = "{ voucherextranet: \"" + sesionUsuario.Sede.DocumentoIdentidad + "/" + atencionDeHotel.codigo + ".jpg\" }"
                    });
                }
                return atencionDeHotel;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar guardar la reserva", e);
            }
        }
        public Transaccion GenerarTransaccionAtencion(AtencionMacroHotel atencionMacro, UserProfileSessionData sesionUsuario, int idEstado, string observacionEstado)
        {
            try
            {
                var fechaActual = DateTimeUtil.FechaActual();
                var atencionDeHotel = ConvertirAtencionMacroEnTransaccion(atencionMacro, sesionUsuario, fechaActual);
                var atencionDeHabitacion = ConvertirAtencionEnTransaccion(atencionMacro, atencionMacro.Atenciones.First(), sesionUsuario, fechaActual, atencionDeHotel);
                if (atencionMacro.FacturadoGlobal)
                {
                    atencionDeHabitacion.Evento_transaccion.Add(new Evento_transaccion(sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoFacturado, fechaActual.AddSeconds(1), "Facturado al confirmar"));
                }
                if (atencionMacro.Atenciones.First().Huespedes != null)
                {
                    foreach (var huesped in atencionMacro.Atenciones.First().Huespedes)
                    {
                        atencionDeHabitacion.Actor_negocio_por_transaccion.Add(new Actor_negocio_por_transaccion()
                        {
                            id_actor_negocio = huesped.Id,
                            id_rol = HotelSettings.Default.IdRolHuesped,
                            id_detalle_maestro = huesped.MotivoDeViaje.Id,
                            extension_json = "{ estitular: \"" + huesped.EsTitular.ToString().ToLower() + "\" }"
                        });
                    }
                }
                atencionDeHabitacion.Estado_transaccion.Add(new Estado_transaccion(sesionUsuario.Empleado.Id, idEstado, fechaActual, observacionEstado));
                if (atencionMacro.HayImagenVoucherExtranet)
                {
                    atencionDeHotel.Traza_pago.Add(new Traza_pago(atencionMacro.IdMedioPagoExtranet, "Pago realizado desde la extranet", MaestroSettings.Default.IdDetalleMaestroEntidadBancariaPorDefecto)
                    {
                        extension_json = "{ voucherextranet: \"" + sesionUsuario.Sede.DocumentoIdentidad + "/" + atencionDeHotel.codigo + ".jpg\" }"
                    });
                }
                atencionDeHabitacion.Transaccion2 = atencionDeHotel;
                return atencionDeHabitacion;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar guardar la reserva", e);
            }
        }
        private List<DetalleDeOperacion> GenerarDetallesDeVentaAtencion(AtencionMacroHotel atencion)
        {
            try
            {
                var detallesVenta = new List<DetalleDeOperacion>();
                var idsTipoHabitacion = atencion.Atenciones.Select(a => a.Habitacion.TipoHabitacion.Id).GroupBy(i => i);
                foreach (var idTipo in idsTipoHabitacion)
                {
                    var atenciones = atencion.Atenciones.Where(a => a.Habitacion.TipoHabitacion.Id == idTipo.Key);
                    detallesVenta.Add(new DetalleDeOperacion()
                    {
                        Producto = new Concepto_Negocio_Comercial { Id = idTipo.Key },
                        Cantidad = atenciones.Sum(g => g.Noches),
                        PrecioUnitario = atenciones.First().PrecioUnitario,
                        Importe = atenciones.Sum(g => g.Importe),
                        MascaraDeCalculo = VentasSettings.Default.MascaraDeCalculoPrecioUnitarioCalculado
                    });
                }
                return detallesVenta;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar determinar los detalles de venta", e);
            }
        }
        public Transaccion ConvertirAtencionMacroEnTransaccion(AtencionMacroHotel atencionMacro, UserProfileSessionData sesionUsuario, DateTime fechaActual)
        {
            Transaccion transaccion = new Transaccion()
            {
                codigo = ObtenerSiguienteCodigoParaReserva(sesionUsuario),
                id_comprobante = TransaccionSettings.Default.IdComprobanteGenerico,
                tipo_cambio = sesionUsuario.TipoDeCambio.ValorVenta,
                importe_total = atencionMacro.Total,
                id_actor_negocio_interno = sesionUsuario.CentroDeAtencionSeleccionado.Id,
                id_actor_negocio_externo = atencionMacro.FacturadoGlobal ? atencionMacro.Comprobante.Orden.Cliente.Id : ActorSettings.Default.IdClienteGenerico,
                id_actor_negocio_externo1 = atencionMacro.Responsable.Id,
                id_moneda = MaestroSettings.Default.IdDetalleMaestroMonedaSoles,
                id_empleado = sesionUsuario.Empleado.Id,
                id = atencionMacro.Id,
                id_tipo_transaccion = HotelSettings.Default.IdTipoTransaccionAtencionDeHotel,
                comentario = "Ninguno",
                id_unidad_negocio = MaestroSettings.Default.IdDetalleMaestroUnidadDeNegocioTransversal,
                es_concreta = false,
                fecha_inicio = fechaActual,
                fecha_fin = fechaActual,
                fecha_registro_sistema = fechaActual,
                fecha_registro_contable = fechaActual,
                enum1 = atencionMacro.FacturadoGlobal ? (atencionMacro.Atenciones.Count() > 1 ? (int)ModoFacturacionHotel.Global : (int)ModoFacturacionHotel.Individual) : (int)ModoFacturacionHotel.NoEspecificado,
                indicador1 = atencionMacro.HayImagenVoucherExtranet
            };
            return transaccion;
        }
        private Transaccion ConvertirAtencionEnTransaccion(AtencionMacroHotel atencionMacro, AtencionHotel atencion, UserProfileSessionData sesionUsuario, DateTime fechaActual, Transaccion atencionHotel)
        {
            Transaccion transaccion = new Transaccion()
            {
                codigo = atencionHotel.codigo,
                id_comprobante = TransaccionSettings.Default.IdComprobanteGenerico,
                tipo_cambio = sesionUsuario.TipoDeCambio.ValorVenta,
                importe_total = atencion.Importe,
                id_actor_negocio_interno = atencion.Habitacion.Id,
                id_actor_negocio_externo = atencionMacro.FacturadoGlobal ? atencionMacro.Comprobante.Orden.Cliente.Id : ActorSettings.Default.IdClienteGenerico,
                id_actor_negocio_externo1 = atencionMacro.Responsable.Id,
                fecha_inicio = DateTime.Parse(atencion.FechaIngreso.ToString("dd/MM/yyyy")),
                fecha_fin = DateTime.Parse(atencion.FechaSalida.ToString("dd/MM/yyyy")),
                id_moneda = MaestroSettings.Default.IdDetalleMaestroMonedaSoles,
                id_empleado = sesionUsuario.Empleado.Id,
                id = atencion.Id,
                id_tipo_transaccion = HotelSettings.Default.IdTipoTransaccionAtencionDeHabitacion,
                comentario = atencion.Anotacion != null ? JsonConvert.SerializeObject(new List<Anotacion>() { new Anotacion(fechaActual.ToString("dd/MM/yyyy HH:mm tt"), atencion.Anotacion) }) : "",
                id_unidad_negocio = MaestroSettings.Default.IdDetalleMaestroUnidadDeNegocioTransversal,
                es_concreta = false,
                importe1 = atencion.PrecioUnitario,
                cantidad1 = atencion.Noches,
                fecha_registro_sistema = fechaActual,
                fecha_registro_contable = fechaActual,
                enum1 = atencionMacro.FacturadoGlobal ? (atencionMacro.Atenciones.Count() > 1 ? (int)ModoFacturacionHotel.Global : (int)ModoFacturacionHotel.Individual) : (int)ModoFacturacionHotel.NoEspecificado,
                indicador1 = atencionMacro.FacturadoGlobal,
                indicador2 = atencionMacro.FacturadoGlobal
            };
            return transaccion;
        }
        public string ObtenerSiguienteCodigoParaReserva(UserProfileSessionData sesionUsuario)
        {
            try
            {
                Serie_comprobante serie = _transaccionRepositorio.ObtenerPrimeraSerieDeComprobantePorCentroDeAtencionYComprobante(HotelSettings.Default.IdDetalleMaestroComprobanteCodigoReserva, sesionUsuario.IdCentroDeAtencionSeleccionado);
                if (serie == null)
                {
                    throw new LogicaException("No se ha encontrado una serie de comprobante para este tipo de transaccion.");
                }
                string codigoReserva = serie.proximo_numero.ToString().PadLeft(8, '0');
                _logicaOperacion.AutoIncrementarSerieMarcandolaComoModificada(serie);
                return codigoReserva;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
