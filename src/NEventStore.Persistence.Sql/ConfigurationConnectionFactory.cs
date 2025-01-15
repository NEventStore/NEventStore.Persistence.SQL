// netstandard does not have support for DbFactoryProviders, we need a totally different way to initialize the driver
#if NET462

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.Extensions.Logging;
using NEventStore.Logging;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Connection factory that uses configuration settings to create connections.
	/// </summary>
	public class ConfigurationConnectionFactory : IConnectionFactory
	{
		private const string DefaultConnectionName = "NEventStore";

		private static readonly ILogger Logger = LogFactory.BuildLogger(typeof(ConfigurationConnectionFactory));

		private static readonly IDictionary<string, ConnectionStringSettings> CachedSettings =
			new Dictionary<string, ConnectionStringSettings>();

		private static readonly IDictionary<string, DbProviderFactory> CachedFactories =
			new Dictionary<string, DbProviderFactory>();

		private readonly string _connectionName;
		private readonly ConnectionStringSettings? _connectionStringSettings;

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationConnectionFactory"/> class.
		/// </summary>
		public ConfigurationConnectionFactory(string connectionName)
		{
			_connectionName = connectionName ?? DefaultConnectionName;
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.ConfiguringConnections, _connectionName);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigurationConnectionFactory"/> class.
		/// </summary>
		public ConfigurationConnectionFactory(string connectionName, string providerName, string connectionString)
			: this(connectionName)
		{
			_connectionStringSettings = new ConnectionStringSettings(_connectionName, connectionString, providerName);
		}

		/// <summary>
		/// Gets the connection string settings for the connection.
		/// </summary>
		public virtual ConnectionStringSettings Settings
		{
			get { return GetConnectionStringSettings(_connectionName); }
		}

		/// <inheritdoc/>
		public virtual ConnectionScope Open()
		{
			if (Logger.IsEnabled(LogLevel.Trace))
			{
				Logger.LogTrace(Messages.OpeningMasterConnection, _connectionName);
			}
			return Open(_connectionName);
		}

		/// <inheritdoc/>
		public Type GetDbProviderFactoryType()
		{
			DbProviderFactory factory = GetFactory(Settings);
			return factory.GetType();
		}

		/// <summary>
		/// Opens a connection using the specified connection name.
		/// </summary>
		protected virtual ConnectionScope Open(string connectionName)
		{
			ConnectionStringSettings setting = GetSetting(connectionName);
			string connectionString = setting.ConnectionString;
			return new ConnectionScope(connectionString, () => Open(connectionString, setting));
		}

		/// <summary>
		/// Opens a connection using the specified connection string and settings.
		/// </summary>
		/// <exception cref="ConfigurationErrorsException"></exception>
		/// <exception cref="StorageUnavailableException"></exception>
		protected virtual DbConnection Open(string connectionString, ConnectionStringSettings setting)
		{
			DbProviderFactory factory = GetFactory(setting);
			DbConnection connection = factory.CreateConnection()
				?? throw new ConfigurationErrorsException(Messages.BadConnectionName);

			connection.ConnectionString = connectionString;

			try
			{
				if (Logger.IsEnabled(LogLevel.Trace))
				{
					Logger.LogTrace(Messages.OpeningConnection, setting.Name);
				}
				connection.Open();
			}
			catch (Exception e)
			{
				if (Logger.IsEnabled(LogLevel.Warning))
				{
					Logger.LogWarning(Messages.OpenFailed, setting.Name);
				}
				throw new StorageUnavailableException(e.Message, e);
			}

			return connection;
		}

		/// <summary>
		/// Gets the connection string settings for the connection.
		/// </summary>
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

		/// <summary>
		/// Gets the provider factory for the connection.
		/// </summary>
		protected virtual DbProviderFactory GetFactory(ConnectionStringSettings setting)
		{
			lock (CachedFactories)
			{
				if (CachedFactories.TryGetValue(setting.Name, out DbProviderFactory factory))
				{
					return factory;
				}
				factory = DbProviderFactories.GetFactory(setting.ProviderName);
				if (Logger.IsEnabled(LogLevel.Debug))
				{
					Logger.LogDebug(Messages.DiscoveredConnectionProvider, setting.Name, factory.GetType());
				}
				return CachedFactories[setting.Name] = factory;
			}
		}

		/// <summary>
		/// Gets the connection string settings for the connection.
		/// </summary>
		/// <exception cref="ConfigurationErrorsException"></exception>
		protected virtual ConnectionStringSettings GetConnectionStringSettings(string connectionName)
		{
			if (Logger.IsEnabled(LogLevel.Debug))
			{
				Logger.LogDebug(Messages.DiscoveringConnectionSettings, connectionName);
			}

			ConnectionStringSettings settings = (_connectionStringSettings
				?? ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>().FirstOrDefault(x => x.Name == connectionName))
				?? throw new ConfigurationErrorsException(Messages.ConnectionNotFound.FormatWith(connectionName));

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