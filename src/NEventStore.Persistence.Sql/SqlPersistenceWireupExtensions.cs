// ReSharper disable once CheckNamespace
namespace NEventStore
{
    using NEventStore.Persistence.Sql;
    using System.Data.Common;

    public static class SqlPersistenceWireupExtensions
    {
        // netstandard does not have support for DbFactoryProviders, we need a totally different way to initialize the driver
#if NET461
        public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, string connectionName)
        {
            var factory = new ConfigurationConnectionFactory(connectionName);
            return wireup.UsingSqlPersistence(factory);
        }

        public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, string connectionName, string providerName, string connectionString)
        {
            var factory = new ConfigurationConnectionFactory(connectionName, providerName, connectionString);
            return wireup.UsingSqlPersistence(factory);
        }
#endif

        public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, DbProviderFactory providerFactory, string connectionString)
        {
            var factory = new NetStandardConnectionFactory(providerFactory, connectionString);
            return wireup.UsingSqlPersistence(factory);
        }

        public static SqlPersistenceWireup UsingSqlPersistence(this Wireup wireup, IConnectionFactory factory)
        {
#if NET461
            // init the global seetings if needed
            int timeout = 0;
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["NEventStore.SqlCommand.Timeout"], out timeout))
            {
                Settings.CommandTimeout = timeout;
            }
#endif
            return new SqlPersistenceWireup(wireup, factory);
        }

        public static SqlPersistenceWireup WithCommandTimeout(this SqlPersistenceWireup wireup, int commandTimeout)
        {
            Settings.CommandTimeout = commandTimeout;
            return wireup;
        }
    }
}