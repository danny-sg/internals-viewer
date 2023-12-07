using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using InternalsViewer.Internals;
using System.Text.RegularExpressions;

namespace InternalsViewer.UI;

public partial class DecodeWindow : UserControl
{
    private PageViewerWindow parentWindow;
    private readonly List<Color> rtfColours;
    private readonly string rtfHeader;

    public DecodeWindow()
    {
        InitializeComponent();

        dataTypeComboBox.SelectedIndex = 0;

        //rtfColours = RtfColour.CreateColourTable();
        //rtfHeader = RtfColour.CreateRtfHeader(rtfColours);
    }

    /// <summary>
    /// Handles the TextChanged event of the FindTextBox control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void FindTextBox_TextChanged(object sender, EventArgs e)
    {
        EncodeText(findTextBox.Text, dataTypeComboBox.SelectedItem.ToString());
    }

    /// <summary>
    /// Encodes the text to a given data type
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="dataType">The data tyoe.</param>
    private void EncodeText(string text, string dataType)
    {
        keyTextBox.Text = string.Empty;
        hexTextBox.ForeColor = Color.Black;

        switch (dataType)
        {
            case "binary":
                CheckHex(text);
                break;

            case "bigint":
                EncodeInt64(text);
                break;

            case "int":
                EncodeInt32(text);
                break;

            case "smallint":
                EncodeInt16(text);
                break;

            case "tinyint":
                EncodeByte(text);
                break;

            case "varchar":
                EncodeChar(text);
                break;

            case "nvarchar":
                EncodeNChar(text);
                break;

            case "datetime":
                EncodeDateTime(text, false);
                break;

            case "smalldatetime":
                EncodeDateTime(text, true);
                break;

            case "real":
                EncodeReal(text);
                break;

            case "float":
                EncodeFloat(text);
                break;

            case "money":
                EncodeMoney(text, false);
                break;

            case "smallmoney":
                EncodeMoney(text, true);
                break;

            case "decimal":
                EncodeDecimal(text);
                break;
        }
    }

    private void EncodeDecimal(string text)
    {
        decimal value;

        if (decimal.TryParse(text, out value))
        {
            hexTextBox.ForeColor = Color.Black;
            hexTextBox.Text = DataConverter.EncodeDecimal(value);
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void EncodeReal(string text)
    {
        float value;

        if (float.TryParse(text, out value))
        {
            hexTextBox.Text = DataConverter.EncodeReal(value);
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void EncodeMoney(string text, bool small)
    {
        decimal value;

        if (decimal.TryParse(text, out value))
        {
            if (small)
            {
                hexTextBox.Text = DataConverter.EncodeSmallMoney(value);
            }
            else
            {
                hexTextBox.Text = DataConverter.EncodeMoney(value);
            }
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void EncodeFloat(string text)
    {
        double value;

        if (double.TryParse(text, out value))
        {
            hexTextBox.Text = DataConverter.EncodeFloat(value);
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void CheckHex(string text)
    {
        var hexRegex = new Regex("^([0-9a-fA-F])*$");

        if (hexRegex.IsMatch(text))
        {
            hexTextBox.Text = text.ToUpper();
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void EncodeInt64(string text)
    {
        long value;

        if (long.TryParse(text, out value))
        {
            hexTextBox.Text = DataConverter.EncodeInt64(value);
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void EncodeDateTime(string text, bool small)
    {
        DateTime value;

        if (DateTime.TryParse(text, out value))
        {
            var sb = new StringBuilder(rtfHeader);

            string[] dateValue;

            if (small)
            {
                dateValue = DataConverter.EncodeSmallDateTime(value);
            }
            else
            {
                dateValue = DataConverter.EncodeDateTime(value);
            }

            sb.Append(RtfColour.RtfTag(rtfColours, "Blue", "White"));
            sb.Append(dateValue[0]);
            sb.Append("} ");
            sb.Append(RtfColour.RtfTag(rtfColours, "Green", "White"));
            sb.Append(dateValue[1]);
            sb.Append("}");

            hexTextBox.Rtf = sb.ToString();

            sb.Length = 0;

            sb.Append(rtfHeader);

            sb.Append(RtfColour.RtfTag(rtfColours, "Blue", "Control"));
            sb.Append("Time");
            sb.Append("} ");
            sb.Append(RtfColour.RtfTag(rtfColours, "Green", "Control"));
            sb.Append("Date");
            sb.Append("}");

            keyTextBox.Rtf = sb.ToString();
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void EncodeNChar(string text)
    {
        hexTextBox.Text = BitConverter.ToString(Encoding.Unicode.GetBytes(text)).Replace("-", " ");
    }

    private void EncodeChar(string text)
    {
        hexTextBox.Text = BitConverter.ToString(Encoding.UTF8.GetBytes(text)).Replace("-", " ");
    }

    private void EncodeByte(string text)
    {
        if (text.Length == 1)
        {
            hexTextBox.Text = ((byte)text.ToCharArray()[0]).ToString();
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void EncodeInt16(string text)
    {
        short value;

        if (short.TryParse(text, out value))
        {
            hexTextBox.Text = DataConverter.EncodeInt16(value);
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void EncodeInt32(string text)
    {
        int value;

        if (int.TryParse(text, out value))
        {
            hexTextBox.Text = DataConverter.EncodeInt32(value);
        }
        else
        {
            hexTextBox.ForeColor = Color.Red;
            hexTextBox.Text = "N/A";
        }
    }

    private void DataTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        EncodeText(findTextBox.Text, dataTypeComboBox.SelectedItem.ToString());
    }

    public PageViewerWindow ParentWindow
    {
        get => parentWindow;
        set
        {
            parentWindow = value;

            System.Diagnostics.Debug.Print("Parent Window set to " + parentWindow.Page.PageAddress.ToString());

            if (parentWindow == null)
            {
                findButton.Enabled = false;
            }
            else
            {
                parentWindow.Disposed += ParentWindow_Disposed;
            }
        }
    }

    void ParentWindow_Disposed(object sender, EventArgs e)
    {
        findButton.Enabled = false;
    }

    private void FindButton_Click(object sender, EventArgs e)
    {
        if (ParentWindow != null)
        {
            parentWindow.FindNext(hexTextBox.Text.Replace(" ", string.Empty));
        }
        else
        {
            findButton.Enabled = false;
        }
    }
}