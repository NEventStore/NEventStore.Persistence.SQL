namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Data;

	/// <summary>
	/// Represents a connection scope.
	/// </summary>
	public class ConnectionScope : ThreadScope<IDbConnection>, IDbConnection
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectionScope"/> class.
		/// </summary>
		public ConnectionScope(string connectionName, Func<IDbConnection> factory)
			: base(connectionName, factory)
		{ }

		IDbTransaction IDbConnection.BeginTransaction()
		{
			return Current.BeginTransaction();
		}

		IDbTransaction IDbConnection.BeginTransaction(IsolationLevel il)
		{
			return Current.BeginTransaction(il);
		}

		void IDbConnection.Close()
		{
			// no-op--let Dispose do the real work.
		}

		void IDbConnection.ChangeDatabase(string databaseName)
		{
			Current.ChangeDatabase(databaseName);
		}

		IDbCommand IDbConnection.CreateCommand()
		{
			return Current.CreateCommand();
		}

		void IDbConnection.Open()
		{
			Current.Open();
		}

		string IDbConnection.ConnectionString
		{
			get { return Current.ConnectionString; }
			set { Current.ConnectionString = value; }
		}

		int IDbConnection.ConnectionTimeout
		{
			get { return Current.ConnectionTimeout; }
		}

		string IDbConnection.Database
		{
			get { return Current.Database; }
		}

		ConnectionState IDbConnection.State
		{
			get { return Current.State; }
		}
	}
}