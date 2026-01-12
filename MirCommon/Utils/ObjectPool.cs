using System;
using System.Collections.Generic;

namespace MirCommon.Utils
{
    
    
    
    
    
    public class ObjectPool<T> : IDisposable where T : class, new()
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _createFunc;
        private readonly int _maxSize;
        private int _createdCount;

        
        
        
        
        
        
        public ObjectPool(Func<T>? createFunc = null, int initialSize = 10, int maxSize = 1000)
        {
            _createFunc = createFunc ?? (() => new T());
            _maxSize = maxSize;
            _pool = new Stack<T>(initialSize);
            _createdCount = 0;

            
            for (int i = 0; i < initialSize; i++)
            {
                _pool.Push(_createFunc());
                _createdCount++;
            }
        }

        
        
        
        
        public T? Get()
        {
            lock (_pool)
            {
                if (_pool.Count > 0)
                {
                    return _pool.Pop();
                }

                
                if (_createdCount < _maxSize)
                {
                    _createdCount++;
                    return _createFunc();
                }

                
                return null;
            }
        }

        
        
        
        
        public void Return(T obj)
        {
            if (obj == null)
                return;

            lock (_pool)
            {
                
                if (_pool.Count < _maxSize)
                {
                    _pool.Push(obj);
                }
                else
                {
                    
                    if (obj is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        
        
        
        public int AvailableCount
        {
            get
            {
                lock (_pool)
                {
                    return _pool.Count;
                }
            }
        }

        
        
        
        public int CreatedCount
        {
            get
            {
                lock (_pool)
                {
                    return _createdCount;
                }
            }
        }

        
        
        
        public void Clear()
        {
            lock (_pool)
            {
                foreach (var obj in _pool)
                {
                    if (obj is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                _pool.Clear();
                _createdCount = 0;
            }
        }

        
        
        
        public IEnumerable<T> GetAllObjects()
        {
            lock (_pool)
            {
                return _pool.ToArray();
            }
        }

        
        
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        
        
        
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Clear();
            }
        }
    }
}
