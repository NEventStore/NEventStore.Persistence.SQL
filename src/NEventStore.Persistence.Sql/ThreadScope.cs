namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Threading;
	using System.Web;
	using Microsoft.Extensions.Logging;
	using NEventStore.Logging;

	// HttpContext.Current is not a good idea, it's not supported in netstandard, possible alternatives (that requires some setup):
	// https://www.strathweb.com/2016/12/accessing-httpcontext-outside-of-framework-components-in-asp-net-core/

	/// <summary>
	/// Represents a thread scope.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class ThreadScope<T> : IDisposable where T : class
	{
#if NET462
		private readonly HttpContext _context = HttpContext.Current;
#endif

		private readonly ILogger _logger = LogFactory.BuildLogger(typeof(ThreadScope<T>));
		private readonly bool _rootScope;
		private readonly string _threadKey;
		private bool _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="ThreadScope{T}"/> class.
		/// </summary>
		/// <exception cref="ArgumentException"></exception>
		public ThreadScope(string key, Func<T> factory)
		{
			_threadKey = typeof(ThreadScope<T>).Name + ":[{0}]".FormatWith(key ?? string.Empty);

			T? parent = Load();
			_rootScope = parent == null;
			_logger.LogDebug(Messages.OpeningThreadScope, _threadKey, _rootScope);

			Current = parent ?? factory();

			if (Current == null)
			{
				throw new ArgumentException(Messages.BadFactoryResult, nameof(factory));
			}

			if (_rootScope)
			{
				Store(Current);
			}
		}

		/// <summary>
		/// Gets the current value of the thread scope.
		/// </summary>
		public T Current { get; }

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

			_logger.LogDebug(Messages.DisposingThreadScope, _rootScope);
			_disposed = true;
			if (!_rootScope)
			{
				return;
			}

			_logger.LogTrace(Messages.CleaningRootThreadScope);
			Store(null);

			if (Current is not IDisposable resource)
			{
				return;
			}

			_logger.LogTrace(Messages.DisposingRootThreadScopeResources);
			resource.Dispose();
		}

		private T? Load()
		{
#if NET462
			if (_context != null)
			{
				return _context.Items[_threadKey] as T;
			}
#endif
			return Thread.GetData(Thread.GetNamedDataSlot(_threadKey)) as T;
		}

		private void Store(T? value)
		{
#if NET462
			if (_context != null)
			{
				_context.Items[_threadKey] = value;
			}
			else
			{
				Thread.SetData(Thread.GetNamedDataSlot(_threadKey), value);
			}
#endif
			Thread.SetData(Thread.GetNamedDataSlot(_threadKey), value);
		}
	}
}