using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Blocktavius.AppDQB2;

public interface IAreaVM
{
	bool IsArea(XZ imageTranslation, out AreaWrapper area);

	bool IsRegional(out TileTagger<bool> tagger);
}

public interface IBlockProviderVM
{
	string DisplayName { get; }

	/// <summary>
	/// Null for something like a mottler
	/// </summary>
	ushort? UniformBlockId { get; }
}

abstract class ScriptNodeVM : ViewModelBaseWithCustomTypeDescriptor
{
	private bool isSelected = false;
	[Browsable(false)]
	public bool IsSelected
	{
		get => isSelected;
		set
		{
			ChangeProperty(ref isSelected, value);
			OnPropertyChanged(nameof(BorderThickness));
		}
	}

	[Browsable(false)]
	public int BorderThickness => IsSelected ? 2 : 1;
}

interface IChildNodeWrapperVM
{
	ScriptNodeVM Child { get; }
}

interface IStageMutator
{
	StageMutation? BuildMutation(StageRebuildContext context);
}

abstract class ScriptLeafNodeVM : ScriptNodeVM { }

abstract class ScriptNonleafNodeVM : ScriptNodeVM
{
	/// <summary>
	/// Should be an ObservableCollection{T} but generics make the XAML more complicated
	/// </summary>
	public abstract IEnumerable<IChildNodeWrapperVM> ChildNodes { get; }
}

sealed class ScriptSettingsVM : ScriptNodeVM
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

sealed class ScriptVM : ScriptNonleafNodeVM, IStageMutator
{
	public sealed class ChildNodeWrapper : IChildNodeWrapperVM
	{
		public required ScriptNodeVM Child { get; init; }
		public required IStageMutator? Mutator { get; init; }
	}

	public ScriptSettingsVM Settings { get; }

	public ScriptVM()
	{
		Settings = new();
		AddChild(Settings);
		AddChild(new ScriptNodes.PutGroundNodeVM());
		AddChild(new ScriptNodes.PutHillNodeVM());
	}

	public ScriptVM SetScriptName(string? name)
	{
		Settings.ScriptName = name;
		return this;
	}

	public string? GetScriptName() => Settings.ScriptName;

	private void AddChild(ScriptNodeVM child)
	{
		Nodes.Add(new ChildNodeWrapper
		{
			Child = child,
			Mutator = child as IStageMutator,
		});
	}

	[Browsable(false)]
	public ObservableCollection<ChildNodeWrapper> Nodes { get; } = new();

	[Browsable(false)]
	public override IEnumerable<IChildNodeWrapperVM> ChildNodes => Nodes;



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
}
