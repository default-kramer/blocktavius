using Blocktavius.DQB2;
using Blocktavius.DQB2.LiquidRoof;
using Blocktavius.DQB2.Mutations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Tests.DQB2;

[TestClass]
public class SnapshotTests
{
	private static async Task<string> RecomputeSnapshot(IStage stage)
	{
		var sb = new StringBuilder();
		var chunks = stage.IterateChunks().ToList();

		foreach (var chunk in chunks)
		{
			var hashString = await chunk.Internals.ComputeBlockdataHash();
			sb.Append($"{chunk.Offset.NorthwestCorner.X},{chunk.Offset.NorthwestCorner.Z}: {hashString}\n");
		}

		return sb.ToString();
	}

	private void AssertSnapshot(string snapshotName, string content, params string[] path)
	{
		var snapshotDir = Path.Combine([TestUtil.SnapshotRoot, .. path]);
		Directory.CreateDirectory(snapshotDir);
		var snapshotPath = Path.Combine(snapshotDir, $"{snapshotName}.txt");

		if (!File.Exists(snapshotPath))
		{
			File.WriteAllText(snapshotPath, content);
			Assert.Inconclusive($"Snapshot created: {snapshotName}. Please verify and rerun the test.");
		}

		var expected = File.ReadAllText(snapshotPath);

		expected = expected.Replace("\r\n", "\n");
		content = content.Replace("\r\n", "\n");
		Assert.AreEqual(expected, content, $"Snapshot {snapshotName} does not match.");
	}

	public static IEnumerable<object[]> AllLiquidFamilies()
	{
		yield return new object[] { LiquidFamily.ClearWater, "ClearWater" };
		yield return new object[] { LiquidFamily.HotWater, "HotWater" };
		yield return new object[] { LiquidFamily.Poison, "Poison" };
		yield return new object[] { LiquidFamily.Lava, "Lava" };
		yield return new object[] { LiquidFamily.BottomlessSwamp, "BottomlessSwamp" };
		yield return new object[] { LiquidFamily.MuddyWater, "MuddyWater" };
		yield return new object[] { LiquidFamily.Seawater, "Seawater" };
		yield return new object[] { LiquidFamily.Plasma, "Plasma" };
	}

	[DataTestMethod]
	[DynamicData(nameof(AllLiquidFamilies), DynamicDataSourceType.Method)]
	public async Task RepairSeaMutation_AllLiquids(LiquidFamily liquidFamily, string name)
	{
		var stage = TestUtil.Stages.Stage01.Value.Clone();

		var mutation = new RepairSeaMutation
		{
			ColumnCleanupMode = ColumnCleanupMode.ExpandBedrock,
			SeaLevel = 31,
			LiquidFamily = liquidFamily
		};
		stage.Mutate(mutation);

		var snapshot = await RecomputeSnapshot(stage);
		AssertSnapshot($"RepairSeaMutation_{name}", snapshot, "RepairSea");
	}

	/// <summary>
	/// Jungle wall is the interesting one here.
	/// There are 3 candidate chunks.
	/// One should not be removed because it has a prop.
	/// The other two should be removed; one is chiseled and the other is not.
	/// </summary>
	[DataTestMethod]
	[DataRow(3, "GrassyEarth", 0)]
	[DataRow(821, "JungleWall", 2)]
	public async Task RemoveChunks(int flagBlock, string name, int removedChunks)
	{
		const int origChunkCount = 154;

		var stage = TestUtil.Stages.Stage02.Value.Clone();
		Assert.AreEqual(origChunkCount, stage.ChunksInUse.Count);

		var mutation = new RemoveChunksMutation()
		{
			FlagBlockId = (ushort)flagBlock,
		};
		stage.Mutate(mutation);

		Assert.AreEqual(origChunkCount - removedChunks, stage.ChunksInUse.Count);

		var snapshot = await RecomputeSnapshot(stage);
		AssertSnapshot($"RemoveChunks_{name}", snapshot, "RemoveChunks");
	}

	[TestMethod]
	public async Task LiquidRoofTest()
	{
		// I didn't actually test Liquid Roof against this particular stage.
		// (I tested against other stages and am simply using an existing stage to lock the behavior.)
		var stage = TestUtil.Stages.Stage01.Value.Clone();
		var plan = LiquidRoofPlan.Create(stage);
		stage.Mutate(plan.GetMutation());
		var snapshot = await RecomputeSnapshot(stage);
		AssertSnapshot("LiquidRoof_Stage01", snapshot, "LiquidRoof");
	}
}
