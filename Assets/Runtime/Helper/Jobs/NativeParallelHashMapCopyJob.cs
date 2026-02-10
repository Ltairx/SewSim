using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace UCloth
{
    /// <summary>
    /// Copies <see cref="NativeParallelHashMap{TKey, TValue}"/> from source to dest.
    /// </summary>
    [BurstCompile]
    internal struct NativeParallelHashMapCopyJob<TKey, TValue> : IJobParallelForBatch
        // ZMIANA TUTAJ: 'struct' zamienione na 'unmanaged'
        where TKey : unmanaged, IEquatable<TKey> 
        where TValue : unmanaged
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]
        private NativeKeyValueArrays<TKey, TValue> KeyValues;

        [WriteOnly]
        private NativeParallelHashMap<TKey, TValue>.ParallelWriter OutputWriter;

        public NativeParallelHashMapCopyJob(NativeParallelHashMap<TKey, TValue> source, NativeParallelHashMap<TKey, TValue> dest)
        {
            // Allocator.TempJob jest bezpieczniejszy tutaj niz Temp, jesli job nie konczy sie w tej samej klatce
            KeyValues = source.GetKeyValueArrays(Allocator.TempJob);
            OutputWriter = dest.AsParallelWriter();
        }

        public void Execute(int index, int count)
        {
            int endIndex = index + count;
            // Zabezpieczenie przed wyjściem poza zakres
            if (index >= KeyValues.Length) return;
            
            endIndex = math.min(endIndex, KeyValues.Length);

            for (int i = index; i < endIndex; i++)
            {
                // NativeKeyValueArrays przechowuje klucze i wartości w osobnych tablicach
                OutputWriter.TryAdd(KeyValues.Keys[i], KeyValues.Values[i]);
            }
        }
    }
}