﻿using NEventStore.Persistence.Sql.Tests;
using NEventStore.Persistence.Sql;
using NEventStore.Persistence.Sql.SqlDialects;
using NEventStore.Serialization.Binary;

namespace NEventStore.Persistence.AcceptanceTests
{
	public partial class PersistenceEngineFixture
	{
		public PersistenceEngineFixture()
		{
#if NET8_0_OR_GREATER
			AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
#endif
			// It will be done when creating the PostgreNpgsql6Dialect dialect
			// AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

#if NET462
			_createPersistence = pageSize =>
			{
				var serializer = new BinarySerializer();
				return new SqlPersistenceFactory(
					new EnviromentConnectionFactory("PostgreSql", "Npgsql"),
					serializer,
					new DefaultEventSerializer(serializer),
					new PostgreNpgsql6Dialect(npgsql6timestamp: true),
					pageSize: pageSize).Build();
			};
#else
			_createPersistence = pageSize =>
			{
				var serializer = new BinarySerializer();
				return new SqlPersistenceFactory(
					new EnvironmentConnectionFactory("PostgreSql", Npgsql.NpgsqlFactory.Instance),
					serializer,
					new DefaultEventSerializer(serializer),
					new PostgreNpgsql6Dialect(npgsql6timestamp: true),
					pageSize: pageSize).Build();
			};
#endif
		}
	}
}