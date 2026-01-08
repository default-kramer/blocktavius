using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.Tests;

public class TestArea : IArea
{
	private readonly HashSet<XZ> points;
	public Rect Bounds { get; }

	public TestArea(IEnumerable<XZ> points)
	{
		this.points = new HashSet<XZ>(points);
		if (this.points.Any())
		{
			Bounds = Rect.GetBounds(this.points);
		}
		else
		{
			Bounds = Rect.Zero;
		}
	}

	public bool InArea(XZ xz) => points.Contains(xz);
}

static class TestUtil
{
	public static readonly string SnapshotRoot;

	public static IArea CreateAreaFromAscii(string pattern)
	{
		var lines = pattern.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

		if (lines is null || lines.Length == 0)
		{
			return new TestArea(Enumerable.Empty<XZ>());
		}

		var width = lines[0].Length;
		if (lines.Any(line => line.Length != width))
		{
			throw new ArgumentException("All lines in the pattern must have the same length.");
		}

		var points = new HashSet<XZ>();
		for (int z = 0; z < lines.Length; z++)
		{
			for (int x = 0; x < width; x++)
			{
				if (lines[z][x] != '_')
				{
					points.Add(new XZ(x, z));
				}
			}
		}
		return new TestArea(points);
	}

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
