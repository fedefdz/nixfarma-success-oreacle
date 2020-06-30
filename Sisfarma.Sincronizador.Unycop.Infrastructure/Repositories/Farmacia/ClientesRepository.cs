﻿//using Oracle.DataAccess.Client;
using Sisfarma.Sincronizador.Core.Config;
using Sisfarma.Sincronizador.Domain.Core.Repositories.Farmacia;
using Sisfarma.Sincronizador.Domain.Entities.Farmacia;
using Sisfarma.Sincronizador.Nixfarma.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sisfarma.Sincronizador.Nixfarma.Infrastructure.Repositories.Farmacia
{
    public class ClientesRepository : FarmaciaRepository, IClientesRepository
    {
        private readonly bool _premium;

        public ClientesRepository(LocalConfig config, bool premium)
            : base(config) => _premium = premium;

        public ClientesRepository()
        {
            _premium = false;
        }

        public List<Cliente> GetGreatThanIdAsDTO(long id, bool cargarPuntosSisfarma)
        {
            var conn = FarmaciaContext.GetConnection();            

            try
            {
                var sql = $@"SELECT * FROM (SELECT CODIGO,
                                APELLIDOS, NOMBRE, NIF, SEXO,
                                DIRECCION, CODIGO_POSTAL, POBLACION,
                                TEL_MOVIL, TELEFONO_1, TELEFONO_2, E_MAIL, DTO_PUNTOS_E,
                                FECHA_ALTA, FECHA_BAJA, FECHA_NTO,
                                OPE_APLICA, TIP_CODIGO, DES_CODIGO, AUTORIZA_COMERCIAL
                                FROM appul.ag_clientes
                                    WHERE codigo > {id}
                                ORDER BY codigo) WHERE ROWNUM <= 999";

                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var reader = cmd.ExecuteReader();

                var clientes = new List<Cliente>();
                while (reader.Read())
                {
                    var apellidos = Convert.ToString(reader["APELLIDOS"]);
                    var nombre = Convert.ToString(reader["NOMBRE"]);
                    var nif = Convert.ToString(reader["NIF"]);
                    var sexo = Convert.ToString(reader["SEXO"]);

                    var direccion = Convert.ToString(reader["DIRECCION"]);
                    var codigoPostal = !Convert.IsDBNull(reader["CODIGO_POSTAL"]) ? (int?)Convert.ToInt32(reader["CODIGO_POSTAL"]) : null;
                    var poblacion = Convert.ToString(reader["POBLACION"]);

                    var telMovil = Convert.ToString(reader["TEL_MOVIL"]);
                    var telefono1 = Convert.ToString(reader["TELEFONO_1"]);
                    var telefono2 = Convert.ToString(reader["TELEFONO_2"]);
                    var eEmail = Convert.ToString(reader["E_MAIL"]);
                    var dtoPuntosE = !Convert.IsDBNull(reader["DTO_PUNTOS_E"]) ? (decimal)Convert.ToDecimal(reader["DTO_PUNTOS_E"]) : 0;

                    var fechaAlta = !Convert.IsDBNull(reader["FECHA_ALTA"]) ? (DateTime?)Convert.ToDateTime(reader["FECHA_ALTA"]) : null;
                    var fechaBaja = !Convert.IsDBNull(reader["FECHA_BAJA"]) ? (DateTime?)Convert.ToDateTime(reader["FECHA_BAJA"]) : null;
                    var fechaNto = !Convert.IsDBNull(reader["FECHA_NTO"]) ? (DateTime?)Convert.ToDateTime(reader["FECHA_NTO"]) : null;

                    var opeAplica = Convert.ToString(reader["OPE_APLICA"]);
                    var tipCodigo = !Convert.IsDBNull(reader["TIP_CODIGO"]) ? (long?)Convert.ToInt32(reader["TIP_CODIGO"]) : null;
                    var desCodigo = !Convert.IsDBNull(reader["DES_CODIGO"]) ? (long?)Convert.ToInt32(reader["DES_CODIGO"]) : null;
                    var autorizaComercial = Convert.ToString(reader["AUTORIZA_COMERCIAL"]);

                    var codigo = Convert.ToInt64(reader["CODIGO"]);

                    var cliente = new Cliente
                    {
                        Id = codigo,
                        NombreCompleto = $"{apellidos} {nombre}".Trim(),
                        NumeroIdentificacion = string.IsNullOrWhiteSpace(nif) ? string.Empty : nif.Trim(),
                        Sexo = string.IsNullOrWhiteSpace(sexo) ? string.Empty
                            : sexo.ToUpper() == "H" ? "Hombre"
                            : sexo.ToUpper() == "M" ? "Mujer" : string.Empty,

                        Direccion = string.IsNullOrWhiteSpace(direccion) ? string.Empty
                            : $"{direccion}{(codigoPostal.HasValue ? $" - {codigoPostal.Value}" : string.Empty)}{(string.IsNullOrWhiteSpace(poblacion) ? string.Empty : $" - {poblacion}")}",

                        Telefono = !string.IsNullOrWhiteSpace(telefono1) ? telefono1.Trim()
                            : !string.IsNullOrWhiteSpace(telefono2) ? telefono2.Trim()
                            : string.Empty,

                        Celular = !string.IsNullOrWhiteSpace(telMovil) ? telMovil.Trim() : string.Empty,
                        Email = !string.IsNullOrWhiteSpace(eEmail) ? eEmail.Trim() : string.Empty,

                        FechaAlta = fechaAlta,
                        Baja = fechaBaja.HasValue,
                        FechaNacimiento = fechaNto,
                        Trabajador = !string.IsNullOrWhiteSpace(opeAplica) ? opeAplica.Trim() : string.Empty,
                        CodigoCliente = tipCodigo ?? 0,
                        CodigoDes = desCodigo ?? 0,
                        LOPD = autorizaComercial == "A",

                        Tarjeta = string.Empty,
                        Puntos = 0,
                    };

                    // cargamos puntos
                    if (cargarPuntosSisfarma)
                    {
                        sql = $@"SELECT NVL(SUM(imp_acumulado),0) as PUNTOS FROM appul.ag_reg_vta_fidel WHERE cod_cliente = {codigo}";
                        cmd.CommandText = sql;
                        var readerPuntos = cmd.ExecuteReader();

                        if (readerPuntos.Read())
                        {
                            var puntosAcumulados = Convert.ToDecimal(readerPuntos["PUNTOS"]);
                            cliente.Puntos = puntosAcumulados - dtoPuntosE;
                        }

                        readerPuntos.Close();
                        readerPuntos.Dispose();
                    }

                    // buscamos tarjeta asociada
                    sql = $@"SELECT CODIGO_ID FROM appul.ag_tarjetas WHERE cod_cliente = {codigo}";
                    cmd.CommandText = sql;
                    var readerTarjetas = cmd.ExecuteReader();

                    if (readerTarjetas.Read())
                    {
                        var tarjeta = Convert.ToString(readerTarjetas["CODIGO_ID"]);
                        cliente.Tarjeta = !string.IsNullOrWhiteSpace(tarjeta) ? tarjeta.Trim() : string.Empty;
                    }

                    readerTarjetas.Close();
                    readerTarjetas.Dispose();

                    clientes.Add(cliente);
                }

                reader.Close();
                reader.Dispose();

                return clientes;
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }
        }

        public Cliente GetOneOrDefaultById(long id, bool cargarPuntosSisfarma)
        {
            var conn = FarmaciaContext.GetConnection();

            try
            {
                var sql = $@"SELECT
                                APELLIDOS, NOMBRE, NIF, SEXO,
                                DIRECCION, CODIGO_POSTAL, POBLACION,
                                TEL_MOVIL, TELEFONO_1, TELEFONO_2, E_MAIL, DTO_PUNTOS_E,
                                FECHA_ALTA, FECHA_BAJA, FECHA_NTO,
                                OPE_APLICA, TIP_CODIGO, DES_CODIGO, AUTORIZA_COMERCIAL
                                FROM appul.ag_clientes WHERE codigo = {id}";

                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var reader = cmd.ExecuteReader();

                if (!reader.Read())
                    return default(Cliente);

                var apellidos = Convert.ToString(reader["APELLIDOS"]);
                var nombre = Convert.ToString(reader["NOMBRE"]);
                var nif = Convert.ToString(reader["NIF"]);
                var sexo = Convert.ToString(reader["SEXO"]);

                var direccion = Convert.ToString(reader["DIRECCION"]);
                var codigoPostal = !Convert.IsDBNull(reader["CODIGO_POSTAL"]) ? (int?)Convert.ToInt32(reader["CODIGO_POSTAL"]) : null;
                var poblacion = Convert.ToString(reader["POBLACION"]);

                var telMovil = Convert.ToString(reader["TEL_MOVIL"]);
                var telefono1 = Convert.ToString(reader["TELEFONO_1"]);
                var telefono2 = Convert.ToString(reader["TELEFONO_2"]);
                var eEmail = Convert.ToString(reader["E_MAIL"]);
                var dtoPuntosE = !Convert.IsDBNull(reader["DTO_PUNTOS_E"]) ? (decimal)Convert.ToDecimal(reader["DTO_PUNTOS_E"]) : 0;

                var fechaAlta = !Convert.IsDBNull(reader["FECHA_ALTA"]) ? (DateTime?)Convert.ToDateTime(reader["FECHA_ALTA"]) : null;
                var fechaBaja = !Convert.IsDBNull(reader["FECHA_BAJA"]) ? (DateTime?)Convert.ToDateTime(reader["FECHA_BAJA"]) : null;
                var fechaNto = !Convert.IsDBNull(reader["FECHA_NTO"]) ? (DateTime?)Convert.ToDateTime(reader["FECHA_NTO"]) : null;

                var opeAplica = Convert.ToString(reader["OPE_APLICA"]);
                var tipCodigo = !Convert.IsDBNull(reader["TIP_CODIGO"]) ? (long?)Convert.ToInt32(reader["TIP_CODIGO"]) : null;
                var desCodigo = !Convert.IsDBNull(reader["DES_CODIGO"]) ? (long?)Convert.ToInt32(reader["DES_CODIGO"]) : null;
                var autorizaComercial = Convert.ToString(reader["AUTORIZA_COMERCIAL"]);

                var cliente = new Cliente
                {
                    Id = id,
                    NombreCompleto = $"{apellidos} {nombre}".Trim(),
                    NumeroIdentificacion = string.IsNullOrWhiteSpace(nif) ? string.Empty : nif.Trim(),
                    Sexo = string.IsNullOrWhiteSpace(sexo) ? string.Empty
                        : sexo.ToUpper() == "H" ? "Hombre"
                        : sexo.ToUpper() == "M" ? "Mujer" : string.Empty,

                    Direccion = string.IsNullOrWhiteSpace(direccion) ? string.Empty
                        : $"{direccion}{(codigoPostal.HasValue ? $" - {codigoPostal.Value}" : string.Empty)}{(string.IsNullOrWhiteSpace(poblacion) ? string.Empty : $" - {poblacion}")}",

                    Telefono = !string.IsNullOrWhiteSpace(telefono1) ? telefono1.Trim()
                        : !string.IsNullOrWhiteSpace(telefono2) ? telefono2.Trim()
                        : string.Empty,

                    Celular = !string.IsNullOrWhiteSpace(telMovil) ? telMovil.Trim() : string.Empty,
                    Email = !string.IsNullOrWhiteSpace(eEmail) ? eEmail.Trim() : string.Empty,

                    FechaAlta = fechaAlta,
                    Baja = fechaBaja.HasValue,
                    FechaNacimiento = fechaNto,
                    Trabajador = !string.IsNullOrWhiteSpace(opeAplica) ? opeAplica.Trim() : string.Empty,
                    CodigoCliente = tipCodigo ?? 0,
                    CodigoDes = desCodigo ?? 0,
                    LOPD = autorizaComercial == "A",

                    Tarjeta = string.Empty,
                    Puntos = 0,
                };

                reader.Close();

                // cargamos puntos
                if (cargarPuntosSisfarma)
                {
                    sql = $@"SELECT NVL(SUM(imp_acumulado),0) as PUNTOS FROM appul.ag_reg_vta_fidel WHERE cod_cliente = {id}";
                    cmd.CommandText = sql;
                    
                    reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        var puntosAcumulados = Convert.ToDecimal(reader["PUNTOS"]);
                        cliente.Puntos = puntosAcumulados - dtoPuntosE;
                    }

                    reader.Close();
                }

                // buscamos tarjeta asociada
                sql = $@"SELECT CODIGO_ID FROM appul.ag_tarjetas WHERE cod_cliente = {id}";
                cmd.CommandText = sql;
                reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    var tarjeta = Convert.ToString(reader["CODIGO_ID"]);
                    cliente.Tarjeta = !string.IsNullOrWhiteSpace(tarjeta) ? tarjeta.Trim() : string.Empty;
                }

                reader.Close();

                reader.Dispose();

                return cliente;
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }
        }

        public bool Exists(int id)
            => GetOneOrDefaultById(id, false) != null;

        public bool EsBeBlue(string cliente, string tarifaDescuento)
        {
            var conn = FarmaciaContext.GetConnection();

            try
            {
                var sql = $"SELECT DESCRIPCION FROM appul.ag_tipos WHERE codigo = {cliente}";
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var reader = cmd.ExecuteReader();

                var aux = string.Empty;
                if (reader.Read())
                    aux = Convert.ToString(reader["DESCRIPCION"]);

                if (aux.Trim().ToLower() == "farmazul")
                    return true;

                reader.Close();

                sql = $"SELECT DESCRIPCION FROM appul.ab_descuentos WHERE codigo = {tarifaDescuento}";
                cmd.CommandText = sql;
                reader = cmd.ExecuteReader();
                aux = string.Empty;
                if (reader.Read())
                    aux = Convert.ToString(reader["DESCRIPCION"]);

                reader.Close();
                reader.Dispose();
                return aux.Trim().ToLower() == "farmazul";
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }
        }

        public string EsResidencia(string tipo, string descuento, string filtros)
        {
            var conn = FarmaciaContext.GetConnection();
            try
            {
                if (!string.IsNullOrWhiteSpace(filtros))
                {
                    var sql = $"SELECT DESCRIPCION FROM appul.ag_tipos WHERE codigo = {tipo}";
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    var reader = cmd.ExecuteReader();
                    if (!reader.Read())
                    {
                        reader.Close();
                        reader.Dispose();
                        return "cliente";
                    }
                        

                    var aFiltros = filtros.Split(',');
                    var aux = Convert.ToString(reader["DESCRIPCION"]);

                    reader.Close();
                    reader.Dispose();

                    if (aFiltros.Contains(aux))
                        return "residencia";

                    return "cliente";
                }
                else
                {
                    var sql = $"SELECT DESCRIPCION FROM appul.ab_descuentos WHERE codigo = {descuento}";
                    conn.Open();
                    var cmd = conn.CreateCommand();
                    cmd.CommandText = sql;
                    var reader = cmd.ExecuteReader();

                    var aux = string.Empty;
                    if (reader.Read())
                        aux = Convert.ToString(reader["DESCRIPCION"]);


                    reader.Close();
                    reader.Dispose();

                    if (aux.ToLower().Contains("residencias"))
                        return "residencia";

                    return "cliente";
                }
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                conn.Close();
                conn.Dispose();
            }
        }
    }
}