using System;
using System.Collections.Generic;

namespace PocketMC.Infrastructure.Services
{
    public class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly object _lock = new();
        private int _start;
        private int _end;
        private int _count;

        public int Capacity { get; }
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentException("Capacity must be greater than zero.", nameof(capacity));
            Capacity = capacity;
            _buffer = new T[capacity];
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                _buffer[_end] = item;
                _end = (_end + 1) % Capacity;
                if (_count < Capacity)
                {
                    _count++;
                }
                else
                {
                    _start = (_start + 1) % Capacity;
                }
            }
        }

        public List<T> ToList()
        {
            lock (_lock)
            {
                var list = new List<T>(_count);
                for (int i = 0; i < _count; i++)
                {
                    int index = (_start + i) % Capacity;
                    list.Add(_buffer[index]);
                }
                return list;
            }
        }
    }
}
