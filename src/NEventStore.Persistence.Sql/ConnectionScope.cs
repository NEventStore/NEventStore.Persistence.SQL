using System.Data.Common;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Represents a connection scope.
	/// Wraps a DbConnection so that we create no more than one connection for each thread.
	/// It's a Disposable class, so all the opened connection are closed when the scope is disposed.
	/// </summary>
	public class ConnectionScope : ThreadScope<DbConnection>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectionScope"/> class.
		/// </summary>
		public ConnectionScope(string connectionName, Func<DbConnection> factory)
			: base(connectionName, factory)
		{ }

		/// <summary>
		/// Implicitly converts a connection scope to a connection.
		/// Unwraps the connection from the scope.
		/// </summary>
		public static implicit operator DbConnection(ConnectionScope scope)
		{
			return scope.Current;
		}
	}
}