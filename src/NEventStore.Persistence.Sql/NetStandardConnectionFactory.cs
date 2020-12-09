﻿using Microsoft.Extensions.Logging;
using NEventStore.Logging;
using System;
using System.Data;
using System.Data.Common;

namespace NEventStore.Persistence.Sql
{
    public class NetStandardConnectionFactory : IConnectionFactory
    {
        private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(NetStandardConnectionFactory));

        private readonly DbProviderFactory _providerFactory;
        private readonly string _connectionString;

        public NetStandardConnectionFactory(DbProviderFactory providerFactory, string connectionString)
        {
            _providerFactory = providerFactory;
            _connectionString = connectionString;
        }

        public Type GetDbProviderFactoryType()
        {
            return _providerFactory.GetType();
        }

        public IDbConnection Open()
        {
            Logger.LogTrace(Messages.OpeningMasterConnection, _connectionString);
            return Open(_connectionString);
        }

        protected virtual IDbConnection Open(string connectionString)
        {
            return new ConnectionScope(connectionString, () => OpenConnection(connectionString));
        }

        protected virtual IDbConnection OpenConnection(string connectionString)
        {
            DbProviderFactory factory = _providerFactory;
            DbConnection connection = factory.CreateConnection();
            if (connection == null)
            {
                throw new ConfigurationErrorsException(Messages.BadConnectionName);
            }

            connection.ConnectionString = connectionString;

            try
            {
                Logger.LogTrace(Messages.OpeningConnection, connectionString);
                connection.Open();
            }
            catch (Exception e)
            {
                Logger.LogWarning(Messages.OpenFailed, connectionString);
                throw new StorageUnavailableException(e.Message, e);
            }

            return connection;
        }
    }
}
