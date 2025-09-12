using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

sealed class Blockdata
{
	public sealed class Dye
	{
		public required int BlockId { get; init; }
		public required string Color { get; init; }
	}

	public required string Name { get; init; }
	public required int BlockId { get; init; }
	public required int PrimaryBlockId { get; init; }
	public required bool IsLiquid { get; init; }
	public required IReadOnlyList<Dye> Dyes { get; init; }


	public static readonly IReadOnlyList<Blockdata> AllBlockdatas;
	public static readonly IReadOnlyList<BlockVM> AllBlockVMs;

	/// <summary>
	/// For example, you can use this as when you create a new hill
	/// so that it has a working and recognizable starting value.
	/// </summary>
	public static readonly BlockVM? AnArbitraryBlockVM;

	static Blockdata()
	{
		string json = System.IO.File.ReadAllText("Blockdata.json");
		AllBlockdatas = System.Text.Json.JsonSerializer.Deserialize<List<Blockdata>>(json)
			?? throw new Exception("Failed to read blockdata");

		AllBlockVMs = AllBlockdatas.Select(BlockVM.Create).ToList();

		AnArbitraryBlockVM = AllBlockVMs.ElementAtOrDefault(3) ?? AllBlockVMs.FirstOrDefault();
	}
}
