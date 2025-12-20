using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Tests.DQB2;

[TestClass]
public class BlockIdValidation
{
	const int firstPropId = 1158;

	[TestMethod]
	public void SimpleSanityChecks()
	{
		for (ushort blockId = 0; blockId < 0x800; blockId++)
		{
			bool isProp = blockId >= firstPropId;

			var block = Block.Lookup(blockId);
			Assert.AreEqual(block.IsProp(), isProp);
			if (!isProp)
			{
				Assert.AreEqual(PropShellIndex.None, block.PropShellIndex);
				Assert.AreEqual(ImmersionIndex.None, block.ImmersionIndex);
			}

			// equality stuff
			var sameBlock = Block.Lookup(blockId);
			Assert.IsTrue(sameBlock == block);
			Assert.IsTrue(sameBlock.Equals(block));
			Assert.IsFalse(sameBlock != block);
			Assert.IsTrue(sameBlock.GetHashCode() == block.GetHashCode());
		}
	}

	[TestMethod]
	public void PropSanityChecks()
	{
		const int numShells = 10;
		const int liquidsPerShell = 8;
		const int immersionsPerLiquid = 11;
		const int shellSize = 89;
		Assert.AreEqual(shellSize, liquidsPerShell * immersionsPerLiquid + 1); // +1 for the "not submerged" case
		Assert.AreEqual(0x800, firstPropId + numShells * shellSize);

		for (int shellId = 0; shellId < numShells; shellId++)
		{
			var propShellIndex = (PropShellIndex)(shellId + 1); // enum starts at 1
			int shellStart = firstPropId + shellId * shellSize;

			// test the "not submerged" case
			var notSubmergedBlock = Block.Lookup((ushort)(shellStart + shellSize - 1));
			Assert.AreEqual(propShellIndex, notSubmergedBlock.PropShellIndex);
			Assert.AreEqual(LiquidFamilyIndex.None, notSubmergedBlock.LiquidFamilyIndex);
			Assert.AreEqual(ImmersionIndex.None, notSubmergedBlock.ImmersionIndex);

			// test all liquid+immersion combinations
			for (int liquidId = 0; liquidId < liquidsPerShell; liquidId++)
			{
				for (int immersionId = 0; immersionId < immersionsPerLiquid; immersionId++)
				{
					int iBlockId = shellStart
						+ liquidId * immersionsPerLiquid
						+ immersionId;
					Assert.IsTrue(iBlockId < 0x800);
					Assert.IsTrue(iBlockId >= firstPropId);

					var liquidFamilyIndex = (LiquidFamilyIndex)(liquidId + 1); // enum starts at 1
					var immersionIndex = (ImmersionIndex)(immersionId + 1); // enum starts at 1

					var block = Block.Lookup((ushort)iBlockId);
					Assert.AreEqual(propShellIndex, block.PropShellIndex);
					Assert.AreEqual(liquidFamilyIndex, block.LiquidFamilyIndex);
					Assert.AreEqual(immersionIndex, block.ImmersionIndex);
				}
			}
		}
	}

	[TestMethod]
	public void DefaultBlockIsConsistent()
	{
		void validate(Block block)
		{
			Assert.IsTrue(block.BlockIdCanonical == 0);
			Assert.IsTrue(block.BlockIdComplete == 0);
			Assert.IsTrue(block.PropShellIndex == PropShellIndex.None);
			Assert.IsTrue(block.LiquidFamilyIndex == LiquidFamilyIndex.None);
			Assert.IsTrue(block.ImmersionIndex == ImmersionIndex.None);
		}
		validate(default);
		validate(Block.Lookup(0));
		Assert.IsTrue(Block.Lookup(0) == default);
	}

	[TestMethod]
	public void NonCanonicalTests()
	{
		for (ushort blockId = 0x800; blockId < ushort.MaxValue; blockId++)
		{
			var block = Block.Lookup(blockId);
			var canonicalBlock = Block.Lookup((ushort)(blockId % 0x800));
			Assert.IsFalse(block == canonicalBlock);
			Assert.IsFalse(block.BlockIdComplete == canonicalBlock.BlockIdComplete);

			// Everything else should be the same:
			Assert.IsTrue(block.IsProp() == canonicalBlock.IsProp());
			Assert.IsTrue(block.BlockIdCanonical == canonicalBlock.BlockIdCanonical);
			Assert.IsTrue(block.PropShellIndex == canonicalBlock.PropShellIndex);
			Assert.IsTrue(block.LiquidFamilyIndex == canonicalBlock.LiquidFamilyIndex);
			Assert.IsTrue(block.ImmersionIndex == canonicalBlock.ImmersionIndex);
		}
	}

	[TestMethod]
	public void PropLiquidChangeTests()
	{
		var liquids = Enum.GetValues(typeof(LiquidFamilyIndex))
			.Cast<LiquidFamilyIndex>()
			.Where(x => x != LiquidFamilyIndex.END)
			.ToList();

		Assert.AreEqual(9, liquids.Count);
		Assert.IsTrue(liquids[0] == LiquidFamilyIndex.None);

		for (ushort blockId = firstPropId; blockId < 0x800; blockId++)
		{
			var orig = Block.Lookup(blockId);

			foreach (var liquid in liquids)
			{
				if (!orig.IsProp(out var prop))
				{
					Assert.Fail($"Not a prop: {blockId}");
					continue;
				}
				bool result = prop.TryChangeLiquidFamily(liquid, out var changed);
				if (orig.LiquidFamilyIndex == LiquidFamilyIndex.None && liquid != LiquidFamilyIndex.None)
				{
					// Cannot change from None to some other value (don't know what Immersion to use)
					Assert.IsFalse(result);
					continue;
				}
				Assert.IsTrue(result);
				Assert.IsTrue(changed.IsProp() == orig.IsProp());
				Assert.IsTrue(changed.PropShellIndex == orig.PropShellIndex);

				if (liquid == LiquidFamilyIndex.None)
				{
					Assert.IsTrue(changed.LiquidFamilyIndex == LiquidFamilyIndex.None);
					Assert.IsTrue(changed.ImmersionIndex == ImmersionIndex.None);
				}
				else
				{
					Assert.IsTrue(changed.LiquidFamilyIndex == liquid);
					Assert.IsTrue(changed.ImmersionIndex == orig.ImmersionIndex);
				}

				Assert.IsTrue(changed == Block.Lookup(changed.BlockIdCanonical));
			}
		}
	}
}