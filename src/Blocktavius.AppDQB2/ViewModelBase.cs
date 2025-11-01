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

	protected bool ChangeProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] moreProperties)
	{
		if (object.Equals(field, value)) { return false; }

		field = value;
		OnPropertyChanged(propertyName);
		foreach (var prop in moreProperties)
		{
			OnPropertyChanged(prop);
		}
		return true;
	}

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		var args = new PropertyChangedEventArgs(propertyName);
		PropertyChanged?.Invoke(this, args);

		foreach (var kvp in subscribers.ToList()) // ToList() so we can mutate the dictionary if needed
		{
			if (kvp.Value.TryGetTarget(out var vm))
			{
				vm.OnSubscribedPropertyChanged(this, args);
			}
			else
			{
				subscribers.Remove(kvp.Key);
			}
		}
	}

	private readonly Dictionary<object, WeakReference<ViewModelBase>> subscribers = new();

	/// <summary>
	/// Subscribes to property changed events. The <paramref name="key"/> is held strongly,
	/// but the <paramref name="subscriber"/> is held via a weak reference.
	/// </summary>
	protected internal void Subscribe(object key, ViewModelBase subscriber)
	{
		subscribers[key] = new WeakReference<ViewModelBase>(subscriber);
	}

	protected internal bool Unsubscribe(object key) => subscribers.Remove(key);

	protected virtual void OnSubscribedPropertyChanged(ViewModelBase sender, PropertyChangedEventArgs e) { }
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
