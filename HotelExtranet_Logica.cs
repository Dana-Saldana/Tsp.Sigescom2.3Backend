using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Generico;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel.ModeloExtranet;
using Tsp.Sigescom.Modelo.Custom;
using Tsp.Sigescom.Modelo.Entidades;
using Tsp.Sigescom.Modelo.Entidades.Custom;
using Tsp.Sigescom.Modelo.Entidades.Exceptions;
using Tsp.Sigescom.Modelo.Entidades.Sesion;
using Tsp.Sigescom.Modelo.Interfaces.Negocio;

namespace Tsp.Sigescom.Logica.SigesHotel
{
    public partial class HotelLogica : IHotelLogica
    {
        public List<RoomType> ObtenerRoomType()
        {
            try
            {
                return _hotelRepositorio.ObtenerRoomType().ToList();
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener tipo habitaciones de hotel", e);
            }
        }
        public List<RoomType> ObtenerRoomTypesDisponibles(DateBooking dateBooking)
        {
            List<RoomType> roomTypes = new List<RoomType>();
            try
            {
                List<ItemGenerico> tipoDeHabitacionesVigentes = ObtenerTiposHabitacionVigentesSimplificado();
                foreach (var tipoDeHabitacion in tipoDeHabitacionesVigentes)
                {
                    List<Habitacion> habitacionesDisponible = BuscarHabitacionesDisponibles(tipoDeHabitacion.Id, dateBooking.EntryDate, dateBooking.DepartureDate, dateBooking.IdEstablishment);
                    if (habitacionesDisponible != null)
                    {
                        roomTypes.Add(new RoomType { Id = tipoDeHabitacion.Id, Name = tipoDeHabitacion.Nombre, AvailabilityAmount = habitacionesDisponible.Count });
                    }
                }
                return roomTypes;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener tipo habitaciones de hotel", e);
            }
        }
        public OperationResult RegistrarBooking(Booking booking, UserProfileSessionData sesion)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                //Registrar el cliente de la reserva
                var idClienteReserva = _logicaActorNegocio.ResolverYObtenerActorComercial(ActorSettings.Default.IdRolCliente, ConvertirPersonalDataARegistroActorComercial(booking.PersonalData)).Id;
                //Convertir booking a atencion macro
                AtencionMacroHotel atencionMacroHotel = ConvertirBookingAAtencionHotel(booking, idClienteReserva);
                List<AtencionHotel> atencionesHotel = new List<AtencionHotel>();
                //Crear una lista de grupos de tipo de habitacion 
                var grupoDeTipoHabitaciones = booking.RoomTypes.GroupBy(t => t.Id).Select(id => id.ToList()).ToList();
                foreach (var tipoHabitacion in grupoDeTipoHabitaciones)
                {
                    List<Habitacion> habitacionesDisponible = BuscarHabitacionesDisponibles(tipoHabitacion[0].Id, booking.DateBooking.EntryDate, booking.DateBooking.DepartureDate, booking.IdFilial);
                    //Verificar si existe la cantidad de habitaciones disponibles 
                    if (habitacionesDisponible.Count >= tipoHabitacion.Count)
                    {
                        //Recorrer la cantidad de tipo de habitacion del primer grupo
                        for (int i = 0; i < tipoHabitacion.Count; i++)
                        {
                            atencionesHotel.Add(new AtencionHotel
                            {
                                FechaIngreso = booking.DateBooking.EntryDate,
                                FechaSalida = booking.DateBooking.DepartureDate,
                                Noches = booking.DateBooking.DepartureDate.Day - booking.DateBooking.EntryDate.Day,
                                PrecioUnitario = tipoHabitacion[i].PriceValue,
                                Importe = (booking.DateBooking.DepartureDate.Day - booking.DateBooking.EntryDate.Day) * tipoHabitacion[i].PriceValue,
                                Habitacion = habitacionesDisponible[i],
                            });
                        }
                    }
                    else
                    {
                        throw new LogicaException(" NO SE ENCONTRO UNA HABITACION DISPONIBLE EN LA LISTA DE HABITACIONES RESERVADAS");
                    }
                }
                atencionMacroHotel.Atenciones = atencionesHotel;
                return RegistrarAtencionMacro(atencionMacroHotel, sesion);
            }
            catch (Exception e)
            {
                throw new LogicaException("ERROR AL REGISTRAR LA RESERVA", e);
            }
        }
        public RegistroActorComercial ConvertirPersonalDataARegistroActorComercial(PersonalData personalData)
        {
            var Responsable = new RegistroActorComercial
            {
                TipoDocumentoIdentidad = new ItemGenerico(personalData.IdTypeDocument),
                NumeroDocumentoIdentidad = personalData.DocumentNumber,
                ApellidoPaterno = personalData.NameComplete,
                ApellidoMaterno = personalData.NameComplete,
                Nombres = personalData.NameComplete,
                NombreComercial = personalData.NameComplete + " " + personalData.NameComplete + " " + personalData.NameComplete,
                Correo = personalData.Email,
                Telefono = personalData.Phone,
                Nacionalidad = new ItemGenerico(personalData.IdCountry),
                DomicilioFiscal = new Direccion_()
                {
                    Pais = new ItemGenerico(personalData.IdCountry),
                    Ubigeo = new ItemGenerico(personalData.IdDistrict),
                    Detalle = personalData.HomeAddress
                }
            };
            return Responsable;
        }
        public AtencionMacroHotel ConvertirBookingAAtencionHotel(Booking booking, int idClienteReserva)
        {
            AtencionMacroHotel atencionMacroHotel = new AtencionMacroHotel
            {
                Responsable = new ActorComercial_
                {
                    Id = idClienteReserva,
                },
                FechaRegistro = booking.RegistrationDate,
                Total = booking.TotalPrice,
                IdMedioPagoExtranet = booking.PaymentTrace.IdPaymentMethod
            };
            if (DiccionarioHotel.IdsMediosDePagoParaGeneracionAutomaticaDeComprobante.Contains(booking.PaymentTrace.IdPaymentMethod))
            {
                atencionMacroHotel.FacturadoGlobal = true;
                var seriesComprobantePorDefecto = _transaccionRepositorio.ObtenerIdsSeriesComprobantes(new int[] { HotelSettings.Default.IdComprobantePorDefectoParaFacturacionDesdeExtranet }, true).ToList();
                if (seriesComprobantePorDefecto == null || seriesComprobantePorDefecto.Count == 0)
                {
                    throw new LogicaException("No existe series del comprobante por defecto en hotel");
                }
                atencionMacroHotel.Comprobante = ConvertirBookingAComprobante(booking, idClienteReserva, seriesComprobantePorDefecto[0]);
            }
            if (booking.PaymentTrace.ImageVoucher != null)
            {
                atencionMacroHotel.HayImagenVoucherExtranet = true;
                atencionMacroHotel.ImagenVoucherExtranet = booking.PaymentTrace.ImageVoucher; //Convert.FromBase64String(base64String);
            }
            return atencionMacroHotel;
        }
        public DatosVentaIntegrada ConvertirBookingAComprobante(Booking booking, int idClienteReserva, int idSerieComprobante)
        {
            var comprobante = new DatosVentaIntegrada()
            {
                FechaRegistro = booking.RegistrationDate,
                EsVentaModoCaja = false,
                MovimientoAlmacen = new DatosMovimientoDeAlmacen() { HayComprobanteDeSalidaDeMercaderia = false },
                Orden = new DatosOrdenVenta()
                {
                    AplicarIGVCuandoEsAmazonia = false,
                    Cliente = new ActorComercial_()
                    {
                        Id = idClienteReserva
                    },
                    Comprobante = new ComprobanteDeNegocio_()
                    {
                        Tipo = new ItemGenerico() { Id = HotelSettings.Default.IdComprobantePorDefectoParaFacturacionDesdeExtranet },
                        Serie = new SerieComprobante_() { Id = idSerieComprobante },
                    },
                    Detalles = new List<DetalleDeOperacion>(),
                    EsVentaPasada = false,
                    FechaEmision = booking.RegistrationDate,
                    DescuentoGlobal = 0,
                    Flete = 0,
                    Icbper = 0,
                    NumeroBolsasDePlastico = 0,
                    Observacion = "NINGUNA",
                    Placa = "",
                    UnificarDetalles = false,
                },
                Pago = new DatosPago()
                {
                    ModoDePago = ModoPago.Contado,
                    Inicial = 0,
                    Traza = new TrazaDePago_()
                    {
                        MedioDePago = new ItemGenerico { Id = booking.PaymentTrace.IdPaymentMethod },
                        Info = new InfoPago { InformacionJson = booking.PaymentTrace.JsonPaymentInformation }
                    }
                }
            };
            return comprobante;
        }
        public OperationResult RegistrarAtencionMacro(AtencionMacroHotel atencionMacro, UserProfileSessionData sesionUsuario)
        {
            try
            {
                OperationResult resultado = new OperationResult();
                int idEstado = DiccionarioHotel.IdsMediosDePagoParaGeneracionAutomaticaDeComprobante.Contains(atencionMacro.IdMedioPagoExtranet) ? MaestroSettings.Default.IdDetalleMaestroEstadoConfirmado : MaestroSettings.Default.IdDetalleMaestroEstadoRegistrado;
                Transaccion atencionDeHotel = atencionMacro.Atenciones.Count() > 1 ? GenerarTransaccionAtencionMacro(atencionMacro, sesionUsuario, idEstado, "Registrado al guardar la reserva desde el extranet") : GenerarTransaccionAtencion(atencionMacro, sesionUsuario, idEstado, "Registrado al guardar la reserva desde el extranet");
                if (atencionMacro.HayImagenVoucherExtranet)
                {
                    SubirImagenExtranetFtp(atencionMacro.ImagenVoucherExtranet, sesionUsuario, atencionDeHotel);
                }
                if (atencionMacro.FacturadoGlobal)
                {
                    atencionMacro.Comprobante.TransaccionOrigen = atencionDeHotel;
                    CompletarDatosGeneralesDeVenta(atencionMacro.Comprobante, sesionUsuario);
                    atencionMacro.Comprobante.Orden.Detalles = GenerarDetallesDeVentaAtencion(atencionMacro);
                    resultado = _logicaOperacion.ConfirmarVentaIntegrada(ModoOperacionEnum.PorMostrador, sesionUsuario, atencionMacro.Comprobante);
                }
                else
                {
                    resultado = _transaccionRepositorio.CrearTransaccion(atencionDeHotel);
                }
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("ERROR AL INTENTAR REGISTRAR UNA ATENCION MACRO", e);
            }
        }
        public void SubirImagenExtranetFtp(string fotoString, UserProfileSessionData sesionUsuario, Transaccion atencionDeHotel)
        {
            try
            {
                string urlFtpImages = HotelSettings.Default.ServidorFptComprobantePagoExtranet.Split('|')[0];
                string usuario = HotelSettings.Default.ServidorFptComprobantePagoExtranet.Split('|')[1];
                string password = HotelSettings.Default.ServidorFptComprobantePagoExtranet.Split('|')[2];
                string fileName = atencionDeHotel.codigo + ".png";
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(urlFtpImages + "/" + sesionUsuario.Sede.DocumentoIdentidad + "/" + fileName);
                request.Credentials = new NetworkCredential(usuario, password);
                request.Method = WebRequestMethods.Ftp.UploadFile;

                byte[] fileBytes = Convert.FromBase64String(fotoString);

                using (Stream fileStream = new MemoryStream(fileBytes))
                using (Stream ftpStream = request.GetRequestStream())
                {
                    fileStream.CopyTo(ftpStream);
                }
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar subir la imagen del comprobante de pago por ftp", e);
            }
        }
        private void ObtenerImagenVoucherExtranet(AtencionMacroHotel atencionMacro, UserProfileSessionData sesionUsuario)
        {
            try
            {
                string downloadUrl = HotelSettings.Default.ServidorFptComprobantePagoExtranet.Split('|')[0];
                string usuario = HotelSettings.Default.ServidorFptComprobantePagoExtranet.Split('|')[1];
                string password = HotelSettings.Default.ServidorFptComprobantePagoExtranet.Split('|')[2];

                var nombreArchivo = atencionMacro.Codigo + ".png";
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(downloadUrl + "/" + sesionUsuario.Sede.DocumentoIdentidad + "/" + nombreArchivo);
                request.Credentials = new NetworkCredential(usuario, password);
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.UseBinary = true;
                request.Proxy = null;

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                Stream stream = response.GetResponseStream();
                MemoryStream memoryStream = new MemoryStream();
                byte[] chunk = new byte[4096];
                int bytesRead;
                while ((bytesRead = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    memoryStream.Write(chunk, 0, bytesRead);
                }
                response.Close();
                stream.Close();
                atencionMacro.ImagenVoucherExtranet = Convert.ToBase64String(memoryStream.ToArray());
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al obtener la imagen del voucher de extranet", e);
            }
        }
    }
}
