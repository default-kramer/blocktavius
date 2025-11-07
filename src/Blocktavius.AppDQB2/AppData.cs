using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

public sealed class AppData
{
	private static readonly string appdataJsonPath = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"Blocktavius",
		"appdata.json");

	private JsonData dataInMemory;
	private JsonData dataOnDisk;

	private AppData(JsonData data)
	{
		this.dataInMemory = data;
		this.dataOnDisk = data;
	}

	public string? EyeOfRubissExePath => dataInMemory.EyeOfRubissExePath;

	public string? MinimapTilesheetPath => dataInMemory.MinimapTilesheetPath;

	internal bool TryLoadMostRecentProfile(out ProfileSettings profile)
	{
		var path = dataInMemory.MostRecentProfilePath;
		if (path == null)
		{
			profile = null!;
			return false;
		}
		return ProfileSettings.TryLoad(new FileInfo(path), out profile);
	}

	/// <remarks>
	/// We might support a list of profiles someday.
	/// But for now we only track the most recently used profile.
	/// </remarks>
	internal void MoveToFront(ProfileSettings profile)
	{
		dataInMemory = dataInMemory with
		{
			MostRecentProfilePath = profile.ConfigFile.FullName,
		};
	}

	public void Save()
	{
		if (dataInMemory == dataOnDisk)
		{
			return;
		}
		Save(dataInMemory);
		dataOnDisk = dataInMemory;
	}

	public static AppData LoadOrCreate()
	{
		JsonData? jsonData = null;
		try
		{
			string json = File.ReadAllText(appdataJsonPath);
			jsonData = System.Text.Json.JsonSerializer.Deserialize<JsonData>(json)!;
		}
		catch (Exception) { }

		if (jsonData == null)
		{
			jsonData = Create();
			Save(jsonData);
		}

		// TODO validate/repair the file here if needed...

		return new AppData(jsonData);
	}

	private static void Save(JsonData data)
	{
		var file = new FileInfo(appdataJsonPath);
		if (file.Directory == null)
		{
			throw new Exception("Assert fail - no directory? " + file.FullName);
		}
		file.Directory.Create();

		var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };

		using var stream = new FileStream(appdataJsonPath, FileMode.Create, FileAccess.Write);
		System.Text.Json.JsonSerializer.Serialize(stream, data, options);
		stream.Flush();
		stream.Close();
	}

	private static JsonData Create()
	{
		string? profilePath = null;
		if (ProfileSettings.TryCreateDefaultProfile(out var profile))
		{
			profilePath = profile.ConfigFile.FullName;
		}

		// TODO attempt to locate Eye of Rubiss
		string? eyeOfRubissPath = null;

		// TODO same
		string? minimapTilesheetPath = null;

		return new JsonData
		{
			MostRecentProfilePath = profilePath,
			EyeOfRubissExePath = eyeOfRubissPath,
			MinimapTilesheetPath = minimapTilesheetPath,
		};
	}

	private sealed record JsonData
	{
		public required string? MostRecentProfilePath { get; init; }
		public required string? EyeOfRubissExePath { get; init; }

		/// <summary>
		/// Path to this file: https://github.com/Sapphire645/DQB2MinimapExporter/blob/main/Script/Data/SheetRetro.png
		/// If null minimap rendering is disabled.
		/// </summary>
		public required string? MinimapTilesheetPath { get; init; }
	}
}
