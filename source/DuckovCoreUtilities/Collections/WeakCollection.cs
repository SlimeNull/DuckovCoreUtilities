using System;
using System.Collections;
using System.Collections.Generic;

namespace SlimeNull.DuckovInterop.Collections
{
    public class WeakCollection<T> : ICollection<T>, IReadOnlyCollection<T>
        where T : class
    {
        private readonly List<WeakReference<T>> weakReferences = new List<WeakReference<T>>();
        private readonly IEqualityComparer<T> comparer;

        public WeakCollection()
            : this(EqualityComparer<T>.Default)
        {
        }

        public WeakCollection(IEqualityComparer<T> comparer)
        {
            this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        }

        public int Count
        {
            get
            {
                Purge();
                return weakReferences.Count;
            }
        }

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            Purge();
            weakReferences.Add(new WeakReference<T>(item));
        }

        public void Clear()
        {
            weakReferences.Clear();
        }

        public bool Contains(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            bool contains = false;

            PurgeAlive((target, _) =>
            {
                if (comparer.Equals(target, item))
                    contains = true;

                return true;
            });

            return contains;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (arrayIndex < 0 || arrayIndex > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            Purge();

            if (array.Length - arrayIndex < weakReferences.Count)
                throw new ArgumentException("The destination array has insufficient space.", nameof(array));

            for (int i = 0; i < weakReferences.Count; i++)
            {
                if (weakReferences[i].TryGetTarget(out var target))
                    array[arrayIndex++] = target;
            }
        }

        public bool Remove(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            bool removed = false;

            PurgeAlive((target, _) =>
            {
                if (!removed && comparer.Equals(target, item))
                {
                    removed = true;
                    return false;
                }

                return true;
            });

            return removed;
        }

        public void Purge()
        {
            PurgeAlive(static (_, _) => true);
        }

        public IEnumerator<T> GetEnumerator()
        {
            Purge();

            for (int i = 0; i < weakReferences.Count; i++)
            {
                if (weakReferences[i].TryGetTarget(out var target))
                    yield return target;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void PurgeAlive(Func<T, WeakReference<T>, bool> keepAlive)
        {
            int availableIndex = 0;

            for (int i = 0; i < weakReferences.Count; i++)
            {
                WeakReference<T> weakReference = weakReferences[i];

                if (!weakReference.TryGetTarget(out var target))
                    continue;

                if (!keepAlive(target, weakReference))
                    continue;

                if (availableIndex != i)
                    weakReferences[availableIndex] = weakReference;

                availableIndex++;
            }

            if (availableIndex < weakReferences.Count)
                weakReferences.RemoveRange(availableIndex, weakReferences.Count - availableIndex);
        }
    }
}
