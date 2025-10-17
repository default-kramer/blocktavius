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
	public TileTagger<bool> BuildTagger();

	bool IsArea(XZ imageTranslation, out AreaWrapper area);
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

	public abstract StageMutation? BuildMutation(StageRebuildContext context);
}

sealed class ScriptVM : ViewModelBase
{
	public ScriptVM()
	{
		Nodes.Add(new ScriptNodes.PutGroundNodeVM());
		Nodes.Add(new ScriptNodes.PutHillNodeVM());
	}

	public ObservableCollection<ScriptNodeVM> Nodes { get; } = new();

	private bool expandBedrock = false;
	public bool ExpandBedrock
	{
		get => expandBedrock;
		set => ChangeProperty(ref expandBedrock, value);
	}

	private string? name = null;
	public string? Name
	{
		get => name;
		set => ChangeProperty(ref name, value);
	}

	private bool isSelected = false;
	public bool IsSelected
	{
		get => isSelected;
		set => ChangeProperty(ref isSelected, value);
	}

	public required bool IsMain { get; init; }

	public IEnumerable<StageMutation> RebuildMutations(StageRebuildContext context)
	{
		foreach (var node in this.Nodes)
		{
			var mutation = node.BuildMutation(context);
			if (mutation != null)
			{
				yield return mutation;
			}
		}
	}
}
