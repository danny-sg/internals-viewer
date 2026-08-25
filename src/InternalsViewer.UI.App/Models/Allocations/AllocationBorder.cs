using System.Collections.Generic;
using System.Drawing;

namespace InternalsViewer.UI.App.Models.Allocations;

public sealed record AllocationBorder(AllocationBorderScope Scope,
                                      short FileId,
                                      Color Colour,
                                      IReadOnlyList<TimedRange> Cells);
