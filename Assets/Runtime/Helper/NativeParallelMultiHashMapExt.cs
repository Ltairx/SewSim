using System;
using Unity.Collections;

namespace UCloth
{
    /// <summary>
    /// Helper class containing various utilities for NativeParallelMultiHashMap.
    /// </summary>
    internal static class NativeParallelMultiHashMapExtensions
    {
        /// <summary>
        /// Returns true if given key contains the specified value.
        /// </summary>
        /// <returns> True if given <paramref name="key"/> contains <paramref name="value"/>. </returns>
        [GenerateTestsForBurstCompatibility]
        internal static bool KeyContainsValue<TKey, TValue>(this NativeParallelMultiHashMap<TKey, TValue> hashmap, TKey key, TValue value)
            // ZMIANA TUTAJ: 'struct' zamienione na 'unmanaged' w obu miejscach
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged, IEquatable<TValue>
        {
            // Checks if contains any value
            if (!hashmap.TryGetFirstValue(key, out TValue outVal, out var iterator))
                return false;

            if (outVal.Equals(value))
                return true;

            // Continues iterating until either value is found, or reached the end
            while (hashmap.TryGetNextValue(out outVal, ref iterator))
            {
                if (outVal.Equals(value))
                    return true;
            }

            // And then check if ran out of elements to check, or because it found the value
            return false;
        }
    }
}