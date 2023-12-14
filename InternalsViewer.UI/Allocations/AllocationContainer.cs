using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using InternalsViewer.Internals;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;
using InternalsViewer.Internals.Engine.Database;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Allocations;

/// <summary>
/// Allocation Container containing one of more allocation maps
/// </summary>
public partial class AllocationContainer : UserControl
{
    private Size extentSize = new(64, 8);
    private MapMode mode;
    private bool showFileInformation;

    public event EventHandler<PageEventArgs> PageClicked;
    public event EventHandler<PageEventArgs> PageOver;
    public event EventHandler RangeSelected;

    public Database? CurrentDatabase { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AllocationContainer"/> class.
    /// </summary>
    public AllocationContainer()
    {
        InitializeComponent();

        Paint += AllocationContainer_Paint;

        SetStyle(ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.DoubleBuffer, true);
    }

    /// <summary>
    /// Creates the allocation maps.
    /// </summary>
    /// <param name="files">The database files.</param>
    public void CreateAllocationMaps(List<DatabaseFile> files)
    {
        SuspendLayout();

        tableLayoutPanel.SuspendLayout();
        tableLayoutPanel.Controls.Clear();
        AllocationMaps.Clear();

        tableLayoutPanel.RowCount = 2;
        tableLayoutPanel.RowStyles.Clear();

        tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 1.0F));

        var fileIndex = 0;

        foreach (var file in files)
        {
            var allocationMap = CreateAllocationMap(file);
            allocationMap.ExtentSize = ExtentSize;
            allocationMap.Mode = Mode;

            var filePanel = new Panel();
            filePanel.Margin = new Padding(0);
            filePanel.Controls.Add(allocationMap);

            filePanel.Dock = DockStyle.Fill;
            tableLayoutPanel.Controls.Add(filePanel, 1, fileIndex);

            filePanel.Margin = new Padding(0, 0, 0, 4);

            if (fileIndex > 0)
            {
                tableLayoutPanel.RowCount += 1;
                tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 1.0F));
            }

            if (!showFileInformation && fileIndex == files.Count - 1)
            {
                filePanel.Margin = new Padding(0);
            }
            else if (showFileInformation)
            {
                filePanel.Controls.Add(new FileInformationControl(file));
            }

            fileIndex++;
        }

        tableLayoutPanel.Invalidate();
        tableLayoutPanel.ResumeLayout();

        ResumeLayout();
    }


    internal void CreateAllocationMaps(Dictionary<int, AllocationMap> dictionary)
    {
        throw new NotImplementedException();
    }

    public override void Refresh()
    {
        base.Refresh();
        Invalidate();
    }

    public void AddMapLayer(AllocationLayer layer)
    {
        AllocationLayers.Add(layer);

        foreach (var allocationMap in AllocationMaps.Values)
        {
            allocationMap.Invalidate();
        }
    }

    public void ClearMapLayers()
    {
        AllocationLayers.Clear();

        foreach (var allocationMap in AllocationMaps.Values)
        {
            allocationMap.Invalidate();
        }
    }

    public bool RemoveLayer(string name)
    {
        var existing = AllocationLayers.FirstOrDefault(layer => (layer.Name == name));

        if (existing != null)
        {
            AllocationLayers.Remove(existing);

            foreach (var allocationMap in AllocationMaps.Values)
            {
                allocationMap.Invalidate();
            }

            return true;
        }

        return false;
    }

    public Size CalculateFitSize()
    {
        double maxExtentCount = 0;

        foreach (var map in AllocationMaps.Values)
        {
            if (map.ExtentCount > maxExtentCount)
            {
                maxExtentCount = map.ExtentCount;
            }
        }

        double width = Width;
        double height = Height / 8;

        var extentsPerRow = Math.Sqrt(maxExtentCount / 8);

        var returnSize = new Size((int)(width / extentsPerRow), (int)(height / extentsPerRow));

        if (returnSize.Height < 1 || returnSize.Width < 1)
        {
            returnSize = new Size(8, 1);
        }

        return returnSize;
    }

    internal void ShowFittedMap()
    {
        foreach (var allocationMap in AllocationMaps.Values)
        {
            allocationMap.ShowFullMap();
        }
    }

    protected void AllocationContainer_Paint(object sender, PaintEventArgs e)
    {
        if (AllocationMaps.Count == 0)
        {
            ControlPaint.DrawBorder(e.Graphics,
                                    new Rectangle(0, 0, Width, Height),
                                    SystemColors.ControlDark,
                                    ButtonBorderStyle.Solid);
        }
    }

    private AllocationMap CreateAllocationMap(DatabaseFile file)
    {
        var allocationMap = new AllocationMap();

        allocationMap.FileId = file.FileId;
        allocationMap.File = file;
        allocationMap.ExtentCount = file.Size / 8;
        allocationMap.Dock = DockStyle.Fill;
        allocationMap.MapLayers = AllocationLayers;

        allocationMap.PageClicked += AllocationMap_PageClicked;
        allocationMap.PageOver += AllocationMap_PageOver;
        allocationMap.RangeSelected += AllocationMap_RangeSelected;

        AllocationMaps.Add(file.FileId, allocationMap);

        return allocationMap;
    }

    private void AllocationMap_RangeSelected(object sender, EventArgs e)
    {
        RangeSelected?.Invoke(this, e);
    }

    private void AllocationMap_PageOver(object sender, PageEventArgs e)
    {
        PageOver?.Invoke(this, e);
    }

    private void AllocationMap_PageClicked(object sender, PageEventArgs e)
    {
        PageClicked?.Invoke(this, e);
    }

    public bool ShowFileInformation
    {
        get => showFileInformation;

        set
        {
            if (value != showFileInformation && CurrentDatabase != null)
            {
                showFileInformation = value;
                CreateAllocationMaps(CurrentDatabase.Files);
            }
        }
    }

    public MapMode Mode
    {
        get => mode;

        set
        {
            mode = value;

            foreach (var allocationMap in AllocationMaps.Values)
            {
                allocationMap.Mode = mode;
            }
        }
    }

    internal Dictionary<int, PfsChain> Pfs
    {
        set
        {
            foreach (var allocationMap in AllocationMaps.Values)
            {
                allocationMap.Pfs = value[allocationMap.FileId];
            }
        }
    }

    internal PfsByte PagePfsByte(PageAddress pageAddress)
    {
        return AllocationMaps[pageAddress.FileId].Pfs.GetPagePfsStatus(pageAddress.PageId);
    }

    public List<AllocationLayer> AllocationLayers { get; } = new();

    public Size ExtentSize
    {
        get => extentSize;

        set
        {
            extentSize = value;

            foreach (var allocationMap in AllocationMaps.Values)
            {
                allocationMap.ExtentSize = extentSize;
            }
        }
    }

    public LayoutStyle LayoutStyle { get; set; }

    public bool IncludeIam { get; set; }

    public Dictionary<int, AllocationMap> AllocationMaps { get; } = new();

    public bool DrawBorder
    {
        get
        {
            if (AllocationMaps.Count > 1)
            {
                return AllocationMaps[0].DrawBorder;
            }

            return true;
        }

        set
        {
            foreach (var allocationMap in AllocationMaps.Values)
            {
                allocationMap.DrawBorder = value;
            }
        }
    }

    public bool Holding
    {
        get
        {
            if (AllocationMaps.Count > 1 && CurrentDatabase != null)
            {
                return AllocationMaps[CurrentDatabase.Files[0].FileId].Holding;
            }

            return true;
        }

        set
        {
            foreach (var allocationMap in AllocationMaps.Values)
            {
                allocationMap.Holding = value;
            }
        }
    }

    public string HoldingMessage
    {
        get
        {
            if (AllocationMaps.Count > 1)
            {
                return AllocationMaps[0].HoldingMessage;
            }

            return string.Empty;
        }

        set
        {
            foreach (var allocationMap in AllocationMaps.Values)
            {
                allocationMap.HoldingMessage = value;
            }
        }
    }
}