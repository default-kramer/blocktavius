using Blocktavius.Core;
using ReactiveUI;
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

abstract class ViewModelBase : ReactiveObject
{
	public ViewModelBase()
	{
		//this.PropertyChanged += HandleOwnPropertyChanged;
	}

	private readonly ThreadLocal<int> changeStack = new();

	/// <summary>
	/// Delays the <see cref="AfterPropertyChanges"/> callback until this scope completes.
	/// The caller MUST use <see cref="ChangeScope.Complete()"/>; disposal is not sufficient.
	/// (If an exception occurs, invoking the callback could make things worse or cloud the original exception.)
	/// </summary>
	protected ChangeScope DeferChanges() => new ChangeScope(this);

	protected ref struct ChangeScope : IDisposable
	{
		private readonly ViewModelBase viewModel;
		private bool completed = false;

		public ChangeScope(ViewModelBase vm)
		{
			viewModel = vm;
			viewModel.changeStack.Value++;
		}

		private void Complete(bool raiseEvent)
		{
			if (completed)
			{
				return;
			}

			viewModel.changeStack.Value--;
			completed = true;

			if (raiseEvent && viewModel.changeStack.Value == 0)
			{
				viewModel.AfterPropertyChanges();
			}

			if (viewModel.changeStack.Value < 0)
			{
				viewModel.changeStack.Value = 0;
				throw new Exception("Assert fail - change stack must never go negative!");
			}
		}

		public void Complete() => Complete(raiseEvent: true);
		public void Dispose() => Complete(raiseEvent: false);
	}

	protected virtual void AfterPropertyChanges() { } // TODO - replace these with Reactive style

	protected bool ChangeProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (object.Equals(field, value)) { return false; }

		using var scope = DeferChanges();
		field = value;
		this.RaisePropertyChanged(propertyName);
		scope.Complete();

		return true;
	}

	[Obsolete("Migrate these to Reactive style")]
	protected bool ChangeProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] moreProperties)
	{
		if (object.Equals(field, value)) { return false; }

		using var scope = DeferChanges();
		field = value;
		this.RaisePropertyChanged(propertyName);
		foreach (var prop in moreProperties)
		{
			this.RaisePropertyChanged(prop);
		}
		scope.Complete();

		return true;
	}

	protected void OnPropertyChanged(string name) => this.RaisePropertyChanged(name); // TODO rename this later...

	/*
	private void HandleOwnPropertyChanged(object? myself, PropertyChangedEventArgs args)
	{
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
	*/

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

// TODO is this no longer used?
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
