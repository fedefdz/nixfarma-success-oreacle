﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Sisfarma.Sincronizador.Core.Extensions;
using Sisfarma.Sincronizador.Domain.Core.Services;
using Sisfarma.Sincronizador.Domain.Entities.Fisiotes;
using CORE = Sisfarma.Sincronizador.Domain.Core.Sincronizadores;
using FAR = Sisfarma.Sincronizador.Domain.Entities.Farmacia;

namespace Sisfarma.Sincronizador.Unycop.Domain.Core.Sincronizadores
{
    public class HuecoSincronizador : CORE.HuecoSincronizador
    {
        private string _cargarPuntos;

        private string _filtrosResidencia;

        public HuecoSincronizador(IFarmaciaService farmacia, ISisfarmaService fisiotes)
            : base(farmacia, fisiotes)
        { }

        public override void LoadConfiguration()
        {
            base.LoadConfiguration();
            _cargarPuntos = ConfiguracionPredefinida[Configuracion.FIELD_CARGAR_PUNTOS] ?? "no";
            _filtrosResidencia = ConfiguracionPredefinida[Configuracion.FIELD_FILTROS_RESIDENCIA];
        }

        public override void PreSincronizacion()
        {
            base.PreSincronizacion();
        }

        public override void Process()
        {
            var remoteHuecos = _sisfarma.Huecos.GetByOrderAsc();

            foreach (var hueco in remoteHuecos)
            {
                Task.Delay(5);

                _cancellationToken.ThrowIfCancellationRequested();

                var cargarPuntosSisfarma = _cargarPuntos == "si";
                var cliente = _farmacia.Clientes.GetOneOrDefaultById(hueco.ToLongOrDefault(), cargarPuntosSisfarma);
                if (cliente != null)
                    InsertOrUpdateCliente(cliente);
            }
        }

        private void InsertOrUpdateCliente(FAR.Cliente cliente)
        {
            cliente.DebeCargarPuntos = _debeCargarPuntos;
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