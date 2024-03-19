using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>This is an array that automatically sets up its length to match indices of a given enum,
/// and allows directly accessing specific entries via enum indexer. This is primarily for non-flag enums.</summary>
/// <typeparam name="TEnum">The enum type</typeparam>
/// <typeparam name="TStored">What we actually store</typeparam>
[System.Serializable]
public class EnumerableArray<TEnum, TStored> : IEnumerable<TStored> where TEnum : Enum {
    /// <summary>Underlying actual array where store entries. Any values in between enums will go as unused memory.
    /// It is indexed such that (int) myEnum gives the entry for that enum. </summary>
    private TStored[] array;
    public EnumerableArray() {
        int maxValue = 0;
        foreach (int index in Enum.GetValues(typeof(TEnum))) {
            UnityEngine.Debug.Assert(index >= 0, "Can’t make an enumerated array if the enum has negative values defined!");
            maxValue = Math.Max(maxValue, index);
        }
        array = new TStored[maxValue + 1];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<TStored> GetEnumerator() {
        foreach (int index in Enum.GetValues(typeof(TEnum))) {
            yield return array[index];
        }
    }

    //public IEnumerator GetIndices() => Enum.GetValues(typeof(TEnum)).GetEnumerator();
    /// <summary>Enumerate all enumerated indices in this enum.</summary>
    public IEnumerable<TEnum> GetIndices() {
        IEnumerator enumerator = Enum.GetValues(typeof(TEnum)).GetEnumerator();
        while (enumerator.MoveNext()) yield return (TEnum) enumerator.Current;
    }

    public int Length => array.Length;

    /// <summary>Access/modify the array for the entry for that given enum value.</summary>
    public TStored this[TEnum index] {
        get => array[(int)(object)index];
        set => array[(int)(object)index] = value;
    }
    /// <summary>Access/modify the array for the entry for that value.</summary>
    public TStored this[int index] {
        get => array[index];
        set => array[index] = value;
    }

    public override string ToString() {
        System.Text.StringBuilder sb = new();
        foreach (TStored entry in this) sb.Append(entry.ToString() + ", ");
        return sb.ToString();
    }
}
