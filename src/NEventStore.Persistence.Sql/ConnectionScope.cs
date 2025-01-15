using System.Data.Common;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// Represents a connection scope.
	/// Creating and managing a connection can be expensive, so we want to make sure we only create one connection per thread.
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
	}
}