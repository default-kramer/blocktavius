#pragma warning disable CA1416 // Bitmap and friends only supported on Windows
using Blocktavius.Core;
using Blocktavius.Core.Generators.Hills;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Blocktavius.Tests
{
	[TestClass]
	public class SnapshotTests
	{
		[TestMethod]
		public void AdamantCliffBuilder_WithFixedSeed_ShouldProduceConsistentOutput()
		{
			var prng = PRNG.Deserialize("12345-67890-12345-67890-12345-67890");
			var defaultConfig = AdamantCliffBuilder.Config.Default;

			var cliff = AdamantCliffBuilder.Generate(prng.Clone(), 128, 32, defaultConfig);
			AssertMatches(cliff, "AdamantCliff01.png");

			var config = defaultConfig with
			{
				RunWidthMin = 3,
				RunWidthMax = 6,
				UnacceptableZFlatness = 7,
			};
			cliff = AdamantCliffBuilder.Generate(prng.Clone(), 72, 10, config);
			AssertMatches(cliff, "AdamantCliff02.png");
		}

		[TestMethod]
		public void WinsomeCliffBuilder_WithFixedSeed_ShouldProduceConsistentOutput()
		{
			var prng = PRNG.Deserialize("12345-67890-12345-67890-12345-67890");

			var cliff = WinsomeCliffBuilder.Generate(prng.Clone(), 128, 32);
			AssertMatches(cliff, "WinsomeCliff01.png");
		}

		[TestMethod]
		public void CornerPusher01()
		{
			var prng = PRNG.Deserialize("12345-67890-12345-67890-12345-67890");

			var tagger = new TileTagger<bool>(new XZ(5, 5), new XZ(20, 20));
			tagger.AddTag(new XZ(2, 2), true);
			var region = tagger.GetRegions(true, XZ.Zero).Single();

			var settings = new CornerPusherHill.Settings
			{
				MaxElevation = 40,
				MinElevation = 5,
				Prng = prng,
			};

			var hill = CornerPusherHill.BuildHill(settings, ShellLogic.ComputeShells(region).Single());

			AssertMatches(hill, "CornerPusher01.png");
		}

		private static void AssertMatches(I2DSampler<int> cliff, string imageName)
		{
			var snapshotImage = CreateImageFromSampler(cliff);
			var snapshotPath = GetSnapshotPath(imageName);

			if (!File.Exists(snapshotPath))
			{
				snapshotImage.Save(snapshotPath, ImageFormat.Png);
				Assert.Inconclusive($"Snapshot created at {snapshotPath}. Please verify and run the test again.");
				return;
			}

			using (var expectedImage = new Bitmap(snapshotPath))
			{
				AssertImagesAreEqual(expectedImage, snapshotImage);
			}
		}

		private static string GetSnapshotPath(string fileName)
		{
			var assemblyLocation = Path.GetDirectoryName(typeof(SnapshotTests).Assembly.Location)!;
			var snapshotDir = Path.Combine(assemblyLocation, "..", "..", "..", "Snapshots");
			Directory.CreateDirectory(snapshotDir);
			return Path.Combine(snapshotDir, fileName);
		}

		private static Bitmap CreateImageFromSampler(I2DSampler<int> sampler)
		{
			var bounds = sampler.Bounds;
			var width = bounds.Size.X;
			var height = bounds.Size.Z;
			var bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

			// Create a grayscale palette
			ColorPalette palette = bmp.Palette;
			for (int i = 0; i < 256; i++)
			{
				palette.Entries[i] = Color.FromArgb(i, i, i);
			}
			bmp.Palette = palette;

			var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, bmp.PixelFormat);
			var stride = bmpData.Stride;
			var bytes = new byte[stride * height];

			int minY = int.MaxValue;
			int maxY = int.MinValue;

			for (int z = 0; z < height; z++)
			{
				for (int x = 0; x < width; x++)
				{
					var elevation = sampler.Sample(new XZ(x + bounds.start.X, z + bounds.start.Z));
					if (elevation >= 0)
					{
						minY = Math.Min(minY, elevation);
						maxY = Math.Max(maxY, elevation);
					}
				}
			}

			var range = (float)(maxY - minY);
			if (range == 0) range = 1;

			for (int z = 0; z < height; z++)
			{
				for (int x = 0; x < width; x++)
				{
					var elevation = sampler.Sample(new XZ(x + bounds.start.X, z + bounds.start.Z));
					byte grayValue = 0;
					if (elevation >= 0)
					{
						grayValue = (byte)(((elevation - minY) / range) * 255);
					}
					bytes[z * stride + x] = grayValue;
				}
			}

			Marshal.Copy(bytes, 0, bmpData.Scan0, bytes.Length);
			bmp.UnlockBits(bmpData);

			return bmp;
		}

		private static void AssertImagesAreEqual(Bitmap expected, Bitmap actual)
		{
			Assert.AreEqual(expected.Width, actual.Width, "Image widths are different.");
			Assert.AreEqual(expected.Height, actual.Height, "Image heights are different.");

			for (int y = 0; y < expected.Height; y++)
			{
				for (int x = 0; x < expected.Width; x++)
				{
					Assert.AreEqual(expected.GetPixel(x, y), actual.GetPixel(x, y), $"Pixel at ({x},{y}) is different.");
				}
			}
		}
	}
}
