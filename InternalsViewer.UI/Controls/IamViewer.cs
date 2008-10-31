using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Markers;
using InternalsViewer.UI.Allocations;

namespace InternalsViewer.UI.Controls
{
    public partial class IamViewer : UserControl
    {
        public IamViewer()
        {
            InitializeComponent();
            
            this.SetColours(IamViewer.CreateIamColours());
        }

        private static List<Color> CreateIamColours()
        {
            List<Color> colours = new List<Color>(9);

            colours.Add(Color.LightGreen);
            colours.Add(Color.Lavender);
            colours.Add(Color.LightBlue);
            colours.Add(Color.LightSteelBlue);
            colours.Add(Color.LightSkyBlue);
            colours.Add(Color.Azure);
            colours.Add(Color.PowderBlue);
            colours.Add(Color.SkyBlue);
            colours.Add(Color.AliceBlue);

            return colours;
        }

        private void SetIamInformation(AllocationPage page)
        {
            if (page.SinglePageSlots.Count == 8)
            {
                slotZeroTextBox.Text = page.SinglePageSlots[0].ToString();
                slotOneTextBox.Text = page.SinglePageSlots[1].ToString();
                slotTwoTextBox.Text = page.SinglePageSlots[2].ToString();
                slotThreeTextBox.Text = page.SinglePageSlots[3].ToString();
                slotFourTextBox.Text = page.SinglePageSlots[4].ToString();
                slotFiveTextBox.Text = page.SinglePageSlots[5].ToString();
                slotSixTextBox.Text = page.SinglePageSlots[6].ToString();
                slotSevenTextBox.Text = page.SinglePageSlots[7].ToString();
                startPageTextBox.Text = page.StartPage.ToString();
            }
        }

        public void SetColours(List<Color> colours)
        {
            startPageBox.Image = ExtentColour.KeyImage(colours[0]);
            slot0Box.Image = ExtentColour.KeyImage(colours[1]);
            slot1Box.Image = ExtentColour.KeyImage(colours[2]);
            slot2Box.Image = ExtentColour.KeyImage(colours[3]);
            slot3Box.Image = ExtentColour.KeyImage(colours[4]);
            slot4Box.Image = ExtentColour.KeyImage(colours[5]);
            slot5Box.Image = ExtentColour.KeyImage(colours[6]);
            slot6Box.Image = ExtentColour.KeyImage(colours[7]);
            slot7Box.Image = ExtentColour.KeyImage(colours[8]);
        }

        private void topPanel_Paint(object sender, PaintEventArgs e)
        {
            ControlPaint.DrawBorder(e.Graphics,
                                    new Rectangle(topPanel.Bounds.X, topPanel.Bounds.Y, topPanel.Width, topPanel.Height + 2),
                                    SystemColors.ControlDark,
                                    ButtonBorderStyle.Solid);
        }

        public List<Marker> SetAllocationPage(PageAddress pageAddress, string databaseName, string connectionString)
        {
            AllocationPage allocationPage = new AllocationPage(connectionString, databaseName, pageAddress);
            
            allocationMap.ExtentCount = 63903;
            allocationMap.StartPage = allocationPage.StartPage;
            allocationMap.FileId = allocationPage.StartPage.FileId;

            AllocationLayer layer = new AllocationLayer(allocationPage.PageAddress.ToString(),
                                                        allocationPage,
                                                        Color.Brown);

            layer.SingleSlotsOnly = false;
            
            allocationMap.MapLayers.Clear();
            allocationMap.MapLayers.Add(layer);

            allocationMap.Invalidate();

            this.SetIamInformation(allocationPage);

            List<Marker> markers = MarkerBuilder.BuildMarkers(allocationPage, string.Empty);

            return markers;
        }
    }
}
