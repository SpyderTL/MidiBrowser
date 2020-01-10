using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MidiBrowser
{
	public partial class MidiBrowserWindow : Form
	{
		public MidiBrowserWindow()
		{
			InitializeComponent();
		}

		private void OpenFile()
		{
			if (openFileDialog.ShowDialog() == DialogResult.OK)
				OpenFile(openFileDialog.FileName);
		}

		private void OpenFile(string fileName)
		{
			var node = new TreeNode
			{
				Text = System.IO.Path.GetFileName(fileName),
				Tag = new MidiFile { Path = fileName },
			};

			node.Nodes.Add("Loading...");

			treeView.Nodes.Add(node);
		}

		private void openToolStripButton_Click(object sender, EventArgs e)
		{
			OpenFile();
		}

		private void openToolStripMenuItem_Click(object sender, EventArgs e)
		{
			OpenFile();
		}

		private void MidiBrowserWindow_DragDrop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent("FileName"))
			{
				foreach (var fileName in (string[])e.Data.GetData("FileName"))
					OpenFile(fileName);
			}
		}

		private void MidiBrowserWindow_DragOver(object sender, DragEventArgs e)
		{
			e.Effect = DragDropEffects.Copy;
		}

		private void treeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
		{
			e.Node.Nodes.Clear();

			if (e.Node.Tag is IFolder)
			{
				foreach (var item in ((IFolder)e.Node.Tag).Items)
				{
					var node = new TreeNode
					{
						Text = item.ToString(),
						Tag = item
					};

					if (item is IFolder)
					{
						node.Nodes.Add("Loading...");
					}

					e.Node.Nodes.Add(node);
				}
			}
		}

		private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
		{
			if (e.Node.Tag is IProperties)
				propertyGrid.SelectedObject = ((IProperties)e.Node.Tag).Properties;
			else
				propertyGrid.SelectedObject = null;
		}

		private void contextMenuStrip_Opening(object sender, CancelEventArgs e)
		{
			if (treeView.SelectedNode.Tag is IMenu)
			{
				contextMenuStrip.Items.Clear();

				contextMenuStrip.Items.AddRange(((IMenu)treeView.SelectedNode.Tag).MenuItems.Select(i => new ToolStripButton
				{
					Text = i,
					Tag = i
				}).ToArray());
			}
			else
				e.Cancel = true;
		}

		private void contextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			if (treeView.SelectedNode.Tag is IMenu)
			{
				((IMenu)treeView.SelectedNode.Tag).Execute((string)e.ClickedItem.Tag);
			}
		}
	}
}
