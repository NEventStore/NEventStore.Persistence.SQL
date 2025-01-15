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

	/// <summary>
	/// Scoped Instance created using a factory method.
	/// Having Thread tied connection might be a problem in async/await scenarios,
	/// maybe it's better to always create new connection instances instead of trying to reuse them.
	/// </summary>
	public class ScopedInstance<T> : IDisposable where T : class
	{
		private readonly Func<CancellationToken, Task<T>>? _factoryAsync;
		private bool _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScopedInstance{T}"/> class.
		/// </summary>
		public ScopedInstance(string key, Func<T> factory)
		{
			Current = factory();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ScopedInstance{T}"/> class.
		/// </summary>
		public ScopedInstance(string key, Func<CancellationToken, Task<T>> factory)
		{
			_factoryAsync = factory;
		}

		/// <summary>
		/// Initializes the thread scope.
		/// </summary>
		public async Task InitAsync(CancellationToken cancellationToken)
		{
			Current = await _factoryAsync!(cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Gets the current value of the thread scope.
		/// </summary>
		public T Current { get; private set; }

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		/// <inheritdoc/>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing || _disposed)
			{
				return;
			}

			_disposed = true;
			if (Current is not IDisposable resource)
			{
				return;
			}

			resource.Dispose();
		}
	}
}