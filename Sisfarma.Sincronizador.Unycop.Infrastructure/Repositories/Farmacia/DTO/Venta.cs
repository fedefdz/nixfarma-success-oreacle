﻿using System;

namespace Sisfarma.Sincronizador.Nixfarma.Infrastructure.Repositories.Farmacia.DTO
{
    public class Venta
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; }

        public int Puesto { get; set; }        
        
        public int Cliente { get; set; }

        public byte Vendedor { get; set; }
        
        public int Descuento { get; set; }

        public int Pago { get; set; }

        public byte Tipo { get; set; }

        public int Importe { get; set; }
    }
}
