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
		var newId = Guid.NewGuid();
		string dictKey = fullPath.ToLowerInvariant();
		string relativePath = Path.GetRelativePath(projectDir.FullName, fullPath);
		var vm = externalImageDict.GetOrAdd(dictKey, (_) => new ExternalImageVM(newId, new FileInfo(fullPath), relativePath));
		vm.ReloadIfStale();

		if (vm.UniqueId == newId) // this means we just added a new one
		{
			AddNewImage(vm);
		}
	}

	private void AddNewImage(ExternalImageVM vm)
	{
		Application.Current.Dispatcher.Invoke(() =>
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
