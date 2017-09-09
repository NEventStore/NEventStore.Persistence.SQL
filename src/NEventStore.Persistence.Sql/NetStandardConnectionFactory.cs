using System;
using System.Data;
using System.Data.Common;

namespace NEventStore.Persistence.Sql
{
    public class NetStandardConnectionFactory : IConnectionFactory
    {
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
            throw new NotImplementedException();
        }
    }
}
