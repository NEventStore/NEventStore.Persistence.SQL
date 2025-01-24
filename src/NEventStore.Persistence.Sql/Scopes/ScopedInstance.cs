namespace NEventStore.Persistence.Sql
{
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