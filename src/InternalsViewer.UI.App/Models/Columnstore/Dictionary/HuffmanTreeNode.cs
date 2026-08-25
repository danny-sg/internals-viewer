using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.UI.App.Models.Columnstore.Dictionary;

/// <summary>
/// One node of the tree a set of Huffman codes describes
/// </summary>
/// <remarks>
/// The table only ever holds codes, the tree being what those codes mean rather than something stored alongside
/// them. Every code is a path from the root, a zero bit taking one branch and a one bit the other, so the tree is
/// rebuilt by walking each code's bits in turn.
/// </remarks>
public sealed class HuffmanTreeNode
{
    public HuffmanTreeNode? Zero { get; private set; }

    public HuffmanTreeNode? One { get; private set; }

    public HuffmanCode? Code { get; private set; }

    public int Depth { get; private init; }

    public bool IsLeaf => Code is not null;

    /// <summary>
    /// Row the node is drawn on, a leaf taking the next row down and a branch sitting between its children
    /// </summary>
    public float Row { get; private set; }

    public int LeafCount { get; private set; }

    public static HuffmanTreeNode Build(IReadOnlyList<HuffmanCode> codes)
    {
        var root = new HuffmanTreeNode();

        foreach (var code in codes)
        {
            var node = root;

            for (var bit = 0; bit < code.BitLength; bit++)
            {
                node = code.IsSet(bit) ? node.TakeOne() : node.TakeZero();
            }

            node.Code = code;
        }

        var row = 0f;

        root.Layout(ref row);

        return root;
    }

    public IEnumerable<HuffmanTreeNode> Descend()
    {
        var stack = new Stack<HuffmanTreeNode>();

        stack.Push(this);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            yield return node;

            if (node.One is { } one)
            {
                stack.Push(one);
            }

            if (node.Zero is { } zero)
            {
                stack.Push(zero);
            }
        }
    }

    private HuffmanTreeNode TakeZero() => Zero ??= new HuffmanTreeNode { Depth = Depth + 1 };

    private HuffmanTreeNode TakeOne() => One ??= new HuffmanTreeNode { Depth = Depth + 1 };

    /// <summary>
    /// Places every node on a row, leaves in the order their codes came in and branches midway between their children
    /// </summary>
    private void Layout(ref float nextRow)
    {
        if (IsLeaf)
        {
            Row = nextRow;

            LeafCount = 1;

            nextRow++;

            return;
        }

        var first = float.MaxValue;

        var last = float.MinValue;

        if (Zero is { } zero)
        {
            zero.Layout(ref nextRow);

            LeafCount += zero.LeafCount;

            first = Math.Min(first, zero.Row);
            last = Math.Max(last, zero.Row);
        }

        if (One is { } one)
        {
            one.Layout(ref nextRow);

            LeafCount += one.LeafCount;

            first = Math.Min(first, one.Row);
            last = Math.Max(last, one.Row);
        }

        Row = LeafCount == 0 ? nextRow : (first + last) / 2;
    }
}
