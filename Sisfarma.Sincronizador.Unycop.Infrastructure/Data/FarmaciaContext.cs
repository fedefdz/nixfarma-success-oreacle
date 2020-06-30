﻿//using Oracle.DataAccess.Client;
using Oracle.DataAccess.Client;
//using Oracle.ManagedDataAccess.Client;
using Sisfarma.Sincronizador.Core.Config;
using System;
using System.Data.Entity;
using System.Data.Odbc;
using System.Data.OleDb;

namespace Sisfarma.Sincronizador.Nixfarma.Infrastructure.Data
{
    public class FarmaciaContext : DbContext
    {
        private OracleConnection OracleConnection { get; set; }

        public FarmaciaContext(string server, string database, string username, string password)
            : base("OracleDbContext")
        //: base($@"providerName=""Oracle.ManagedDataAccess.Client"" connectionString=""User Id ={username}; Password={password};Data Source ={server}/{database}""")
        { }

        public static FarmaciaContext Create(LocalConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return new FarmaciaContext(config.Server, config.Database, config.Username, config.Password);
        }

        private static string _localServer = "";
        private static string _user = "";
        private static string _password = "";

        public FarmaciaContext()
        {
        }

        public static int ListaDeArticulo { get; set; }

        public static void Setup(string localServer, string user, string password, int listaDeArticulo)
        {
            if (string.IsNullOrWhiteSpace(localServer))
                throw new System.ArgumentException("message", nameof(localServer));

            _localServer = localServer;
            _user = user ?? throw new System.ArgumentNullException(nameof(user));
            _password = password ?? throw new System.ArgumentNullException(nameof(password));

            ListaDeArticulo = listaDeArticulo;
        }

        //public static OdbcConnection GetConnection()
        public static OracleConnection GetConnection()
        {
            //string connectionString = $@"User Id=""{_user.ToUpper()}""; Password=""{_password}""; Enlist=false; Pooling=true;" +
            //    @"Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=IPC)(KEY=DP9))" +
            //        $@"(ADDRESS=(PROTOCOL=TCP)(HOST={_localServer})(PORT=1521)))(CONNECT_DATA=(INSTANCE_NAME=DP9)(SERVICE_NAME=ORACLE9)))";

            //ORA-12504: TNS:el listener no ha recibido el SERVICE_NAME en CONNECT_DATA
            //string connectionString = $@"User Id=""{_user.ToUpper()}""; Password=""{_password}""; Enlist=false; Pooling=true;" +
            //    $@"Data Source={_localServer.ToUpper()};Min Pool Size=10;Connection Lifetime=120;Connection Timeout=60;Incr Pool Size=5;Decr Pool Size=2;";

            // si falla probar con _localServer = SERVERDATOS
            // ORA-12514: TNS:el listener no conoce actualmente el servicio solicitado en el descriptor de conexión
            // ORA-12545: La conexión ha fallado porque el host destino o el objeto no existen
            //string connectionString = $@"User Id=""{_user.ToUpper()}""; Password=""{_password}""; Enlist=false; Pooling=true;" +
            //    @"Data Source=(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=IPC)(KEY=DP11))" +
            //        $@"(ADDRESS=(PROTOCOL=TCP)(HOST=SERVERDATOS)(PORT=1521)))(CONNECT_DATA=(DEDICATED)(INSTANCE_NAME=DP11)(SERVICE_NAME=ORACLE11)))";

            // connection string VB
            //si falla probar con _localServer = SERVERDATOS
            // 'Driver' es un atributo de cadena de conexión no válido
            //string connectionString = $@"Driver={{Microsoft ODBC for Oracle}};Server={_localServer};Uid={_user.ToUpper()};Pwd={_password};";

            string connectionString = $@"Data Source={_localServer};User ID={_user.ToUpper()};Password={_password};";

            //var conn = new OdbcConnection(connectionString);
            var conn = new OracleConnection(connectionString);

            return conn;
        }
    }

    [Serializable]
    public class FarmaciaContextException : Exception
    {
        public FarmaciaContextException()
        {
        }
    }
}