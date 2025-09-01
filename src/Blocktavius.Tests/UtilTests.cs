
using System;
using Blocktavius.Core;

namespace Blocktavius.Tests
{
	[TestClass]
	public class UtilTests
	{
		private class TestSampler : I2DSampler<int>
		{
			private readonly int[,] data;
			public Rect Bounds { get; }
			private readonly XZ start;

			public TestSampler(int[,] data, XZ? start = null)
			{
				this.data = data;
				this.start = start ?? new XZ(0, 0);
				var height = data.GetLength(0);
				var width = data.GetLength(1);
				this.Bounds = new Rect(this.start, new XZ(width, height));
			}

			public int Sample(XZ xz)
			{
				var localX = xz.X - this.start.X;
				var localZ = xz.Z - this.start.Z;

				if (localX < 0 || localX >= Bounds.Size.X || localZ < 0 || localZ >= Bounds.Size.Z)
				{
					return -1; // Out of bounds
				}
				return data[localZ, localX];
			}
		}

		private XZ RandomTranslation()
		{
			var random = new Random();
			int x = random.Next(1000) - 500;
			int z = random.Next(1000) - 500;
			return new XZ(x, z);
		}

		[TestMethod]
		public void TestTranslate()
		{
			var sampler = new TestSampler(new int[,]
			{
				{ 1, 2, 3 },
				{ 4, 5, 6 }
			});

			var translation = new XZ(10, 20);
			var translated = sampler.Translate(translation);

			Assert.AreEqual(sampler.Bounds.Size, translated.Bounds.Size);
			Assert.AreEqual(sampler.Bounds.start.Add(translation), translated.Bounds.start);

			// Test sampling at corners
			Assert.AreEqual(1, translated.Sample(new XZ(10, 20))); // Top-left
			Assert.AreEqual(3, translated.Sample(new XZ(12, 20))); // Top-right
			Assert.AreEqual(4, translated.Sample(new XZ(10, 21))); // Bottom-left
			Assert.AreEqual(6, translated.Sample(new XZ(12, 21))); // Bottom-right
		}

		[TestMethod]
		public void TestRotate0()
		{
			var sampler = new TestSampler(new int[,] { { 1 } });
			var rotated = sampler.Rotate(0);
			Assert.AreSame(sampler, rotated);
		}

		[TestMethod]
		public void TestRotate360()
		{
			var sampler = new TestSampler(new int[,] { { 1 } });
			var rotated = sampler.Rotate(360);
			Assert.AreSame(sampler, rotated);
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentException))]
		public void TestRotateInvalid()
		{
			var sampler = new TestSampler(new int[,] { { 1 } });
			sampler.Rotate(45);
		}

		private int[,] RenderSampler(I2DSampler<int> sampler)
		{
			var width = sampler.Bounds.Size.X;
			var height = sampler.Bounds.Size.Z;
			var result = new int[height, width];
			for (var z = 0; z < height; z++)
			{
				for (var x = 0; x < width; x++)
				{
					result[z, x] = sampler.Sample(sampler.Bounds.start.Add(new XZ(x, z)));
				}
			}
			return result;
		}

		[DataTestMethod]
		[DataRow(90)]
		[DataRow(90 + 360)]
		[DataRow(90 - 360)]
		public void TestRotate90(int rotation)
		{
			var sampler = new TestSampler(new int[,]
			{
				{ 1, 2, 3 },
				{ 4, 5, 6 }
			});

			var rotated = sampler
				.Translate(RandomTranslation())
				.Rotate(rotation)
				.Translate(RandomTranslation());

			var expected = new int[,]
			{
				{ 4, 1 },
				{ 5, 2 },
				{ 6, 3 }
			};

			var actual = RenderSampler(rotated);

			CollectionAssert.AreEqual(expected, actual);
		}

		[DataTestMethod]
		[DataRow(270)]
		[DataRow(270 + 360)]
		[DataRow(270 - 360)]
		public void TestRotate270(int rotation)
		{
			var sampler = new TestSampler(new int[,]
			{
				{ 1, 2, 3 },
				{ 4, 5, 6 },
			});

			var rotated = sampler
				.Translate(RandomTranslation())
				.Rotate(rotation)
				.Translate(RandomTranslation());

			var expected = new int[,]
			{
				{ 3, 6 },
				{ 2, 5 },
				{ 1, 4 },
			};

			var actual = RenderSampler(rotated);

			CollectionAssert.AreEqual(expected, actual);
		}

		[TestMethod]
		[DataRow(180)]
		[DataRow(180 + 360)]
		[DataRow(180 - 360)]
		[DataRow(180 + 3600)]
		public void TestRotate180(int rotation)
		{
			var sampler = new TestSampler(new int[,]
			{
				{ 1, 2, 3 },
				{ 4, 5, 6 },
			});

			var rotated = sampler
				.Translate(RandomTranslation())
				.Rotate(rotation)
				.Translate(RandomTranslation());

			var expected = new int[,]
			{
				{6, 5, 4 },
				{3, 2, 1 },
			};

			var actual = RenderSampler(rotated);

			CollectionAssert.AreEqual(expected, actual);
		}
	}
}
