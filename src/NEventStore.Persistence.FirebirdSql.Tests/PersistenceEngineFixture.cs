namespace NEventStore.Persistence.AcceptanceTests
{
	using System;

	using FirebirdSql.Data.FirebirdClient;

	using NEventStore.Persistence.Sql;
	using NEventStore.Persistence.Sql.SqlDialects;
	using NEventStore.Persistence.Sql.Tests;
	using NEventStore.Serialization;

	public partial class PersistenceEngineFixture
	{
		const string ConnectionString = "User=SYSDBA;Password=doesntmatter;Database=neventstore.fdb;DataSource=localhost;Port=3050;Dialect=3;Charset=UTF8;Role=;Connection lifetime=15;Pooling=true;MinPoolSize=0;MaxPoolSize=50;Packet Size=8192;ServerType=1;";
		private const string EnvVariable = "NEventStore.FirebirdSql";

		public PersistenceEngineFixture()
		{
			FbConnection.CreateDatabase(ConnectionString, true);

			_createPersistence = pageSize =>
				new FirebirdSqlPersistenceFactory(
					new EnviromentConnectionFactory("FirebirdSql", "FirebirdSql"),
					new BinarySerializer(),
					new FirebirdSqlDialect(),
					pageSize: pageSize).Build();
		}

		partial void SetEnvironmentVariable()
		{
			Environment.SetEnvironmentVariable(EnvVariable, ConnectionString, EnvironmentVariableTarget.User);
		}

		partial void ClearEnvironmentVariable()
		{
			Environment.SetEnvironmentVariable(EnvVariable, null, EnvironmentVariableTarget.User);
		}
	}
}