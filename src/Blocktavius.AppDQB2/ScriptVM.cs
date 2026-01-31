using Blocktavius.AppDQB2.Persistence;
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

	string PersistentId { get; }
}

public interface IBlockProviderVM
{
	string DisplayName { get; }

	/// <summary>
	/// Null for something like a mottler
	/// </summary>
	ushort? UniformBlockId { get; }

	string PersistentId { get; }
}

interface ISelectedNodeManager
{
	bool IsSelected(ScriptNodeVM node);

	void ChangeSelectedNode(ScriptNodeVM? node);
}

/// <summary>
/// Interface for <see cref="ScriptNodeVM"/> types which are not recognized specially.
/// They must implement <see cref="ToPersistModel"/> to participate in save+load.
/// They may implement <see cref="SelfAsMutator"/> to participate in stage mutation.
/// </summary>
interface IDynamicScriptNodeVM
{
	ScriptNodeVM SelfAsVM { get; }

	IStageMutator? SelfAsMutator { get; }

	IPersistentScriptNode ToPersistModel();

	bool CanBeDisabled => true;
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
}

sealed class ScriptVM : ScriptNonleafNodeVM, IStageMutator, ISelectedNodeManager
{
	public ScriptSettingsVM Settings { get; } = new();
	public ObservableCollection<ChildNodeWrapper> Nodes { get; } = new();
	public override IEnumerable<IChildNodeWrapperVM> ChildNodes => Nodes;
	public IReadOnlyList<NodeKindVM> NodeKinds { get; }

	public ScriptVM(bool initSampleNodes) : this()
	{
		if (initSampleNodes)
		{
			AddChild(new ScriptNodes.PutGroundNodeVM());
			AddChild(new ScriptNodes.PutHillNodeVM());
		}
	}

	internal ScriptVM()
	{
		var kinds = new List<NodeKindVM>();
		kinds.Add(new NodeKindVM(() => new ScriptNodes.PutGroundNodeVM()) { DisplayName = "Put Ground" });
		kinds.Add(new NodeKindVM(() => new ScriptNodes.PutHillNodeVM()) { DisplayName = "Put Hill" });
		kinds.Add(new NodeKindVM(() => new ScriptNodes.PutSnippetNodeVM()) { DisplayName = "Put Snippet" });
		kinds.Add(new NodeKindVM(() => new ScriptNodes.RemoveChunksNodeVM()) { DisplayName = "Remove Chunks" });
		NodeKinds = kinds;

		CommandAddNode = new RelayCommand(_ => SelectedNodeKind != null, DoCommandAddNode);

		Nodes.Add(ChildNodeWrapper.Create(this, Settings));
	}

	private NodeKindVM? _selectedNodeKind;
	public NodeKindVM? SelectedNodeKind
	{
		get => _selectedNodeKind;
		set => ChangeProperty(ref _selectedNodeKind, value);
	}

	public ICommand CommandAddNode { get; }
	private void DoCommandAddNode(object? arg)
	{
		if (SelectedNodeKind == null)
		{
			return;
		}
		var node = SelectedNodeKind.CreateNode();
		AddChild(node);
	}

	public override bool SelectDataTemplate(out string resourceKey) { resourceKey = ""; return false; }

	public ScriptVM SetScriptName(string? name)
	{
		Settings.ScriptName = name;
		return this;
	}

	public string? GetScriptName() => Settings.ScriptName;

	private void AddChild(IDynamicScriptNodeVM child, Persistence.V1.ScriptNodeWrapperV1 wrapper)
	{
		var item = ChildNodeWrapper.Create(this, child);
		item.Enabled51 = wrapper.Enabled;
		Nodes.Add(item);
	}

	private void AddChild(IDynamicScriptNodeVM child)
	{
		var item = ChildNodeWrapper.Create(this, child);
		Nodes.Add(item);
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

		return Settings.BuildFinalMutation(mutations);
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

		private ChildNodeWrapper(ScriptVM parent, ScriptNodeVM child, IDynamicScriptNodeVM? dynamicChild)
		{
			this.parent = parent;
			this.nodes = parent.Nodes;
			CollectionChangedEventManager.AddListener(nodes, this);
			Child = child;
			DynamicChild = dynamicChild;

			var mutator = dynamicChild?.SelfAsMutator;
			if (mutator != null)
			{
				Mutator = new WrappedMutator
				{
					Decorated = mutator,
					Wrapper = this,
				};
			}
		}

		public static ChildNodeWrapper Create(ScriptVM parent, ScriptSettingsVM settings)
		{
			return new ChildNodeWrapper(parent, settings, null);
		}

		public static ChildNodeWrapper Create(ScriptVM parent, IDynamicScriptNodeVM child)
		{
			return new ChildNodeWrapper(parent, child.SelfAsVM, child);
		}

		sealed class WrappedMutator : IStageMutator
		{
			public required IStageMutator Decorated { get; init; }
			public required ChildNodeWrapper Wrapper { get; init; }

			public StageMutation? BuildMutation(StageRebuildContext context)
			{
				return Wrapper.Enabled51 ? Decorated.BuildMutation(context) : null;
			}
		}

		public ScriptNodeVM Child { get; }
		public IDynamicScriptNodeVM? DynamicChild { get; }
		public IStageMutator? Mutator { get; }

		private bool _enabled = true;
		public bool Enabled51
		{
			get => _enabled;
			set => ChangeProperty(ref _enabled, value);
		}

		public Visibility ShowEnabled61 => (DynamicChild?.CanBeDisabled ?? false) ? Visibility.Visible : Visibility.Collapsed;

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

		public Persistence.V1.ScriptNodeWrapperV1? ToPersistModel()
		{
			if (DynamicChild == null)
			{
				return null;
			}
			return new Persistence.V1.ScriptNodeWrapperV1
			{
				Enabled = this.Enabled51,
				ScriptNode = DynamicChild.ToPersistModel(),
			};
		}
	}

	public Persistence.V1.ScriptV1 ToPersistModelConcrete()
	{
		return new Persistence.V1.ScriptV1
		{
			ScriptName = Settings.ScriptName,
			ScriptNodes2 = this.Nodes.Select(n => n.ToPersistModel()).WhereNotNull().ToList(),
		};
	}

	public static ScriptVM Load(Persistence.V1.ScriptV1 script, ScriptDeserializationContext context)
	{
		var me = new ScriptVM();
		me.Settings.ScriptName = script.ScriptName;

		foreach (var node in script.GetScriptNodes())
		{
			if (node.ScriptNode.TryDeserializeV1(out var nodeVM, context))
			{
				if (nodeVM is IDynamicScriptNodeVM vm)
				{
					me.AddChild(vm, node);
				}
				else
				{
					throw new Exception($"Expected an {nameof(IDynamicScriptNodeVM)} here, but got {nodeVM.GetType()}");
				}
			}
		}

		return me;
	}
}

sealed class NodeKindVM
{
	private readonly Func<IDynamicScriptNodeVM> factory;

	public NodeKindVM(Func<IDynamicScriptNodeVM> factory)
	{
		this.factory = factory;
	}

	public required string DisplayName { get; init; }

	public IDynamicScriptNodeVM CreateNode() => factory();
}
