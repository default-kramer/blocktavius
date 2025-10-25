using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Blocktavius.AppDQB2;

/// <summary>
/// A profile approximately means "An SD directory on a specific computer."
/// The "specific computer" part means that a single DQB account could have multiple profiles
/// if they play that account on multiple computers.
///
/// By convention we store the profile JSON at
///     blah\blah\DRAGON QUEST BUILDERS II\Steam\76561198073553084\SD\.blocktavius\profile.json
/// </summary>
sealed class ProfileSettings
{
	public required DirectoryInfo ConfigDir { get; init; }
	public required FileInfo ConfigFile { get; init; }
	public required string ProfileId { get; init; }

	/// <summary>
	/// The purpose of the verification hash is to detect if a saved project file
	/// has been opened by a different profile and should require the user to review
	/// and/or re-set certain values (especially the STGDAT read and write paths).
	/// In theory the <see cref="ProfileId"/> would be enough, but if someone shares
	/// a config file example it's possible people would copy the ProfileId too.
	/// So we'll include things like the computername and username in this hash.
	/// </summary>
	public required string VerificationHash { get; init; }

	/// <summary>
	/// Null means the user does not want to create backups.
	/// </summary>
	public required DirectoryInfo? BackupDir { get; init; }

	public required IReadOnlyList<SaveSlot> SaveSlots { get; init; }
	public IEnumerable<WritableSaveSlot> WritableSaveSlots => SaveSlots.OfType<WritableSaveSlot>();

	public class SaveSlot
	{
		public required string Name { get; init; }
		public required string FullPath { get; init; }
		public required string? RelativePath { get; init; }
		public virtual bool IsWritable => false;
	}

	public sealed class WritableSaveSlot : SaveSlot
	{
		public override bool IsWritable => true;
	}

	private static string DefaultBackupDir(DirectoryInfo configDir)
	{
		return Path.Combine(configDir.FullName, "backups");
	}

	public void Save()
	{
		var jsonModel = ToJsonModel();

		string tempFile = $"{ConfigFile.FullName}.{Guid.NewGuid()}.tmp";
		using var stream = File.OpenWrite(tempFile);
		JsonSerializer.Serialize(stream, jsonModel, new JsonSerializerOptions { WriteIndented = true });
		stream.Flush();
		stream.Close();

		File.Move(tempFile, ConfigFile.FullName, overwrite: true);
	}

	private JsonProfile ToJsonModel()
	{
		bool saveBackups;
		string? customBackupLocation;

		if (BackupDir == null)
		{
			saveBackups = false;
			customBackupLocation = null;
		}
		else if (string.Equals(BackupDir.FullName, DefaultBackupDir(ConfigDir), StringComparison.OrdinalIgnoreCase))
		{
			saveBackups = true;
			customBackupLocation = null;
		}
		else
		{
			string relative = Path.GetRelativePath(ConfigDir.FullName, BackupDir.FullName);
			if (relative.StartsWith("..") || Path.IsPathFullyQualified(relative))
			{
				saveBackups = true;
				customBackupLocation = BackupDir.FullName;
			}
			else
			{
				saveBackups = true;
				customBackupLocation = relative;
			}
		}

		var slots = this.SaveSlots.Select(ToJsonModel).ToList();

		return new JsonProfile()
		{
			ProfileId = this.ProfileId,
			CustomBackupLocation = customBackupLocation,
			SaveBackups = saveBackups,
			Slots = slots,
		};
	}

	public static ProfileSettings TODO()
	{
		var user = Environment.GetEnvironmentVariable("USERNAME") ?? "<missing username>";

		var sdPath = @$"C:\Users\{user}\Documents\My Games\DRAGON QUEST BUILDERS II\Steam\76561198073553084\SD\";
		var sdDir = new DirectoryInfo(sdPath);
		if (!sdDir.Exists)
		{
			throw new Exception("TODO");
		}

		var configDir = sdDir.CreateSubdirectory(".blocktavius");
		var configFile = new FileInfo(Path.Combine(configDir.FullName, "profile.json"));
		if (configFile.Exists)
		{
			return Load(configFile);
		}

		var profile = CreateNew(sdDir.FullName);
		profile.Save();
		return profile;
	}

	private static ProfileSettings CreateNew(string sdPath)
	{
		var sdDir = new DirectoryInfo(sdPath);
		if (!sdDir.Exists)
		{
			throw new NotImplementedException("TODO!");
		}

		var configDir = sdDir.CreateSubdirectory(".blocktavius");
		var configFile = new FileInfo(Path.Combine(configDir.FullName, "profile.json"));

		string profileId = Guid.NewGuid().ToString();
		string hash = CreateVerificationHash(profileId);

		var slots = new string[] { "B00", "B01", "B02" }.Select(path => new SaveSlot
		{
			RelativePath = $"../{path}",
			FullPath = Path.Combine(sdDir.FullName, path),
			Name = $"Slot {1 + int.Parse(path.Substring(2))} ({path})",
		}).ToList();

		return new ProfileSettings()
		{
			BackupDir = new DirectoryInfo(DefaultBackupDir(configDir)),
			ConfigDir = configDir,
			ConfigFile = configFile,
			ProfileId = profileId,
			VerificationHash = hash,
			SaveSlots = slots,
		};
	}

	public static ProfileSettings Load(FileInfo configFile)
	{
		if (!configFile.Exists)
		{
			throw new FileNotFoundException(configFile.FullName);
		}

		var configDir = configFile.Directory ?? throw new Exception("assert fail: no directory contains " + configFile.FullName);

		var config = JsonSerializer.Deserialize<JsonProfile>(File.ReadAllText(configFile.FullName))
			?? throw new Exception("Invalid json in " + configFile.FullName);

		var slots = config.Slots.EmptyIfNull()
			.Select(slot => ParseSlot(slot, configDir))
			.WhereNotNull()
			.ToList();

		DirectoryInfo? backupDir;
		if (config.SaveBackups.GetValueOrDefault(true)) // default to "yes, backup"
		{
			if (!string.IsNullOrWhiteSpace(config.CustomBackupLocation))
			{
				string backupPath = ResolvePath(config.CustomBackupLocation, configDir, out _);
				backupDir = new DirectoryInfo(backupPath);
			}
			else
			{
				backupDir = new DirectoryInfo(DefaultBackupDir(configDir));
			}
		}
		else
		{
			backupDir = null;
		}

		string profileId = config.ProfileId ?? Guid.NewGuid().ToString();
		string verificationHash = CreateVerificationHash(profileId);

		return new ProfileSettings()
		{
			ConfigFile = configFile,
			ConfigDir = configDir,
			ProfileId = profileId,
			VerificationHash = verificationHash,
			SaveSlots = slots,
			BackupDir = backupDir,
		};
	}

	private static SaveSlot? ParseSlot(JsonSlot slot, DirectoryInfo configDir)
	{
		if (string.IsNullOrEmpty(slot.Path))
		{
			return null;
		}

		string fullPath = ResolvePath(slot.Path, configDir, out string? relativePath);

		string name;
		if (!string.IsNullOrWhiteSpace(slot.SlotName))
		{
			name = slot.SlotName;
		}
		else
		{
			name = relativePath
				?? Path.GetDirectoryName(fullPath)
				?? throw new Exception("Assert fail: no dir name for " + fullPath);
		}

		if (slot.IsWritable.GetValueOrDefault(false))
		{
			return new WritableSaveSlot()
			{
				Name = name,
				FullPath = fullPath,
				RelativePath = relativePath,
			};
		}
		else
		{
			return new SaveSlot()
			{
				Name = name,
				FullPath = fullPath,
				RelativePath = relativePath,
			};
		}
	}

	private static string ResolvePath(string path, DirectoryInfo configDir, out string? relativePath)
	{
		if (Path.IsPathFullyQualified(path))
		{
			relativePath = null;
			return new Uri(path).LocalPath;
		}
		else
		{
			relativePath = path;
			return new Uri(Path.Combine(configDir.FullName, path)).LocalPath;
		}
	}

	private static string CreateVerificationHash(string profileId)
	{
		var sb = new StringBuilder();
		sb.Append(profileId).Append("::");
		sb.Append(Environment.GetEnvironmentVariable("COMPUTERNAME") ?? "no computername").Append("::");
		sb.Append(Environment.GetEnvironmentVariable("USERNAME") ?? "no username").Append("::");

		var enc = Encoding.GetEncoding(65001); // utf-8 code page
		byte[] buffer = enc.GetBytes(sb.ToString());
		byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(buffer);
		return Convert.ToBase64String(hashBytes);
	}

	private static JsonSlot ToJsonModel(SaveSlot saveSlot)
	{
		return new JsonSlot()
		{
			Path = saveSlot.RelativePath ?? saveSlot.FullPath,
			SlotName = saveSlot.Name,
			IsWritable = saveSlot.IsWritable,
		};
	}

	sealed class JsonProfile
	{
		public required string? ProfileId { get; init; }
		public required IReadOnlyList<JsonSlot>? Slots { get; init; }
		public required bool? SaveBackups { get; init; }
		public required string? CustomBackupLocation { get; init; }
	}

	sealed class JsonSlot
	{
		public required string? SlotName { get; init; }
		public required string? Path { get; init; }
		public required bool? IsWritable { get; init; }
	}
}
