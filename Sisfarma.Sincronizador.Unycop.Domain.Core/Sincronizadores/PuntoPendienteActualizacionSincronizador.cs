using System;
using Sisfarma.Sincronizador.Core.Extensions;
using Sisfarma.Sincronizador.Domain.Core.Services;
using Sisfarma.Sincronizador.Domain.Entities.Fisiotes;
using Sisfarma.Sincronizador.Infrastructure.Fisiotes.DTO;
using DC = Sisfarma.Sincronizador.Domain.Core.Sincronizadores;

namespace Sisfarma.Sincronizador.Unycop.Domain.Core.Sincronizadores
{
    public class PuntoPendienteActualizacionSincronizador : DC.PuntoPendienteActualizacionSincronizador
    {
        private int _anioInicio;
        private string _empresaUno;
        private string _empresaDos;

        public PuntoPendienteActualizacionSincronizador(IFarmaciaService farmacia, ISisfarmaService fisiotes)
            : base(farmacia, fisiotes)
        { }

        public override void LoadConfiguration()
        {
            base.LoadConfiguration();
            _anioInicio = ConfiguracionPredefinida[Configuracion.FIELD_ANIO_INICIO]
                .ToIntegerOrDefault(@default: DateTime.Now.Year - 2);
        }

        public override void PreSincronizacion()
        {
            base.PreSincronizacion();
            _empresaUno = _farmacia.Empresas.GetCodigoByNumero(1);
            _empresaDos = _farmacia.Empresas.GetCodigoByNumero(2);
        }

        public override void Process()
        {
            var puntos = _sisfarma.PuntosPendientes.GetWithoutRedencion();
            foreach (var pto in puntos)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var ventaSerial = pto.VentaId.ToString();
                var numeroVenta = long.Parse(ventaSerial.SubstringEnd(5));
                var empresa = ventaSerial.Substring(ventaSerial.Length - 5) == "00001" ? _empresaUno : _empresaDos;
                var venta = _farmacia.Ventas.GetOneOrDefaultById(numeroVenta, empresa, _anioInicio);
                if (venta != null)
                {
                    var detalle = _farmacia.Ventas.GetDetalleDeVentaPendienteByVentaId(numeroVenta, empresa);
                    foreach (var item in detalle)
                    {
                        if (item.Situacion != "A")
                        {
                            _sisfarma.PuntosPendientes.Sincronizar(new UpdatePuntuacion
                            {
                                tipoPago = venta.TipoOperacion,
                                proveedor = item.Farmaco.Proveedor?.Nombre ?? string.Empty,
                                idventa = pto.VentaId,
                                idnlinea = item.Linea,
                            });
                        }
                        else _sisfarma.PuntosPendientes.Sincronizar(new DeletePuntuacion { idventa = pto.VentaId, idnlinea = item.Linea });
                    }
                }
                // TODO aqui podría fallar la api
                else _sisfarma.PuntosPendientes.Sincronizar(new DeletePuntuacion { idventa = pto.VentaId });
            }
        }
    }
}