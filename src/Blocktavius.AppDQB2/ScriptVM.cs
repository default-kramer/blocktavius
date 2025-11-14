using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Blocktavius.AppDQB2;

public interface IAreaVM
{
	bool IsArea(XZ imageTranslation, out AreaWrapper area);

	bool IsRegional(out TileTagger<bool> tagger);

	string? DisplayName { get; }
}

public interface IBlockProviderVM
{
	string DisplayName { get; }

	/// <summary>
	/// Null for something like a mottler
	/// </summary>
	ushort? UniformBlockId { get; }
}

interface ISelectedNodeManager
{
	bool IsSelected(ScriptNodeVM node);

	void ChangeSelectedNode(ScriptNodeVM? node);
}

abstract class ScriptNodeVM : ViewModelBaseWithCustomTypeDescriptor
{
	private bool _isSelected = false;
	/// <summary>
	/// Intended only for binding (to display the thicker/highlight border on the selcted node)
	/// </summary>
	[Browsable(false)]
	public bool IsSelected
	{
		get => _isSelected;
		private set => ChangeProperty(ref _isSelected, value);
	}

	internal void UpdateSelected(ISelectedNodeManager manager)
	{
		IsSelected = manager.IsSelected(this);
	}

	/// <summary>
	/// A node that is capable of having child nodes should return true
	/// even if it currently has zero children.
	/// </summary>
	public abstract bool CanHaveChildNodes { get; }

	public abstract bool SelectDataTemplate(out string resourceKey);
}

interface IChildNodeWrapperVM
{
	ScriptNodeVM Child { get; }

	ICommand CommandMoveUp { get; }
	ICommand CommandMoveDown { get; }
	ICommand CommandDelete { get; }
}

interface IStageMutator
{
	StageMutation? BuildMutation(StageRebuildContext context);
}

/// <summary>
/// When implemented by a <see cref="ScriptLeafNodeVM"/>, enables a data template
/// that shows the contents of <see cref="LongStatus"/>.
/// This content can contain newlines.
/// </summary>
interface IHaveLongStatusText
{
	BindableRichText LongStatus { get; }
}

abstract class ScriptLeafNodeVM : ScriptNodeVM
{
	[Browsable(false)]
	public override bool CanHaveChildNodes => false;

	public override bool SelectDataTemplate(out string resourceKey)
	{
		if (this is IHaveLongStatusText)
		{
			resourceKey = ScriptNodeTemplateSelector.TemplateNames.SCRIPT_NODE_LONG_STATUS_TEMPLATE;
			return true;
		}
		resourceKey = "";
		return false;
	}
}

abstract class ScriptNonleafNodeVM : ScriptNodeVM
{
	/// <summary>
	/// Should be an ObservableCollection{T} but generics make the XAML more complicated
	/// </summary>
	public abstract IEnumerable<IChildNodeWrapperVM> ChildNodes { get; }

	public override bool CanHaveChildNodes => true;
}

sealed class ScriptSettingsVM : ScriptLeafNodeVM
{
	private bool _expandBedrock = false;
	public bool ExpandBedrock
	{
		get => _expandBedrock;
		set => ChangeProperty(ref _expandBedrock, value);
	}

	private string? _scriptName = null;
	public string? ScriptName
	{
		get => _scriptName;
		set => ChangeProperty(ref _scriptName, value);
	}
}

sealed class ScriptVM : ScriptNonleafNodeVM, IStageMutator, ISelectedNodeManager
{
	public ScriptSettingsVM Settings { get; } = new();
	public ObservableCollection<ChildNodeWrapper> Nodes { get; } = new();
	public override IEnumerable<IChildNodeWrapperVM> ChildNodes => Nodes;

	public ScriptVM()
	{
		AddChild(Settings);
		AddChild(new ScriptNodes.PutGroundNodeVM());
		AddChild(new ScriptNodes.PutHillNodeVM());
	}

	public override bool SelectDataTemplate(out string resourceKey) { resourceKey = ""; return false; }

	public ScriptVM SetScriptName(string? name)
	{
		Settings.ScriptName = name;
		return this;
	}

	public string? GetScriptName() => Settings.ScriptName;

	private void AddChild(ScriptNodeVM child)
	{
		Nodes.Add(new ChildNodeWrapper(this)
		{
			Child = child,
			Mutator = child as IStageMutator,
		});
	}


	private bool _isTheActiveScript = false;
	public bool IsTheActiveScript
	{
		get => _isTheActiveScript;
		private set => ChangeProperty(ref _isTheActiveScript, value);
	}

	internal void SetActive(bool isActive)
	{
		IsTheActiveScript = isActive;
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		List<StageMutation> mutations = new();
		foreach (var node in Nodes)
		{
			var mutation = node.Mutator?.BuildMutation(context);
			if (mutation != null)
			{
				mutations.Add(mutation);
			}
		}
		if (mutations.Count > 0)
		{
			return StageMutation.Combine(mutations);
		}
		return null;
	}

	private ScriptNodeVM? _selectedNode = null;
	public ScriptNodeVM? SelectedNode
	{
		get => _selectedNode;
		private set => ChangeProperty(ref _selectedNode, value);
	}

	bool ISelectedNodeManager.IsSelected(ScriptNodeVM node) => node == SelectedNode;
	void ISelectedNodeManager.ChangeSelectedNode(ScriptNodeVM? node)
	{
		var prev = SelectedNode;
		SelectedNode = node;
		prev?.UpdateSelected(this);
		node?.UpdateSelected(this);
	}

	private void DeleteNode(ChildNodeWrapper node)
	{
		Nodes.Remove(node);
		if (SelectedNode == node.Child)
		{
			SelectedNode = null;
		}
	}

	/// <summary>
	/// This is needed so that (for example) when new images/layers are added,
	/// the drop-downs in the property grid will include them.
	/// </summary>
	public void RefreshPropertyGrid()
	{
		var node = SelectedNode;
		if (node != null)
		{
			ISelectedNodeManager manager = this;
			manager.ChangeSelectedNode(null);
			manager.ChangeSelectedNode(node);
		}
	}

	public sealed class ChildNodeWrapper : ViewModelBase, IChildNodeWrapperVM, IWeakEventListener
	{
		private readonly ScriptVM parent;
		private readonly ObservableCollection<ChildNodeWrapper> nodes;
		public ChildNodeWrapper(ScriptVM parent)
		{
			this.parent = parent;
			this.nodes = parent.Nodes;
			CollectionChangedEventManager.AddListener(nodes, this);
		}

		public required ScriptNodeVM Child { get; init; }
		public required IStageMutator? Mutator { get; init; }

		private ICommand _commandMoveUp = NullCommand.Instance;
		public ICommand CommandMoveUp
		{
			get => _commandMoveUp;
			private set => ChangeProperty(ref _commandMoveUp, value);
		}

		private ICommand _commandMoveDown = NullCommand.Instance;
		public ICommand CommandMoveDown
		{
			get => _commandMoveDown;
			private set => ChangeProperty(ref _commandMoveDown, value);
		}

		private ICommand _commandDelete = NullCommand.Instance;
		public ICommand CommandDelete
		{
			get => _commandDelete;
			private set => ChangeProperty(ref _commandDelete, value);
		}

		public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
		{
			if (Child is ScriptSettingsVM)
			{
				return true; // cannot move or delete, always first in list
			}

			CommandMoveUp = BuildMoveCommand(this, -1);
			CommandMoveDown = BuildMoveCommand(this, 1);
			CommandDelete = new RelayCommand(_ => true, _ =>
			{
				CollectionChangedEventManager.RemoveListener(nodes, this);
				parent.DeleteNode(this);
			});
			return true;
		}

		private static ICommand BuildMoveCommand(ChildNodeWrapper item, int delta)
		{
			var nodes = item.nodes;

			Func<(int from, int? to)> Compute = () =>
			{
				int from = nodes.IndexOf(item);
				int to = from + delta;
				if (to > 0 && to < nodes.Count) // index 0 is reserved for the script settings
				{
					return (from, to);
				}
				return (from, null);
			};

			return new RelayCommand(_ => Compute().to.HasValue, _ =>
			{
				var (from, to) = Compute();
				if (to.HasValue)
				{
					nodes.RemoveAt(from);
					nodes.Insert(to.Value, item);
				}
			});
		}
	}
}
