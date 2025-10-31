using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Blocktavius.AppDQB2;

/// <summary>
/// A profile approximately means "An SD directory on a specific computer."
/// The "specific computer" part means that a single DQB account could have multiple profiles
/// if they play that account on multiple computers.
///
/// By convention we store the profile JSON at
///     blah\blah\DRAGON QUEST BUILDERS II\Steam\76561198073553084\SD\.blocktavius\profile.json
/// </summary>
sealed class ProfileSettings : IEquatable<ProfileSettings>
{
	public required DirectoryInfo ConfigDir { get; init; }
	public required FileInfo ConfigFile { get; init; }

	public bool TryFindSD(out DirectoryInfo sd)
	{
		if (ConfigDir?.Parent?.Name?.ToLowerInvariant() == "sd")
		{
			sd = ConfigDir.Parent;
			return true;
		}
		sd = null!;
		return false;
	}

	/// <summary>
	/// A GUID for this profile.
	/// </summary>
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

	/// <summary>
	/// The <see cref="BackupDir"/> has the final say on whether or not to create backups.
	/// This method is just for detecting <see cref="IsBackupDirCustomized"/>.
	/// </summary>
	internal DirectoryInfo GetDefaultBackupDir() => new DirectoryInfo(DefaultBackupDir(ConfigDir));

	public bool IsBackupDirCustomized => !string.Equals(BackupDir?.FullName, GetDefaultBackupDir().FullName, StringComparison.OrdinalIgnoreCase);

	public required IReadOnlyList<SaveSlot> SaveSlots { get; init; }
	public IEnumerable<WritableSaveSlot> WritableSaveSlots => SaveSlots.OfType<WritableSaveSlot>();

	public class SaveSlot
	{
		protected readonly string fullPath;
		protected readonly string? relativePath;

		public SaveSlot(string fullPath, string? relativePath)
		{
			this.fullPath = fullPath;
			this.relativePath = relativePath;
		}

		public required int? SlotNumber { get; init; }
		public required string Name { get; init; }
		public virtual bool IsWritable => false;

		internal object ToJsonSlot()
		{
			return new JsonSlot()
			{
				Path = relativePath ?? fullPath,
				SlotName = Name,
				IsWritable = IsWritable,
				SlotNumber = SlotNumber,
			};
		}

		public SaveSlot MakeModified(string name, bool isWritable)
		{
			if (isWritable)
			{
				return new WritableSaveSlot(this.fullPath, this.relativePath)
				{
					Name = name,
					SlotNumber = this.SlotNumber,
				};
			}
			else
			{
				return new SaveSlot(this.fullPath, this.relativePath)
				{
					Name = name,
					SlotNumber = this.SlotNumber,
				};
			}
		}
	}

	public sealed class WritableSaveSlot : SaveSlot, IWritableSaveSlot
	{
		public override bool IsWritable => true;

		public WritableSaveSlot(string fullPath, string? relativePath)
			: base(fullPath, relativePath) { }

		DirectoryInfo IWritableSaveSlot.Directory => new DirectoryInfo(this.fullPath);
	}

	private static string DefaultBackupDir(DirectoryInfo configDir)
	{
		return Path.Combine(configDir.FullName, "backups");
	}

	public void Save()
	{
		if (!ConfigDir.Exists)
		{
			ConfigDir.Create();
		}

		var jsonModel = ToJsonModel();

		string tempFile = $"{ConfigFile.FullName}.{Guid.NewGuid()}.tmp";
		using var stream = File.OpenWrite(tempFile);
		JsonSerializer.Serialize(stream, jsonModel, new JsonSerializerOptions { WriteIndented = true });
		stream.Flush();
		stream.Close();

		File.Move(tempFile, ConfigFile.FullName, overwrite: true);
	}

	public override bool Equals(object? obj) => this.Equals(obj as ProfileSettings);
	public bool Equals(ProfileSettings? other) => other != null && this.ToJsonModel().Equals(other.ToJsonModel());
	public override int GetHashCode() => ToJsonModel().GetHashCode();

	internal JsonProfile ToJsonModel()
	{
		bool saveBackups;
		string? customBackupLocation;

		if (BackupDir == null)
		{
			saveBackups = false;
			customBackupLocation = null;
		}
		else if (!IsBackupDirCustomized)
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

		var slots = this.SaveSlots.Select(x => x.ToJsonSlot()).Cast<JsonSlot>().ToList();

		return new JsonProfile()
		{
			ProfileId = this.ProfileId,
			CustomBackupLocation = customBackupLocation,
			SaveBackups = saveBackups,
			Slots = slots,
		};
	}

	public static bool TryCreateDefaultProfile(out ProfileSettings defaultProfile)
	{
		var user = Environment.GetEnvironmentVariable("USERNAME") ?? "<missing username>";

		var sdPath = @$"C:\Users\{user}\Documents\My Games\DRAGON QUEST BUILDERS II\Steam\76561198073553084\SD\";
		var sdDir = new DirectoryInfo(sdPath);
		if (!sdDir.Exists)
		{
			defaultProfile = null!;
			return false;
		}

		defaultProfile = LoadOrCreate(sdDir);
		if (!defaultProfile.ConfigFile.Exists)
		{
			defaultProfile.Save();
		}
		return true;
	}

	public static ProfileSettings LoadOrCreate(DirectoryInfo sdDir)
	{
		var configFile = new FileInfo(Path.Combine(sdDir.FullName, ".blocktavius", "profile.json"));
		if (TryLoad(configFile, out var profile))
		{
			return profile;
		}
		return CreateNew(sdDir);
	}

	/// <summary>
	/// Assumes that <paramref name="sdDir"/> is truly an SD directory and creates a profile
	/// with 3 slots for B00,B01,B02.
	/// In general this app and its UX will be optimized for a typical Steam installation,
	/// but manually editing the profile json could unlock other possibilities.
	/// </summary>
	private static ProfileSettings CreateNew(DirectoryInfo sdDir)
	{
		if (!sdDir.Exists)
		{
			throw new NotImplementedException("TODO!");
		}

		var configDir = new DirectoryInfo(Path.Combine(sdDir.FullName, ".blocktavius"));
		var configFile = new FileInfo(Path.Combine(configDir.FullName, "profile.json"));

		string profileId = Guid.NewGuid().ToString();
		string hash = CreateVerificationHash(profileId);

		var slots = new string[] { "B00", "B01", "B02" }.Select(path =>
		{
			var fullPath = Path.Combine(sdDir.FullName, path);
			var relativePath = $"../{path}";
			int slotNumber = 1 + int.Parse(path.Substring(2));
			return new SaveSlot(fullPath, relativePath)
			{
				Name = $"Slot {slotNumber} ({path})",
				SlotNumber = slotNumber,
			};
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

	private static readonly JsonSerializerOptions jsonOptions = new()
	{
		TypeInfoResolver = NullablePropertiesNotRequiredResolver.Instance,
	};

	public static bool TryLoad(FileInfo configFile, out ProfileSettings settings)
	{
		if (!configFile.Exists)
		{
			settings = null!;
			return false;
		}

		var configDir = configFile.Directory ?? throw new Exception("assert fail: no directory contains " + configFile.FullName);

		string json = File.ReadAllText(configFile.FullName);

		JsonProfile? config = null;
		try
		{
			config = JsonSerializer.Deserialize<JsonProfile>(json, jsonOptions);
		}
		catch (Exception) { }

		if (config == null)
		{
			// should log the invalid json maybe
			settings = null!;
			return false;
		}

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

		settings = new ProfileSettings()
		{
			ConfigFile = configFile,
			ConfigDir = configDir,
			ProfileId = profileId,
			VerificationHash = verificationHash,
			SaveSlots = slots,
			BackupDir = backupDir,
		};
		return true;
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
			return new WritableSaveSlot(fullPath, relativePath)
			{
				Name = name,
				SlotNumber = slot.SlotNumber,
			};
		}
		else
		{
			return new SaveSlot(fullPath, relativePath)
			{
				Name = name,
				SlotNumber = slot.SlotNumber,
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

	internal sealed record JsonProfile
	{
		private IContentEqualityList<JsonSlot>? _slots;

		public required string? ProfileId { get; init; }
		public required IReadOnlyList<JsonSlot>? Slots
		{
			get => _slots;
			init => _slots = value?.ToContentEqualityList();
		}
		public required bool? SaveBackups { get; init; }
		public required string? CustomBackupLocation { get; init; }
	}

	internal sealed record JsonSlot
	{
		public required string? SlotName { get; init; }
		public required string? Path { get; init; }
		public required bool? IsWritable { get; init; }
		public required int? SlotNumber { get; init; }
	}
}
