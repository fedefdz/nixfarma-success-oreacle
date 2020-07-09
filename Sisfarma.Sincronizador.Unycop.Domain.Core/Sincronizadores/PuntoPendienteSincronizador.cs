﻿using Sisfarma.Sincronizador.Core.Extensions;
using Sisfarma.Sincronizador.Domain.Core.ExternalServices.Fisiotes.DTO.VentasPendientes;
using Sisfarma.Sincronizador.Domain.Core.Services;
using Sisfarma.Sincronizador.Domain.Entities.Farmacia;
using Sisfarma.Sincronizador.Domain.Entities.Fisiotes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Threading.Tasks;
using DC = Sisfarma.Sincronizador.Domain.Core.Sincronizadores;

using FAR = Sisfarma.Sincronizador.Domain.Entities.Farmacia;

namespace Sisfarma.Sincronizador.Unycop.Domain.Core.Sincronizadores
{
    public class PuntoPendienteSincronizadorEmp1 : DC.PuntoPendienteSincronizador
    {
        protected const string TIPO_CLASIFICACION_DEFAULT = "Familia";
        protected const string TIPO_CLASIFICACION_CATEGORIA = "Categoria";
        protected const string SISTEMA_NIXFARMA = "nixfarma";

        private string _clasificacion;
        private bool _debeCopiarClientes;
        private string _copiarClientes;
        private ICollection<int> _aniosProcesados;
        protected string _codigoEmpresa;
        protected DateTime _timestampUltimaVenta;
        private string _filtrosResidencia;
        private string _verCategorias;
        private string _sourceEmpresa;

        public PuntoPendienteSincronizadorEmp1(IFarmaciaService farmacia, ISisfarmaService fisiotes)
            : base(farmacia, fisiotes)
        {
            _aniosProcesados = new HashSet<int>();
        }

        public override void LoadConfiguration()
        {
            base.LoadConfiguration();
            _clasificacion = !string.IsNullOrWhiteSpace(ConfiguracionPredefinida[Configuracion.FIELD_TIPO_CLASIFICACION])
                ? ConfiguracionPredefinida[Configuracion.FIELD_TIPO_CLASIFICACION]
                : TIPO_CLASIFICACION_DEFAULT;
            _clasificacion = TIPO_CLASIFICACION_CATEGORIA;
            _copiarClientes = ConfiguracionPredefinida[Configuracion.FIELD_COPIAS_CLIENTES];
            _debeCopiarClientes = _copiarClientes.ToLower().Equals("si") || string.IsNullOrWhiteSpace(_copiarClientes);
            _filtrosResidencia = ConfiguracionPredefinida[Configuracion.FIELD_FILTROS_RESIDENCIA];
            _verCategorias = ConfiguracionPredefinida[Configuracion.FIELD_VER_CATEGORIAS];
        }

        public override void PreSincronizacion()
        {
            _codigoEmpresa = "00001";
            _timestampUltimaVenta = _sisfarma.PuntosPendientes.GetTimestampUltimaVentaByEmpresa(_codigoEmpresa);

            if (_timestampUltimaVenta == DateTime.MinValue)
                _timestampUltimaVenta = new DateTime(_anioInicio, 1, 1);
            _sourceEmpresa = _farmacia. Empresas.GetCodigoByNumero(1);        
        }

        public override void Process()
        {
            var cargarPuntosSisfarma = _cargarPuntos == "si";
            var ventas = _farmacia.Ventas.GetAllByDateTimeGreaterOrEqual(_anioInicio, _timestampUltimaVenta, _sourceEmpresa);
            if (!ventas.Any())
                return;

            var batchPuntosPendientes = new List<PuntosPendientes>();
            var batchVentasPendientes = new List<VentaPendiente>();

            foreach (var venta in ventas)
            {
                Task.Delay(5).Wait();
                _cancellationToken.ThrowIfCancellationRequested();

                if (venta.ClienteId > 0)
                    venta.Cliente = _farmacia.Clientes.GetOneOrDefaultById(venta.ClienteId, cargarPuntosSisfarma);

                if (venta.FechaFin.HasValue)
                {
                    //venta.VendedorNombre = _farmacia.Vendedores.GetOneOrDefaultById(venta.VendedorId)?.Nombre;
                    venta.Detalle = _farmacia.Ventas.GetDetalleDeVentaByVentaId(venta.Operacion, _sourceEmpresa);

                    if (venta.HasCliente() && _debeCopiarClientes)
                        InsertOrUpdateCliente(venta.Cliente);
                    var puntosPendientes = GenerarPuntosPendientes(venta);
                    batchPuntosPendientes.AddRange(puntosPendientes);
                }
                else
                {
                    batchVentasPendientes.Add(new VentaPendiente { idventa = venta.Operacion, empresa = _sourceEmpresa });
                }
            }

            if (batchPuntosPendientes.Any())
            {
                _sisfarma.PuntosPendientes.Sincronizar(batchPuntosPendientes);
                _timestampUltimaVenta = ventas.Last().FechaHora;
            }

            if (batchVentasPendientes.Any()) _sisfarma.Ventas.Sincronizar(batchVentasPendientes);
        }

        private IEnumerable<PuntosPendientes> GenerarPuntosPendientes(Venta venta)
        {
            if (!venta.HasDetalle()) return venta.TipoOperacion == "P"
                ? new PuntosPendientes[] { GenerarPuntoPendienteVentaSinDetalle(venta) }
                : new PuntosPendientes[0];

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
                    VentaId = $"{venta.Operacion}{_codigoEmpresa}".ToLongOrDefault(),
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

        private PuntosPendientes GenerarPuntoPendienteVentaSinDetalle(Venta venta)
        {
            return new PuntosPendientes
            {
                VentaId = $"{venta.Operacion}{_codigoEmpresa}".ToLongOrDefault(),
                LineaNumero = 0,
                CodigoBarra = string.Empty,
                CodigoNacional = string.Empty,
                Descripcion = "PAGO",

                Familia = string.Empty,
                SuperFamilia = string.Empty,
                SuperFamiliaAux = string.Empty,
                FamiliaAux = string.Empty,
                CambioClasificacion = _clasificacion == TIPO_CLASIFICACION_CATEGORIA ? 1 : 0,

                Cantidad = 0,
                Precio = 0,
                Pago = 0,
                TipoPago = venta.TipoOperacion,
                Fecha = venta.FechaHora.Date.ToDateInteger(),
                DNI = venta.Cliente?.Id.ToString() ?? "0",
                Cargado = _cargarPuntos.ToLower().Equals("si") ? "no" : "si",
                Puesto = $"{venta.Puesto}",
                Trabajador = !string.IsNullOrWhiteSpace(venta.VendedorCodigo) ? venta.VendedorCodigo.Trim() : string.Empty,
                LaboratorioCodigo = string.Empty,
                Laboratorio = string.Empty,
                Proveedor = string.Empty,
                Receta = string.Empty,
                FechaVenta = venta.FechaHora,
                PVP = 0,
                PUC = 0,
                Categoria = string.Empty,
                Subcategoria = string.Empty,
                VentaDescuento = venta.TotalDescuento,
                LineaDescuento = 0,
                TicketNumero = venta.Ticket?.Numero,
                Serie = venta.Ticket?.Serie ?? string.Empty,
                Sistema = SISTEMA_NIXFARMA,
                Ubicacion = string.Empty
            };
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

    public class PuntoPendienteSincronizadorEmp2 : DC.PuntoPendienteSincronizador
    {
        protected const string TIPO_CLASIFICACION_DEFAULT = "Familia";
        protected const string TIPO_CLASIFICACION_CATEGORIA = "Categoria";
        protected const string SISTEMA_NIXFARMA = "nixfarma";

        private string _clasificacion;
        private bool _debeCopiarClientes;
        private string _copiarClientes;
        private ICollection<int> _aniosProcesados;
        protected string _codigoEmpresa;
        protected DateTime _timestampUltimaVenta;
        private string _filtrosResidencia;
        private string _verCategorias;
        private string _sourceEmpresa;

        public PuntoPendienteSincronizadorEmp2(IFarmaciaService farmacia, ISisfarmaService fisiotes)
            : base(farmacia, fisiotes)
        {
            _aniosProcesados = new HashSet<int>();
        }

        public override void LoadConfiguration()
        {
            base.LoadConfiguration();
            _clasificacion = !string.IsNullOrWhiteSpace(ConfiguracionPredefinida[Configuracion.FIELD_TIPO_CLASIFICACION])
                ? ConfiguracionPredefinida[Configuracion.FIELD_TIPO_CLASIFICACION]
                : TIPO_CLASIFICACION_DEFAULT;
            _clasificacion = TIPO_CLASIFICACION_CATEGORIA;
            _copiarClientes = ConfiguracionPredefinida[Configuracion.FIELD_COPIAS_CLIENTES];
            _debeCopiarClientes = _copiarClientes.ToLower().Equals("si") || string.IsNullOrWhiteSpace(_copiarClientes);
            _filtrosResidencia = ConfiguracionPredefinida[Configuracion.FIELD_FILTROS_RESIDENCIA];
            _verCategorias = ConfiguracionPredefinida[Configuracion.FIELD_VER_CATEGORIAS];
        }

        public override void PreSincronizacion()
        {
            _codigoEmpresa = "00002";
            _timestampUltimaVenta = _sisfarma.PuntosPendientes.GetTimestampUltimaVentaByEmpresa(_codigoEmpresa);

            if (_timestampUltimaVenta == DateTime.MinValue)
                _timestampUltimaVenta = new DateTime(_anioInicio, 1, 1);

            _sourceEmpresa = _farmacia.Empresas.GetCodigoByNumero(2);
        }

        public override void Process()
        {
            var cargarPuntosSisfarma = _cargarPuntos == "si";
            var ventas = _farmacia.Ventas.GetAllByDateTimeGreaterOrEqual(_anioInicio, _timestampUltimaVenta, _sourceEmpresa);
            if (!ventas.Any())
                return;

            var batchPuntosPendientes = new List<PuntosPendientes>();
            var batchVentasPendientes = new List<VentaPendiente>();

            foreach (var venta in ventas)
            {
                Task.Delay(5).Wait();
                _cancellationToken.ThrowIfCancellationRequested();

                if (venta.ClienteId > 0)
                    venta.Cliente = _farmacia.Clientes.GetOneOrDefaultById(venta.ClienteId, cargarPuntosSisfarma);

                if (venta.FechaFin.HasValue)
                {
                    //venta.VendedorNombre = _farmacia.Vendedores.GetOneOrDefaultById(venta.VendedorId)?.Nombre;
                    venta.Detalle = _farmacia.Ventas.GetDetalleDeVentaByVentaId(venta.Operacion, _sourceEmpresa);

                    if (venta.HasCliente() && _debeCopiarClientes)
                        InsertOrUpdateCliente(venta.Cliente);
                    var puntosPendientes = GenerarPuntosPendientes(venta);
                    batchPuntosPendientes.AddRange(puntosPendientes);
                }
                else
                {
                    batchVentasPendientes.Add(new VentaPendiente { idventa = venta.Operacion, empresa = _sourceEmpresa });
                }
            }

            if (batchPuntosPendientes.Any())
            {
                _sisfarma.PuntosPendientes.Sincronizar(batchPuntosPendientes);
                _timestampUltimaVenta = ventas.Last().FechaHora;
            }

            if (batchVentasPendientes.Any()) _sisfarma.Ventas.Sincronizar(batchVentasPendientes);
        }

        private IEnumerable<PuntosPendientes> GenerarPuntosPendientes(Venta venta)
        {
            //if (!venta.HasCliente() && venta.Tipo != "1")
            //    return new PuntosPendientes[0];

            if (!venta.HasDetalle()) return venta.TipoOperacion == "P"
                 ? new PuntosPendientes[] { GenerarPuntoPendienteVentaSinDetalle(venta) }
                 : new PuntosPendientes[0];

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
                    VentaId = $"{venta.Operacion}{_codigoEmpresa}".ToLongOrDefault(),
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

        private PuntosPendientes GenerarPuntoPendienteVentaSinDetalle(Venta venta)
        {
            return new PuntosPendientes
            {
                VentaId = $"{venta.Operacion}{_codigoEmpresa}".ToLongOrDefault(),
                LineaNumero = 0,
                CodigoBarra = string.Empty,
                CodigoNacional = string.Empty,
                Descripcion = "PAGO",

                Familia = string.Empty,
                SuperFamilia = string.Empty,
                SuperFamiliaAux = string.Empty,
                FamiliaAux = string.Empty,
                CambioClasificacion = _clasificacion == TIPO_CLASIFICACION_CATEGORIA ? 1 : 0,

                Cantidad = 0,
                Precio = 0,
                Pago = 0,
                TipoPago = venta.TipoOperacion,
                Fecha = venta.FechaHora.Date.ToDateInteger(),
                DNI = venta.Cliente?.Id.ToString() ?? "0",
                Cargado = _cargarPuntos.ToLower().Equals("si") ? "no" : "si",
                Puesto = $"{venta.Puesto}",
                Trabajador = !string.IsNullOrWhiteSpace(venta.VendedorCodigo) ? venta.VendedorCodigo.Trim() : string.Empty,
                LaboratorioCodigo = string.Empty,
                Laboratorio = string.Empty,
                Proveedor = string.Empty,
                Receta = string.Empty,
                FechaVenta = venta.FechaHora,
                PVP = 0,
                PUC = 0,
                Categoria = string.Empty,
                Subcategoria = string.Empty,
                VentaDescuento = venta.TotalDescuento,
                LineaDescuento = 0,
                TicketNumero = venta.Ticket?.Numero,
                Serie = venta.Ticket?.Serie ?? string.Empty,
                Sistema = SISTEMA_NIXFARMA,
                Ubicacion = string.Empty
            };
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