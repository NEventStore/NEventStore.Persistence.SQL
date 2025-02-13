using System.Data.Common;

namespace NEventStore.Persistence.Sql
{
	/// <summary>
	/// <para>Represents a connection scope.</para>
	/// <para>
	/// Original Idea:
	/// Creating and managing a connection can be expensive, so we want to make sure we only create one connection per thread.
	/// Wraps a DbConnection so that we create no more than one connection for each thread.
	/// It's a Disposable class, so all the opened connection are closed when the scope is disposed.
	/// </para>
	/// </summary>
	public class ConnectionScope :
		ScopedInstance<DbConnection>
		// ThreadScope<DbConnection> // ThreadScope might have problems in async/await scenarios, even implementing it with AsyncLocal might not be enough (there are problem in non async/await parallel code)
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectionScope"/> class.
		/// </summary>
		public ConnectionScope(string connectionName, Func<DbConnection> factory)
			: base(connectionName, factory)
		{ }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConnectionScope"/> class.
		/// </summary>
		public ConnectionScope(string connectionName, Func<CancellationToken, Task<DbConnection>> factory)
			: base(connectionName, factory)
		{ }
	}
}