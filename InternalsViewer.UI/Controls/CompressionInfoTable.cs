using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using InternalsViewer.Internals.Compression;

namespace InternalsViewer.UI.Controls
{
    public partial class CompressionInfoTable : UserControl
    {
        private class CompressionStructure
        {
            public CompressionStructure(CompressionInformation.CompressionInfoStructure structure)
            {
                this.Structure = structure;
            }
            private CompressionInformation.CompressionInfoStructure structure;

            public CompressionInformation.CompressionInfoStructure Structure
            {
                get { return structure; }
                set { structure = value; }
            }

            public string Description
            {
                get { return Enum.GetName(typeof(CompressionInformation.CompressionInfoStructure), structure); }
            }
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
            List<CompressionStructure> elements = new List<CompressionStructure>();

            elements.Add(new CompressionStructure(CompressionInformation.CompressionInfoStructure.Header));

            if (hasAnchor)
            {
                elements.Add(new CompressionStructure(CompressionInformation.CompressionInfoStructure.Anchor));
            }

            if (hasDictionary)
            {
                elements.Add(new CompressionStructure(CompressionInformation.CompressionInfoStructure.Dictionary));
            }

            offsetDataGridView.DataSource = elements;
            offsetDataGridView.ClearSelection();
        }

        private List<CompressionStructure> GenerateCompressionElements()
        {
            List<CompressionStructure> elements = new List<CompressionStructure>();

            foreach (CompressionInformation.CompressionInfoStructure s in Enum.GetValues(typeof(CompressionInformation.CompressionInfoStructure)))
            {
                if (s != CompressionInformation.CompressionInfoStructure.None)
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

        public CompressionInformation.CompressionInfoStructure SelectedStructure
        {
            get
            {
                if (offsetDataGridView.SelectedRows.Count > 0)
                {
                    return (CompressionInformation.CompressionInfoStructure)offsetDataGridView.SelectedRows[0].Cells[0].Value;
                }
                else
                {
                    return CompressionInformation.CompressionInfoStructure.None;
                }
            }

            set
            {
                if (value == CompressionInformation.CompressionInfoStructure.None)
                {
                    offsetDataGridView.ClearSelection();
                }
            }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        #endregion

        private event PropertyChangedEventHandler _changed;

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
            if (null != _changed)
            {
                _changed(this, new PropertyChangedEventArgs(prop));
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
}
