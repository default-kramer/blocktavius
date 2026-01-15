using Blocktavius.Core;
using Blocktavius.Core.Generators;
using Blocktavius.Core.Generators.BasicHill;
using Blocktavius.Core.Generators.Hills;

namespace Blocktavius.Tests
{
	[TestClass]
	public sealed class Test1
	{
		[TestMethod]
		public void TestMethod2()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			for (int i = 0; i < 1000; i++)
			{
				var hill = BasicHill2.Create(prng, width: 50);
				Assert.IsNotNull(hill);
			}
		}

		[TestMethod]
		public void TestMethod3()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			for (int i = 0; i < 1000; i++)
			{
				var hill = Hillish.Create(prng);
				Assert.IsNotNull(hill);
			}
		}

		[TestMethod]
		public void ExerciseAdamantCliff()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			for (int i = 0; i < 1000; i++)
			{
				var cliff = Core.Generators.Hills.AdamantCliffBuilder.Generate(prng, 100, 60);
				Assert.IsNotNull(cliff);
			}
		}

		[TestMethod]
		public void AdamantCliffKnownBug()
		{
			var prng = PRNG.Deserialize("3309110861-16864670-3535033132-2513591414-720943084-1714556781");
			try
			{
				for (int i = 0; i < 1000; i++)
				{
					var cliff = Core.Generators.Hills.AdamantCliffBuilder.Generate(prng, 100, 60);
					Assert.IsNotNull(cliff);
				}
			}
			catch (Exception)
			{
				Assert.Inconclusive("The known issue in adamant cliff is still present... Fix is not urgent.");
				return;
			}
			Assert.Fail("ATTENTION: Is the bug in Adamant Cliff fixed? Or is it just fixed for this particular seed?");
		}

		[TestMethod]
		public void ExerciseNewHill()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			var area = TestUtil.CreateAreaFromAscii(@"
xxxxxx
x____x
x_____
x____x
xxxxxx");

			var shell = ShellLogic.ComputeShells(area).Single();
			var settings = new NewHill.Settings
			{
				MaxElevation = 30,
				MinElevation = 10,
				PRNG = prng,
			};

			for (int i = 0; i < 1000; i++)
			{
				NewHill.BuildNewHill(settings, shell);
			}
		}

		[TestMethod]
		public void ExerciseTileTagger()
		{
			var prng = PRNG.Create(new Random());
			Console.WriteLine(prng.Serialize());

			// Use a single tag
			// Generate a blob-shaped hill
			// Render it!
		}
	}
}