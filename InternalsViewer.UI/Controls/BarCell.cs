using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Controls;

public sealed class BarCell : DataGridViewImageCell
{
    public BarCell()
    {
        ValueType = typeof(decimal);
    }

    public override object DefaultNewRowValue => 0;

    protected override object GetFormattedValue(object value,
                                                int rowIndex,
                                                ref DataGridViewCellStyle cellStyle,
                                                TypeConverter valueTypeConverter,
                                                TypeConverter formattedValueTypeConverter,
                                                DataGridViewDataErrorContexts context)
    {
        return new Bitmap(1, 1);
    }

    protected override void Paint(Graphics graphics,
                                  Rectangle clipBounds,
                                  Rectangle cellBounds,
                                  int rowIndex,
                                  DataGridViewElementStates elementState,
                                  object? value,
                                  object formattedValue,
                                  string errorText,
                                  DataGridViewCellStyle cellStyle,
                                  DataGridViewAdvancedBorderStyle advancedBorderStyle,
                                  DataGridViewPaintParts paintParts)
    {
        base.Paint(graphics,
                   clipBounds,
                   cellBounds,
                   rowIndex,
                   elementState,
                   value,
                   formattedValue,
                   errorText,
                   cellStyle,
                   advancedBorderStyle,
                   paintParts);

        string cellText;

        var font = new Font(DataGridView!.DefaultCellStyle.Font, FontStyle.Regular);

        if (value != null)
        {
            Color gradientColour;

            var r = (OwningColumn as BarColumn)?.ColourRanges.FirstOrDefault(range => range.From <= Convert.ToInt32(value ?? 0)
                                                                             && range.To >= Convert.ToInt32(value ?? 0));

            if (r != null)
            {
                gradientColour = r.Colour;
            }
            else
            {
                gradientColour = Color.DarkGray;
            }

            using (var brush = new LinearGradientBrush(cellBounds,
                                                       gradientColour,
                                                       ExtentColour.LightBackgroundColour(gradientColour),
                                                       90F,
                                                       false))
            {
                graphics.FillRectangle(brush,
                                       cellBounds.X + 2,
                                       cellBounds.Y + 3,
                                       (int)((cellBounds.Width - 6) * (Convert.ToDecimal(value) / 100)),
                                       cellBounds.Height - 8);
            }

            graphics.DrawRectangle(Pens.Gray, cellBounds.X + 2, cellBounds.Y + 3, cellBounds.Width - 6, cellBounds.Height - 8);

            cellText = $"{Convert.ToDecimal(value):0}%";
        }
        else
        {
            cellText = "Pending...";
        }

        // Centre the text in the middle of the bar
        var textPoint = new Point(cellBounds.X + cellBounds.Width / 2 - (TextRenderer.MeasureText(cellText, font).Width / 2),
                                  cellBounds.Y + 4);

        TextRenderer.DrawText(graphics, cellText, font, textPoint, Color.Black);
    }
}