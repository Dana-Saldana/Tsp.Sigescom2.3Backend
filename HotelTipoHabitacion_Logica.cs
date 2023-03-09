using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Tsp.Sigescom.Config;
using Tsp.Sigescom.Modelo;
using Tsp.Sigescom.Modelo.ClasesNegocio.Core.Generico;
using Tsp.Sigescom.Modelo.ClasesNegocio.SigesHotel;
using Tsp.Sigescom.Modelo.Entidades;
using Tsp.Sigescom.Modelo.Entidades.Custom.Partial;
using Tsp.Sigescom.Modelo.Entidades.Exceptions;
using Tsp.Sigescom.Modelo.Entidades.Sesion;
using Tsp.Sigescom.Modelo.Interfaces.Negocio;
namespace Tsp.Sigescom.Logica.SigesHotel
{
    public partial class HotelLogica : IHotelLogica
    {
        public List<ItemGenerico> ObtenerTiposHabitacionVigentesSimplificado()
        {
            try
            {
                return _hotelRepositorio.ObtenerTiposHabitacionVigentesSimplificado().ToList();
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener tipo habitaciones de hotel", e);
            }
        }
        public OperationResult CambiarVigenciaDelTipoHabitacion(int idTipoHabitacion)
        {
            try
            {
                if (_hotelRepositorio.ExisteHabitacionesVigentesTipoHabitacion(idTipoHabitacion))
                {
                    throw new LogicaException("existe habitaciones en vigencia para este tipo de habitacion");
                }
                else
                {
                    return _conceptoRepositorio.InvertirEsVigenteConceptoNegocio(idTipoHabitacion);
                }
            }
            catch (Exception e)
            {
                throw new LogicaException("Error en la Logica, No se puede cambiar el estado del Tipo de habitacion, debido a que tiene habitaciones vigentas asignadas", e);
            }
        }
        public TipoHabitacion ObtenerTipoHabitacion(int id, UserProfileSessionData sesionUsuario)
        {
            try
            {
                List<Precio_Compra_Venta_Concepto> precios = _logicaConcepto.ObtenerPreciosCompraVentaDeConceptoNegocio(id);
                Concepto_negocio concepto_Negocio = _conceptoRepositorio.ObtenerConceptoNegocioIncluyendoValorCaracteristicaConceptoNegocioYDetalleMaestroYCaracteristicaConcepto(id);
                TipoHabitacion tipoHabitacion = new TipoHabitacion
                {
                    Id = concepto_Negocio.id,
                    Nombre = concepto_Negocio.sufijo,
                    Descripcion = concepto_Negocio.propiedades,
                    Precios = precios,
                    EsVigente = concepto_Negocio.es_vigente,
                };
                var valorCaracteristicas = concepto_Negocio.Valor_caracteristica_concepto_negocio.Select(vc => vc.Valor_caracteristica).ToList();
                tipoHabitacion.Caracteristicas = new List<ItemGenerico>();
                foreach (var valor in valorCaracteristicas)
                {
                    if (valor.id_caracteristica == HotelSettings.Default.IdCaracteristicaAforoAdultos)
                    {
                        tipoHabitacion.AforoAdultos = new ItemGenerico { Id = valor.id, Nombre = valor.valor };
                    }
                    else
                    {
                        if (valor.id_caracteristica == HotelSettings.Default.IdCaracteristicaAforoNinos)
                        {
                            tipoHabitacion.AforoNinos = new ItemGenerico { Id = valor.id, Nombre = valor.valor };
                        }
                        else
                        {
                            tipoHabitacion.Caracteristicas.Add(new ItemGenerico { Id = valor.id, Nombre = valor.valor });
                        }
                    }
                }
                ObtenerFotosTipoHabitacion(tipoHabitacion, sesionUsuario);
                return tipoHabitacion;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener Habitaciones", e);
            }
        }
        public List<TipoHabitacionesBandeja> ObtenerTipoHabitaciones()
        {
            try
            {
                List<Concepto_negocio> concepto_Negocios = _conceptoRepositorio.ObtenerConceptosDeNegocioConPreciosPorFamilia(HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion).ToList();
                List<TipoHabitacionesBandeja> tipoHabitaciones = new List<TipoHabitacionesBandeja>();
                foreach (var concepto_negocio in concepto_Negocios)
                {
                    var tipoHabitacion = new TipoHabitacionesBandeja
                    {
                        Id = concepto_negocio.id,
                        Nombre = concepto_negocio.nombre,
                        Precios = concepto_negocio.Precio1.Where(p => p.es_vigente).ToList(),
                        CapacidadNinio = concepto_negocio.Valor_caracteristica_concepto_negocio.Single(v => v.Valor_caracteristica.id_caracteristica == HotelSettings.Default.IdCaracteristicaAforoNinos).Valor_caracteristica.valor,
                        CapacidadAdulto = concepto_negocio.Valor_caracteristica_concepto_negocio.Single(v => v.Valor_caracteristica.id_caracteristica == HotelSettings.Default.IdCaracteristicaAforoAdultos).Valor_caracteristica.valor,
                        EsVigente = concepto_negocio.es_vigente
                    };
                    tipoHabitaciones.Add(tipoHabitacion);
                }
                return tipoHabitaciones;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar obtener tipo habitaciones de hotel", e);
            }
        }
        public OperationResult CrearTipoHabitacion(TipoHabitacion tipoHabitacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                int idRol = HotelSettings.Default.IdRolConceptoHotel;
                int idFamilia = HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion;
                var familiaHabitacion = _maestroRepositorio.ObtenerDetalle(idFamilia);
                var nombreTipoHabitacion = familiaHabitacion.nombre + " " + tipoHabitacion.Nombre;
                if (_hotelRepositorio.ExisteNombreTipoHabitacion(nombreTipoHabitacion))
                {
                    throw new LogicaException("Error al intentar crear el tipo de habitacion, el nombre del tipo de habitacion ya existe.");
                }
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var codigo = idFamilia + "-" + _logicaConcepto.ObtenerSiguienteCodigoParaMercaderia(idRol, idFamilia);
                Concepto_negocio conceptoNegocioTipoHabitacion = new Concepto_negocio
                {
                    id = 0,
                    codigo = codigo,
                    nombre = nombreTipoHabitacion,
                    sufijo = tipoHabitacion.Nombre,
                    propiedades = tipoHabitacion.Descripcion,
                    id_unidad_medida_primaria = ConceptoSettings.Default.idUnidadMedidaPorDefecto,
                    id_presentacion = ConceptoSettings.Default.idPresentacionPorDefecto,
                    contenido = 1,
                    es_vigente = true,
                    id_unidad_medida_secundaria = ConceptoSettings.Default.idUnidadMedidaPorDefecto,
                    id_concepto_basico = idFamilia,
                    codigo_negocio1 = ""
                };
                conceptoNegocioTipoHabitacion.Concepto_negocio_rol.Add(new Concepto_negocio_rol() { id_rol = idRol });
                _logicaConcepto.AgregarCaracteristicas(tipoHabitacion.IdsValoresCaracteristicas.ToArray(), conceptoNegocioTipoHabitacion);
                conceptoNegocioTipoHabitacion.Precio = _logicaConcepto.GenerarPrecios(tipoHabitacion.Precios, fechaActual, 0, sesionUsuario.Empleado.Id);
                var resultado = _conceptoRepositorio.CrearConceptoDeNegocio(conceptoNegocioTipoHabitacion);
                tipoHabitacion.Id = (int)resultado.data;
                if (resultado.code_result == OperationResultEnum.Success)
                {
                    if (tipoHabitacion.Fotos != null)
                        AgregarFotosTipoHabitacion(tipoHabitacion, sesionUsuario, 1);
                }
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar registrar HABITACION", e);
            }
        }
        public OperationResult EditarTipoHabitacion(TipoHabitacion tipoHabitacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                int idRol = HotelSettings.Default.IdRolConceptoHotel;
                int idFamilia = HotelSettings.Default.IdDetalleMaestroFamiliaHabitacion;
                var familiaHabitacion = _maestroRepositorio.ObtenerDetalle(idFamilia);
                var nombreTipoHabitacion = familiaHabitacion.nombre + " " + tipoHabitacion.Nombre;
                if (_hotelRepositorio.ExisteNombreTipoHabitacionExceptoTipoHabitacion(nombreTipoHabitacion, tipoHabitacion.Id))
                {
                    throw new LogicaException("Error al intentar editar el tipo de habitacion, el nombre del tipo de habitacion ya existe.");
                }
                DateTime fechaActual = DateTimeUtil.FechaActual();
                var codigo = idFamilia + "-" + _logicaConcepto.ObtenerSiguienteCodigoParaMercaderia(idRol, idFamilia);
                Concepto_negocio conceptoNegocioTipoHabitacion = new Concepto_negocio
                {
                    id = tipoHabitacion.Id,
                    codigo = codigo,
                    nombre = nombreTipoHabitacion,
                    sufijo = tipoHabitacion.Nombre,
                    propiedades = tipoHabitacion.Descripcion,
                    id_unidad_medida_primaria = ConceptoSettings.Default.idUnidadMedidaPorDefecto,
                    id_presentacion = ConceptoSettings.Default.idPresentacionPorDefecto,
                    contenido = 1,
                    es_vigente = true,
                    id_unidad_medida_secundaria = ConceptoSettings.Default.idUnidadMedidaPorDefecto,
                    id_concepto_basico = idFamilia,
                    codigo_negocio1 = ""
                };
                conceptoNegocioTipoHabitacion.Concepto_negocio_rol.Add(new Concepto_negocio_rol() { id_rol = idRol });
                _logicaConcepto.AgregarCaracteristicas(tipoHabitacion.IdsValoresCaracteristicas.ToArray(), conceptoNegocioTipoHabitacion);
                conceptoNegocioTipoHabitacion.Precio = _logicaConcepto.GenerarPrecios(tipoHabitacion.Precios, fechaActual, 0, sesionUsuario.Empleado.Id);
                var resultado = _conceptoRepositorio.ActualizarConceptoNegocio(conceptoNegocioTipoHabitacion);
                if (resultado.code_result == OperationResultEnum.Success)
                {
                    if (tipoHabitacion.HayFotosEliminadas)
                        EliminarFotosTipoHabitacion(tipoHabitacion, sesionUsuario);
                    int indicador = (tipoHabitacion.HayFotosEliminadas && tipoHabitacion.HayFotos) ? RenombrarFotosTipoHabitacion(tipoHabitacion, sesionUsuario) : (tipoHabitacion.HayFotos ? tipoHabitacion.Fotos.Where(f => !string.IsNullOrEmpty(f.Nombre)).Count() + 1 : 1);
                    if (tipoHabitacion.HayFotos)
                        AgregarFotosTipoHabitacion(tipoHabitacion, sesionUsuario, indicador);

                    var resultadoPrecios = ResolverPreciosTipoHabitacion(tipoHabitacion, sesionUsuario, fechaActual);
                    if (resultadoPrecios.code_result != OperationResultEnum.Success)
                        return new OperationResult(OperationResultEnum.Warning, "El tipo de habitacion se guardo correctamente, pero no se actualizaron los precios. " + resultadoPrecios.message);
                }
                return resultado;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al intentar editar HABITACION", e);
            }
        }
        private void AgregarFotosTipoHabitacion(TipoHabitacion tipoHabitacion, UserProfileSessionData sesionUsuario, int indicador)
        {
            try
            {
                string downloadUrl = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[0];
                string usuario = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[1];
                string password = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[2];
                foreach (var foto in tipoHabitacion.Fotos.Where(f => string.IsNullOrEmpty(f.Nombre)))
                {
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(downloadUrl + "/" + sesionUsuario.Sede.DocumentoIdentidad + "/" + tipoHabitacion.Id + "-" + indicador + ".png");
                    request.Credentials = new NetworkCredential(usuario, password);
                    request.Method = WebRequestMethods.Ftp.UploadFile;

                    byte[] fileBytes = Convert.FromBase64String(foto.Foto.Remove(0,23));

                    using (Stream fileStream = new MemoryStream(fileBytes))
                    using (Stream ftpStream = request.GetRequestStream())
                    {
                        fileStream.CopyTo(ftpStream);
                    }
                    ++indicador;
                }
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al agregar las fotos de tipo de habitacion", e);
            }
        }
        private OperationResult ResolverPreciosTipoHabitacion(TipoHabitacion tipoHabitacion, UserProfileSessionData sesionUsuario, DateTime fechaActual)
        {
            List<Precio> nuevosPrecios = new List<Precio>();
            List<Precio> preciosCaducos = new List<Precio>();
            foreach (var item in tipoHabitacion.Precios)
            {
                if (item.Seleccionado)
                {
                    Precio precio = new Precio(item.IdPrecio, item.IdPuntoPrecio, MaestroSettings.Default.IdDetalleMaestroUnidadDeNegocioTransversal, tipoHabitacion.Id, item.Valor, item.IdTarifa, MaestroSettings.Default.IdDetalleMaestroMonedaSoles, item.FechaInicio, item.FechaFin, fechaActual, true, true, false, 1, 0, MaestroSettings.Default.IdDetalleMaestroTipoPrecioPrecio, item.Descripcion, sesionUsuario.Empleado.Id);
                    nuevosPrecios.Add(precio);
                }
                else
                {
                    if (item.IdPrecio != 0)
                    {
                        Precio precio = _precioRepositorio.obtenerPrecio(item.IdPrecio);
                        precio.fecha_fin = fechaActual;
                        precio.fecha_modificacion = fechaActual;
                        precio.es_vigente = false;
                        preciosCaducos.Add(precio);
                    }
                }
            }
            var resultadoPrecios = _precioRepositorio.ResolverPrecios(nuevosPrecios, preciosCaducos);
            return resultadoPrecios;
        }
        private void ObtenerFotosTipoHabitacion(TipoHabitacion tipoHabitacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                tipoHabitacion.Fotos = new List<FotoTipoHabitacion>();
                string downloadUrl = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[0];
                string usuario = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[1];
                string password = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[2];
                List<byte[]> imagenesTiposHabitacion = new List<byte[]>();
                bool hayImagenesTiposHabitacion = true;
                int indicador = 1;
                do
                {
                    var nombreArchivo = tipoHabitacion.Id.ToString() + "-" + indicador + ".png";
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(downloadUrl + "/" + sesionUsuario.Sede.DocumentoIdentidad + "/" + nombreArchivo);
                    request.Credentials = new NetworkCredential(usuario, password);
                    request.Method = WebRequestMethods.Ftp.DownloadFile;
                    request.UseBinary = true;
                    request.Proxy = null;
                    try
                    {
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
                        tipoHabitacion.Fotos.Add(new FotoTipoHabitacion
                        {
                            Nombre = nombreArchivo,
                            Foto = Convert.ToBase64String(memoryStream.ToArray())
                        });
                    }
                    catch (Exception)
                    {
                        hayImagenesTiposHabitacion = false;
                    }
                    ++indicador;
                } while (hayImagenesTiposHabitacion);
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al obtener las fotos de tipo de habitacion", e);
            }
        }
        private void EliminarFotosTipoHabitacion(TipoHabitacion tipoHabitacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                string downloadUrl = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[0];
                string usuario = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[1];
                string password = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[2];
                List<string> statusDescriptions = new List<string>();
                foreach (var nombrearchivoEliminado in tipoHabitacion.FotosEliminadas)
                {
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(downloadUrl + "/" + sesionUsuario.Sede.DocumentoIdentidad + "/" + nombrearchivoEliminado);
                    request.Method = WebRequestMethods.Ftp.DeleteFile;
                    request.Credentials = new NetworkCredential(usuario, password);
                    using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                    {
                        statusDescriptions.Add(response.StatusDescription);
                    }
                }
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al obtener las fotos de tipo de habitacion", e);
            }
        }
        private int RenombrarFotosTipoHabitacion(TipoHabitacion tipoHabitacion, UserProfileSessionData sesionUsuario)
        {
            try
            {
                string downloadUrl = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[0];
                string usuario = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[1];
                string password = HotelSettings.Default.ServidorFptTipoHabitacion.Split('|')[2];
                int indicador = 1;
                foreach (var archivoFoto in tipoHabitacion.Fotos.Where(th => th.Nombre != null))
                {
                    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(downloadUrl + "/" + sesionUsuario.Sede.DocumentoIdentidad + "/" + archivoFoto.Nombre);
                    request.Method = WebRequestMethods.Ftp.Rename;
                    request.Credentials = new NetworkCredential(usuario, password);
                    request.RenameTo = tipoHabitacion.Id + "-" + indicador + ".png";
                    request.UseBinary = true;
                    FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                    Stream ftpStream = response.GetResponseStream();
                    ftpStream.Close();
                    response.Close();
                    ++indicador;
                }
                return indicador;
            }
            catch (Exception e)
            {
                throw new LogicaException("Error al obtener las fotos de tipo de habitacion", e);
            }

        }
    }
}
