using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.UI
{
    /// <summary>
    /// Hex Viewer
    /// </summary>
    public partial class HexViewer : UserControl
    {
        private bool hexMode = false;
        private static readonly string rtfLineBreak = @"\par ";
        private bool addressHex;
        private Dictionary<int, Color> colourAndOffsetDictionary;
        private bool colourise;
        private short currentOffset;
        private string dataRtf;
        private string dataText;
        private Color headerColour = Color.FromArgb(245, 245, 250);
        private Color offsetColour = Color.FromArgb(245, 250, 245);
        private readonly List<Marker> markers = new List<Marker>();
        private Page page;
        private readonly VisualStyleRenderer renderer;
        private readonly List<Color> rtfColours;
        private readonly string rtfHeader;
        private int selectedOffset = -1;
        private int selectedRecord = -1;
        private bool suppressTooltip;

        public event EventHandler<OffsetEventArgs> OffsetOver;
        public event EventHandler<OffsetEventArgs> OffsetSet;
        public event EventHandler<OffsetEventArgs> RecordFind;

        /// <summary>
        /// Initializes a new instance of the <see cref="HexViewer"/> class.
        /// </summary>
        public HexViewer()
        {
            InitializeComponent();

            this.rtfColours = RtfColour.CreateColourTable();

            this.rtfColours.Add(this.headerColour);
            this.rtfColours.Add(this.offsetColour);

            this.rtfHeader = RtfColour.CreateRtfHeader(this.rtfColours);

            if (VisualStyleRenderer.IsSupported)
            {
                this.renderer = new VisualStyleRenderer(VisualStyleElement.Header.Item.Normal);
            }

            dataRichTextBox.TextSize = TextRenderer.MeasureText("00", new Font("Courier New", 8.25F));
            dataRichTextBox.TextLineSize = TextRenderer.MeasureText("00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00",
                                                                    new Font("Courier New", 8.25F));
        }

        /// <summary>
        /// Creates RTF output for the page
        /// </summary>
        /// <param name="targetPage">The target page.</param>
        /// <returns></returns>
        private string FormatPageDetails(Page targetPage)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(this.rtfHeader);

            // Start header
            sb.Append(RtfColour.RtfTag(this.rtfColours, Color.Blue.Name, this.headerColour.Name));

            int currentPos = 0;

            for (int rows = 0; rows < targetPage.PageData.Length / 16; rows++)
            {
                for (int cols = 0; cols < 16; cols++)
                {
                    if (currentPos == 96)
                    {
                        // End Header
                        sb.Append("}");
                    }

                    if (currentPos == this.selectedOffset)
                    {
                        sb.Append(@"{\uldb ");
                    }

                    if (currentPos == 8192 - targetPage.Header.SlotCount * 2)
                    {
                        // Start offset table
                        sb.Append(RtfColour.RtfTag(this.rtfColours, Color.Green.Name, this.offsetColour.Name));
                    }

                    // Start marker/colour tag
                    if (this.colourise)
                    {
                        this.FindStartMarkers(currentPos, sb);
                    }

                    // Add the byte
                    sb.Append(DataConverter.ToHexString(targetPage.PageData[currentPos]));

                    // End marker/close colour tag
                    if (this.colourise)
                    {
                        this.FindEndMarkers(currentPos, sb);
                    }

                    if (currentPos == this.selectedOffset)
                    {
                        sb.Append("}");
                    }

                    if (cols != 15)
                    {
                        sb.Append(" ");
                    }

                    currentPos++;
                }

                sb.Append(rtfLineBreak);
            }

            sb.Append("}");

            return sb.ToString();
        }

        /// <summary>
        /// Adds a collection of markers
        /// </summary>
        /// <param name="markers">The markers.</param>
        public void AddMarkers(List<Marker> markers)
        {
            this.markers.Clear();
            this.markers.AddRange(markers);

            this.DataRtf = this.FormatPageDetails(this.page);
        }

        /// <summary>
        /// Clear all markers
        /// </summary>
        public void ClearMarkers()
        {
            this.markers.Clear();
        }

        /// <summary>
        /// Select a given marker
        /// </summary>
        /// <param name="marker">The marker.</param>
        public void SelectMarker(Marker marker)
        {
            if (marker == null || marker.StartPosition < 0)
            {
                return;
            }

            this.suppressTooltip = true;

            dataRichTextBox.SelectionStart = 3 * marker.StartPosition;
            dataRichTextBox.SelectionLength = 3 * (marker.EndPosition - marker.StartPosition + 1) - 1;
            dataRichTextBox.ScrollToCaret();

            this.suppressTooltip = false;
        }

        /// <summary>
        /// Find if a marker starts at a given positions, and if it does add a start RTF colour tag
        /// </summary>
        /// <param name="position">The current position.</param>
        /// <param name="sb">The StringBuilder.</param>
        private void FindStartMarkers(int position, StringBuilder sb)
        {
            List<Marker> startMarkers = this.markers.FindAll(delegate(Marker marker) { return marker.StartPosition == position; });

            foreach (Marker startMarker in startMarkers)
            {
                sb.Append(RtfColour.RtfTag(this.rtfColours, startMarker.ForeColour.Name, startMarker.BackColour.Name));
            }
        }

        /// <summary>
        /// Find if a marker ends at a given positions, and if it does add an end RTF colour tag
        /// </summary>
        /// <param name="position">The current position.</param>
        /// <param name="sb">The StringBuilder.</param>
        private void FindEndMarkers(int position, StringBuilder sb)
        {
            if (position <= 0)
            {
                return;
            }

            List<Marker> endMarkers = this.markers.FindAll(delegate(Marker marker) { return marker.EndPosition == position; });

            for (int i = 0; i < endMarkers.Count; i++)
            {
                sb.Append("}");
            }
        }

        /// <summary>
        /// Gets the marker at a given offset.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        private Marker GetMarkerAtPosition(short offset)
        {
            return this.markers.Find(delegate(Marker marker)
                {
                    return offset >= marker.StartPosition && offset <= marker.EndPosition;
                });
        }

        /// <summary>
        /// Updates the address text box.
        /// </summary>
        private void UpdateAddressTextBox()
        {
            if (this.page == null)
            {
                return;
            }

            StringBuilder addressText = new StringBuilder();

            Point topPos = new Point(0, 0);

            int firstIndex = dataRichTextBox.GetCharIndexFromPosition(topPos);
            int firstLine = dataRichTextBox.GetLineFromCharIndex(firstIndex);

            topPos.X = ClientRectangle.Width;
            topPos.Y = ClientRectangle.Height;

            int lastIndex = dataRichTextBox.GetCharIndexFromPosition(topPos);
            int lastLine = dataRichTextBox.GetLineFromCharIndex(lastIndex);

            for (int i = firstLine; i <= lastLine + 1; i++)
            {
                if ((i * 16) < 8192)
                {
                    if (this.hexMode)
                    {
                        addressText.AppendFormat("{0,8:X}\n", (i * 16)).Replace(" ", "0");
                    }
                    else
                    {
                        addressText.AppendFormat("{0:00000000}\n", (i * 16));
                    }
                }
            }

            addressLabel.Text = addressText.ToString();
        }

        /// <summary>
        /// Refresh the page hex
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();

            if (null != this.page)
            {
                this.DataRtf = this.FormatPageDetails(this.page);
            }
        }

        /// <summary>
        /// Hides the decode tool tip.
        /// </summary>
        public void HideToolTip()
        {
            this.dataToolTip.Hide(this);
        }

        /// <summary>
        /// Creates a decoded data string for the selection
        /// </summary>
        /// <returns></returns>
        private string CreateDataString()
        {
            string dataString = dataRichTextBox.SelectedText.Replace(" ", string.Empty).Replace("\n", string.Empty);

            if (dataString.Length % 2 == 0)
            {
                int startPos = dataRichTextBox.SelectionStart;
                int endPos = dataRichTextBox.SelectionStart + dataRichTextBox.SelectionLength;
                int startOffset = startPos / 3;
                int endOffset = endPos / 3;

                dataToolTip.ToolTipTitle = string.Format(CultureInfo.InvariantCulture,
                                                         "Offset {0} - {1} (0x{2} - 0x{3})",
                                                         startOffset,
                                                         endOffset,
                                                         DataConverter.ToHexString((byte)startOffset),
                                                         DataConverter.ToHexString((byte)endOffset));

                List<string> decodedData = DataConverter.DecodeDataString(dataString);
                StringBuilder decodedDataStringBuilder = new StringBuilder();

                foreach (string s in decodedData)
                {
                    decodedDataStringBuilder.AppendLine(s);
                }

                return decodedDataStringBuilder.ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Scrolls to an offset.
        /// </summary>
        /// <param name="offset">The offset.</param>
        public void ScrollToOffset(int offset)
        {
            if (dataRichTextBox.GetPositionFromCharIndex(3 * offset).Y > dataRichTextBox.Height)
            {
                dataRichTextBox.SelectionStart = 3 * offset;
                dataRichTextBox.ScrollToCaret();
            }
        }

        /// <summary>
        /// Sets the selection range.
        /// </summary>
        /// <param name="startPos">The start pos.</param>
        /// <param name="endPos">The end pos.</param>
        public void SetSelection(int startPos, int endPos)
        {
            dataRichTextBox.SelectionStart = 3 * startPos;
            dataRichTextBox.SelectionLength = 3 * (endPos - startPos + 1) - 1;
            dataRichTextBox.ScrollToCaret();
        }

        #region Events

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Control.Paint"/> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.Forms.PaintEventArgs"/> that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            ControlPaint.DrawBorder(e.Graphics,
                                    new Rectangle(0, 0, Width, Height),
                                    SystemColors.ControlDark, ButtonBorderStyle.Solid);
        }

        /// <summary>
        /// Handles the Paint event of the HeaderPanel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            Rectangle addressRectangle = new Rectangle(headerPanel.Bounds.X,
                                                       headerPanel.Bounds.Y,
                                                       addressLabel.Width,
                                                       headerPanel.Height + 1);

            Rectangle dataRectangle = new Rectangle(headerPanel.Bounds.X + addressLabel.Width,
                                                    headerPanel.Bounds.Y,
                                                    headerPanel.Width - addressLabel.Width,
                                                    headerPanel.Height + 1);

            if (VisualStyleRenderer.IsSupported)
            {
                this.renderer.DrawBackground(e.Graphics, addressRectangle);
                this.renderer.DrawBackground(e.Graphics, dataRectangle);
            }
            else
            {
                e.Graphics.FillRectangle(SystemBrushes.ControlLight,
                                         new Rectangle(0, 0, headerPanel.Width, headerPanel.Height));
            }

            TextRenderer.DrawText(e.Graphics,
                                  "Address",
                                  this.Font,
                                  new Point(addressRectangle.X + 2, addressRectangle.Y + 4),
                                  SystemColors.ControlText);

            TextRenderer.DrawText(e.Graphics,
                                  "Data",
                                  this.Font,
                                  new Point(dataRectangle.X + 2, dataRectangle.Y + 4),
                                  SystemColors.ControlText);

            e.Graphics.DrawLine(SystemPens.ControlDark, 0, 0, Width, 0);
        }

        /// <summary>
        /// Handles the Paint event of the LeftPanel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.PaintEventArgs"/> instance containing the event data.</param>
        private void LeftPanel_Paint(object sender, PaintEventArgs e)
        {
            using (LinearGradientBrush gradientBrush = new LinearGradientBrush(leftPanel.Bounds,
                                                                               Color.White,
                                                                               Color.WhiteSmoke,
                                                                               LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(gradientBrush, leftPanel.ClientRectangle);
            }
        }

        /// <summary>
        /// Handles the VScroll event of the DataRichTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void DataRichTextBox_VScroll(object sender, EventArgs e)
        {
            this.UpdateAddressTextBox();

            int position = dataRichTextBox.GetPositionFromCharIndex(0).Y % (addressLabel.Font.Height + 1);
            addressLabel.Location = new Point(1, position);
        }

        /// <summary>
        /// Handles the Resize event of the DataRichTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void DataRichTextBox_Resize(object sender, EventArgs e)
        {
            addressLabel.Height = dataRichTextBox.Height + 40;

            this.DataRichTextBox_VScroll(null, null);
        }

        /// <summary>
        /// Handles the Leave event of the DataRichTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void DataRichTextBox_Leave(object sender, EventArgs e)
        {
            dataToolTip.Active = false;
        }

        /// <summary>
        /// Handles the Click event of the CopyToolStripMenuItem control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(this.CreateDataString());
        }

        /// <summary>
        /// Handles the MouseMove event of the DataRichTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.MouseEventArgs"/> instance containing the event data.</param>
        private void DataRichTextBox_MouseMove(object sender, MouseEventArgs e)
        {
            short offset = (short)(dataRichTextBox.GetCharIndexFromPosition(e.Location) / 3);

            setOffsetToolStripMenuItem.Text = "Set offset to: " + offset;
            this.currentOffset = offset;
            string markerDescription = string.Empty;

            Marker hoverMarker = this.GetMarkerAtPosition(offset);
            Color foreColour = Color.Black;
            Color backColour = Color.Transparent;

            if (hoverMarker != null)
            {
                markerDescription = hoverMarker.Name;
                foreColour = hoverMarker.ForeColour;
                backColour = hoverMarker.BackColour;
            }

            EventHandler<OffsetEventArgs> temp = this.OffsetOver;

            if (temp != null)
            {
                temp(this, new OffsetEventArgs(offset, markerDescription, foreColour, backColour));
            }
        }

        /// <summary>
        /// Handles the MouseLeave event of the DataRichTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void DataRichTextBox_MouseLeave(object sender, EventArgs e)
        {
            OnMouseLeave(EventArgs.Empty);
        }

        /// <summary>
        /// Handles the SelectionChanged event of the DataRichTextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void DataRichTextBox_SelectionChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(dataRichTextBox.SelectedText) & !this.suppressTooltip)
            {
                int charIndex = dataRichTextBox.SelectionStart + dataRichTextBox.SelectionLength + 47;
                Point dataToolTipPoint = dataRichTextBox.GetPositionFromCharIndex(charIndex);

                dataToolTipPoint.Y += 22;

                dataToolTip.Active = true;
                dataToolTip.Show(this.CreateDataString(), this, dataToolTipPoint);
            }
            else
            {
                dataToolTip.Hide(this);
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the colour and offset dictionary.
        /// </summary>
        /// <value>The colour and offset dictionary.</value>
        public Dictionary<int, Color> ColourAndOffsetDictionary
        {
            get { return this.colourAndOffsetDictionary; }
            set { this.colourAndOffsetDictionary = value; }
        }

        /// <summary>
        /// Gets or sets the data text.
        /// </summary>
        /// <value>The data text.</value>
        public string DataText
        {
            get
            {
                return this.dataText;
            }

            set
            {
                this.dataText = value;
                this.dataRichTextBox.Text = this.dataText;
            }
        }

        /// <summary>
        /// Gets or sets the data RTF.
        /// </summary>
        /// <value>The data RTF.</value>
        public string DataRtf
        {
            get
            {
                return this.dataRtf;
            }

            set
            {
                this.dataRtf = value;
                this.dataRichTextBox.Rtf = this.DataRtf;
                this.UpdateAddressTextBox();
            }
        }

        /// <summary>
        /// Gets or sets the page.
        /// </summary>
        /// <value>The page.</value>
        public Page Page
        {
            get
            {
                return this.page;
            }

            set
            {
                this.page = value;

                if (null != this.page)
                {
                    this.markers.Clear();
                    this.Refresh();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use hex or integer numbers for the address
        /// </summary>
        /// <value><c>true</c> if [address hex]; otherwise, <c>false</c>.</value>
        public bool AddressHex
        {
            get { return this.addressHex; }
            set { this.addressHex = value; }
        }

        /// <summary>
        /// Gets or sets the selected record.
        /// </summary>
        /// <value>The selected record.</value>
        public int SelectedRecord
        {
            get
            {
                return this.selectedRecord;
            }

            set
            {
                this.selectedRecord = value;

                if (null != this.page)
                {
                    this.DataRtf = this.FormatPageDetails(this.page);
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected offset.
        /// </summary>
        /// <value>The selected offset.</value>
        public int SelectedOffset
        {
            get
            {
                return this.selectedOffset;
            }

            set
            {
                this.selectedOffset = value;
                this.Refresh();
            }
        }

        /// <summary>
        /// Gets the index of the selection char.
        /// </summary>
        /// <value>The index of the selection char.</value>
        public int SelectionCharIndex
        {
            get { return this.dataRichTextBox.SelectionStart / 3; }
        }

        /// <summary>
        /// Gets the length of the selection.
        /// </summary>
        /// <value>The length of the selection.</value>
        public int SelectionLength
        {
            get { return this.dataRichTextBox.SelectionLength / 3; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the hex viewer is colourised by markers
        /// </summary>
        /// <value><c>true</c> if colourise; otherwise, <c>false</c>.</value>
        public bool Colourise
        {
            get { return this.colourise; }
            set { this.colourise = value; }
        }

        /// <summary>
        /// </summary>
        /// <value></value>
        /// <returns>The text associated with this control.</returns>
        public override string Text
        {
            get { return this.dataRichTextBox.Text; }
        }

        #endregion
    }
}
