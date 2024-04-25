using System.Collections.Concurrent;

namespace CodeProject.AI.SDK.Utils
{
    /// <summary>
    /// Provides a pool of objects that can be used to reduce memory allocation.
    /// </summary>
    /// <remarks>
    /// Usage:
    ///     T obj = pool.Get();
    ///     .... // use the object
    ///     pool.Release(obj);
    /// </remarks>
    /// <typeparam name="TPooled">The type of the object pooled.</typeparam>

    public class ObjectPool<TPooled> where TPooled : class
    {
        private readonly Func<TPooled> _factory;
        private readonly Action<TPooled>? _init;
        private readonly int _maxPooled;
        private readonly ConcurrentQueue<TPooled> _pool = new ConcurrentQueue<TPooled>();

        /// <summary>
        /// Initializes a new instance of the ObjectPool class;
        /// </summary>
        /// <param name="maxPooled">The maximum number of objects pooled.</param>
        /// <param name="factory">A method to create a new object.</param>
        /// <param name="init">A method to initialize the object before it is used.</param>
        public ObjectPool(int maxPooled, Func<TPooled> factory, Action<TPooled>? init = null)
        {
            _factory   = factory;
            _init      = init;
            _maxPooled = maxPooled;
        }

        /// <summary>
        /// Gets an initialized object from the poll.
        /// If the pool is empty, then a new instance is created and initialized.
        /// </summary>
        /// <returns>An initialized object.</returns>
        public TPooled Get()
        {
            if (!_pool.TryDequeue(out TPooled? obj))
                obj = _factory();

            _init?.Invoke(obj);

            return obj;
        }

        /// <summary>
        /// Returns a object to the pool.
        /// If the pool is full, the object is not added to the queue and is subject to GC.
        /// </summary>
        /// <param name="obj">The object to add to the pool.</param>
        public void Release(object obj)
        {
            TPooled? tObj = obj as TPooled;
            if (tObj is not null && _pool.Count < _maxPooled)
                _pool.Enqueue(tObj);
        }
    }
}
