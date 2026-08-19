using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.Models.Columnstore;

/// <summary>
/// One packed value, positioned by its bits within the unit rather than by a byte offset
/// </summary>
public sealed class BitpackValueDetail
{
    public required int Index { get; init; }

    public required int BitOffset { get; init; }

    public required int BitLength { get; init; }

    /// <summary>
    /// Data id the value resolves to, being what was packed with the reserved floor added back
    /// </summary>
    public required long DataId { get; init; }

    public required long MinId { get; init; }

    /// <summary>
    /// Working from the data id to the value it stands for, which only a decoded segment can supply
    /// </summary>
    public ValueDerivation? ValueDerivation { get; init; }

    /// <summary>
    /// The value held in the bits, before the reserved floor the segment subtracted is added back
    /// </summary>
    public long Packed => DataId - MinId;

    /// <summary>
    /// Working from the packed bits to the data id, the floor being the only thing between them
    /// </summary>
    public ValueDerivation Derivation => new()
    {
        Steps =
        [
            new DerivationStep { Name = "Packed Value", Value = $"{Packed}" },
            new DerivationStep { Operator = "+", Name = "Min Id", Value = $"{MinId}" }
        ],
        Result = $"{DataId}"
    };

    public string BitRange => $"{BitOffset} - {BitOffset + BitLength - 1}";
}
