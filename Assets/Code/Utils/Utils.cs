using System.Collections;
using System.Collections.Generic;
using UnityEngine;



namespace ReGecko.Utils
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class UniqueOrderedList<T> : IEnumerable<T>
    {
        private HashSet<T> hashSet = new HashSet<T>();
        private List<T> list = new List<T>();

        /// <summary>
        /// 添加元素到列表末尾（如果不存在）
        /// </summary>
        public bool Add(T item)
        {
            if (!hashSet.Contains(item))
            {
                hashSet.Add(item);
                list.Add(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 从列表开头移除并返回元素
        /// </summary>
        public T Dequeue()
        {
            if (list.Count == 0)
                throw new System.InvalidOperationException("Queue is empty");

            T item = list[0];
            list.RemoveAt(0);
            hashSet.Remove(item);
            return item;
        }

        /// <summary>
        /// 尝试从列表开头移除并返回元素
        /// </summary>
        public bool TryDequeue(out T result)
        {
            if (list.Count == 0)
            {
                result = default(T);
                return false;
            }

            result = list[0];
            list.RemoveAt(0);
            hashSet.Remove(result);
            return true;
        }

        /// <summary>
        /// 查看列表开头的元素但不移除
        /// </summary>
        public T Peek()
        {
            if (list.Count == 0)
                throw new System.InvalidOperationException("Queue is empty");

            return list[0];
        }

        /// <summary>
        /// 尝试查看列表开头的元素但不移除
        /// </summary>
        public bool TryPeek(out T result)
        {
            if (list.Count == 0)
            {
                result = default(T);
                return false;
            }

            result = list[0];
            return true;
        }
        /// <summary>
        /// 查看列表末尾的元素但不移除（队尾元素）
        /// </summary>
        public T PeekLast()
        {
            if (list.Count == 0)
                throw new System.InvalidOperationException("Queue is empty");

            return list[list.Count - 1];
        }

        /// <summary>
        /// 尝试查看列表末尾的元素但不移除（队尾元素）
        /// </summary>
        public bool TryPeekLast(out T result)
        {
            if (list.Count == 0)
            {
                result = default(T);
                return false;
            }

            result = list[list.Count - 1];
            return true;
        }

        /// <summary>
        /// 移除指定元素
        /// </summary>
        public bool Remove(T item)
        {
            if (hashSet.Contains(item))
            {
                hashSet.Remove(item);
                list.Remove(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空所有元素
        /// </summary>
        public void Clear()
        {
            hashSet.Clear();
            list.Clear();
        }

        public bool Contains(T item) => hashSet.Contains(item);
        public int Count => list.Count;

        public T this[int index]
        {
            get => list[index];
            set
            {
                // 确保设置新值时保持唯一性
                if (!hashSet.Contains(value) || EqualityComparer<T>.Default.Equals(list[index], value))
                {
                    hashSet.Remove(list[index]);
                    list[index] = value;
                    hashSet.Add(value);
                }
                else
                {
                    throw new System.ArgumentException("Item already exists in the collection");
                }
            }
        }

        public IEnumerator<T> GetEnumerator() => list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
