using System.Drawing;
using System.Windows.Forms;

namespace InternalsViewer.UI.Controls;

public class KeyImageColumn : DataGridViewImageColumn
{
    public KeyImageColumn()
    {
        CellTemplate = new KeyImageCell();
        ValueType = typeof(Color);
    }
}