using Blocktavius.AppDQB2.Persistence;
using Blocktavius.AppDQB2.Resources;
using Blocktavius.Core;
using Blocktavius.DQB2;
using Blocktavius.DQB2.Mutations;
using System.ComponentModel;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class PutSnippetNodeVM : ScriptLeafNodeVM, IHaveLongStatusText, IStageMutator, IDynamicScriptNodeVM
{
	[PersistentScriptNode(Discriminator = "PutSnippet-7890")]
	sealed record PersistModel : IPersistentScriptNode
	{
		public required string? SnippetPersistentId { get; init; }
		public required int? Rotation { get; init; }
		public required int? NorthwestX { get; init; }
		public required int? NorthwestZ { get; init; }
		public required int? AdjustY { get; init; }

		public bool TryDeserializeV1(out ScriptNodeVM node, ScriptDeserializationContext context)
		{
			var me = new PutSnippetNodeVM();
			me.Snippet = context.Snippets.FirstOrDefault(x => x.PersistentId == SnippetPersistentId);
			me.Rotation = this.Rotation ?? 0;
			me.NorthwestX = this.NorthwestX ?? 0;
			me.NorthwestZ = this.NorthwestZ ?? 0;
			me.AdjustY = this.AdjustY ?? 0;

			node = me;
			return true;
		}
	}

	public IPersistentScriptNode ToPersistModel()
	{
		return new PersistModel
		{
			SnippetPersistentId = this.Snippet?.PersistentId,
			Rotation = this.Rotation,
			NorthwestX = this.NorthwestX,
			AdjustY = this.AdjustY,
			NorthwestZ = this.NorthwestZ,
		};
	}

	IStageMutator? IDynamicScriptNodeVM.SelfAsMutator => this;
	ScriptNodeVM IDynamicScriptNodeVM.SelfAsVM => this;

	public PutSnippetNodeVM()
	{
		RebuildLongStatus();
	}

	private ExtractedSnippetResourceVM? _snippet;
	[Category("Source")]
	[ItemsSource(typeof(Global.SnippetsItemsSource))]
	public ExtractedSnippetResourceVM? Snippet
	{
		get => _snippet;
		set => ChangeProperty(ref _snippet, value);
	}

	private int _rotation = 0;
	[Category("Transform")]
	[ItemsSource(typeof(Global.RotationItemsSource))]
	public int Rotation
	{
		get => _rotation;
		set => ChangeProperty(ref _rotation, value);
	}

	private int _northwestX = 0;
	[Category("Transform")]
	public int NorthwestX
	{
		get => _northwestX;
		set => ChangeProperty(ref _northwestX, value);
	}

	private int _northwestZ = 0;
	[Category("Transform")]
	public int NorthwestZ
	{
		get => _northwestZ;
		set => ChangeProperty(ref _northwestZ, value);
	}

	private int _adjustY = 0;
	[Category("Transform")]
	public int AdjustY
	{
		get => _adjustY;
		set => ChangeProperty(ref _adjustY, value);
	}

	private BindableRichText _longStatus = BindableRichText.Empty;
	[Browsable(false)]
	public BindableRichText LongStatus
	{
		get => _longStatus;
		private set => ChangeProperty(ref _longStatus, value);
	}

	protected override void AfterPropertyChanges()
	{
		RebuildLongStatus();
	}

	private void RebuildLongStatus()
	{
		var rtb = new BindableRichTextBuilder();
		rtb.Append("Put Snippet: ").FallbackIfNull("None Selected", Snippet?.Name);
		rtb.AppendLine().Append($"  Rotation: {Rotation}°");
		rtb.AppendLine().Append($"  Where: X={NorthwestX}, Z={NorthwestZ}, AdjustY={AdjustY}");

		LongStatus = rtb.Build();
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		var snippet = Snippet?.LoadSnippet(context);
		if (snippet == null)
		{
			return null;
		}

		var translated = snippet
			.Rotate(this.Rotation)
			.TranslateTo(new XZ(NorthwestX, NorthwestZ));

		// TEMP NOMERGE
		var toCopy = new MaskedBlockLookup<bool>();
		toCopy[2] = true; // earth
		toCopy[3] = true; // grassy earth
		toCopy[4] = true; // limegrassy earth
		toCopy[10] = true; // obsidian
		toCopy[130] = true; // dolomite
		toCopy[131] = true; // dolomite

		return new PutSnippetMutation()
		{
			Snippet = translated,
			AdjustY = this.AdjustY,
			BlocksToCopy = toCopy,
			//AgressiveFilldown = true,
		};
	}
}
