using System;
using System.Collections.Generic;
using System.Linq;
using Sisfarma.Sincronizador.Nixfarma.Infrastructure.Data;
using CR = Sisfarma.Sincronizador.Domain.Core.Repositories.Farmacia;

namespace Sisfarma.Sincronizador.Nixfarma.Infrastructure.Repositories.Farmacia
{
    public interface IEmpresaRepository : CR.IEmpresaRepository
    {
        int Count();
    }

    public class EmpresaRepository : IEmpresaRepository
    {
        public int Count()
        {
            var conn = FarmaciaContext.GetConnection();
            try
            {
                var sql = $@"SELECT distinct emp_codigo FROM appul.ab_articulos";
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var reader = cmd.ExecuteReader();

                if (!reader.HasRows)
                {
                    reader.Close();
                    reader.Dispose();
                    return 1;
                }
                    

                var count = 0;
                while (reader.Read())
                {
                    count++;
                }

                reader.Close();
                reader.Dispose();
                return count;
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

        public string GetCodigoByNumero(int numero)
        {
            var conn = FarmaciaContext.GetConnection();
            try
            {
                var sql = $@"SELECT distinct EMP_CODIGO FROM appul.ab_articulos order by emp_codigo asc";
                conn.Open();
                var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                var reader = cmd.ExecuteReader();
                
                var empresas = new List<string>();
                while (reader.Read())
                {
                    var codigo = Convert.ToString(reader["EMP_CODIGO"]);
                    empresas.Add(codigo);                 
                }
                reader.Close();
                reader.Dispose();

                if (!empresas.Any())
                    return "EMP1";

                if (numero == 1)
                    return empresas.First();


                return empresas.Count == 2 ? empresas.Last() : "EMP2";                
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