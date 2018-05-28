using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace InternalsViewer.UI.Controls
{
    public class KeyImageCell : DataGridViewImageCell
    {
        public KeyImageCell()
        {
            ValueType = typeof(Color);
        }

        public override object DefaultNewRowValue
        {
            get { return 0; }
        }

        protected override object GetFormattedValue(object value,
            int rowIndex,
            ref DataGridViewCellStyle cellStyle,
            TypeConverter valueTypeConverter,
            TypeConverter formattedValueTypeConverter,
            DataGridViewDataErrorContexts context)
        {
            if (value != DBNull.Value)
            {
                return ExtentColour.KeyImage((Color)value);
            }
            else
            {
                return null;
            }
        }
    }
}