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
        public ObjectExplorerExtender(SqlConnectionInfo connection)
        {
            IExplorerHierarchy hierarchy = GetHierarchyForConnection(connection);
            HierarchyTreeNode databasesNode = GetUserDatabasesNode(hierarchy.Root);

            databasesNode.Parent.TreeView.AfterExpand += new TreeViewEventHandler(TreeView_AfterExpand);
            databasesNode.Parent.TreeView.BeforeExpand += new TreeViewCancelEventHandler(TreeView_BeforeExpand);

            databasesNode.Parent.TreeView.ImageList.Images.Add("Page", Properties.Resources.pageImage);
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

                if (Hobt.HobtType(databaseName, tableName) == StructureType.Heap)
                {
                    TreeNode heapNode = new TreeNode("(Heap)", tableImageIndex, tableImageIndex);

                    AddIndexPageNodes(heapNode, databaseName, tableName, string.Empty, e.Node.Parent.Parent.ImageIndex);

                    e.Node.Nodes.Add(heapNode);
                }
            }
        }

        private void AddIndexPageNodes(TreeNode node, string databaseName, string tableName, string indexName, int folderImageIndex)
        {
            // This suppresses the Object Explorer expand behaviour
            ChildrenEnumerated(node, true);

            List<HobtEntryPoint> entryPoints = Hobt.EntryPoints(databaseName, tableName, indexName);

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

        private IExplorerHierarchy GetHierarchyForConnection(SqlConnectionInfo connection)
        {
            IObjectExplorerService objExplorer = ServiceCache.GetObjectExplorer();
            Type t = objExplorer.GetType();
            MethodInfo getHierarchyMethod = t.GetMethod("GetHierarchy", BindingFlags.Instance | BindingFlags.NonPublic);

            if (getHierarchyMethod != null)
            {
                IExplorerHierarchy hierarchy = getHierarchyMethod.Invoke(objExplorer, new object[] { connection }) as IExplorerHierarchy;
                return hierarchy;
            }

            return null;
        }

        private HierarchyTreeNode GetUserDatabasesNode(HierarchyTreeNode rootNode)
        {
            if (rootNode != null)
            {
                if (rootNode.Expandable)
                {
                    EnumerateChildrenSynchronously(rootNode);
                    rootNode.Expand();

                    return (HierarchyTreeNode)rootNode.Nodes[0];
                }
            }

            return null;
        }

        private void EnumerateChildrenSynchronously(HierarchyTreeNode node)
        {
            Type t = node.GetType();
            MethodInfo method = t.GetMethod("EnumerateChildren", new Type[] { typeof(Boolean) });

            if (method != null)
            {
                method.Invoke(node, new object[] { false });
            }
            else
            {
                node.EnumerateChildren();
            }
        }

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
