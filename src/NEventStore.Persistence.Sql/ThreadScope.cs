namespace NEventStore.Persistence.Sql
{
    using System;
    using System.Threading;
    using System.Web;
    using Microsoft.Extensions.Logging;
    using NEventStore.Logging;

    // HttpContext.Current is not a good idea, it's not supported in netstandard, possible alternatives (that requires some setup):
    // https://www.strathweb.com/2016/12/accessing-httpcontext-outside-of-framework-components-in-asp-net-core/

    public class ThreadScope<T> : IDisposable where T : class
    {
#if NET461
        private readonly HttpContext _context = HttpContext.Current;
#endif

        private readonly ILogger _logger = LogFactory.BuildLogger(typeof(ThreadScope<T>));
        private readonly bool _rootScope;
        private readonly string _threadKey;
        private bool _disposed;

        public ThreadScope(string key, Func<T> factory)
        {
            _threadKey = typeof(ThreadScope<T>).Name + ":[{0}]".FormatWith(key ?? string.Empty);

            T parent = Load();
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

        public T Current { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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

            if (!(Current is IDisposable resource))
            {
                return;
            }

            _logger.LogTrace(Messages.DisposingRootThreadScopeResources);
            resource.Dispose();
        }

        private T Load()
        {
#if NET461
            if (_context != null)
            {
                return _context.Items[_threadKey] as T;
            }
#endif
            return Thread.GetData(Thread.GetNamedDataSlot(_threadKey)) as T;
        }

        private void Store(T value)
        {
#if NET461
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