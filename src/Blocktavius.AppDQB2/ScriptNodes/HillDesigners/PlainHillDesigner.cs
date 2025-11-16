using Blocktavius.AppDQB2.Persistence;
using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.ScriptNodes.HillDesigners;

// This hill wants the area but not the shells.
// Maybe create a base class for this kind?
sealed class PlainHillDesigner : ViewModelBase, IHillDesigner
{
	[PersistentHillDesigner(Discriminator = "PlainHill-7791")]
	sealed record PersistModel : IPersistentHillDesigner
	{
		public required int? Steepness { get; init; }
		public required PlainHill.CornerType? CornerType { get; init; }

		public bool TryDeserializeV1(ScriptDeserializationContext context, out IHillDesigner designer)
		{
			var me = new PlainHillDesigner();
			me.Steepness = this.Steepness ?? me.Steepness;
			me.CornerType = this.CornerType ?? me.CornerType;
			designer = me;
			return true;
		}
	}

	IPersistentHillDesigner IHillDesigner.ToPersistModel()
	{
		return new PersistModel
		{
			Steepness = this.Steepness,
			CornerType = this.CornerType,
		};
	}

	public StageMutation? CreateMutation(HillDesignContext context)
	{
		if (context.AreaVM.IsArea(context.ImageCoordTranslation, out var area))
		{
			context = context with { ImageCoordTranslation = XZ.Zero };

			var settings = new PlainHill.Settings
			{
				MaxElevation = context.Elevation,
				MinElevation = context.Elevation - 10,
				Steepness = this.steepness,
				CornerType = this.CornerType,
			};
			if (!settings.Validate(out settings))
			{
				this.Steepness = settings.Steepness;
			}
			var sampler = PlainHill.BuildPlainHill(area.Area, settings);
			return StageMutation.CreateHills(sampler, context.FillBlockId);
		}
		return null;
	}

	private int steepness = 1;
	public int Steepness
	{
		get => steepness;
		set => ChangeProperty(ref steepness, value);
	}

	private PlainHill.CornerType _cornerType = PlainHill.CornerType.Bevel;
	public PlainHill.CornerType CornerType
	{
		get => _cornerType;
		set => ChangeProperty(ref _cornerType, value);
	}
}
