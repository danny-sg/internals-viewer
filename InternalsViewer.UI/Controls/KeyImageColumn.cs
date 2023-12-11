using System.Drawing;
using System.Windows.Forms;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Controls;

public class KeyImageColumn : DataGridViewImageColumn
{
    public KeyImageColumn()
    {
        CellTemplate = new KeyImageCell();
        ValueType = typeof(Color);
    }
}