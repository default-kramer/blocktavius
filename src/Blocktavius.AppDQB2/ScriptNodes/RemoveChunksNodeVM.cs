using Blocktavius.AppDQB2.Persistence;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class RemoveChunksNodeVM : ScriptLeafNodeVM, IHaveLongStatusText, IStageMutator, IDynamicScriptNodeVM
{
	IStageMutator? IDynamicScriptNodeVM.SelfAsMutator => this;
	ScriptNodeVM IDynamicScriptNodeVM.SelfAsVM => this;

	[PersistentScriptNode(Discriminator = "RemoveChunks-5682")]
	sealed record PersistModel : IPersistentScriptNode
	{
		public required string? BlockPersistId { get; init; }

		public bool TryDeserializeV1(out ScriptNodeVM node, ScriptDeserializationContext context)
		{
			var me = new RemoveChunksNodeVM();
			me.FlagBlock = context.BlockManager.FindBlock(BlockPersistId);
			node = me;
			return true;
		}
	}

	public IPersistentScriptNode ToPersistModel()
	{
		return new PersistModel()
		{
			BlockPersistId = this.FlagBlock?.PersistentId,
		};
	}

	public RemoveChunksNodeVM()
	{
		RebuildLongStatus();
	}

	private IBlockProviderVM? _flagBlock = null;
	[Editor(typeof(PropGridEditors.BlockProviderEditor), typeof(PropGridEditors.BlockProviderEditor))]
	public IBlockProviderVM? FlagBlock
	{
		get => _flagBlock;
		set
		{
			if (ChangeProperty(ref _flagBlock, value)) { RebuildLongStatus(); }
		}
	}

	private BindableRichText _longStatus = BindableRichText.Empty;
	[Browsable(false)]
	public BindableRichText LongStatus
	{
		get => _longStatus;
		set => ChangeProperty(ref _longStatus, value);
	}

	private void RebuildLongStatus()
	{
		LongStatus = new BindableRichTextBuilder()
			.Append("Chunks containing ZERO props and having flag block ")
			.FallbackIfNull("<not selected>", FlagBlock?.DisplayName)
			.Append(" at Y=1 will be removed.")
			.Build();
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		var flagBlockId = this.FlagBlock?.UniformBlockId;
		if (flagBlockId.HasValue)
		{
			return new Blocktavius.DQB2.Mutations.RemoveChunksMutation
			{
				FlagBlockId = flagBlockId.Value,
			};
		}
		return null;
	}
}
