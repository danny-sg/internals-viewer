using InternalsViewer.Query.Events.Operators;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>
/// An operator row in the Callstack tree — the operator, plus how its call tree beneath it was built
/// </summary>
/// <remarks>
/// A wrapper rather than binding the operator event itself, because <see cref="Unsegmented"/> is a fact about this
/// view's toggle state, not about the operator, and the Query layer's model should not carry it.
/// </remarks>
/// <param name="Operator">The plan operator this row shows</param>
/// <param name="Unsegmented">
/// No entry frame was found for this operator, so it carries no frames of its own and renders empty. Surfaced so the
/// row admits it: an operator that genuinely did nothing and one whose frames could not be located look the same.
/// </param>
public sealed record OperatorRow(ExecutionOperatorEvent Operator, bool Unsegmented);
