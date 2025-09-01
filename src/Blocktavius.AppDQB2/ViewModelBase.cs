using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2;

class ViewModelBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}

class MainVM : ViewModelBase
{
	public ObservableCollection<LayerVM> Layers { get; } = new ObservableCollection<LayerVM>();

	private LayerVM? _selectedLayer;
	public LayerVM? SelectedLayer
	{
		get => _selectedLayer;
		set
		{
			if (_selectedLayer != value)
			{
				_selectedLayer = value;
				OnPropertyChanged();
			}
		}
	}
}

class TileSizeItemsSource : IItemsSource
{
	public Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ItemCollection GetValues()
	{
		var items = new Xceed.Wpf.Toolkit.PropertyGrid.Attributes.ItemCollection();
		foreach (var i in new[] { 4, 8, 12, 16, 24, 32 })
		{
			items.Add(i, i.ToString());
		}
		return items;
	}
}
