using Antipasta;
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

abstract class ViewModelBase : INotifyPropertyChanged, IViewmodel
{
	public event PropertyChangedEventHandler? PropertyChanged;

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

	protected virtual void AfterPropertyChanges() { }

	protected bool ChangeProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] moreProperties)
	{
		if (object.Equals(field, value)) { return false; }

		using var scope = DeferChanges();
		field = value;
		OnPropertyChanged(propertyName);
		foreach (var prop in moreProperties)
		{
			OnPropertyChanged(prop);
		}
		scope.Complete();

		return true;
	}

	protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		using var scope = DeferChanges();

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

		scope.Complete();
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

	protected sealed class TaskProxy<TResult>
	{
		private readonly ViewModelBase owner;
		private readonly string? propertyName;
		private TResult? value = default;

		internal TaskProxy(ViewModelBase owner, string? propertyName)
		{
			this.owner = owner;
			this.propertyName = propertyName;
		}

		public TResult? Value => value;

		public void SetValue(TResult? value)
		{
			if (!object.Equals(value, this.value))
			{
				this.value = value;
				if (propertyName != null)
				{
					owner.OnPropertyChanged(propertyName);
				}
			}
		}
	}

	protected TaskProxy<TResult> Init<TResult>(TaskProxy<TResult>? typeHint, string? propertyName)
	{
		return new TaskProxy<TResult>(this, propertyName);
	}

	private IChangeset? currentChangeset = null;

	protected void SetElement<T>(ISettableElement<T> element, T value)
	{
		if (currentChangeset != null)
		{
			currentChangeset.RequestChange(element, value);
		}
		else
		{
			try
			{
				currentChangeset = BlockPasta.NewChangeset();
				currentChangeset.RequestChange(element, value);
				currentChangeset.ApplyChanges();
			}
			finally
			{
				currentChangeset = null;
			}
		}
	}

	public virtual void OnPropagationCompleted(IPropagationContext context) { }

	void INodeGroup.OnChanged(IImmediateNotifyNode node) => OnPropertyChanged(node.PropertyName);
}
