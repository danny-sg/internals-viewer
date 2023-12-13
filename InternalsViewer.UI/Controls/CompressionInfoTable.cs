using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Compression;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Controls;

public partial class CompressionInfoTable : UserControl
{
    private class CompressionStructure
    {
        public CompressionStructure(CompressionInfoStructure structure)
        {
            Structure = structure;
        }

        public CompressionInfoStructure Structure { get; set; }

        public string Description => Enum.GetName(typeof(CompressionInfo), Structure);
    }

    public CompressionInfoTable()
    {
        InitializeComponent();
        offsetDataGridView.AutoGenerateColumns = false;
        offsetDataGridView.DataSource = GenerateCompressionElements();
        offsetDataGridView.ClearSelection();
        // AddCompressionElements();
    }

    public void RefreshElements(bool hasAnchor, bool hasDictionary)
    {
        var elements = new List<CompressionStructure>();

        elements.Add(new CompressionStructure(CompressionInfoStructure.Header));

        if (hasAnchor)
        {
            elements.Add(new CompressionStructure(CompressionInfoStructure.Anchor));
        }

        if (hasDictionary)
        {
            elements.Add(new CompressionStructure(CompressionInfoStructure.Dictionary));
        }

        offsetDataGridView.DataSource = elements;
        offsetDataGridView.ClearSelection();
    }

    private List<CompressionStructure> GenerateCompressionElements()
    {
        var elements = new List<CompressionStructure>();

        foreach (CompressionInfoStructure s in Enum.GetValues(typeof(CompressionInfoStructure)))
        {
            if (s != CompressionInfoStructure.None)
            {
                elements.Add(new CompressionStructure(s));
            }
        }

        return elements;
    }

    private void AddCompressionElements()
    {
        bindingSource.ResetBindings(false);
    }

    public CompressionInfoStructure SelectedStructure
    {
        get
        {
            if (offsetDataGridView.SelectedRows.Count > 0)
            {
                return (CompressionInfoStructure)offsetDataGridView.SelectedRows[0].Cells[0].Value;
            }

            return CompressionInfoStructure.None;
        }

        set
        {
            if (value == CompressionInfoStructure.None)
            {
                offsetDataGridView.ClearSelection();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged
    {
        add => Changed += value;
        remove => Changed -= value;
    }

    private event PropertyChangedEventHandler Changed;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        ControlPaint.DrawBorder(e.Graphics,
            new Rectangle(0, 0, Width, Height),
            SystemColors.ControlDark,
            ButtonBorderStyle.Solid);
    }

    protected void OnPropertyChanged(string prop)
    {
        if (null != Changed)
        {
            Changed(this, new PropertyChangedEventArgs(prop));
        }
    }

    private void OffsetDataGridView_SelectionChanged(object sender, EventArgs e)
    {
        if (offsetDataGridView.SelectedRows.Count > 0)
        {
            OnPropertyChanged("CompressionStructure");
        }
    }
}