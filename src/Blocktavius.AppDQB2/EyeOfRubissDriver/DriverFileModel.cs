using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2.EyeOfRubissDriver;

class DriverFileModel
{
	public string IntegrationType { get; init; } = "FSWatcher";

	public required IReadOnlyList<ChunkInfo> ChunkInfos { get; init; }

	/// <summary>
	/// Ensure that the FileSystemWatcher mechanism sees a change by putting a Guid here.
	/// (Not sure if necessary, but no reason not to do it.)
	/// </summary>
	public required string UniqueValue { get; init; }

	public record ChunkInfo
	{
		public required string RelativePath { get; init; }
		public required int OffsetX { get; init; }
		public required int OffsetZ { get; init; }
	}

	public static DriverFileModel CreateEmpty() => new DriverFileModel()
	{
		ChunkInfos = new List<ChunkInfo>(),
		UniqueValue = Guid.NewGuid().ToString(),
	};

	public void WriteToFile(FileInfo file)
	{
		var options = new JsonSerializerOptions();
		options.WriteIndented = true;
		string json = JsonSerializer.Serialize(this, options);
		File.WriteAllText(file.FullName, json);
	}
}
