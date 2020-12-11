// netstandard does not have support for DbFactoryProviders, we need a totally different way to initialize the driver
#if NET461
namespace NEventStore.Persistence.Sql
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Data.Common;
    using System.Linq;
    using Microsoft.Extensions.Logging;
    using NEventStore.Logging;

    public class ConfigurationConnectionFactory : IConnectionFactory
    {
        private const string DefaultConnectionName = "NEventStore";

        private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(ConfigurationConnectionFactory));

        private static readonly IDictionary<string, ConnectionStringSettings> CachedSettings =
            new Dictionary<string, ConnectionStringSettings>();

        private static readonly IDictionary<string, DbProviderFactory> CachedFactories =
            new Dictionary<string, DbProviderFactory>();

        private readonly string _connectionName;
        private readonly ConnectionStringSettings _connectionStringSettings;

        public ConfigurationConnectionFactory(string connectionName)
        {
            _connectionName = connectionName ?? DefaultConnectionName;
            Logger.LogDebug(Messages.ConfiguringConnections, _connectionName);
        }

        public ConfigurationConnectionFactory(string connectionName, string providerName, string connectionString)
            : this(connectionName)
        {
            _connectionStringSettings = new ConnectionStringSettings(_connectionName, connectionString, providerName);
        }

        public virtual ConnectionStringSettings Settings
        {
            get { return GetConnectionStringSettings(_connectionName); }
        }

        public virtual IDbConnection Open()
        {
            Logger.LogTrace(Messages.OpeningMasterConnection, _connectionName);
            return Open(_connectionName);
        }

        public Type GetDbProviderFactoryType()
        {
            DbProviderFactory factory = GetFactory(Settings);
            return factory.GetType();
        }

        protected virtual IDbConnection Open(string connectionName)
        {
            ConnectionStringSettings setting = GetSetting(connectionName);
            string connectionString = setting.ConnectionString;
            return new ConnectionScope(connectionString, () => Open(connectionString, setting));
        }

        protected virtual IDbConnection Open(string connectionString, ConnectionStringSettings setting)
        {
            DbProviderFactory factory = GetFactory(setting);
            DbConnection connection = factory.CreateConnection();
            if (connection == null)
            {
                throw new ConfigurationErrorsException(Messages.BadConnectionName);
            }

            connection.ConnectionString = connectionString;

            try
            {
                Logger.LogTrace(Messages.OpeningConnection, setting.Name);
                connection.Open();
            }
            catch (Exception e)
            {
                Logger.LogWarning(Messages.OpenFailed, setting.Name);
                throw new StorageUnavailableException(e.Message, e);
            }

            return connection;
        }

        protected virtual ConnectionStringSettings GetSetting(string connectionName)
        {
            lock (CachedSettings)
            {
                if (CachedSettings.TryGetValue(connectionName, out ConnectionStringSettings setting))
                {
                    return setting;
                }

                setting = GetConnectionStringSettings(connectionName);
                return CachedSettings[connectionName] = setting;
            }
        }

        protected virtual DbProviderFactory GetFactory(ConnectionStringSettings setting)
        {
            lock (CachedFactories)
            {
                if (CachedFactories.TryGetValue(setting.Name, out DbProviderFactory factory))
                {
                    return factory;
                }
                factory = DbProviderFactories.GetFactory(setting.ProviderName);
                Logger.LogDebug(Messages.DiscoveredConnectionProvider, setting.Name, factory.GetType());
                return CachedFactories[setting.Name] = factory;
            }
        }

        protected virtual ConnectionStringSettings GetConnectionStringSettings(string connectionName)
        {
            Logger.LogDebug(Messages.DiscoveringConnectionSettings, connectionName);

            ConnectionStringSettings settings = _connectionStringSettings ?? ConfigurationManager.ConnectionStrings
                                                                    .Cast<ConnectionStringSettings>()
                                                                    .FirstOrDefault(x => x.Name == connectionName);

            if (settings == null)
            {
                throw new ConfigurationErrorsException(Messages.ConnectionNotFound.FormatWith(connectionName));
            }

            if ((settings.ConnectionString ?? string.Empty).Trim().Length == 0)
            {
                throw new ConfigurationErrorsException(Messages.MissingConnectionString.FormatWith(connectionName));
            }

            if ((settings.ProviderName ?? string.Empty).Trim().Length == 0)
            {
                throw new ConfigurationErrorsException(Messages.MissingProviderName.FormatWith(connectionName));
            }

            return settings;
        }
    }
}
#endif