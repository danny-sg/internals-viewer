using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;
using InternalsViewer.UI.Markers;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Controls;

public partial class MarkerKeyTable : UserControl
{
    private bool loading;
    public event EventHandler SelectionChanged;
    public event EventHandler SelectionClicked;
    public event EventHandler<PageEventArgs> PageNavigated;

    public MarkerKeyTable()
    {
        InitializeComponent();
        markerBindingSource.CurrentChanged += MarkerBindingSource_CurrentChanged;
    }

    private void MarkerBindingSource_CurrentChanged(object sender, EventArgs e)
    {
        if (!loading)
        {
            OnSelectedMarkerChanged(sender, e);
        }
    }

    internal virtual void OnSelectedMarkerChanged(object sender, EventArgs e)
    {
        if (SelectionChanged != null)
        {
            SelectionChanged(sender, e);
        }
    }

    public void SetMarkers(List<Marker> markers)
    {
        loading = true;
        markerBindingSource.DataSource = markers;
        loading = false;
    }

    public void ClearMarkers()
    {
        markerBindingSource.DataSource = null;
    }

    private void MarkersDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        try
        {
            if (e.ColumnIndex == 0 && e.CellStyle != null)
            {
                markersDataGridView.Rows[e.RowIndex].SetValues("00");

                Color? backColour;

                if (e.RowIndex % 2 == 0)
                {
                    backColour = (Color?)markersDataGridView.Rows[e.RowIndex].Cells["BackColourColumn"].Value;


                }
                else
                {
                    backColour = (Color?)markersDataGridView.Rows[e.RowIndex].Cells["AlternateBackColourColumn"].Value;
                }

                if (backColour != null)
                {
                    e.CellStyle.BackColor = backColour.Value;
                }

                e.CellStyle.SelectionBackColor = e.CellStyle.BackColor;

                e.CellStyle.ForeColor = (Color)markersDataGridView.Rows[e.RowIndex].Cells["ForeColourColumn"].Value;
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            }

            if (e.ColumnIndex == 7 | e.ColumnIndex == 8)
            {
                if ((bool)markersDataGridView.Rows[e.RowIndex].Cells["IsNullColumn"].Value)
                {
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.SelectionForeColor = Color.White;
                }
            }

            if (e.ColumnIndex == markersDataGridView.Columns["valueDataGridViewTextboxColumn"].Index)
            {
                if ((MarkerType)markersDataGridView.Rows[e.RowIndex].Cells["DataTypeColumn"].Value ==
                    MarkerType.PageAddress)
                {
                    ((DataGridViewLinkCell)markersDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex]).LinkColor = Color.Blue;
                    ((DataGridViewLinkCell)markersDataGridView.Rows[e.RowIndex].Cells[e.ColumnIndex]).LinkBehavior = LinkBehavior.AlwaysUnderline;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Print(ex.ToString());
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlPaint.DrawBorder(e.Graphics, new Rectangle(0, 0, Width, Height), SystemColors.ControlDark,
            ButtonBorderStyle.Solid);
    }

    private void MarkersDataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0)
        {
            if ((MarkerType)markersDataGridView.Rows[e.RowIndex].Cells["DataTypeColumn"].Value ==
                MarkerType.PageAddress)
            {
                var temp = PageNavigated;

                if (temp != null)
                {
                    var pageEvent =
                        new PageEventArgs(
                            RowIdentifier.Parse(
                                markersDataGridView.Rows[e.RowIndex].Cells["valueDataGridViewTextBoxColumn"].Value.
                                    ToString()), false);

                    temp(this, pageEvent);
                }
            }
            else
            {
                if (SelectionClicked != null)
                    SelectionClicked(sender, e);
            }
        }
    }

    private void MarkersDataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (SelectionChanged != null && e.RowIndex >= 0 &&
            (MarkerType)markersDataGridView.Rows[e.RowIndex].Cells["DataTypeColumn"].Value !=
            MarkerType.PageAddress)
        {
            SelectionChanged(sender, e);
        }
    }

    public Marker SelectedMarker => (Marker)markerBindingSource.Current;
}