using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Tests;

static class TestUtil
{
	public static readonly string SnapshotRoot;

	static TestUtil()
	{
		var assemblyLocation = AppContext.BaseDirectory;
		var current = new DirectoryInfo(assemblyLocation);
		while (current != null && current.Name != "Blocktavius.Tests")
		{
			current = current.Parent;
		}

		if (current == null)
		{
			throw new DirectoryNotFoundException("Could not find the 'Blocktavius.Tests' directory.");
		}

		SnapshotRoot = Path.Combine(current.FullName, "Snapshots");
		if (!Directory.Exists(SnapshotRoot))
		{
			throw new DirectoryNotFoundException("Could not find Blocktavius.Test/Snapshots/ directory");
		}
	}

	public static class Stages
	{
		/// <summary>
		/// The IoA I just happened to be using when I manually verified the Repair Sea function.
		/// </summary>
		public static readonly Lazy<ICloneableStage> Stage01 = LazyLoad("01/STGDAT01.BIN");

		private static Lazy<ICloneableStage> LazyLoad(string path)
		{
			path = Path.Combine(SnapshotRoot, "DQB2_Saves", path);
			return new Lazy<ICloneableStage>(() => ImmutableStage.LoadStgdat(path));
		}
	}
}
