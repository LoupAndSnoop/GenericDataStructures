using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary> This stores a 2D array with some translation, so there are some empty
/// inaccessible entries around the perimeter. It helps avoid having to check edge cases.</summary>
public class PaddedMatrix<TStored> : IEnumerable {
    private TStored[,] matrix;
    private int padding;
    private int paddingMinus1;
    private const int DEFAULT_PADDING = 5;

    public PaddedMatrix(int sizeX, int sizeY, int padding = DEFAULT_PADDING) {
        this.padding = padding;
        paddingMinus1 = padding - 1;
        matrix = new TStored[sizeX + padding * 2, sizeY + padding * 2];
    }

    public TStored this[int x, int y] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => matrix[x + paddingMinus1, y + paddingMinus1];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => matrix[x + paddingMinus1, y + paddingMinus1] = value;
    }
    public TStored this[Vector2Int vector] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => matrix[vector.x + paddingMinus1, vector.y + paddingMinus1];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set {
            matrix[vector.x + paddingMinus1, vector.y + paddingMinus1] = value;
        }
    }

    /// <summary>Give the non-padded dimensions </summary>
    public Vector2Int UnpaddedSize => new Vector2Int(matrix.GetLength(0), matrix.GetLength(1)) - 2 * padding * Vector2Int.one;
    /// <summary>Give the padded dimensions</summary>
    public Vector2Int PaddedSize => new Vector2Int(matrix.GetLength(0), matrix.GetLength(1));

    /// <summary> Enumerate all non-null entries in matrix. </summary>
    IEnumerator IEnumerable.GetEnumerator() {
        for (int x = padding - 1; x < matrix.GetLength(0) - padding; x++) {
            for (int y = padding - 1; y < matrix.GetLength(1) - padding; y++) {
                if (matrix[x, y] != null) {
                    yield return matrix[x, y];
                }
            }
        }
    }

    /// <summary>Enumerate through all Vector2Int that are in the bounds.
    /// If we get a range with nothing in it, we just won't return anything.
    /// Only spit out non-null entries.</summary>
    public IEnumerable<TStored> EnumerateWithinBounds(Bounds bounds) {
        int minX = Mathf.Max(Mathf.CeilToInt(bounds.min.x), 0);
        int maxX = Mathf.Min(Mathf.FloorToInt(bounds.max.x), matrix.GetLength(0));
        int minY = Mathf.Max(Mathf.CeilToInt(bounds.min.y), 0);
        int maxY = Mathf.Min(Mathf.FloorToInt(bounds.max.y), matrix.GetLength(1));

        for (int x = minX; x <= maxX; x++) {
            for (int y = minY; y <= maxY; y++) {
                if (matrix[x, y] != null) yield return matrix[x, y];
            }
        }
    }


    /// <summary> Set all coordinates in the matrix to a given value. </summary>
    public void SetAllCoordinates(TStored newValue) {
        for (int x = padding - 1; x < matrix.GetLength(0) - padding; x++) {
            for (int y = padding - 1; y < matrix.GetLength(1) - padding; y++) {
                matrix[x, y] = newValue;
            }
        }
    }

}
