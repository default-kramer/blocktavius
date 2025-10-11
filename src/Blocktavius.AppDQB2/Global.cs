using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2;

sealed class BlockVM : IBlockProviderVM
{
	public required int BlockId { get; init; }
	public required string BlockName { get; init; }

	public static BlockVM Create(Blockdata block)
	{
		return new BlockVM() { BlockId = block.BlockId, BlockName = block.Name };
	}

	public string DisplayName => $"{BlockName} ({BlockId})";

	public ushort? UniformBlockId => (ushort)BlockId;
}

interface IBlockList
{
	IReadOnlyList<BlockVM> Blocks { get; }
}

/// <summary>
/// Global variables... what's the worst that could happen?
/// </summary>
static class Global
{
	/// <summary>
	/// We could allow this to be set using a command line flag or something.
	/// </summary>
	public static bool IsDebug => System.Diagnostics.Debugger.IsAttached;

	private static ProjectVM currentProject = new ProjectVM();

	public static void SetCurrentProject(ProjectVM project)
	{
		currentProject = project;
	}

	public sealed class LayersItemsSource : IItemsSource
	{
		public ItemCollection GetValues()
		{
			var list = new ItemCollection();
			foreach (var layerVM in currentProject.Layers)
			{
				list.Add(layerVM, layerVM.LayerName);
			}
			return list;
		}
	}

	public static IEnumerable<DependencyObject> VisualTreeAncestors(this DependencyObject obj)
	{
		do
		{
			yield return obj;
			obj = VisualTreeHelper.GetParent(obj);
		} while (obj != null);
	}

	public static IEnumerable<object> DataContextAncestors(this DependencyObject obj)
	{
		object? prevDC = null;
		foreach (var ancestor in obj.VisualTreeAncestors())
		{
			var currDC = (ancestor as FrameworkElement)?.DataContext;
			if (currDC != null && currDC != prevDC)
			{
				yield return currDC;
				prevDC = currDC;
			}
		}
	}
}
