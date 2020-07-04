﻿using System;
using System.Collections.Generic;
using System.Linq;
using Sisfarma.Sincronizador.Core.Extensions;
using Sisfarma.Sincronizador.Domain.Core.ExternalServices.Fisiotes.DTO.VentasPendientes;
using Sisfarma.Sincronizador.Domain.Core.Services;
using Sisfarma.Sincronizador.Domain.Core.Sincronizadores.SuperTypes;
using Sisfarma.Sincronizador.Domain.Entities.Fisiotes;
using FAR = Sisfarma.Sincronizador.Domain.Entities.Farmacia;

namespace Sisfarma.Sincronizador.Unycop.Domain.Core.Sincronizadores
{
    public class VentaPendienteSincronizador : TaskSincronizador
    {
        protected const string TIPO_CLASIFICACION_CATEGORIA = "Categoria";
        protected const string TIPO_CLASIFICACION_DEFAULT = "Familia";
        protected const string FAMILIA_DEFAULT = "<Sin Clasificar>";
        protected const string LABORATORIO_DEFAULT = "<Sin Laboratorio>";
        protected const string VENDEDOR_DEFAULT = "NO";
        protected const string SISTEMA_NIXFARMA = "nixfarma";

        private string _clasificacion;
        private int _anioInicio;
        private string _verCategorias;
        private string _puntosDeSisfarma;
        private string _cargarPuntos;
        private bool _debeCopiarClientes;
        private string _copiarClientes;
        private bool _perteneceFarmazul;
        private string _filtrosResidencia;

        public VentaPendienteSincronizador(IFarmaciaService farmacia, ISisfarmaService fisiotes)
            : base(farmacia, fisiotes)
        { }

        public override void LoadConfiguration()
        {
            base.LoadConfiguration();
            _clasificacion = !string.IsNullOrWhiteSpace(ConfiguracionPredefinida[Configuracion.FIELD_TIPO_CLASIFICACION])
                ? ConfiguracionPredefinida[Configuracion.FIELD_TIPO_CLASIFICACION]
                : TIPO_CLASIFICACION_DEFAULT;
            _anioInicio = ConfiguracionPredefinida[Configuracion.FIELD_ANIO_INICIO]
                .ToIntegerOrDefault(@default: DateTime.Now.Year - 2);
            _verCategorias = ConfiguracionPredefinida[Configuracion.FIELD_VER_CATEGORIAS];
            _puntosDeSisfarma = ConfiguracionPredefinida[Configuracion.FIELD_PUNTOS_SISFARMA];
            _cargarPuntos = ConfiguracionPredefinida[Configuracion.FIELD_CARGAR_PUNTOS] ?? "no";
            _copiarClientes = ConfiguracionPredefinida[Configuracion.FIELD_COPIAS_CLIENTES];
            _debeCopiarClientes = _copiarClientes.ToLower().Equals("si") || string.IsNullOrWhiteSpace(_copiarClientes);
            _perteneceFarmazul = bool.Parse(ConfiguracionPredefinida[Configuracion.FIELD_ES_FARMAZUL]);
            _filtrosResidencia = ConfiguracionPredefinida[Configuracion.FIELD_FILTROS_RESIDENCIA];
        }

        public override void PreSincronizacion()
        {
            base.PreSincronizacion();
        }

        public override void Process()
        {
            var cargarPuntosSisfarma = _cargarPuntos == "si";
            var ventasPendientes = _sisfarma.Ventas.GetAllPendientes();
            foreach (var pendiente in ventasPendientes)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var numeroVenta = pendiente.idventa;
                var empresa = pendiente.empresa;

                var ventas = _farmacia.Ventas.GetAllByIdGreaterOrEqual(_anioInicio, numeroVenta, empresa);
                if (!ventas.Any()) continue;

                var batchPuntosPendientes = new List<PuntosPendientes>();
                var batchVentasPendientesDelete = new List<DeleteVentaPendiente>();
                foreach (var venta in ventas)
                {
                    var empresaSerial = empresa == "EMP1" ? "00001" : "00002";
                    var existe = _sisfarma.PuntosPendientes.Exists(long.Parse($"{numeroVenta}{empresaSerial}"));
                    if (!existe)
                    {
                        _sisfarma.Configuraciones.Update(Configuracion.FIELD_POR_DONDE_VOY_VENTAS_NO_INCLUIDAS, $"{numeroVenta}");
                        if (venta.ClienteId > 0)
                            venta.Cliente = _farmacia.Clientes.GetOneOrDefaultById(venta.ClienteId, cargarPuntosSisfarma);

                        if (venta.HasCliente() && _debeCopiarClientes)
                            InsertOrUpdateCliente(venta.Cliente);

                        venta.Detalle = _farmacia.Ventas.GetDetalleDeVentaByVentaId(venta.Operacion, empresa);
                        var puntosPendientes = GenerarPuntosPendientes(venta, empresaSerial);
                        batchPuntosPendientes.AddRange(puntosPendientes);
                    }
                    else batchVentasPendientesDelete.Add(new DeleteVentaPendiente { idventa = numeroVenta, empresa = empresa });
                }

                if (batchPuntosPendientes.Any()) _sisfarma.PuntosPendientes.Sincronizar(batchPuntosPendientes);
                if (batchVentasPendientesDelete.Any()) _sisfarma.Ventas.Sincronizar(batchVentasPendientesDelete);
            }
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

        private IEnumerable<PuntosPendientes> GenerarPuntosPendientes(FAR.Venta venta, string empresa)
        {
            if (!venta.HasDetalle()) return venta.TipoOperacion == "P"
                ? new PuntosPendientes[] { GenerarPuntoPendienteVentaSinDetalle(venta, empresa) }
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
                    VentaId = $"{venta.Operacion}{empresa}".ToLongOrDefault(),
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

        private PuntosPendientes GenerarPuntoPendienteVentaSinDetalle(FAR.Venta venta, string empresa)
        {
            return new PuntosPendientes
            {
                VentaId = $"{venta.Operacion}{empresa}".ToLongOrDefault(),
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
                TicketNumero = 0,
                Serie = string.Empty,
                Sistema = SISTEMA_NIXFARMA,
                Ubicacion = string.Empty
            };
        }
    }
}