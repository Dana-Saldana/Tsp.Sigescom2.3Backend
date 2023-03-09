using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Generico;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel.PlainModel;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel.Report;
using Tsp.Sigescom.Modelo.Entidades;
using Tsp.Sigescom.Modelo.Entidades.Custom;
using Tsp.Sigescom.Modelo.Entidades.Exceptions;
using Tsp.Sigescom.Modelo.Entidades.Sesion;
using Tsp.Sigescom.Modelo.Interfaces.Datos;
using Tsp.Sigescom.Modelo.Interfaces.Datos.Hotel;
using Tsp.Sigescom.Modelo.Interfaces.Logica;
using Tsp.Sigescom.Modelo.Interfaces.Negocio;
using Tsp.Sigescom.Modelo.Interfaces.Repositorio;
using Tsp.Sigescom.Modelo.Negocio.Core.Actor;
using Tsp.Sigescom.Modelo.Negocio.Establecimientos;

namespace Tsp.Sigescom.Logica.SigesHotel
{
    public class HotelReporte_Logica : IHotelReporte_Logica
    {
        protected readonly IActorNegocioLogica _actorNegocioLogica;
        protected readonly IOperacionLogica _operacionLogica;
        protected readonly IMaestroRepositorio _maestroDatos;
        protected readonly ITransaccionRepositorio _transaccionDatos;
        protected readonly IEstablecimiento_Logica _establecimientoLogica;
        protected readonly IHotelReporte_Repositorio _hotelReporte_Datos;
        protected readonly IHotelUtilitario_Logica _hotelUtilitario_Logica;
        protected readonly IHotelLogica _hotelLogica;

        public HotelReporte_Logica(IActorNegocioLogica actorNegocioLogica, IMaestroRepositorio maestroDatos, IOperacionLogica operacionLogica, ITransaccionRepositorio transaccionDatos, IEstablecimiento_Logica establecimientoLogica, IHotelReporte_Repositorio hotelReporte_Datos, IHotelUtilitario_Logica hotelUtilitario_Logica, IHotelLogica hotelLogica)
        {
            _actorNegocioLogica = actorNegocioLogica;
            _maestroDatos = maestroDatos;
            _operacionLogica = operacionLogica;
            _transaccionDatos = transaccionDatos;
            _establecimientoLogica = establecimientoLogica;
            _hotelReporte_Datos = hotelReporte_Datos;
            _hotelUtilitario_Logica = hotelUtilitario_Logica;
            _hotelLogica = hotelLogica;
        }
        public PrincipalReportData ObtenerDatosParaReportePrincipal(UserProfileSessionData profileData)
        {
            var establecimientos = new List<Establecimiento>();
            var establecimientoSesion = profileData.EstablecimientoComercialSeleccionado.ToEstablecimiento();
            var TieneRolAdministradorDeNegocio = profileData.Empleado.TieneRol(ActorSettings.Default.idRolAdministradorDeNegocio);

            if (TieneRolAdministradorDeNegocio)
            {
                establecimientos = Establecimiento.Convert(_establecimientoLogica.ObtenerEstablecimientosComercialesVigentes());
            }
            var tiposHabitacion = _hotelLogica.ObtenerTiposHabitacionVigentesSimplificado();
            var data = new PrincipalReportData()
            {
                FechaActual_ = DateTimeUtil.FechaActual(),
                EstablecimientoSesion = establecimientoSesion,
                EsAdministrador = TieneRolAdministradorDeNegocio,
                Establecimientos = TieneRolAdministradorDeNegocio ? establecimientos : new List<Establecimiento>() { establecimientoSesion },
                TiposHabitacion = tiposHabitacion,
            };
            return data;
        }
        public List<RegistroHuesped> ObtenerRegistroHuespedes(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<RegistroHuesped> registroHuespedes = _hotelReporte_Datos.ObtenerRegistroHuespedes(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                registroHuespedes.ForEach(rh => rh.Noches = _hotelUtilitario_Logica.ObtenerNumeroNoches(rh.FechaIngreso, rh.FechaSalida));
                registroHuespedes = registroHuespedes.Where(rh => rh.ImporteTotal > 0).ToList();
                registroHuespedes.ForEach(rh => rh.Tarifa = rh.ImporteTotal / rh.Noches);
                return registroHuespedes;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener ingresos", e);
            }
        }
        public List<Ingreso> ObtenerIngresos(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<Ingreso> ingresos = _hotelReporte_Datos.ObtenerIngresos(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                //ingresos =  ingresos.Where(i => i.Noches > 0).ToList();
                return ingresos;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener ingresos", e);
            }
        }
        public List<Salida> ObtenerSalidas(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<Salida> salidas = _hotelReporte_Datos.ObtenerSalidas(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                return salidas;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener salidas", e);
            }
        }
        public List<Anulada> ObtenerAnuladas(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<Anulada> anuladas = _hotelReporte_Datos.ObtenerAnuladas(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                return anuladas;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener anuladas", e);
            }
        }
        public FormularioT1 ObtenerFormularioT1(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<RegistroHuesped> registroHuespedes = _hotelReporte_Datos.ObtenerRegistroHuespedesCompleto(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                registroHuespedes.ForEach(rh => rh.Noches = _hotelUtilitario_Logica.ObtenerNumeroNoches(rh.FechaIngreso, rh.FechaSalida));
                registroHuespedes = registroHuespedes.Where(rh => rh.ImporteTotal > 0).ToList();
                registroHuespedes.ForEach(rh => rh.Tarifa = rh.ImporteTotal / rh.Noches);
                FormularioT1 formularioT1 = new FormularioT1();
                formularioT1.Dias1_8Arribos = ObtenerDiasArribos(registroHuespedes, 1, 8);
                formularioT1.Dias9_16Arribos = ObtenerDiasArribos(registroHuespedes, 9, 16);
                formularioT1.Dias17_24Arribos = ObtenerDiasArribos(registroHuespedes, 17, 24);
                formularioT1.Dias25_TotalArribos = ObtenerDiasArribos(registroHuespedes, 25, 31);
                formularioT1.Dias25_TotalArribos.Add(new DiaArribo("TOTAL", registroHuespedes.Count()));
                formularioT1.ArribosPernoctacionesExtranjeros = ObtenerArribosPernoctacionesExtranjeros(registroHuespedes);
                formularioT1.ArribosPernoctacionesNacionales = ObtenerArribosPernoctacionesNacionales(registroHuespedes);
                formularioT1.MotivoViajes = new List<MotivoViaje>
                {
                    ObtenerMotivoViaje("Extranjeros y no residentes", registroHuespedes.Where(rh => rh.IdPais != MaestroSettings.Default.IdDetalleMaestroNacionPeru).ToList()),
                    ObtenerMotivoViaje("Peruanos y residentes", registroHuespedes.Where(rh => rh.IdPais == MaestroSettings.Default.IdDetalleMaestroNacionPeru).ToList())
                };
                return formularioT1;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener el formulario T1", e);
            }
        }
        public List<DiaArribo> ObtenerDiasArribos(List<RegistroHuesped> registroHuespedes, int diaInicio, int diaFin)
        {
            List<DiaArribo> diasArribos = new List<DiaArribo>();
            int diaIteracion = diaInicio;
            while (diaIteracion <= diaFin)
            {
                diasArribos.Add(new DiaArribo()
                {
                    Nombre = "Dia " + diaIteracion + " °",
                    Arribo = registroHuespedes.Where(rh => rh.FechaIngreso.Day == diaIteracion).Count()
                });
                diaIteracion++;
            }
            return diasArribos;
        }
        public List<ArriboPernoctacion> ObtenerArribosPernoctacionesExtranjeros(List<RegistroHuesped> registroHuespedes)
        {
            List<ArriboPernoctacion> extranjeros = new List<ArriboPernoctacion>();
            var detalleMaestroPaises = _maestroDatos.ObtenerDetalles(MaestroSettings.Default.IdMaestroPaises);
            List<ItemGenerico> paisesExtranjeros = new List<ItemGenerico>();
            foreach (var codigoPais in HotelSettings.Default.CodigoPaisesExtranjerosArribosPernoctacionesFormularioT1.Split('|'))
            {
                paisesExtranjeros.Add(new ItemGenerico(detalleMaestroPaises.Single(p => p.codigo == codigoPais)));
            }
            foreach (var paisExtrajero in paisesExtranjeros)
            { 
                extranjeros.Add(new ArriboPernoctacion()
                {
                    Nombre = paisExtrajero.Nombre,
                    Arribo = registroHuespedes.Where(rh => rh.IdPais == paisExtrajero.Id).Sum(rh => rh.Arribos),
                    Pernoctacion = registroHuespedes.Where(rh => rh.IdPais == paisExtrajero.Id).Sum(rh => rh.Pernoctaciones),
                });
            }
            extranjeros.Add(new ArriboPernoctacion("África", registroHuespedes.Where(rh => rh.Continente == "ÁFRICA").Sum(rh => rh.Arribos), registroHuespedes.Where(rh => rh.Continente == "ÁFRICA").Sum(rh => rh.Pernoctaciones)));
            extranjeros.Add(new ArriboPernoctacion("Oceanía", registroHuespedes.Where(rh => rh.Continente == "OCEANÍA").Sum(rh => rh.Arribos), registroHuespedes.Where(rh => rh.Continente == "OCEANÍA").Sum(rh => rh.Pernoctaciones)));
            extranjeros.Add(new ArriboPernoctacion("Otro país de América", registroHuespedes.Where(rh => rh.IdPais != MaestroSettings.Default.IdDetalleMaestroNacionPeru && rh.Continente == "AMÉRICA" && !paisesExtranjeros.Select(p => p.Id).Contains(rh.IdPais)).Sum(rh => rh.Arribos), registroHuespedes.Where(rh => rh.IdPais != MaestroSettings.Default.IdDetalleMaestroNacionPeru && rh.Continente == "AMÉRICA" && !paisesExtranjeros.Select(p => p.Id).Contains(rh.IdPais)).Sum(rh => rh.Pernoctaciones)));
            extranjeros.Add(new ArriboPernoctacion("Otro país de Asia", registroHuespedes.Where(rh => rh.Continente == "ASIA" && !paisesExtranjeros.Select(p => p.Id).Contains(rh.IdPais)).Sum(rh => rh.Arribos), registroHuespedes.Where(rh => rh.Continente == "ASIA" && !paisesExtranjeros.Select(p => p.Id).Contains(rh.IdPais)).Sum(rh => rh.Pernoctaciones)));
            extranjeros.Add(new ArriboPernoctacion("Otro país de Europa", registroHuespedes.Where(rh => rh.Continente == "EUROPA" && !paisesExtranjeros.Select(p => p.Id).Contains(rh.IdPais)).Sum(rh => rh.Arribos), registroHuespedes.Where(rh => rh.Continente == "EUROPA" && !paisesExtranjeros.Select(p => p.Id).Contains(rh.IdPais)).Sum(rh => rh.Pernoctaciones)));
            return extranjeros;
        }
        public List<ArriboPernoctacion> ObtenerArribosPernoctacionesNacionales(List<RegistroHuesped> registroHuespedes)
        {
            List<ArriboPernoctacion> nacionales = new List<ArriboPernoctacion>();
            var regiones = _maestroDatos.ObtenerRegiones();
            nacionales.Add(new ArriboPernoctacion("Lima Metropolitana y Callao", registroHuespedes.Where(rh => rh.IdRegionUbigeo == MaestroSettings.Default.IdRegionUbigeoLima && rh.IdProvinciaUbigeo == MaestroSettings.Default.IdProvinciaUbigeoLima).Sum(rh => rh.Arribos) + registroHuespedes.Where(rh => rh.IdRegionUbigeo == MaestroSettings.Default.IdRegionUbigeoCallao).Sum(rh => rh.Arribos), registroHuespedes.Where(rh => rh.IdRegionUbigeo == MaestroSettings.Default.IdRegionUbigeoLima && rh.IdProvinciaUbigeo == MaestroSettings.Default.IdProvinciaUbigeoLima).Sum(rh => rh.Pernoctaciones) + registroHuespedes.Where(rh => rh.IdRegionUbigeo == MaestroSettings.Default.IdRegionUbigeoCallao).Sum(rh => rh.Pernoctaciones)));
            nacionales.Add(new ArriboPernoctacion("Región Lima (3)", registroHuespedes.Where(rh => rh.IdRegionUbigeo == MaestroSettings.Default.IdRegionUbigeoLima && rh.IdProvinciaUbigeo != MaestroSettings.Default.IdProvinciaUbigeoLima).Sum(rh => rh.Arribos), registroHuespedes.Where(rh => rh.IdRegionUbigeo == MaestroSettings.Default.IdRegionUbigeoLima && rh.IdProvinciaUbigeo != MaestroSettings.Default.IdProvinciaUbigeoLima).Sum(rh => rh.Pernoctaciones)));
            foreach (var item in regiones.Where(r => r.id_region != MaestroSettings.Default.IdRegionUbigeoLima || r.id_region != MaestroSettings.Default.IdRegionUbigeoCallao))
            {
                nacionales.Add(new ArriboPernoctacion()
                {
                    Nombre = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(item.descripcion_corta.ToLower()),
                    Arribo = registroHuespedes.Where(rh => rh.IdRegionUbigeo == item.id_region).Sum(rh => rh.Arribos),
                    Pernoctacion = registroHuespedes.Where(rh => rh.IdRegionUbigeo == item.id_region).Sum(rh => rh.Pernoctaciones)
                });
            }
            return nacionales;
        }
        public MotivoViaje ObtenerMotivoViaje(string detalle, List<RegistroHuesped> registroHuespedes)
        {
            MotivoViaje motivoViaje = new MotivoViaje()
            {
                Nombre = detalle,
                Total = registroHuespedes.Sum(rh => rh.Arribos),
                Vacaciones = registroHuespedes.Where(rh => rh.IdMotivoViaje == HotelSettings.Default.IdDetalleMaestroMotivoDeViajeVacaciones).Sum(rh => rh.Arribos),
                Visita = registroHuespedes.Where(rh => rh.IdMotivoViaje == HotelSettings.Default.IdDetalleMaestroMotivoDeViajeVisita).Sum(rh => rh.Arribos),
                Educacion = registroHuespedes.Where(rh => rh.IdMotivoViaje == HotelSettings.Default.IdDetalleMaestroMotivoDeViajeEducacion).Sum(rh => rh.Arribos),
                Salud = registroHuespedes.Where(rh => rh.IdMotivoViaje == HotelSettings.Default.IdDetalleMaestroMotivoDeViajeSalud).Sum(rh => rh.Arribos),
                Religion = registroHuespedes.Where(rh => rh.IdMotivoViaje == HotelSettings.Default.IdDetalleMaestroMotivoDeViajeReligion).Sum(rh => rh.Arribos),
                Compras = registroHuespedes.Where(rh => rh.IdMotivoViaje == HotelSettings.Default.IdDetalleMaestroMotivoDeViajeCompras).Sum(rh => rh.Arribos),
                Negocios = registroHuespedes.Where(rh => rh.IdMotivoViaje == HotelSettings.Default.IdDetalleMaestroMotivoDeViajeNegocios).Sum(rh => rh.Arribos),
                Otros = registroHuespedes.Where(rh => rh.IdMotivoViaje == HotelSettings.Default.IdDetalleMaestroMotivoDeViajeOtros).Sum(rh => rh.Arribos),
            };
            return motivoViaje;
        }
        public List<Facturada> ObtenerFacturadas(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<Facturada> facturadas = _hotelReporte_Datos.ObtenerFacturadas(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                return facturadas;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener facturadas", e);
            }
        }
        public List<NoFacturada> ObtenerNoFacturadas(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<NoFacturada> noFacturadas = _hotelReporte_Datos.ObtenerNoFacturadas(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                return noFacturadas;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener no facturadas", e);
            }
        }
        public List<Incidente> ObtenerIncidentes(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta)
        {
            try
            {
                List<Incidente> incidentes = _hotelReporte_Datos.ObtenerIncidentes(idEstablecimiento, fechaDesde, fechaHasta).ToList();
                return incidentes;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener incidentes", e);
            }
        }
        public List<Reserva> ObtenerReservasConfirmadas(int idEstablecimiento, DateTime fechaDesde, DateTime fechaHasta, bool todosTiposHabitacion, int[] idsTiposHabitacion)
        {
            try
            {
                List<Reserva> reservasConfirmadas = todosTiposHabitacion ? _hotelReporte_Datos.ObtenerReservasConfirmadas(idEstablecimiento, fechaDesde, fechaHasta).ToList() : _hotelReporte_Datos.ObtenerReservasConfirmadasPorTipoHabitacion(idEstablecimiento, fechaDesde, fechaHasta, idsTiposHabitacion).ToList();
                return reservasConfirmadas;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener facturadas", e);
            }
        }
        //public List<IngresoEgreso> Ingresos(bool esCuenta, int idCajaCuenta, DateTime fechaDesde, DateTime fechaHasta, bool todosLosMediosPago, int[] mediosPago, bool todasLasOperaciones, int[] operaciones)
        //{
        //    try
        //    {
        //        List<IngresoEgreso> ingresos;
        //        if (esCuenta)
        //        {
        //            ingresos = (todosLosMediosPago ? (todasLasOperaciones ? _finanzaReportingDatos.ObtenerIngresosEgresosEnCuentaBancaria(idCajaCuenta, fechaDesde, fechaHasta, true).ToList() : _finanzaReportingDatos.ObtenerIngresosEgresosEnCuentaBancariaPorOperaciones(idCajaCuenta, fechaDesde, fechaHasta, operaciones).ToList()) : (todasLasOperaciones ? _finanzaReportingDatos.ObtenerIngresosEgresosEnCuentaBancariaPorMediosPago(idCajaCuenta, fechaDesde, fechaHasta, true, mediosPago).ToList() : _finanzaReportingDatos.ObtenerIngresosEgresosEnCuentaBancariaPorOperacionesYMediosPago(idCajaCuenta, fechaDesde, fechaHasta, operaciones, mediosPago).ToList()));
        //        }
        //        else
        //        {
        //            ingresos = (todosLosMediosPago ? (todasLasOperaciones ? _finanzaReportingDatos.ObtenerIngresosEgresos(idCajaCuenta, fechaDesde, fechaHasta, true).ToList() : _finanzaReportingDatos.ObtenerIngresosEgresosPorOperaciones(idCajaCuenta, fechaDesde, fechaHasta, operaciones).ToList()) : (todasLasOperaciones ? _finanzaReportingDatos.ObtenerIngresosEgresosPorMediosPago(idCajaCuenta, fechaDesde, fechaHasta, true, mediosPago).ToList() : _finanzaReportingDatos.ObtenerIngresosEgresosPorOperacionesYMediosPago(idCajaCuenta, fechaDesde, fechaHasta, operaciones, mediosPago).ToList()));
        //        }
        //        ingresos = ingresos.OrderBy(m => m.Fecha).ToList();
        //        return ingresos;
        //    }
        //    catch (Exception e)
        //    {
        //        throw new LogicaException("Error al intentar obtener Ingresos", e);
        //    }
        //}

        //public InventarioValorizado ObtenerInventarioValorizadoHistorico(int idAlmacen, int idEmpleado, DateTime fecha, int idConcepto)
        //{
        //    try
        //    {
        //        InventarioValorizado inventario = new InventarioValorizado() { Fecha = fecha };
        //        DateTime? fechaPrimeraTransaccion = (DateTime)fecha;
        //        ///Obtener ultimoInventarioLogico
        //        InventarioValorizado ultimoInventario = _inventarioHistoricoDatos.ObtenerUltimoInventarioValorizadoHistoricoAnteriorA(idAlmacen, idConcepto, fecha);
        //        ///en caso no exista un inventario, obtenemos la fecha de la primera transaccion realizada en el almacen
        //        if (ultimoInventario == null)
        //        {
        //            fechaPrimeraTransaccion = _consultaTransaccionDatos.ObtenerFechaPrimeraTransaccion(idAlmacen);
        //        }
        //        ///Calculamos la fecha a partir de la cual se contemplaran las transacciones que participaran en el inventario
        //        var fechaDesde = ultimoInventario != null ? ultimoInventario.Fecha.AddMilliseconds(1) : fechaPrimeraTransaccion != null ? (DateTime)fechaPrimeraTransaccion : fecha;
        //        ///Obtener fecha hasta la cual se contemplará las transacciones que participarán en el inventario 
        //        var _fechaHasta = fecha;
        //        ///Obtener los movimientos ocurridos luego del ultimo inventario
        //        Movimientos_concepto_negocio_actor_negocio_interno movimientos = _movimientosDatos.ObtenerMovimientosDeConceptoNegocio(idAlmacen, idConcepto, fechaDesde, _fechaHasta);
        //        inventario.IdAlmacen = idAlmacen;
        //        inventario.IdConcepto = idConcepto;
        //        inventario.Cantidad = (ultimoInventario != null ? ultimoInventario.Cantidad : 0) + (movimientos != null ? movimientos.Entradas_principal - movimientos.Salidas_principal : 0);
        //        inventario.ValorTotal = (ultimoInventario != null ? ultimoInventario.ValorTotal : 0) + (movimientos != null ? movimientos.Total : 0);
        //        inventario.ValorUnitario = inventario.Cantidad != 0 ? inventario.ValorTotal / inventario.Cantidad : 0;
        //        return inventario;
        //    }
        //    catch (Exception e)
        //    {
        //        throw new LogicaException("Error al obtener inventario" + "\n " + e.Message, e);
        //    }
        //}
        //public List<InventarioValorizado> InventarioValorizadoHistorico(int idAlmacen, int idEmpleado, DateTime fechaHasta, bool todasLasFamilias, int[] familias)
        //{
        //    try
        //    {
        //        List<InventarioValorizado> inventario = new List<InventarioValorizado>();
        //        inventario = (todasLasFamilias ? _inventarioHistoricoLogica.ObtenerInventariosValorizados(idEmpleado, idAlmacen, fechaHasta) : _inventarioHistoricoLogica.ObtenerInventariosValorizados(idEmpleado, idAlmacen, fechaHasta, familias)).ToList();
        //        return inventario;
        //    }
        //    catch (Exception e)
        //    {
        //        throw new LogicaException("Error al obtener inventario" + "\n " + e.Message, e);
        //    }
        //}


        //public List<EntradaAlmacen> Entradas(int idAlmacen, DateTime fechaDesde, DateTime fechaHasta, bool todasLasFamilias, int[] familias)
        //{
        //    try
        //    {
        //        List<EntradaAlmacen> entradas = (todasLasFamilias ? _movimientosDatos.ObtenerEntradas(idAlmacen, fechaDesde, fechaHasta) : _movimientosDatos.ObtenerEntradas(idAlmacen, fechaDesde, fechaHasta, familias)).ToList();
        //        return entradas;
        //    }
        //    catch (Exception e)
        //    {
        //        throw new LogicaException("Error al intentar obtener Entradas", e);
        //    }

        //}
        //public List<SalidaAlmacen> Salidas(int idAlmacen, DateTime fechaDesde, DateTime fechaHasta, bool todasLasFamilias, int[] familias)
        //{
        //    try
        //    {
        //        List<SalidaAlmacen> salidas = (todasLasFamilias ? _movimientosDatos.ObtenerSalidas(idAlmacen, fechaDesde, fechaHasta) : _movimientosDatos.ObtenerSalidas(idAlmacen, fechaDesde, fechaHasta, familias)).ToList();
        //        return salidas;
        //    }
        //    catch (Exception e)
        //    {
        //        throw new LogicaException("Error al intentar obtener Entradas", e);
        //    }

        //}
        //public List<InventarioVencimiento> Vencimientos(int idAlmacen, DateTime fechaDesde, DateTime fechaHasta, bool todasLasFamilias, int[] familias)
        //{
        //    try
        //    {
        //        List<InventarioVencimiento> inventario = new List<InventarioVencimiento>();
        //        var gestionGlobalLotes = AplicacionSettings.Default.PermitirGestionDeLotes;
        //        if (gestionGlobalLotes)
        //        {
        //            inventario = (todasLasFamilias ? _inventarioActualDatos.ObtenerVencimientosInventarioActual(idAlmacen, fechaDesde, fechaHasta) : _inventarioActualDatos.ObtenerVencimientosInventarioActual(idAlmacen, fechaDesde, fechaHasta, familias)).ToList();
        //        }
        //        else
        //        {
        //            var vencimientosEntradas = (todasLasFamilias ? _almacenReportingDatos.ObtenerVencimientoConceptosIngresados(idAlmacen, fechaDesde, fechaHasta) : _almacenReportingDatos.ObtenerVencimientoConceptosIngresados(idAlmacen, fechaDesde, fechaHasta, familias)).ToList();

        //            var idsConceptos = vencimientosEntradas.Select(v => v.IdConcepto).Distinct().ToArray();
        //            var inventarioActual = _inventarioActualDatos.ObtenerInventarioFisicoConceptosActual(idAlmacen, idsConceptos);

        //            vencimientosEntradas.ForEach(ve => inventario.Add(new InventarioVencimiento()
        //            {
        //                CodigoBarra = ve.CodigoBarra,
        //                Concepto = ve.Concepto,
        //                UnidadMedida = ve.UnidadMedida,
        //                Lote = ve.Lote,
        //                Cantidad = inventarioActual.Where(i => i.IdConcepto == ve.IdConcepto).Sum(dt => dt.Cantidad),
        //                FechaVencimiento = ve.FechaVencimiento,
        //                IdConcepto = ve.IdConcepto
        //            }));
        //        }
        //        return inventario;
        //    }
        //    catch (Exception e)
        //    {
        //        throw new LogicaException("Error al intentar obtener vencimientos de inventario", e);
        //    }

        //}


        //public List<InventarioSemaforo> InventarioSemaforoHistorico(int idAlmacen, int idEmpleado, DateTime fechaHasta, bool todasLasFamilias, int[] familias, bool estadoBajo, bool estadoNormal, bool estadoAlto)
        //{
        //    List<int> nivelesRequeridos = new List<int>();
        //    nivelesRequeridos.Add((int)NivelStockSemaforoEnum.Indeterminado);
        //    if (estadoAlto) nivelesRequeridos.Add((int)NivelStockSemaforoEnum.Alto);
        //    if (estadoBajo) nivelesRequeridos.Add((int)NivelStockSemaforoEnum.Bajo);
        //    if (estadoNormal) nivelesRequeridos.Add((int)NivelStockSemaforoEnum.Normal);
        //    try
        //    {
        //        List<InventarioSemaforo> inventario = new List<InventarioSemaforo>();
        //        inventario = (todasLasFamilias ? _inventarioHistoricoLogica.ObtenerInventariosSemaforo(idEmpleado, idAlmacen, fechaHasta) : _inventarioHistoricoLogica.ObtenerInventariosSemaforo(idEmpleado, idAlmacen, fechaHasta, familias)).ToList();
        //        return inventario.Where(i => nivelesRequeridos.Contains(i.ValorSemaforoInt)).OrderBy(i => i.Concepto).ToList();
        //    }
        //    catch (Exception e)
        //    {
        //        throw new LogicaException("Error al obtener inventario" + "\n " + e.Message, e);
        //    }
        //}




        //public List<DetalleKardexFisico> KardexFisico(int idAlmacen, int idEmpleado, DateTime fechaDesde, DateTime fechaHasta, int idConcepto)
        //{
        //    var saldoInicial = InventarioFisicoHistorico(idAlmacen, fechaDesde, idConcepto);
        //    var movimientos = _movimientosDatos.ObtenerDetallesMovimientoAlmacen(idAlmacen, idConcepto, fechaDesde, fechaHasta).ToList();
        //    var idsOrdenMovimientoDiferenteABoletaFactura = movimientos.Where(m => m.IdTipoComprobante != MaestroSettings.Default.IdDetalleMaestroComprobanteBoleta && m.IdTipoComprobante != MaestroSettings.Default.IdDetalleMaestroComprobanteFactura).Select(m => (long)m.IdOrden).ToList();
        //    var comprobantesOrdenMovimientoADiferenteBoletaFactura = _movimientosDatos.ObtenerComprobantesDeOrdenes(idsOrdenMovimientoDiferenteABoletaFactura.ToArray()).ToList();
        //    DetalleKardexFisico detalleSaldoInicial = new DetalleKardexFisico() { Index = 0, Fecha = fechaDesde, ActorExterno = "", Operacion = "Saldo Inicial", CantidadSaldo = saldoInicial.Cantidad };
        //    List<DetalleKardexFisico> kardex = new List<DetalleKardexFisico>();
        //    kardex.Add(detalleSaldoInicial);
        //    var cantidadInicial = detalleSaldoInicial.CantidadSaldo;
        //    int factor = 1;
        //    decimal saldoCantidad = cantidadInicial;
        //    int index = detalleSaldoInicial.Index;
        //    movimientos.ForEach(m =>
        //    {
        //        factor = m.EsEntrada ? 1 : -1;
        //        saldoCantidad += m.Cantidad * factor;
        //        kardex.Add(new DetalleKardexFisico() { Index = ++index, Fecha = m.Fecha, ActorExterno = m.NombreActorNegocioExterno, Operacion = m.NombreTipoTransaccion + ((m.IdTipoComprobante != MaestroSettings.Default.IdDetalleMaestroComprobanteBoleta && m.IdTipoComprobante != MaestroSettings.Default.IdDetalleMaestroComprobanteFactura) ? " (" + comprobantesOrdenMovimientoADiferenteBoletaFactura.Single(c => c.IdOperacion == m.IdOrden).Comprobante + ")" : ""), CodigoTipoComprobante = m.CodigoTipoComprobante, SerieYNumeroComprobante = m.NumeroSerie + "-" + m.NumeroComprobante, CantidadEntrada = m.EsEntrada ? m.Cantidad : 0, CantidadSaldo = saldoCantidad, CantidadSalida = !m.EsEntrada ? m.Cantidad : 0, });
        //    });
        //    return kardex;
        //}

        //public List<DetalleKardexValorizado> KardexValorizado(int idAlmacen, int idEmpleado, DateTime fechaDesde, DateTime fechaHasta, int idConcepto)
        //{
        //    var saldoInicial = ObtenerInventarioValorizadoHistorico(idAlmacen, idEmpleado, fechaDesde, idConcepto);
        //    var movimientos = _movimientosDatos.ObtenerDetallesMovimientoAlmacen(idAlmacen, idConcepto, fechaDesde, fechaHasta).ToList();
        //    var idsOrdenMovimientoDiferenteABoletaFactura = movimientos.Where(m => m.IdTipoComprobante != MaestroSettings.Default.IdDetalleMaestroComprobanteBoleta && m.IdTipoComprobante != MaestroSettings.Default.IdDetalleMaestroComprobanteFactura).Select(m => (long)m.IdOrden).ToList();
        //    var comprobantesOrdenMovimientoADiferenteBoletaFactura = _movimientosDatos.ObtenerComprobantesDeOrdenes(idsOrdenMovimientoDiferenteABoletaFactura.ToArray()).ToList();
        //    DetalleKardexValorizado detalleSaldoInicial = new DetalleKardexValorizado() { Index = 0, Fecha = fechaDesde, ActorExterno = "", Operacion = "Saldo Inicial", CantidadSaldo = saldoInicial.Cantidad, ImporteUnitarioSaldo = saldoInicial.ValorUnitario, ImporteTotalSaldo = saldoInicial.ValorTotal };
        //    List<DetalleKardexValorizado> kardex = new List<DetalleKardexValorizado>();
        //    kardex.Add(detalleSaldoInicial);
        //    int factor = 1;
        //    decimal saldoCantidad = detalleSaldoInicial.CantidadSaldo;
        //    decimal saldoImporteTotal = detalleSaldoInicial.ImporteTotalSaldo;
        //    int index = detalleSaldoInicial.Index;
        //    movimientos.ForEach(m =>
        //    {
        //        factor = m.EsEntrada ? 1 : -1;
        //        var factorEntrada = m.EsEntrada ? 1 : 0;
        //        var factorSalida = !m.EsEntrada ? 1 : 0;

        //        saldoCantidad += m.Cantidad * factor;
        //        saldoImporteTotal += m.ImporteTotal * factor;

        //        kardex.Add(new DetalleKardexValorizado() { Index = ++index, Fecha = m.Fecha, ActorExterno = m.NombreActorNegocioExterno, Operacion = m.NombreTipoTransaccion + ((m.IdTipoComprobante != MaestroSettings.Default.IdDetalleMaestroComprobanteBoleta && m.IdTipoComprobante != MaestroSettings.Default.IdDetalleMaestroComprobanteFactura) ? " (" + comprobantesOrdenMovimientoADiferenteBoletaFactura.Single(c => c.IdOperacion == m.IdOrden).Comprobante + ")" : ""), CodigoTipoComprobante = m.CodigoTipoComprobante, SerieYNumeroComprobante = m.NumeroSerie + "-" + m.NumeroComprobante, CantidadEntrada = factorEntrada * m.Cantidad, ImporteUnitarioEntrada = factorEntrada * m.ImporteUnitario, ImporteTotalEntrada = factorEntrada * m.ImporteTotal, CantidadSalida = factorSalida * m.Cantidad, ImporteUnitarioSalida = factorSalida * m.ImporteUnitario, ImporteTotalSalida = factorSalida * m.ImporteTotal, CantidadSaldo = saldoCantidad, ImporteUnitarioSaldo = saldoCantidad != 0 ? (saldoImporteTotal / saldoCantidad) : 0, ImporteTotalSaldo = saldoImporteTotal });
        //    });
        //    return kardex;
        //}

        //public StockMinMax ObtenerStockMinimoYMaximo(int idConcepto)
        //{
        //    decimal stockMinimo = _maestrosAlmacenDatos.ObtenerStockMinimo(idConcepto);
        //    decimal stockMaximo = stockMinimo * (1 + ((decimal)ConceptoSettings.Default.PorcentajeParaObtenerStockMaximo / 100));
        //    return new StockMinMax() { IdConcepto = idConcepto, StockMinimo = stockMinimo, StockMaximo = stockMaximo };
        //}


    }
}
