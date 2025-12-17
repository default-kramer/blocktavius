using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.DQB2;

/// <summary>
/// Dumping ground for code that depends on <see cref="BitConverter.IsLittleEndian"/>.
/// </summary>
static class LittleEndianStuff
{
	static LittleEndianStuff()
	{
		if (!BitConverter.IsLittleEndian)
		{
			throw new Exception("TODO - support Big Endian systems??");
		}
	}

	public readonly struct ReadonlyBytes
	{
		private readonly byte[] bytes;

		public ReadonlyBytes(byte[] bytes)
		{
			this.bytes = bytes;
		}

		public int Length => bytes.Length;

		public byte[] Decompress()
		{
			var stream = new MemoryStream(bytes, writable: false);
			using var zlib = new System.IO.Compression.ZLibStream(stream, System.IO.Compression.CompressionMode.Decompress);
			var decomp = new MemoryStream();
			zlib.CopyTo(decomp);
			return decomp.ToArray();
		}

		public ushort GetUInt16(int addr) => BitConverter.ToUInt16(bytes, addr);

		public uint GetUInt32(int addr) => BitConverter.ToUInt32(bytes, addr);

		/// <summary>
		/// Warning: <paramref name="addr"/> is in bytes (not "count of ushorts to skip")
		/// </summary>
		public ReadOnlySpan<ushort> SliceUInt16(int addr)
		{
			var slice = bytes.AsSpan().Slice(addr);
			return MemoryMarshal.Cast<byte, ushort>(slice);
		}

		public ReadOnlySpan<byte> AsSpan => bytes.AsSpan();
	}

	public readonly struct ByteArrayBlockdata : IMutableBlockdata
	{
		private readonly byte[] array;

		public ByteArrayBlockdata(byte[] array)
		{
			ChunkMath.ValidateLength(array, nameof(array));
			this.array = array;
		}

		public static ByteArrayBlockdata Nothing => default;
		public bool IsNothing => array == null;

		public ushort GetBlock(Point point) => BitConverter.ToUInt16(array, ChunkMath.GetByteIndex(point));

		/// <remarks>
		/// Blocktavius should never write to the Y=0 layer except during column cleanup,
		/// read <see cref="ColumnCleanupMode"/> for more info.
		/// </remarks>
		public void SetBlock(Point point, ushort block)
		{
			if (point.Y == 0)
			{
				return;
			}

			var index = ChunkMath.GetUshortIndex(point);
			var shortArray = MemoryMarshal.Cast<byte, ushort>(array);
			if (shortArray[index].IsSimple())
			{
				shortArray[index] = block;
			}
		}

		public void ReplaceProp(Point point, ushort block)
		{
			if (point.Y == 0)
			{
				return;
			}

			var index = ChunkMath.GetUshortIndex(point);
			var shortArray = MemoryMarshal.Cast<byte, ushort>(array);
			if (shortArray[index].IsProp())
			{
				shortArray[index] = block;
			}
		}

		public ByteArrayBlockdata Clone()
		{
			var copy = GC.AllocateUninitializedArray<byte>(ChunkMath.BytesPerChunk);
			this.array.CopyTo(copy.AsMemory());
			return new ByteArrayBlockdata(copy);
		}

		public ValueTask WriteAsync(Stream stream) => stream.WriteAsync(array);

		public TSelf HackySelfCast<TSelf>() where TSelf : struct, IBlockdata
		{
			if (this is TSelf me)
			{
				return me;
			}
			throw new Exception($"Assert fail -- cannot cast {this.GetType().FullName} to {typeof(TSelf).FullName}");
		}

		public bool IsEmpty() => array.All(x => x == 0);

		public void PerformColumnCleanup(ColumnCleanupMode __mode)
		{
			// First validate that the requested mode requires any action
			ColumnCleanupMode mode;
			if (__mode == ColumnCleanupMode.ExpandBedrock || __mode == ColumnCleanupMode.ConstrainToBedrock)
			{
				mode = __mode;
			}
			else
			{
				return;
			}

			var shortArray = MemoryMarshal.Cast<byte, ushort>(array);
			for (int z = 0; z < ChunkMath.i32; z++)
			{
				for (int x = 0; x < ChunkMath.i32; x++)
				{
					var xz = new XZ(x, z);
					if (mode == ColumnCleanupMode.ExpandBedrock)
					{
						ExpandBedrock(shortArray, xz);
					}
					else if (mode == ColumnCleanupMode.ConstrainToBedrock)
					{
						ConstrainToBedrock(shortArray, xz);
					}
				}
			}
		}

		// If the column is non-empty, ensure Y=0 has bedrock
		private static void ExpandBedrock(Span<ushort> shortArray, XZ xz)
		{
			int index0 = ChunkMath.GetUshortIndex(new Point(xz, 0));
			ushort block0 = shortArray[index0];
			if (block0 == DQB2Constants.BlockId.Bedrock)
			{
				return; // already has bedrock at Y=0
			}
			if (!block0.IsSimple())
			{
				// They must have used some other tool to put an item at Y=0.
				// Let's ignore the entire column.
				return;
			}

			for (int y = 1; y < DQB2Constants.MaxElevation; y++)
			{
				int index = ChunkMath.GetUshortIndex(new Point(xz, y));
				if (shortArray[index] != DQB2Constants.BlockId.Empty)
				{
					shortArray[index0] = DQB2Constants.BlockId.Bedrock;
					return;
				}
			}
		}

		// If column does not have bedrock at Y=0, clear it (if possible)
		private static void ConstrainToBedrock(Span<ushort> shortArray, XZ xz)
		{
			int index0 = ChunkMath.GetUshortIndex(new Point(xz, 0));
			ushort block0 = shortArray[index0];
			if (block0 == DQB2Constants.BlockId.Bedrock)
			{
				return; // column has bedrock, nothing to do
			}
			else if (block0 != DQB2Constants.BlockId.Empty)
			{
				// This column must have been edited using some other tool.
				// Leaving it the way it is seems safest.
				return;
			}

			for (int y = 1; y < DQB2Constants.MaxElevation; y++)
			{
				int index = ChunkMath.GetUshortIndex(new Point(xz, y));
				if (shortArray[index].IsSimple())
				{
					shortArray[index] = DQB2Constants.BlockId.Empty;
				}
				else
				{
					// There's really nothing reasonable we can do in this situation.
					// They must have used another tool (or there is a bug in Blocktavius)
					// which created a column with nothing at Y=0 and an item at Y>0...
					// Just leave the item where it is.
					// (Maybe we should leave the whole column as-is... but I think not.
					//  Making the troublesome item stand out seems preferable to leaving
					//  it buried by blocks, whether or not Blocktavius placed those blocks.)
				}
			}
		}
	}
}
