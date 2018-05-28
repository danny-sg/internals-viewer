using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using InternalsViewer.Internals;
using System.Text.RegularExpressions;

namespace InternalsViewer.UI
{
    public partial class DecodeWindow : UserControl
    {
        private PageViewerWindow parentWindow;
        private readonly List<Color> rtfColours;
        private readonly string rtfHeader;

        public DecodeWindow()
        {
            InitializeComponent();

            dataTypeComboBox.SelectedIndex = 0;

            this.rtfColours = RtfColour.CreateColourTable();
            this.rtfHeader = RtfColour.CreateRtfHeader(this.rtfColours);
        }

        /// <summary>
        /// Handles the TextChanged event of the FindTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void FindTextBox_TextChanged(object sender, EventArgs e)
        {
            this.EncodeText(findTextBox.Text, dataTypeComboBox.SelectedItem.ToString());
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
                    this.CheckHex(text);
                    break;

                case "bigint":
                    this.EncodeInt64(text);
                    break;

                case "int":
                    this.EncodeInt32(text);
                    break;

                case "smallint":
                    this.EncodeInt16(text);
                    break;

                case "tinyint":
                    this.EncodeByte(text);
                    break;

                case "varchar":
                    this.EncodeChar(text);
                    break;

                case "nvarchar":
                    this.EncodeNChar(text);
                    break;

                case "datetime":
                    this.EncodeDateTime(text, false);
                    break;

                case "smalldatetime":
                    this.EncodeDateTime(text, true);
                    break;

                case "real":
                    this.EncodeReal(text);
                    break;

                case "float":
                    this.EncodeFloat(text);
                    break;

                case "money":
                    this.EncodeMoney(text, false);
                    break;

                case "smallmoney":
                    this.EncodeMoney(text, true);
                    break;

                case "decimal":
                    this.EncodeDecimal(text);
                    break;
            }
        }

        private void EncodeDecimal(string text)
        {
            decimal value;

            if (decimal.TryParse(text, out value))
            {
                this.hexTextBox.ForeColor = Color.Black;
                this.hexTextBox.Text = DataConverter.EncodeDecimal(value);
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void EncodeReal(string text)
        {
            float value;

            if (float.TryParse(text, out value))
            {
                this.hexTextBox.Text = DataConverter.EncodeReal(value);
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void EncodeMoney(string text, bool small)
        {
            decimal value;

            if (decimal.TryParse(text, out value))
            {
                if (small)
                {
                    this.hexTextBox.Text = DataConverter.EncodeSmallMoney(value);
                }
                else
                {
                    this.hexTextBox.Text = DataConverter.EncodeMoney(value);
                }
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void EncodeFloat(string text)
        {
            double value;

            if (double.TryParse(text, out value))
            {
                this.hexTextBox.Text = DataConverter.EncodeFloat(value);
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void CheckHex(string text)
        {
            var hexRegex = new Regex("^([0-9a-fA-F])*$");

            if (hexRegex.IsMatch(text))
            {
                this.hexTextBox.Text = text.ToUpper();
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void EncodeInt64(string text)
        {
            long value;

            if (long.TryParse(text, out value))
            {
                this.hexTextBox.Text = DataConverter.EncodeInt64(value);
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void EncodeDateTime(string text, bool small)
        {
            DateTime value;

            if (DateTime.TryParse(text, out value))
            {
                var sb = new StringBuilder(this.rtfHeader);

                string[] dateValue;

                if (small)
                {
                    dateValue = DataConverter.EncodeSmallDateTime(value);
                }
                else
                {
                    dateValue = DataConverter.EncodeDateTime(value);
                }

                sb.Append(RtfColour.RtfTag(this.rtfColours, "Blue", "White"));
                sb.Append(dateValue[0]);
                sb.Append("} ");
                sb.Append(RtfColour.RtfTag(this.rtfColours, "Green", "White"));
                sb.Append(dateValue[1]);
                sb.Append("}");

                this.hexTextBox.Rtf = sb.ToString();

                sb.Length = 0;

                sb.Append(this.rtfHeader);

                sb.Append(RtfColour.RtfTag(this.rtfColours, "Blue", "Control"));
                sb.Append("Time");
                sb.Append("} ");
                sb.Append(RtfColour.RtfTag(this.rtfColours, "Green", "Control"));
                sb.Append("Date");
                sb.Append("}");

                this.keyTextBox.Rtf = sb.ToString();
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void EncodeNChar(string text)
        {
            this.hexTextBox.Text = BitConverter.ToString(Encoding.Unicode.GetBytes(text)).Replace("-", " ");
        }

        private void EncodeChar(string text)
        {
            this.hexTextBox.Text = BitConverter.ToString(Encoding.UTF8.GetBytes(text)).Replace("-", " ");
        }

        private void EncodeByte(string text)
        {
            if (text.Length == 1)
            {
                this.hexTextBox.Text = ((byte)text.ToCharArray()[0]).ToString();
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void EncodeInt16(string text)
        {
            short value;

            if (short.TryParse(text, out value))
            {
                this.hexTextBox.Text = DataConverter.EncodeInt16(value);
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void EncodeInt32(string text)
        {
            int value;

            if (int.TryParse(text, out value))
            {
                this.hexTextBox.Text = DataConverter.EncodeInt32(value);
            }
            else
            {
                this.hexTextBox.ForeColor = Color.Red;
                this.hexTextBox.Text = "N/A";
            }
        }

        private void DataTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.EncodeText(findTextBox.Text, dataTypeComboBox.SelectedItem.ToString());
        }

        public PageViewerWindow ParentWindow
        {
            get
            {
                return parentWindow;
            }
            set
            {
                this.parentWindow = value;

                System.Diagnostics.Debug.Print("Parent Window set to " + parentWindow.Page.PageAddress.ToString());

                if (this.parentWindow == null)
                {
                    this.findButton.Enabled = false;
                }
                else
                {
                    this.parentWindow.Disposed += new EventHandler(ParentWindow_Disposed);
                }
            }
        }

        void ParentWindow_Disposed(object sender, EventArgs e)
        {
            this.findButton.Enabled = false;
        }

        private void FindButton_Click(object sender, EventArgs e)
        {
            if (this.ParentWindow != null)
            {
                this.parentWindow.FindNext(hexTextBox.Text.Replace(" ", string.Empty));
            }
            else
            {
                findButton.Enabled = false;
            }
        }
    }
}
