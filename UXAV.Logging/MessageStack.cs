using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace UXAV.Logging
{
    public class MessageStack : IEnumerable<LoggerMessage>
    {
        private readonly int _size;
        private readonly Queue<LoggerMessage> _queue = new Queue<LoggerMessage>();

        public MessageStack(int size)
        {
            _size = size;
        }

        public int Size => _size;

        public void Add(LoggerMessage message)
        {
            lock (_queue)
            {
                _queue.Enqueue(message);

                while (_queue.Count > Size)
                {
                    _queue.Dequeue();
                }
            }
        }

        public IEnumerable<LoggerMessage> GetLast(int count)
        {
            lock (_queue)
            {
                return _queue.Reverse().Take(count).Reverse();
            }
        }

        public IEnumerator<LoggerMessage> GetEnumerator()
        {
            lock (_queue)
            {
                return _queue.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}