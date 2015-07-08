namespace NEventStore.Persistence.AcceptanceTests
{

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
			_createPersistence = pageSize =>
				new FirebirdSqlPersistenceFactory(
					new EnviromentConnectionFactory("FirebirdSql", "FirebirdSql"),
					new JsonSerializer(),
					new FirebirdSqlDialect(),
					pageSize: pageSize).Build();
		}

		//partial void PrepEnvironment()
		//{
		//	Environment.SetEnvironmentVariable(EnvVariable, ConnectionString, EnvironmentVariableTarget.Process);
		//	FbConnection.CreateDatabase(ConnectionString, true);

		//}

		//partial void CleanEnvironment()
		//{
		//	Environment.SetEnvironmentVariable(EnvVariable, null, EnvironmentVariableTarget.Process);
		//	FbConnection.ClearAllPools();
		//	FbConnection.DropDatabase(ConnectionString);
		//}
	}
}