using LibDQB.B2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibDQBTests;

[TestClass]
public class RawCmndatTests
{
	private static DirectoryInfo FindTestdataDir()
	{
		const string projectRoot = "LibDQBTests";
		const string subdir = "testdata";

		var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
		while (dir.Name != projectRoot && dir.Parent != null)
		{
			dir = dir.Parent;
		}
		if (dir.Name != projectRoot)
		{
			throw new Exception($"Could not find: {projectRoot}");
		}

		return dir.GetDirectories(subdir)
			.Where(d => subdir.Equals(d.Name, StringComparison.OrdinalIgnoreCase))
			.FirstOrDefault()
			?? throw new Exception($"Could not find: {subdir}");
	}

	private static FileInfo FindTestFile(params string[] path)
	{
		var testdata = FindTestdataDir();
		return new FileInfo(Path.Combine([testdata.FullName, .. path]));
	}

	[TestMethod]
	public async Task TestCmndat01()
	{
		var file = FindTestFile("game-saves", "01", "CMNDAT.BIN");
		var cmndat = await RawCmndat.LoadAsync(file);

		// Buildertopia Gamma (16), not sailing
		Assert.AreEqual(16, cmndat.ToIslandId.Value);
		Assert.AreEqual(16, cmndat.FromIslandId.Value);

		Assert.AreEqual((uint)0x75182684, cmndat.SaveFileKey.Value);

		// Game showed 2026-02-02 20:54 with my local timzone set to UTC-6 (Chicago)
		var lastSaveTime = cmndat.LastSaveTime;
		var expectedTime = new DateTime(2026, 2, 2, 20, 54, 0, DateTimeKind.Utc).AddHours(6);
		Assert.AreEqual(expectedTime.Date, lastSaveTime.Date);
		Assert.AreEqual(expectedTime.Hour, lastSaveTime.Hour);
		Assert.AreEqual(expectedTime.Minute, lastSaveTime.Minute);
	}
}
