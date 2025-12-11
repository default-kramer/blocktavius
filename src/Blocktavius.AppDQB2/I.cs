using Antipasta;
using Antipasta.IndexedPropagation;
using Blocktavius.AppDQB2.Services;
using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Blocktavius.AppDQB2;

static class I
{
	internal static readonly StaticGraphIndexer indexer = new(typeof(I));
	static I()
	{
		System.Diagnostics.Trace.WriteLine(indexer.DUMP());
	}

	public static class Project
	{
		public interface Profile : IProperty<ProfileSettings> { }
		public interface SourceSlots : IProperty<IReadOnlyList<SlotVM>> { }
		public interface SelectedSourceSlot : IProperty<SlotVM?> { }
		public interface DestSlots : IProperty<IReadOnlyList<WritableSlotVM>> { }
		public interface SelectedDestSlot : IProperty<WritableSlotVM?> { }
		public interface SourceStages : IProperty<IReadOnlyList<SlotStageVM>> { }
		public interface SelectedSourceStage : IProperty<SlotStageVM?> { }
		public interface SourceFullPath : IProperty<string?> { }
		public interface DestFullPath : IProperty<string?> { }
		public interface ChunkExpansion : IProperty<IReadOnlySet<ChunkOffset>> { }
		public interface LoadedStage : IProperty<LoadStageResult?> { }
		public interface ChunkMaskImage : IProperty<BitmapSource?> { }
		public interface MinimapImage : IProperty<BitmapSource?> { }
		public interface Notes : IProperty<string?> { }

		// commands:
		public interface CommandEditChunkGrid : ICommandNode { }
	}
}
