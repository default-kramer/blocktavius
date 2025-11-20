using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

sealed class RecentProjectManager
{
	private readonly FileInfo jsonFile;
	private IReadOnlyList<RecentProject> projectsOnDisk;
	private LinkedList<RecentProject> projectsInMemory;

	private RecentProjectManager(FileInfo jsonFile, LinkedList<RecentProject> projects)
	{
		this.jsonFile = jsonFile;
		projectsOnDisk = projects.ToList();
		projectsInMemory = projects;
	}

	public IReadOnlyCollection<RecentProject> Projects => projectsInMemory;

	private static readonly JsonSerializerOptions jsonOptions = new()
	{
		TypeInfoResolver = NullablePropertiesNotRequiredResolver.Instance,
		WriteIndented = true,
	};

	public static bool TryLoad(FileInfo jsonFile, out RecentProjectManager manager)
	{
		try
		{
			string json = File.ReadAllText(jsonFile.FullName);
			var projects = JsonSerializer.Deserialize<IReadOnlyList<RecentProject>>(json, jsonOptions)
				?? throw new InvalidDataException(jsonFile.FullName + " contained invalid data");
			manager = new RecentProjectManager(jsonFile, new LinkedList<RecentProject>(projects));
			return true;
		}
		catch
		{
			manager = null!;
			return false;
		}
	}

	public static RecentProjectManager LoadOrCreate(FileInfo jsonFile)
	{
		if (jsonFile.Exists && TryLoad(jsonFile, out var existing))
		{
			return existing;
		}
		else
		{
			var me = new RecentProjectManager(jsonFile, []);
			WriteToDisk(jsonFile, []);
			return me;
		}
	}

	private static void WriteToDisk(FileInfo jsonFile, IReadOnlyList<RecentProject> projects)
	{
		using var stream = File.Open(jsonFile.FullName, FileMode.Create, FileAccess.Write);
		JsonSerializer.Serialize(stream, projects, jsonOptions);
		stream.Flush();
		stream.Close();
	}

	public void Save()
	{
		if (projectsInMemory.SequenceEqual(projectsOnDisk))
		{
			return;
		}
		var list = projectsInMemory.ToList();
		WriteToDisk(jsonFile, list);
		projectsOnDisk = list;
	}

	public void OnOpened(string projectFile)
	{
		var item = projectsInMemory.FirstOrDefault(x => x.FullPath.Equals(projectFile, StringComparison.OrdinalIgnoreCase));
		if (item != null)
		{
			projectsInMemory.Remove(item);
		}

		projectsInMemory.AddFirst(new RecentProject { FullPath = projectFile, LastOpenedUtc = DateTime.UtcNow });
		while (projectsInMemory.Count > 50)
		{
			projectsInMemory.RemoveLast();
		}

		Save();
	}

	public sealed record RecentProject
	{
		public required string FullPath { get; init; }
		public required DateTime LastOpenedUtc { get; init; }
	}
}
