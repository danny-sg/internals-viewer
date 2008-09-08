using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using InternalsViewer.Internals.Structures;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.UI.VSIntegration;
using Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer;

namespace InternalsViewer.SSMSAddIn
{
    class ObjectExplorerExtender
    {
        public ObjectExplorerExtender()
        {
            TreeView tree = GetObjectExplorerTreeView();

            tree.AfterExpand += new TreeViewEventHandler(TreeView_AfterExpand);
            tree.BeforeExpand += new TreeViewCancelEventHandler(TreeView_BeforeExpand);

            tree.ImageList.Images.Add("Page", Properties.Resources.pageImage);
        }


        private TreeView GetObjectExplorerTreeView()
        {
            Type t = ServiceCache.GetObjectExplorer().GetType();
            FieldInfo field = t.GetField("tree", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                return (TreeView)field.GetValue(ServiceCache.GetObjectExplorer());
            }
            else
            {
                return null;
            }
        }

        private void TreeView_AfterExpand(object sender, System.Windows.Forms.TreeViewEventArgs e)
        {
            if (e.Node.FullPath.Substring(e.Node.FullPath.LastIndexOf(@"\") + 1).StartsWith("Indexes"))
            {
                string tableName = e.Node.Parent.Text;
                int tableImageIndex = e.Node.Parent.ImageIndex;

                string databaseName = e.Node.Parent.Parent.Parent.Text;

                // Wait for the async node expand to finish or we could miss indexes
                while ((e.Node as HierarchyTreeNode).Expanding)
                {
                    Application.DoEvents();
                }

                foreach (TreeNode node in e.Node.Nodes)
                {
                    if (node.Text != "(Heap)")
                    {
                        AddIndexPageNodes(node, databaseName, tableName, NodeName(node), 1);
                    }
                }
            }
        }

        private void TreeView_BeforeExpand(object sender, System.Windows.Forms.TreeViewCancelEventArgs e)
        {
            if (e.Node.Text == "Indexes")
            {
                string tableName = e.Node.Parent.Text;
                int tableImageIndex = e.Node.Parent.ImageIndex;

                string databaseName = e.Node.Parent.Parent.Parent.Text;

                if (Hobt.HobtType(GetConnectionString(), databaseName, tableName) == StructureType.Heap)
                {
                    TreeNode heapNode = new TreeNode("(Heap)", tableImageIndex, tableImageIndex);

                    AddIndexPageNodes(heapNode, databaseName, tableName, string.Empty, e.Node.Parent.Parent.ImageIndex);

                    e.Node.Nodes.Add(heapNode);
                }
            }
        }

        private string GetConnectionString()
        {
            return ConnectionManager.GetConnectionString(ServiceCache.ScriptFactory.CurrentlyActiveWndConnectionInfo.UIConnectionInfo);
        }

        private void AddIndexPageNodes(TreeNode node, string databaseName, string tableName, string indexName, int folderImageIndex)
        {
            // This suppresses the Object Explorer expand behavior
            ChildrenEnumerated(node, true);

            List<HobtEntryPoint> entryPoints = Hobt.EntryPoints(GetConnectionString(), databaseName, tableName, indexName);

            bool partitioned = entryPoints.Count > 1;

            foreach (HobtEntryPoint entryPoint in entryPoints)
            {
                TreeNode parentNode;

                if (partitioned)
                {
                    parentNode = new TreeNode(string.Format("Partition {0}", entryPoint.PartitionNumber));
                    parentNode.SelectedImageIndex = folderImageIndex;
                    parentNode.ImageIndex = folderImageIndex;

                    node.Nodes.Add(parentNode);
                }
                else
                {
                    parentNode = node;
                }

                TreeNode firstIam = new TreeNode(string.Format("First IAM {0}", entryPoint.FirstIam));
                firstIam.SelectedImageKey = "Page";
                firstIam.ImageKey = "Page";

                TreeNode rootPage = new TreeNode(string.Format("Root Page {0}", entryPoint.RootPage));
                rootPage.SelectedImageKey = "Page";
                rootPage.ImageKey = "Page";

                TreeNode firstPage = new TreeNode(string.Format("First Page {0}", entryPoint.FirstPage));
                firstPage.SelectedImageKey = "Page";
                firstPage.ImageKey = "Page";

                parentNode.Nodes.Add(firstIam);
                parentNode.Nodes.Add(rootPage);
                parentNode.Nodes.Add(firstPage);
            }
        }

        /// <summary>
        /// Get the Node Name property
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns></returns>
        private static string NodeName(TreeNode node)
        {
            Type t = node.GetType();
            PropertyInfo property = t.GetProperty("NodeName", typeof(string));

            if (property != null)
            {
                return Convert.ToString(property.GetValue(node, null));
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Sets the childrenEnumerated field for a node
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="enumerated">if set to <c>true</c> [enumerated].</param>
        /// <remarks>
        /// This is to suppress an error when SSMS assumes the nodes we add are HierarchyTreeNodes as opposed to 
        /// bog standard TreeNodes.
        /// </remarks>
        private static void ChildrenEnumerated(TreeNode node, bool enumerated)
        {
            Type t = node.GetType();
            FieldInfo field = t.GetField("childrenEnumerated", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(node, enumerated);
            }
        }
    }
}
