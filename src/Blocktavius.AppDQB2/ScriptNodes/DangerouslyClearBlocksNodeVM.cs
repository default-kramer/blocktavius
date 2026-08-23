using Blocktavius.AppDQB2.Persistence;
using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes;

sealed class DangerouslyClearBlocksNodeVM : ScriptLeafNodeVM, IStageMutator, IDynamicScriptNodeVM, ICodedNodeVM
{
	IStageMutator? IDynamicScriptNodeVM.SelfAsMutator => this;
	ScriptNodeVM IDynamicScriptNodeVM.SelfAsVM => this;

	[PersistentScriptNode(Discriminator = "DangerouslyClearBlocks-7183")]
	sealed record PersistModel : IPersistentScriptNode
	{
		public required string ScriptCodeText { get; init; }
		public required ushort? BlockValue { get; init; }

		public bool TryDeserializeV1(out ScriptNodeVM node, ScriptDeserializationContext context)
		{
			var me = new DangerouslyClearBlocksNodeVM();
			me.ScriptCodeText = this.ScriptCodeText;
			me.BlockValue = this.BlockValue ?? me.BlockValue;
			node = me;
			return true;
		}
	}

	public IPersistentScriptNode ToPersistModel() => new PersistModel
	{
		ScriptCodeText = this.ScriptCodeText,
		BlockValue = this.BlockValue,
	};

	private string codeText = "";
	[Browsable(false)]
	public string ScriptCodeText
	{
		get => codeText;
		set => ChangeProperty(ref codeText, value);
	}

	private ushort _blockValue = 0;
	public ushort BlockValue
	{
		get => _blockValue;
		set => ChangeProperty(ref _blockValue, value);
	}

	public StageMutation? BuildMutation(StageRebuildContext context)
	{
		List<Point> points = new();

		foreach (var line in ScriptCodeText.ReplaceLineEndings("\n").Split("\n"))
		{
			if (line.StartsWith("step") && points.Count > 0)
			{
				var steps = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
				var from = points.Last();

				foreach (var step in steps.Skip(1))
				{
					Direction? dir = step.ToUpperInvariant() switch
					{
						"N" => Direction.North,
						"S" => Direction.South,
						"E" => Direction.East,
						"W" => Direction.West,
						_ => null,
					};
					if (dir != null)
					{
						from = new Point(from.xz.Step(dir), from.Y);
						points.Add(from);
					}
				}
			}
			else
			{
				var items = line.Split(",");
				if (items.Length >= 2
					&& int.TryParse(items[0], out int x)
					&& int.TryParse(items[1], out int z)
					&& int.TryParse(items[2], out int y))
				{
					points.Add(new Point(new XZ(x, z), y));
				}
			}
		}

		if (points.Count == 0)
		{
			return null;
		}

		return new DQB2.Mutations.DangerouslyClearBlocksMutation()
		{
			BlockValue = BlockValue,
			Points = points
		};
	}
}
