using Sisfarma.Sincronizador.Core.Extensions;
using Sisfarma.Sincronizador.Domain.Core.Services;
using Sisfarma.Sincronizador.Domain.Core.Sincronizadores.SuperTypes;
using Sisfarma.Sincronizador.Domain.Entities.Fisiotes;
using Sisfarma.Sincronizador.Infrastructure.Fisiotes.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

using FAR = Sisfarma.Sincronizador.Domain.Entities.Farmacia;

namespace Sisfarma.Sincronizador.Unycop.Domain.Core.Sincronizadores
{
    public class VentasNoIncluidasActualizacionSincronizador : TaskSincronizador
    {
        private string _cargarPuntos;
        private string _filtrosResidencia;
        private string _verCategorias;
        private string _puntosDeSisfarma;
        private bool _perteneceFarmazul;
        private string _clasificacion;
        private string _empresaUno;

        const string FAMILIA_DEFAULT = "<Sin Clasificar>";
        const string LABORATORIO_DEFAULT = "<Sin Laboratorio>";
        const string SISTEMA_NIXFARMA = "nixfarma";
        const string TIPO_CLASIFICACION_CATEGORIA = "Categoria";

        public VentasNoIncluidasActualizacionSincronizador(IFarmaciaService farmacia, ISisfarmaService fisiotes)
            : base(farmacia, fisiotes)
        {
        }

        public override void LoadConfiguration()
        {
            base.LoadConfiguration();
            _cargarPuntos = ConfiguracionPredefinida[Configuracion.FIELD_CARGAR_PUNTOS] ?? "no";
            _filtrosResidencia = ConfiguracionPredefinida[Configuracion.FIELD_FILTROS_RESIDENCIA];
            _verCategorias = ConfiguracionPredefinida[Configuracion.FIELD_VER_CATEGORIAS];
            _perteneceFarmazul = bool.Parse(ConfiguracionPredefinida[Configuracion.FIELD_ES_FARMAZUL]);
            _puntosDeSisfarma = ConfiguracionPredefinida[Configuracion.FIELD_PUNTOS_SISFARMA];
            _clasificacion = _clasificacion = TIPO_CLASIFICACION_CATEGORIA;
            _empresaUno = _farmacia.Empresas.GetCodigoByNumero(1);
        }

        public override void PreSincronizacion()
        {
            base.PreSincronizacion();
        }

        public override void Process()
        {
            // numero de venta no incluída sin el sufijo de empresa
            var cargarPuntosSisfarma = _cargarPuntos == "si";
            var porDondeVoyVentasNoIncluidas = _sisfarma.Configuraciones.GetByCampo(Configuracion.FIELD_POR_DONDE_VOY_VENTAS_NO_INCLUIDAS);
            var ventaNumero = porDondeVoyVentasNoIncluidas.ToLongOrDefault();
            
            //var ventaNumero = "
            var findFromYear = DateTime.Now.Year - 2;
            var ventas = _farmacia.Ventas.GetAllGteYearAndLteNumber(findFromYear, ventaNumero);
            if (!ventas.Any())
                return;
            var ventasNormales = ventas.Where(x => x.Situacion.ToUpper() == "N");
            var ventasAnuladas = ventas.Where(x => x.Situacion.ToUpper() == "A");

            var batchPuntosPendientes = new List<PuntosPendientes>();
            var lastVentaNoIncluida = 0L;
            foreach (var venta in ventasNormales)
            {
                var empresaSerial = _empresaUno.Equals(venta.EmpresaCodigo) ? "00001" : "00002";

                if (venta.ClienteId > 0)
                    venta.Cliente = _farmacia.Clientes.GetOneOrDefaultById(venta.ClienteId, cargarPuntosSisfarma);
                
                if (venta.HasCliente())
                    InsertOrUpdateCliente(venta.Cliente);
                
                venta.Detalle = _farmacia.Ventas.GetDetalleDeVentaByVentaId(venta.Operacion, venta.EmpresaCodigo);
                if (venta.HasDetalle())
                {
                    var puntosPendientes = GenerarPuntosPendientes(venta, empresaSerial);
                    batchPuntosPendientes.AddRange(puntosPendientes);
                    lastVentaNoIncluida = venta.Operacion;
                }                                                
            }

            if (batchPuntosPendientes.Any())
            {
                _sisfarma.PuntosPendientes.Sincronizar(batchPuntosPendientes);
                _sisfarma.Configuraciones.Update(Configuracion.FIELD_POR_DONDE_VOY_VENTAS_NO_INCLUIDAS, $"{lastVentaNoIncluida}");
            }

            foreach (var anulada in ventasAnuladas)
            {
                var empresaSerial = _empresaUno.Equals(anulada.EmpresaCodigo) ? "00001" : "00002";
                var ventaAnulada = $"{anulada.Operacion}{empresaSerial}".ToLongOrDefault();
                _sisfarma.PuntosPendientes.Sincronizar(new DeletePuntuacion { idventa = ventaAnulada });
            }
        }

        private IEnumerable<PuntosPendientes> GenerarPuntosPendientes(FAR.Venta venta, string empresaSerial)
        {            
            var puntosPendientes = new List<PuntosPendientes>();
            foreach (var item in venta.Detalle.Where(d => d.HasFarmaco()))
            {
                var familia = !string.IsNullOrWhiteSpace(item.Farmaco.Familia?.Nombre) ? item.Farmaco.Familia.Nombre : FAMILIA_DEFAULT;
                var superFamilia = !string.IsNullOrWhiteSpace(item.Farmaco.SuperFamilia?.Nombre) ? item.Farmaco.SuperFamilia.Nombre : FAMILIA_DEFAULT;

                var categoria = item.Farmaco.Categoria?.Nombre;
                if (_verCategorias == "si" && !string.IsNullOrWhiteSpace(categoria) && categoria.ToLower() != "sin categoria" && categoria.ToLower() != "sin categoría")
                {
                    if (string.IsNullOrEmpty(superFamilia) || superFamilia == FAMILIA_DEFAULT)
                        superFamilia = categoria;
                    else superFamilia = $"{superFamilia} ~~~~~~~~ {categoria}";
                }

                var puntoPendiente = new PuntosPendientes
                {
                    VentaId = $"{venta.Operacion}{empresaSerial}".ToLongOrDefault(),
                    LineaNumero = item.Linea,
                    CodigoBarra = item.Farmaco.CodigoBarras ?? "847000" + item.Farmaco.Codigo.PadLeft(6, '0'),
                    CodigoNacional = item.Farmaco.Codigo,
                    Descripcion = item.Farmaco.Denominacion,

                    Familia = familia,
                    SuperFamilia = superFamilia,
                    SuperFamiliaAux = string.Empty,
                    FamiliaAux = string.Empty,
                    CambioClasificacion = _clasificacion == TIPO_CLASIFICACION_CATEGORIA ? 1 : 0,

                    Cantidad = item.Cantidad,
                    Precio = item.Precio,
                    Pago = item.Equals(venta.Detalle.First()) ? venta.TotalBruto : 0,
                    TipoPago = venta.TipoOperacion,
                    Fecha = venta.FechaHora.Date.ToDateInteger(),
                    DNI = venta.Cliente?.Id.ToString() ?? "0",
                    Cargado = _cargarPuntos.ToLower().Equals("si") ? "no" : "si",
                    Puesto = $"{venta.Puesto}",
                    Trabajador = !string.IsNullOrWhiteSpace(venta.VendedorCodigo) ? venta.VendedorCodigo.Trim() : string.Empty,
                    LaboratorioCodigo = item.Farmaco.Laboratorio?.Codigo ?? string.Empty,
                    Laboratorio = item.Farmaco.Laboratorio?.Nombre ?? LABORATORIO_DEFAULT,
                    Proveedor = item.Farmaco.Proveedor?.Nombre ?? string.Empty,
                    Receta = item.Receta,
                    FechaVenta = venta.FechaHora,
                    PVP = item.PVP,
                    PUC = item.Farmaco?.PrecioCoste ?? 0,
                    Categoria = item.Farmaco.Categoria?.Nombre ?? string.Empty,
                    Subcategoria = item.Farmaco.Subcategoria?.Nombre ?? string.Empty,
                    VentaDescuento = item.Equals(venta.Detalle.First()) ? venta.TotalDescuento : 0,
                    LineaDescuento = item.Descuento,
                    TicketNumero = venta.Ticket?.Numero,
                    Serie = venta.Ticket?.Serie ?? string.Empty,
                    Sistema = SISTEMA_NIXFARMA,
                    Ubicacion = item.Farmaco?.Ubicacion
                };

                puntosPendientes.Add(puntoPendiente);
            }

            return puntosPendientes;
        }

        private void InsertOrUpdateCliente(FAR.Cliente cliente)
        {
            var debeCargarPuntos = _puntosDeSisfarma.ToLower().Equals("no") || string.IsNullOrWhiteSpace(_puntosDeSisfarma);
            cliente.DebeCargarPuntos = debeCargarPuntos;

            cliente.Tipo = _farmacia.Clientes.EsResidencia($"{cliente.CodigoCliente}", $"{cliente.CodigoDes}", _filtrosResidencia);

            if (_perteneceFarmazul)
            {
                var beBlue = _farmacia.Clientes.EsBeBlue($"{cliente.CodigoCliente}", $"{cliente.CodigoDes}");
                cliente.BeBlue = beBlue;
            }

            _sisfarma.Clientes.Sincronizar(new List<FAR.Cliente>() { cliente });
        }
    }
}
