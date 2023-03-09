using System;
using System.Collections.Generic;
using System.Linq;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Facturacion;
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
        public Atencion ObtenerAtencionDesdeAtencionMacro(long idAtencionMacro)
        {
            try
            {
                Atencion atencion = _hotelRepositorio.ObtenerAtencionDesdeAtencionMacro(idAtencionMacro);
                atencion.Ordenes.ToList().ForEach(or => or.Codigo = or.Codigo.Replace("|", " ") + " - " + or.FechaHoraRegistroString);
                atencion.Ordenes = atencion.Ordenes.OrderBy(o => o.TieneFacturacion);
                atencion.OrdenPrincipal.Detalles = atencion.OrdenPrincipal.Detalles.Where(d => d.Importe > 0);
                var detallesOrdenPrincipal = new List<DetalleOrdenAtencion>();
                foreach (var detalle in atencion.OrdenPrincipal.Detalles)
                {
                    var cantidadDetalle = detalle.Cantidad;
                    for (int i = 1; i <= cantidadDetalle; i++)
                    {
                        detalle.Cantidad = 1;
                        detalle.Importe = detalle.PrecioUnitario;
                        detallesOrdenPrincipal.Add(detalle);
                    }
                }
                atencion.OrdenPrincipal.Detalles = detallesOrdenPrincipal;
                foreach (var orden in atencion.Ordenes.ToList())
                {
                    var detallesOrden = new List<DetalleOrdenAtencion>();
                    foreach (var detalle in orden.Detalles)
                    {
                        var cantidadDetalle = detalle.Cantidad;
                        for (int i = 1; i <= cantidadDetalle; i++)
                        {
                            detalle.Cantidad = 1;
                            detalle.Importe = detalle.PrecioUnitario;
                            detallesOrden.Add(detalle);
                        }
                    }
                    orden.Detalles = detallesOrden;
                }
                return atencion;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener la atencion desde una atencion macro", e);
            }
        }
        public Atencion ObtenerAtencionDesdeAtencion(long idAtencion)
        {
            try
            {
                Atencion atencion = _hotelRepositorio.ObtenerAtencionDesdeAtencion(idAtencion);
                atencion.Ordenes.ToList().ForEach(or => or.Codigo = or.Codigo.Replace("|", " ") + " - " + or.FechaHoraRegistroString);
                atencion.Ordenes = atencion.Ordenes.OrderBy(o => o.TieneFacturacion);
                atencion.OrdenPrincipal.Detalles = atencion.OrdenPrincipal.Detalles.Where(d => d.Importe > 0);
                atencion.OrdenPrincipal.Importe = atencion.OrdenPrincipal.Importe - atencion.OrdenPrincipal.Detalles.Where(d => d.EstaAnulado).Sum(d => d.Importe);
                var detallesOrdenPrincipal = new List<DetalleOrdenAtencion>();
                foreach (var detalle in atencion.OrdenPrincipal.Detalles)
                {
                    var cantidadDetalle = detalle.Cantidad;
                    for (int i = 1; i <= cantidadDetalle; i++)
                    {
                        detalle.Cantidad = 1;
                        detalle.Importe = detalle.PrecioUnitario;
                        detallesOrdenPrincipal.Add(detalle);
                    }
                }
                atencion.OrdenPrincipal.Detalles = detallesOrdenPrincipal;
                foreach (var orden in atencion.Ordenes.ToList())
                {
                    var detallesOrden = new List<DetalleOrdenAtencion>();
                    foreach (var detalle in orden.Detalles)
                    {
                        var cantidadDetalle = detalle.Cantidad;
                        for (int i = 1; i <= cantidadDetalle; i++)
                        {
                            detalle.Cantidad = 1;
                            detalle.Importe = detalle.PrecioUnitario;
                            detallesOrden.Add(detalle);
                        }
                    }
                    orden.Detalles = detallesOrden;
                }
                return atencion;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar facturar la atencion", e);
            }
        }
        public OperationResult FacturarAtencionMacro(Atencion atencion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                var resultado = new OperationResult();
                var estadosActualesAtencionHabitacion = new List<EstadoAtencionHotel>();
                var fechaActual = DateTimeUtil.FechaActual();
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado, MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn, MaestroSettings.Default.IdDetalleMaestroEstadoSalidaCambiado, MaestroSettings.Default.IdDetalleMaestroEstadoEntradaCambiado };
                var atencionHotel = _transaccionRepositorio.ObtenerTransaccion(atencion.Id);
                var atencionesHabitacion = _transaccionRepositorio.ObtenerTransacciones1DeTransaccionInclusiveEstadoTransaccionDetalleMaestro(atencion.Id).ToList();
                var consumosPendientesFacturacion = _transaccionRepositorio.ObtenerTransacciones(atencion.Ordenes.Where(o => o.Id != atencion.OrdenPrincipal.Id && !o.TieneFacturacion).Select(o => o.Id).ToArray()).ToList();
                var numeroAtencionesPermitidas = atencionesHabitacion.Where(a => idsEstadosValidos.Contains((int)a.id_estado_actual)).Count();
                if (atencionesHabitacion.Count() != numeroAtencionesPermitidas)
                {
                    throw new LogicaException("Error al validar las atenciones y sus estados.");
                }
                if (atencionHotel.enum1 == (int)ModoFacturacionHotel.NoEspecificado)
                {
                    var eventoTransaccion = new Evento_transaccion(atencionHotel.id, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoFacturado, fechaActual, "NINGUNO");
                    atencionHotel.Evento_transaccion.Add(eventoTransaccion);
                    atencionHotel.enum1 = (int)ModoFacturacionHotel.Global;
                }
                var eventoFacturado = _maestroRepositorio.ObtenerDetalle(MaestroSettings.Default.IdDetalleMaestroEstadoFacturado);
                foreach (var atencionHabitacion in atencionesHabitacion)
                {
                    if (atencionHabitacion.importe_total > 0)
                    {
                        if (!atencion.TieneFacturacion)
                        {
                            var estadoActualAtencionHabitacion = new EstadoAtencionHotel { IdAuxiliar = atencionHabitacion.id, Estados = new List<ItemEstado> { new ItemEstado(atencionHabitacion.Estado_transaccion.Last()) } };
                            atencionHabitacion.enum1 = (int)ModoFacturacionHotel.Global;
                            estadoActualAtencionHabitacion.Estados.Add(new ItemEstado(new Evento_transaccion(atencionHabitacion.id, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoFacturado, fechaActual, "NINGUNO") { Detalle_maestro = eventoFacturado }));
                            estadosActualesAtencionHabitacion.Add(estadoActualAtencionHabitacion);
                        }
                        foreach (var consumoHabitacion in consumosPendientesFacturacion.Where(ch => ch.id_transaccion_referencia == atencionHabitacion.id))
                        {
                            consumoHabitacion.Evento_transaccion.Add(new Evento_transaccion(consumoHabitacion.id, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoFacturado, fechaActual, "NINGUNO"));
                            consumoHabitacion.indicador1 = true;
                            atencionHabitacion.Transaccion11.Add(consumoHabitacion);
                        }
                        atencionHabitacion.indicador1 = true;
                        atencionHabitacion.indicador2 = true;
                        atencionHotel.Transaccion1.Add(atencionHabitacion);
                    }
                }
                if (!atencion.TieneFacturacion)
                    if (atencion.Ordenes == null) atencion.Ordenes = new List<OrdenAtencion>() { atencion.OrdenPrincipal };
                    else atencion.Ordenes.ToList().Add(atencion.OrdenPrincipal);
                switch (atencion.TipoDePago)
                {
                    case (int)TipoPagoAtencion.General:
                        resultado = ResolverFacturacionTipoPagoGeneral(atencion, sesionUsuario, atencionHotel); break;
                    case (int)TipoPagoAtencion.Diferenciado:
                        resultado = ResolverFacturacionTipoPagoDiferenciado(atencion, sesionUsuario, atencionHotel); break;
                }
                estadosActualesAtencionHabitacion.ForEach(e => e.Facturado = true);
                estadosActualesAtencionHabitacion.ForEach(e => e.FacturadoGlobal = true);
                estadosActualesAtencionHabitacion.ForEach(e => e.TieneFacturacion = true);
                resultado.information = estadosActualesAtencionHabitacion;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar facturar la atencion macro", e);
            }
        }
        public OperationResult FacturarAtencion(Atencion atencion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var idsEstadosValidos = new List<int>() { MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado, MaestroSettings.Default.IdDetalleMaestroEstadoCheckedIn, MaestroSettings.Default.IdDetalleMaestroEstadoSalidaCambiado, MaestroSettings.Default.IdDetalleMaestroEstadoEntradaCambiado };
                var atencionHotel = _transaccionRepositorio.ObtenerTransaccionPadre(atencion.Id);
                var atencionHabitacion = _transaccionRepositorio.ObtenerTransaccionInclusiveEstadoTransaccionDetalleMaestro(atencion.Id);
                var consumosPendientesFacturacion = _transaccionRepositorio.ObtenerTransacciones(atencion.Ordenes.Where(o => o.Id != atencion.OrdenPrincipal.Id && !o.TieneFacturacion).Select(o => o.Id).ToArray()).ToList();
                if (!idsEstadosValidos.Contains((int)atencionHabitacion.id_estado_actual))
                {
                    throw new LogicaException("Error al validar la atencion y su estado.");
                }
                var estadoActualAtencionHabitacion = new EstadoAtencionHotel();
                var eventoFacturado = _maestroRepositorio.ObtenerDetalle(MaestroSettings.Default.IdDetalleMaestroEstadoFacturado);
                if (!atencion.TieneFacturacion)
                {
                    estadoActualAtencionHabitacion.IdAuxiliar = atencionHabitacion.id;
                    estadoActualAtencionHabitacion.Estados = new List<ItemEstado>() { new ItemEstado(atencionHabitacion.Estado_transaccion.Last()) };
                    var eventoTransaccion = new Evento_transaccion(atencionHabitacion.id, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoFacturado, fechaActual, "NINGUNO");
                    atencionHabitacion.Evento_transaccion.Add(eventoTransaccion);
                    atencionHabitacion.enum1 = (int)ModoFacturacionHotel.Individual;
                    atencionHotel.enum1 = (int)ModoFacturacionHotel.Individual;
                    estadoActualAtencionHabitacion.Estados.Add(new ItemEstado(new Evento_transaccion(atencionHabitacion.id, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoFacturado, fechaActual, "NINGUNO") { Detalle_maestro = eventoFacturado }));
                }
                foreach (var consumoHabitacion in consumosPendientesFacturacion.Where(ch => ch.id_transaccion_referencia == atencionHabitacion.id))
                {
                    consumoHabitacion.Evento_transaccion.Add(new Evento_transaccion(consumoHabitacion.id, sesionUsuario.Empleado.Id, MaestroSettings.Default.IdDetalleMaestroEstadoFacturado, fechaActual, "NINGUNO"));
                    consumoHabitacion.indicador1 = true;
                    atencionHabitacion.Transaccion11.Add(consumoHabitacion);
                }
                atencionHabitacion.indicador1 = true;
                atencionHabitacion.indicador2 = true;
                atencionHabitacion.Transaccion2 = atencionHotel;
                if (!atencion.TieneFacturacion)
                    if (atencion.Ordenes == null) atencion.Ordenes = new List<OrdenAtencion>() { atencion.OrdenPrincipal };
                    else atencion.Ordenes.ToList().Add(atencion.OrdenPrincipal);
                switch (atencion.TipoDePago)
                {
                    case (int)TipoPagoAtencion.General:
                        resultado = ResolverFacturacionTipoPagoGeneral(atencion, sesionUsuario, atencionHabitacion); break;
                    case (int)TipoPagoAtencion.Diferenciado:
                        resultado = ResolverFacturacionTipoPagoDiferenciado(atencion, sesionUsuario, atencionHabitacion); break;
                }
                estadoActualAtencionHabitacion.Facturado = true;
                estadoActualAtencionHabitacion.FacturadoGlobal = false;
                estadoActualAtencionHabitacion.TieneFacturacion = true;
                resultado.information = estadoActualAtencionHabitacion;
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar facturar la atencion", e);
            }
        }
        public OperationResult ResolverFacturacionTipoPagoGeneral(Atencion atencion, UserProfileSessionData sesionUsuario, Transaccion transaccionAtencion)
        {
            if (atencion.Importe != atencion.NuevosComprobantes.Sum(c => c.Orden.Total))
                throw new LogicaException("No se puede registrar los comprobantes debido a que el total de la atencion es diferente a la suma del importe de los comprobantes");
            var datosVentas = atencion.NuevosComprobantes;
            if (HotelSettings.Default.MostrarInformacionHabitacionComprobante)
            {
                if (datosVentas.Count == 1)
                    datosVentas.First().Orden.Informacion = ObtenerInformacionAtencion(atencion);
                else
                    datosVentas.ForEach(v => v.Orden.Informacion = ObtenerInformacionAtencionVariosComprobantes(atencion));
            }
            if (datosVentas.Count > 1) datosVentas.ForEach(v => v.Orden.UnificarDetalles = true);
            datosVentas.ForEach(v => v.TransaccionOrigen = transaccionAtencion);
            datosVentas.ForEach(v => CompletarDatosGeneralesDeVenta(v, sesionUsuario));
            datosVentas.ForEach(v => v.Orden.Detalles = GenerarDetallesDeOperacion(atencion, v.Orden.Total));
            RecalcularCantidadEImporteTipoPagoDivididoPorMonto(atencion, datosVentas);
            OperationResult resultado = _logicaOperacion.ConfirmarVentasIntegradas(ModoOperacionEnum.PorMostrador, sesionUsuario, datosVentas);
            return resultado;
        }

        private string ObtenerInformacionAtencion(Atencion atencion)
        {
            var informacion = "";
            var detallesDeOrden = atencion.Ordenes.Where(o => !o.TieneFacturacion).SelectMany(o => o.Detalles).Where(d => d.IdFamilia == HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion).ToList();
            if (detallesDeOrden.Count() > 0)
            {
                var idAtencionDistintos = detallesDeOrden.Select(d => d.Id).Distinct();
                foreach (var idAtencion in idAtencionDistintos)
                {
                    var detallesAtencion = detallesDeOrden.Where(h => h.Id == idAtencion).ToList();
                    var found = detallesAtencion.First().NombreConcepto.LastIndexOf(" - ");
                    informacion += detallesAtencion.Count().ToString() + "N-" + detallesAtencion.First().NombreConcepto.Substring(found + 3) + ", ";
                }
                informacion = informacion.Substring(0, informacion.Length - 2);
            }
            return informacion;
        }
        private string ObtenerInformacionAtencionVariosComprobantes(Atencion atencion)
        {
            var informacion = "";
            var homogeneidad = true;
            var detallesDeOrden = atencion.Ordenes.Where(o => !o.TieneFacturacion).SelectMany(o => o.Detalles).Where(d => d.IdFamilia == HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion).ToList();
            if (detallesDeOrden.Count() > 0)
            {
                var detallesAgrupados = detallesDeOrden.GroupBy(d => d.Id);
                var numeroNoches = detallesAgrupados.First().Count();
                foreach (var detalles in detallesAgrupados)
                {
                    homogeneidad = homogeneidad && numeroNoches == detalles.Count();
                }
                informacion = homogeneidad ? numeroNoches.ToString() + (numeroNoches == 1 ? " Noche" : " Noches") : "";
            }
            return informacion;
        }

        private List<DetalleDeOperacion> GenerarDetallesDeOperacion(Atencion atencion, decimal importeParcial)
        {
            try
            {
                var detallesOperacion = new List<DetalleDeOperacion>();
                var detallesVentaAgrupados = GenerarDetallesDeOperacion(atencion);
                var porcentageDelPago = importeParcial / atencion.Importe;
                foreach (var detalle in detallesVentaAgrupados)
                {
                    detallesOperacion.Add(new DetalleDeOperacion()
                    {
                        Producto = new Concepto_Negocio_Comercial { Id = detalle.Producto.Id },
                        Cantidad = detalle.Cantidad * porcentageDelPago,
                        PrecioUnitario = detalle.PrecioUnitario,
                        Importe = Math.Round(detalle.Importe * porcentageDelPago, 2),
                        MascaraDeCalculo = VentasSettings.Default.MascaraDeCalculoDeNingunValorCalculado
                    });
                }
                return detallesOperacion;
            }
            catch (Exception e)
            {
                throw new LogicaException("ERROR al intentar determinar los detalles de venta", e);
            }
        }
        private List<DetalleDeOperacion> GenerarDetallesDeOperacion(Atencion atencion)
        {
            try
            {
                var detallesOperacion = new List<DetalleDeOperacion>();
                var detallesAgrupados = atencion.Ordenes.Where(o => !o.TieneFacturacion).SelectMany(o => o.Detalles).GroupBy(d => new { d.IdConcepto, d.PrecioUnitario });
                foreach (var grupo in detallesAgrupados)
                {
                    detallesOperacion.Add(new DetalleDeOperacion()
                    {
                        Producto = new Concepto_Negocio_Comercial { Id = grupo.Key.IdConcepto },
                        Cantidad = grupo.Sum(g => g.Cantidad),
                        PrecioUnitario = grupo.Key.PrecioUnitario,
                        Importe = grupo.Sum(g => g.Importe),
                        MascaraDeCalculo = VentasSettings.Default.MascaraDeCalculoCantidadCalculada
                    });
                }
                return detallesOperacion;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar establecer los detalles de operacion", e);
            }
        }
        public OperationResult ResolverFacturacionTipoPagoDiferenciado(Atencion atencion, UserProfileSessionData sesionUsuario, Transaccion transaccionAtencion)
        {
            var datosVentas = atencion.NuevosComprobantes;
            var detallesDeOrden = atencion.Ordenes.Where(o => !o.TieneFacturacion).SelectMany(o => o.Detalles).ToList();
            foreach (var datoVenta in datosVentas)
            {
                datoVenta.Orden.Detalles = ConvertirDetalleOrdenADetalleOperacion(detallesDeOrden, datoVenta.Orden.Detalles.Select(d => d.Id).ToList());
            }
            if (HotelSettings.Default.MostrarInformacionHabitacionComprobante)
            {
                datosVentas.ForEach(v => v.Orden.Informacion = ObtenerInformacionAtencionDiferenciado(atencion, v));
            }
            datosVentas.ForEach(v => v.TransaccionOrigen = transaccionAtencion);
            datosVentas.ForEach(v => CompletarDatosGeneralesDeVenta(v, sesionUsuario));
            datosVentas.ForEach(v => AgruparDetallesDeVenta(v));
            var resultado = _logicaOperacion.ConfirmarVentasIntegradas(ModoOperacionEnum.PorMostrador, sesionUsuario, datosVentas);
            return resultado;
        }
        private string ObtenerInformacionAtencionDiferenciado(Atencion atencion, DatosVentaIntegrada venta)
        {
            var informacion = "";
            List<DetalleOrdenAtencion> todosDetallesAtencion = new List<DetalleOrdenAtencion>();
            var detallesPorFacturar = atencion.Ordenes.Where(o => !o.TieneFacturacion).SelectMany(o => o.Detalles).ToList();
            foreach (var detalle in venta.Orden.Detalles)
            {
                todosDetallesAtencion.Add(detallesPorFacturar.First(d => d.Id == detalle.Id));
            }
            var detallesDeOrden = todosDetallesAtencion.Where(d => d.IdFamilia == HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion).ToList();
            if (detallesDeOrden.Count() > 0)
            {
                var idAtencionDistintos = detallesDeOrden.Select(d => d.Id).Distinct();
                foreach (var idAtencion in idAtencionDistintos)
                {
                    var detallesAtencion = detallesDeOrden.Where(h => h.Id == idAtencion).ToList();
                    var found = detallesAtencion.First().NombreConcepto.LastIndexOf(" - ");
                    informacion += detallesAtencion.Count().ToString() + "N-" + detallesAtencion.First().NombreConcepto.Substring(found + 3) + ", ";
                }
                informacion = informacion.Substring(0, informacion.Length - 2);
            }
            return informacion;
        }
        public List<DetalleDeOperacion> ConvertirDetalleOrdenADetalleOperacion(List<DetalleOrdenAtencion> detallesDeOrden, List<long> idsDetalle)
        {
            List<DetalleDeOperacion> nuevosDetalles = new List<DetalleDeOperacion>();
            foreach (var idDetalle in idsDetalle)
            {
                var detalle = detallesDeOrden.First(d => d.Id == idDetalle);
                nuevosDetalles.Add(new DetalleDeOperacion()
                {
                    Id = idDetalle,
                    Cantidad = detalle.Cantidad,
                    PrecioUnitario = detalle.PrecioUnitario,
                    Importe = detalle.Importe,
                    Producto = new Concepto_Negocio_Comercial() { Id = detalle.IdConcepto }
                });
            }
            return nuevosDetalles;
        }
        private void AgruparDetallesDeVenta(DatosVentaIntegrada venta)
        {
            try
            {
                var detallesVenta = new List<DetalleDeOperacion>();
                var detallesAgrupados = venta.Orden.Detalles.GroupBy(d => new { d.Producto.Id, d.PrecioUnitario });
                foreach (var grupo in detallesAgrupados)
                {
                    detallesVenta.Add(new DetalleDeOperacion()
                    {
                        Producto = new Concepto_Negocio_Comercial { Id = grupo.Key.Id },
                        Cantidad = grupo.Sum(g => g.Cantidad),
                        PrecioUnitario = grupo.Key.PrecioUnitario,
                        Importe = grupo.Sum(g => g.Importe),
                        MascaraDeCalculo = VentasSettings.Default.MascaraDeCalculoCantidadCalculada
                    });
                }
                venta.Orden.Detalles = detallesVenta;
            }
            catch (Exception e)
            {
                throw new LogicaException("ERROR al intentar determinar los detalles de operacion", e);
            }
        }
        private void RecalcularCantidadEImporteTipoPagoDivididoPorMonto(Atencion atencion, List<DatosVentaIntegrada> comprobantesVenta)
        {
            try
            {
                var detallesAtencion = GenerarDetallesDeOperacion(atencion);
                for (int i = 0; i < comprobantesVenta.Count; i++)
                {
                    var detallesComprobante = AgruparDetallesComprobantes(comprobantesVenta);
                    if (atencion.NuevosComprobantes[i].Orden.Total != comprobantesVenta[i].Orden.ImporteTotal)
                    {
                        var diferencia = atencion.NuevosComprobantes[i].Orden.Total - comprobantesVenta[i].Orden.ImporteTotal;
                        for (int j = 0; j < detallesComprobante.Count; j++)
                        {
                            if (detallesComprobante[j].Importe != detallesAtencion.Single(d => d.Producto.Id == detallesComprobante[j].Producto.Id && d.PrecioUnitario == detallesComprobante[j].PrecioUnitario).Importe)
                            {
                                comprobantesVenta[i].Orden.Detalles.Single(d => d.Producto.Id == detallesComprobante[j].Producto.Id && d.PrecioUnitario == detallesComprobante[j].PrecioUnitario).Importe += diferencia;
                                break;
                            }
                        }
                    }
                    for (int j = 0; j < detallesAtencion.Count; j++)
                    {
                        var diferencia = detallesAtencion[j].Importe - comprobantesVenta.SelectMany(c => c.Orden.Detalles).Where(d => d.Producto.Id == detallesAtencion[j].Producto.Id && d.PrecioUnitario == detallesAtencion[j].PrecioUnitario).Sum(d => d.Importe);
                        if (diferencia != 0)
                        {
                            comprobantesVenta[i].Orden.Detalles.First(d => d.Producto.Id == detallesAtencion[j].Producto.Id && d.PrecioUnitario == detallesAtencion[j].PrecioUnitario).Importe += diferencia;
                        }
                    }
                }
                for (int i = 0; i < detallesAtencion.Count; i++)
                {
                    var diferencia = detallesAtencion[i].Cantidad - comprobantesVenta.SelectMany(c => c.Orden.Detalles).Where(d => d.Producto.Id == detallesAtencion[i].Producto.Id && d.PrecioUnitario == detallesAtencion[i].PrecioUnitario).Sum(d => d.Cantidad);
                    if (diferencia != 0)
                    {
                        comprobantesVenta.SelectMany(c => c.Orden.Detalles).First(d => d.Producto.Id == detallesAtencion[i].Producto.Id && d.PrecioUnitario == detallesAtencion[i].PrecioUnitario).Cantidad += diferencia;
                    }
                }
                foreach (var item in detallesAtencion)
                {
                    if (detallesAtencion.Single(i => i.Producto.Id == item.Producto.Id && i.PrecioUnitario == item.PrecioUnitario).Cantidad != comprobantesVenta.SelectMany(dv => dv.Orden.Detalles).Where(d => d.Producto.Id == item.Producto.Id && d.PrecioUnitario == item.PrecioUnitario).Sum(d => d.Cantidad))
                    {
                        throw new LogicaException("No se puede registrar los comprobantes debido a que los totales en las cantidades no son congruentes");
                    }
                    if (detallesAtencion.Single(i => i.Producto.Id == item.Producto.Id && i.PrecioUnitario == item.PrecioUnitario).Importe != comprobantesVenta.SelectMany(dv => dv.Orden.Detalles).Where(d => d.Producto.Id == item.Producto.Id && d.PrecioUnitario == item.PrecioUnitario).Sum(d => d.Importe))
                    {
                        throw new LogicaException("No se puede registrar los comprobantes debido a que los totales en los importes no son congruentes");
                    }
                }
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al verificar la cantidad o importe en el tipo de pago general", e);
            }
        }
        private List<DetalleDeOperacion> AgruparDetallesComprobantes(List<DatosVentaIntegrada> comprobantesGenerados)
        {
            try
            {
                var nuevosDetallesAgrupados = new List<DetalleDeOperacion>();
                var detallesAgrupados = comprobantesGenerados.Select(c => c.Orden).SelectMany(o => o.Detalles).GroupBy(d => new { d.Producto.Id, d.PrecioUnitario });
                foreach (var grupo in detallesAgrupados)
                {
                    nuevosDetallesAgrupados.Add(new DetalleDeOperacion()
                    {
                        Producto = new Concepto_Negocio_Comercial { Id = grupo.Key.Id },
                        Cantidad = grupo.Sum(g => g.Cantidad),
                        PrecioUnitario = grupo.Key.PrecioUnitario,
                        Importe = grupo.Sum(g => g.Importe),
                        MascaraDeCalculo = VentasSettings.Default.MascaraDeCalculoCantidadCalculada
                    });
                }
                return nuevosDetallesAgrupados;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar agrupar detalles de comprobantes", e);
            }
        }






















    }
}
