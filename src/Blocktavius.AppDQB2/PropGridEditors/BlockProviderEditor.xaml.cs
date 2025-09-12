using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit.PropertyGrid;
using Xceed.Wpf.Toolkit.PropertyGrid.Editors;

namespace Blocktavius.AppDQB2.PropGridEditors;

/// <summary>
/// Property Grid Editor for properties of type <see cref="IBlockProviderVM"/>.
/// </summary>
public partial class BlockProviderEditor : UserControl, ITypeEditor
{
	public BlockProviderEditor()
	{
		InitializeComponent();
	}

	public FrameworkElement ResolveEditor(PropertyItem propertyItem)
	{
		if (propertyItem.PropertyType != typeof(IBlockProviderVM))
		{
			throw new InvalidOperationException($"Cannot use {nameof(BlockProviderEditor)} to edit property of type {propertyItem.PropertyType}, specifically {propertyItem.PropertyDescriptor.ComponentType} / {propertyItem.PropertyName}");
		}

		this.txtDisplayName.SetBinding(TextBlock.TextProperty, $"{nameof(propertyItem.Value)}.{nameof(IBlockProviderVM.DisplayName)}");
		return this;
	}

	private void Button_Click(object sender, RoutedEventArgs e)
	{
		var propGrid = this.VisualTreeAncestors().OfType<PropertyGrid>().FirstOrDefault();
		if (propGrid == null)
		{
			return;
		}

		var propItem = propGrid.SelectedPropertyItem as PropertyItem;
		if (propItem == null)
		{
			return;
		}

		var propOwner = propGrid.SelectedObject;
		var currentValue = propItem.PropertyDescriptor.GetValue(propOwner);
		var blockProvider = this.DataContextAncestors().OfType<IBlockList>().FirstOrDefault();

		var vm = new BlockSelectorWindow.Viewmodel()
		{
			Blocks = blockProvider?.Blocks ?? Blockdata.AllBlockVMs,
		};
		vm.Initialize(currentValue as IBlockProviderVM);

		var ownerWindow = Window.GetWindow(this);
		var dialog = new BlockSelectorWindow()
		{
			Owner = ownerWindow,
			DataContext = vm,
		};

		if (dialog.ShowDialog() == true)
		{
			var selection = vm.SelectedProvider();
			if (selection != null)
			{
				propItem.Value = selection;
				propItem.PropertyDescriptor.SetValue(propOwner, selection);
			}
		}
	}
}
