using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Blocktavius.AppDQB2;

sealed class ExternalImageManager : IDisposable
{
	private readonly DirectoryInfo projectDir;
	private readonly FileSystemWatcher watcher;
	public ObservableCollection<ExternalImageVM> ExternalImages { get; } = new();
	private readonly ConcurrentDictionary<string, ExternalImageVM> externalImageDict = new();
	private readonly object locker = new();
	const string filter = "*.bmp";
	const string extension = ".bmp";

	public ExternalImageManager(DirectoryInfo projectDir)
	{
		this.projectDir = projectDir;
		this.watcher = new FileSystemWatcher(projectDir.FullName, filter);

		watcher.IncludeSubdirectories = true;
		watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
		watcher.EnableRaisingEvents = true;
		watcher.Created += OnWatcherEvent;
		watcher.Changed += OnWatcherEvent;
		watcher.Deleted += OnWatcherEvent;
		watcher.Renamed += OnWatcherRenamed;

		DiscoverExistingImages();
	}

	private void DiscoverExistingImages()
	{
		Task.Run(() =>
		{
			foreach (var file in projectDir.EnumerateFiles(filter, SearchOption.AllDirectories))
			{
				ProcessImageFile(file.FullName);
			}
		});
	}

	private void OnWatcherEvent(object sender, FileSystemEventArgs e)
	{
		ProcessImageFile(e.FullPath);
	}

	private void OnWatcherRenamed(object sender, RenamedEventArgs e)
	{
		ProcessImageFile(e.FullPath);
	}

	private void ProcessImageFile(string fullPath)
	{
		// Ignore files like "foo.bmp~Fj34.TMP" created by Paint.NET and probably other programs
		if (!string.Equals(Path.GetExtension(fullPath), extension, StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		string relativePath = Path.GetRelativePath(projectDir.FullName, fullPath);
		var vm = FindOrCreate(relativePath, out bool wasFreshlyAdded);
		vm.ReloadIfStale();
		if (wasFreshlyAdded)
		{
			AddNewImage(vm);
		}
	}

	public ExternalImageVM FindOrCreate(string relativePath, out bool wasFreshlyAdded)
	{
		var newId = Guid.NewGuid();
		string fullPath = Path.Combine(projectDir.FullName, relativePath);
		string dictKey = fullPath.ToLowerInvariant();
		var vm = externalImageDict.GetOrAdd(dictKey, _ => new ExternalImageVM(newId, new FileInfo(fullPath), relativePath));
		wasFreshlyAdded = vm.UniqueId == newId;
		return vm;
	}

	private void AddNewImage(ExternalImageVM vm)
	{
		// Application.Current can actually be null here if you close the app quickly,
		// before scanning all the images completes.
		var dispatcher = Application.Current?.Dispatcher;
		if (dispatcher == null)
		{
			return;
		}

		dispatcher.Invoke(() =>
		{
			lock (locker)
			{
				int i = 0;
				while (i < ExternalImages.Count && ExternalImages[i].RelativePath.CompareTo(vm.RelativePath) < 0)
				{
					i++;
				}
				ExternalImages.Insert(i, vm);
			}
		});
	}

	public void Dispose()
	{
		watcher?.Dispose();
		ExternalImages?.Clear();
		externalImageDict?.Clear();
	}
}
