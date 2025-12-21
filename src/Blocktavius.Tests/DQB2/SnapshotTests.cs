using Blocktavius.Core;
using Blocktavius.DQB2;
using Blocktavius.DQB2.Mutations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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

	private void AssertSnapshot(string snapshotName, string content)
	{
		var snapshotDir = Path.Combine(TestUtil.SnapshotRoot, "RepairSea");
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
		var stagePath = Path.Combine(TestUtil.SnapshotRoot, "DQB2_Saves", "01", "STGDAT01.BIN");
		var stage = TestUtil.Stages.Stage01.Value.Clone();

		var mutation = new RepairSeaMutation
		{
			ColumnCleanupMode = ColumnCleanupMode.ExpandBedrock,
			SeaLevel = 31,
			LiquidFamily = liquidFamily
		};
		stage.Mutate(mutation);

		var snapshot = await RecomputeSnapshot(stage);
		AssertSnapshot($"RepairSeaMutation_{name}", snapshot);
	}
}
