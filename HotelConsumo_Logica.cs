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
        public List<Consumo> ObtenerConsumos(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<Consumo> consumos = _hotelRepositorio.ObtenerConsumos(idEstablecimiento, fechaDesde, fechaHasta).ToList().OrderBy(c => c.Fecha).ToList();
                return consumos;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener Habitaciones", e);
            }
        }
        public ConsumoHabitacion ObtenerConsumoHabitacion(int idAtencion)
        {
            try
            {
                ConsumoHabitacion consumoHabitacion = _hotelRepositorio.ObtenerConsumoHabitacion(idAtencion);
                consumoHabitacion.Huespedes.ForEach(h => h.Valor = (h.Valor != null && JsonConvert.DeserializeObject<JsonHuesped>(h.Valor).estitular).ToString().ToLower());
                consumoHabitacion.Titular = consumoHabitacion.Huespedes.Where(h => h.Valor == "true").FirstOrDefault().Nombre;
                return consumoHabitacion;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener Habitaciones", e);
            }
        }
        public OperationResult ConfirmarConsumo(ConsumoHabitacion consumoHabitacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var atencionHabitacion = _transaccionRepositorio.ObtenerTransaccion(consumoHabitacion.IdAtencion);
                atencionHabitacion.indicador1 = false;
                ResolverDetalles(consumoHabitacion.Detalles);
                Transaccion consumoTransaccion = ConvertirConsumoTransaccion(consumoHabitacion, sesionUsuario, fechaActual, TransaccionSettings.Default.IdTipoTransaccionConsumoHabitacion, _codigosOperacion_Logica.ObtenerSiguienteCodigoParaFacturacion(Diccionario.MapeoTipoTransaccionVsCodigoDeOperacion.Single(n => n.Key == TransaccionSettings.Default.IdTipoTransaccionConsumoHabitacion).Value, TransaccionSettings.Default.IdTipoTransaccionConsumoHabitacion));
                Transaccion ordenConsumoTransaccion = ConvertirConsumoTransaccion(consumoHabitacion, sesionUsuario, fechaActual, TransaccionSettings.Default.IdTipoTransaccionOrdenConsumoHabitacion, _codigosOperacion_Logica.ObtenerSiguienteCodigoParaFacturacion(Diccionario.MapeoTipoTransaccionVsCodigoDeOperacion.Single(n => n.Key == TransaccionSettings.Default.IdTipoTransaccionOrdenConsumoHabitacion).Value, TransaccionSettings.Default.IdTipoTransaccionOrdenConsumoHabitacion));
                ordenConsumoTransaccion.id_transaccion_referencia = consumoHabitacion.IdAtencion;
                ordenConsumoTransaccion.AgregarDetalles(DetalleDeOperacion.Convert(consumoHabitacion.Detalles));
                ordenConsumoTransaccion.Estado_transaccion.Add(new Estado_transaccion(sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado, fechaActual, "Estado agregado al confimar un consumo de habitacion"));
                bool haySalidaBienes = consumoHabitacion.Detalles.Where(d => d.Producto.EsBien).Count() > 0;
                Transaccion salidaMercaderiaConsumoTransaccion =  new Transaccion();
                if (haySalidaBienes)
                {
                    salidaMercaderiaConsumoTransaccion = ConvertirConsumoTransaccion(consumoHabitacion, sesionUsuario, fechaActual, TransaccionSettings.Default.IdTipoTransaccionSalidaMercaderiaConsumoHabitacion, _codigosOperacion_Logica.ObtenerSiguienteCodigoParaFacturacion(Diccionario.MapeoTipoTransaccionVsCodigoDeOperacion.Single(n => n.Key == TransaccionSettings.Default.IdTipoTransaccionSalidaMercaderiaConsumoHabitacion).Value, TransaccionSettings.Default.IdTipoTransaccionSalidaMercaderiaConsumoHabitacion));
                    salidaMercaderiaConsumoTransaccion.AgregarDetalles(DetalleDeOperacion.Convert(consumoHabitacion.Detalles.Where(d => d.Producto.EsBien).ToList()));
                }

                var operacionIntegrada = new OperacionIntegrada(consumoTransaccion, ordenConsumoTransaccion, null, haySalidaBienes ? new List<Transaccion> { salidaMercaderiaConsumoTransaccion } : new List<Transaccion>(), atencionHabitacion, null);
                operacionIntegrada.EnlazarTransacciones();

                OperationResult resultado = _logicaOperacion.AfectarInventarioFisicoYGuardarOperacion(operacionIntegrada, sesionUsuario);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar confirmar el consumo", e);
            }
        }
        public void ResolverDetalles(List<DetalleDeOperacion> detalles)
        {
            //Validar los montos de los detalles y calcular el igv de los detalles de la venta
            foreach (var item in detalles)
            {
                if (item.Cantidad <= 0)
                {
                    throw new LogicaException("No es posible realizar una venta con cantidad 0 en alguno de sus detalles");
                }
                if (item.Importe < 0)
                {
                    throw new LogicaException("El total del detalle debe ser mayor o igual a cero");
                }
                if (VerificarCalculadoMascaraDeCalculo(item.MascaraDeCalculo, ElementoDeCalculoEnVentasEnum.Cantidad))
                {
                    item.Cantidad = item.Importe / item.PrecioUnitario;
                }
                if (VerificarCalculadoMascaraDeCalculo(item.MascaraDeCalculo, ElementoDeCalculoEnVentasEnum.PrecioUnitario))
                {
                    item.PrecioUnitario = item.Importe / item.Cantidad;
                }
                if (VerificarCalculadoMascaraDeCalculo(item.MascaraDeCalculo, ElementoDeCalculoEnVentasEnum.Importe))
                {
                    item.Importe = item.Cantidad * item.PrecioUnitario;
                }
            }
        }
        private bool VerificarCalculadoMascaraDeCalculo(string mascaraDeCalculo, ElementoDeCalculoEnVentasEnum orden)
        {
            List<int> mascaraDeCalculoArray = mascaraDeCalculo.Select(digito => int.Parse(digito.ToString())).ToList();
            //Retornamos si el valor de mascara es igual a 1
            return !Convert.ToBoolean(mascaraDeCalculoArray[(int)orden]);
        }
        public Transaccion ConvertirConsumoTransaccion(ConsumoHabitacion consumoHabitacion, UserProfileSessionData sesionUsuario, DateTime fechaActual, int idTipoTransaccion, string codigo)
        {
            Transaccion transaccion = new Transaccion()
            {
                codigo = codigo,
                id_comprobante = TransaccionSettings.Default.IdComprobanteGenerico,
                tipo_cambio = sesionUsuario.TipoDeCambio.ValorVenta,
                importe_total = consumoHabitacion.Importe,
                id_actor_negocio_interno = sesionUsuario.CentroDeAtencionSeleccionado.Id,
                id_actor_negocio_externo = consumoHabitacion.HuespedConsumo.Id,
                id_actor_negocio_interno1 = consumoHabitacion.IdHabitacion,
                id_moneda = MaestroSettings.Default.IdDetalleMaestroMonedaSoles,
                id_empleado = sesionUsuario.Empleado.Id,
                id_tipo_transaccion = idTipoTransaccion,
                comentario = "Ninguno",
                id_unidad_negocio = MaestroSettings.Default.IdDetalleMaestroUnidadDeNegocioTransversal,
                es_concreta = true,
                fecha_inicio = fechaActual,
                fecha_fin = fechaActual,
                fecha_registro_sistema = fechaActual,
                fecha_registro_contable = fechaActual,
            };
            return transaccion;
        }
        public OperationResult InvalidarConsumo(long idConsumo, UserProfileSessionData sesionUsuario)
        {
            try
            {
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var transaccionOrdenConsumo = _transaccionRepositorio.ObtenerTransaccion(idConsumo);

                Transaccion invalidacionConsumoTransaccion = ConvertirTransaccionInvalidacionConsumo(transaccionOrdenConsumo, sesionUsuario, fechaActual, TransaccionSettings.Default.IdTipoTransaccionInvalidacionConsumoHabitacion, _codigosOperacion_Logica.ObtenerSiguienteCodigoParaFacturacion(Diccionario.MapeoTipoTransaccionVsCodigoDeOperacion.Single(n => n.Key == TransaccionSettings.Default.IdTipoTransaccionInvalidacionConsumoHabitacion).Value, TransaccionSettings.Default.IdTipoTransaccionInvalidacionConsumoHabitacion));
                invalidacionConsumoTransaccion.id_transaccion_referencia = transaccionOrdenConsumo.id_transaccion_padre;
                Transaccion ordenInvalidacionConsumoTransaccion = ConvertirTransaccionInvalidacionConsumo(transaccionOrdenConsumo, sesionUsuario, fechaActual, TransaccionSettings.Default.IdTipoTransaccionOrdenInvalidacionConsumoHabitacion, _codigosOperacion_Logica.ObtenerSiguienteCodigoParaFacturacion(Diccionario.MapeoTipoTransaccionVsCodigoDeOperacion.Single(n => n.Key == TransaccionSettings.Default.IdTipoTransaccionOrdenInvalidacionConsumoHabitacion).Value, TransaccionSettings.Default.IdTipoTransaccionOrdenInvalidacionConsumoHabitacion));
                ordenInvalidacionConsumoTransaccion.AgregarDetalles(ConvertirDetallesTransaccionInvalidacionConsumo(transaccionOrdenConsumo));
                invalidacionConsumoTransaccion.id_transaccion_referencia = transaccionOrdenConsumo.id;
                ordenInvalidacionConsumoTransaccion.Estado_transaccion.Add(new Estado_transaccion(sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado, fechaActual, "Estado inicial asignado al confirmar la invalidacion"));
                Transaccion entradaMercaderiaInvalidacionConsumoTransaccion = ConvertirTransaccionInvalidacionConsumo(transaccionOrdenConsumo, sesionUsuario, fechaActual, TransaccionSettings.Default.IdTipoTransaccionEntradaMercaderiaInvalidacionConsumoHabitacion, _codigosOperacion_Logica.ObtenerSiguienteCodigoParaFacturacion(Diccionario.MapeoTipoTransaccionVsCodigoDeOperacion.Single(n => n.Key == TransaccionSettings.Default.IdTipoTransaccionEntradaMercaderiaInvalidacionConsumoHabitacion).Value, TransaccionSettings.Default.IdTipoTransaccionEntradaMercaderiaInvalidacionConsumoHabitacion));
                entradaMercaderiaInvalidacionConsumoTransaccion.AgregarDetalles(ConvertirDetallesTransaccionInvalidacionConsumo(transaccionOrdenConsumo));

                Estado_transaccion estadoDeOrdenDeConsumo = new Estado_transaccion(idConsumo, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoInvalidado, fechaActual,
                    "Estado que se agrega al invalidar el consumo");

                var operacionModificatoria = new OperacionModificatoria() { Operacion = invalidacionConsumoTransaccion, OrdenDeOperacion = ordenInvalidacionConsumoTransaccion, MovimientoEconomico = null, MovimientosBienes = new List<Transaccion> { entradaMercaderiaInvalidacionConsumoTransaccion }, NuevosEstadosTransaccionesModificadas = new List<Estado_transaccion>() { estadoDeOrdenDeConsumo }, NuevosEstadosParaCuotasTransaccionesModificadas = null, TransaccionesModificadas = null };
                operacionModificatoria.EnlazarTransacciones();
                OperationResult resultado = _logicaOperacion.AfectarInventarioFisicoYGuardarOperacion(operacionModificatoria, sesionUsuario);
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar invalidar el consumo", e);
            }
        }
        public Transaccion ConvertirTransaccionInvalidacionConsumo(Transaccion ordenConsumo, UserProfileSessionData sesionUsuario, DateTime fechaActual, int idTipoTransaccion, string codigo)
        {
            Transaccion transaccion = new Transaccion()
            {
                codigo = codigo,
                id_comprobante = TransaccionSettings.Default.IdComprobanteGenerico,
                tipo_cambio = sesionUsuario.TipoDeCambio.ValorVenta,
                importe_total = ordenConsumo.importe_total,
                id_actor_negocio_interno = sesionUsuario.CentroDeAtencionSeleccionado.Id,
                id_actor_negocio_externo = ordenConsumo.id_actor_negocio_externo,
                id_actor_negocio_interno1 = ordenConsumo.id_actor_negocio_interno1,
                id_moneda = MaestroSettings.Default.IdDetalleMaestroMonedaSoles,
                id_empleado = sesionUsuario.Empleado.Id,
                id_tipo_transaccion = idTipoTransaccion,
                comentario = "Ninguno",
                id_unidad_negocio = MaestroSettings.Default.IdDetalleMaestroUnidadDeNegocioTransversal,
                es_concreta = true,
                fecha_inicio = fechaActual,
                fecha_fin = fechaActual,
                fecha_registro_sistema = fechaActual,
                fecha_registro_contable = fechaActual,
            };
            return transaccion;
        }
        public List<Detalle_transaccion> ConvertirDetallesTransaccionInvalidacionConsumo(Transaccion ordenConsumo)
        {
            List<Detalle_transaccion> detallesTransaccion = new List<Detalle_transaccion>();
            foreach (var detalle in ordenConsumo.Detalle_transaccion)
            {
                detallesTransaccion.Add(detalle.Clone());
            }
            return detallesTransaccion;
        }
        public List<ItemGenerico> ObtenerAtencionesEnCheckedInComoHabitaciones(int idEstablecimiento)
        {
            try
            {
                List<ItemGenerico> habitaciones = _hotelRepositorio.ObtenerAtencionesEnCheckedInComoHabitaciones(idEstablecimiento).ToList();
                return habitaciones;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener atenciones como habitaciones", e);
            }
        }
    }
}
