﻿using System;
using System.Collections.Generic;
using Sisfarma.Sincronizador.Domain.Entities.Farmacia;

namespace Sisfarma.Sincronizador.Domain.Core.Repositories.Farmacia
{
    public interface IVentasRepository
    {
        List<Venta> GetAllByIdGreaterOrEqual(int year, long value, string empresa);

        List<Venta> GetAllByDateTimeGreaterOrEqual(int year, DateTime timestamp, string empresa);

        List<Venta> GetAllByIdGreaterOrEqual(long venta, DateTime fecha, string empresa);

        List<Venta> GetAllGteYearAndLteNumber(int year, long number);

        List<VentaDetalle> GetDetalleDeVentaByVentaId(long venta, string empresa);

        Venta GetOneOrDefaultById(long venta, string empresa, int anio);

        List<VentaDetalle> GetDetalleDeVentaPendienteByVentaId(long numeroVenta, string empresa);
    }
}