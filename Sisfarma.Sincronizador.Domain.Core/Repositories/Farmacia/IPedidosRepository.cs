﻿using Sisfarma.Sincronizador.Domain.Entities.Farmacia;
using System;
using System.Collections.Generic;

namespace Sisfarma.Sincronizador.Domain.Core.Repositories.Farmacia
{
    public interface IPedidosRepository
    {
        IEnumerable<Pedido> GetAllByFechaGreaterOrEqual(DateTime dateTime);

        IEnumerable<Pedido> GetAllByIdGreaterOrEqual(long idPedido, DateTime dateTime);

        IEnumerable<PedidoDetalle> GetAllDetalleByPedido(long numero, string empresa, int anio);
    }
}