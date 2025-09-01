using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Blocktavius.Core.Generators
{
	/// <summary>
	/// BUGGY... but might be worth looking at anyway for comparing aesthetics
	/// </summary>
	public static class Hillish
	{
		// these values should be configurable eventually:
		const int initialY = 30;
		const int initialRange = 2; // plus-or-minus
		const int minSeparation = 1;
		const int maxSeparation = 4;
		static IEnumerable<int> dyChoices => [2, 3, 1, 4, 5, 6]; // trying 6 first causes some kind of vicious cycle??
		const bool enforceRunVsContour = false; // TODO NOMERGE... but it actually looks okay this way??

		// A "run" is a segment within a layer where all the cells have the same (Y + Z) value.
		// Locking (Y + Z) instead of just Y is an aesthetic decision which means the Y value drops
		// by 1 when the layer takes 1 step closer, and Y increases by 1 when the layer takes 1 step back.
		const int runLengthMin = 2;
		const int runLengthRand = 4;

		/// <summary>
		/// Represents a marker for a corner in a layer, indicating the direction of the turn.
		/// </summary>
		public enum CornerMarker
		{
			Prev,
			Next
		}

		/// <summary>
		/// Distinguishes between different types of corners in a hill layer cell.
		/// </summary>
		public enum CornerType
		{
			/// <summary>The cell is not a corner.</summary>
			None,
			/// <summary>The cell is an unfilled corner, awaiting connection to an adjacent cell.</summary>
			Unfilled,
			/// <summary>The cell is a filled corner, with its corner space occupied.</summary>
			Filled
		}

		/// <summary>
		/// Represents the state of a corner in a hill layer cell.
		/// This struct provides a type-safe way to handle the different corner states,
		/// replacing the original implementation's use of `object`.
		/// </summary>
		public readonly struct CornerInfo
		{
			/// <summary>Gets the type of the corner.</summary>
			public CornerType Type { get; }

			/// <summary>Gets the marker for an unfilled corner. Valid only if Type is Unfilled.</summary>
			public CornerMarker Marker { get; }

			/// <summary>Gets the Y-value of the cell filling the corner space. Valid only if Type is Filled.</summary>
			public int FillY { get; }

			private CornerInfo(CornerType type, CornerMarker marker, int fillY)
			{
				Type = type;
				Marker = marker;
				FillY = fillY;
			}

			/// <summary>Represents a cell that is not a corner.</summary>
			public static CornerInfo None => new CornerInfo(CornerType.None, default, 0);

			/// <summary>Creates an unfilled corner with a specific marker.</summary>
			public static CornerInfo Unfilled(CornerMarker marker) => new CornerInfo(CornerType.Unfilled, marker, 0);

			/// <summary>Creates a filled corner with a specific Y-value.</summary>
			public static CornerInfo Filled(int y) => new CornerInfo(CornerType.Filled, default, y);
		}


		/// <summary>
		/// Represents a single cell in a hill layer.
		/// </summary>
		public class LayerCell
		{
			public int X { get; set; }
			public int Z { get; set; }
			public int Y { get; set; }
			/// <summary>
			/// Describes the corner state of this cell.
			/// </summary>
			public CornerInfo Corner { get; set; }

			public LayerCell(int x, int z, int y, CornerInfo corner)
			{
				X = x;
				Z = z;
				Y = y;
				Corner = corner;
			}

			public LayerCell Clone()
			{
				return (LayerCell)MemberwiseClone();
			}
		}

		public static I2DSampler<int> Create(PRNG prng)
		{
			var layers = Generate(prng);
			var maxZ = layers.SelectMany(l => l).Max(cell => cell.Z);
			var box = new Rect(new XZ(0, 0), new XZ(layers[0].Count, maxZ + 2));
			var array = new MutableArray2D<int>(box, -1);

			foreach (var layer in layers)
			{
				foreach (var cell in layer)
				{
					array.Put(new XZ(cell.X, cell.Z), cell.Y);
					if (cell.Corner.Type == CornerType.Filled)
					{
						array.Put(new XZ(cell.X, cell.Z + 1), cell.Corner.FillY);
					}
				}
			}

			return array;
		}

		/// <summary>
		/// The main method to generate the hill.
		/// </summary>
		/// <param name="minContourLength">The minimum length of the initial contour for the top of the hill.</param>
		/// <returns>A list of layers, from bottom to top, representing the hill.</returns>
		private static List<List<LayerCell>> Generate(PRNG prng, int minContourLength = 60)
		{
			var initialContour = BuildContour(minContourLength, prng);
			if (initialContour.Count > minContourLength)
			{
				initialContour = initialContour.Take(minContourLength).ToList();
			}

			bool firstLayerRejecter(ReadOnlySpan<LayerCell> run)
			{
				foreach (var cell in run)
				{
					int sum = cell.Y + cell.Z;
					if (sum < (initialY - initialRange) || sum > (initialY + initialRange))
					{
						return true;
					}
				}
				return false;
			}

			int y = initialY - initialContour[0];
			var firstLayer = ContourToLayerNEW(initialContour, y, firstLayerRejecter, prng);
			firstLayer = FillCorners(firstLayer);

			var layers = new List<List<LayerCell>> { firstLayer };

			while (true)
			{
				var prevLayer = layers.First();

				if (prevLayer.All(cell => cell.Y <= 0))
				{
					layers.RemoveAt(0);
					break;
				}

				var nextContour = LayerToContour(prevLayer);

				bool done = false;
				foreach (var dy in dyChoices)
				{
					try
					{
						int newY = y - dy;
						var nextLayer = ContourToLayerNEW(nextContour, newY, run => ShouldRejectRun(prevLayer, run), prng);
						nextLayer = FillCorners(nextLayer);
						layers.Insert(0, nextLayer);
						y = newY;
						done = true;
						break;
					}
					catch (Exception)
					{
					}
				}
				if (!done)
				{
					throw new Exception("failed...");
				}
			}

			layers.Reverse();
			return layers;
		}

		private static List<int> BuildContour(int minLength, PRNG prng)
		{
			const int minZ = 0;
			const int maxZ = 5;

			var contour = new List<int>();
			int z = prng.NextInt32(minZ, maxZ + 1);

			while (contour.Count < minLength)
			{
				int runLength = 3 + prng.NextInt32(6);
				int newZ = z + prng.RandomChoice(-1, 1);

				if (newZ >= minZ && newZ <= maxZ)
				{
					for (int i = 0; i < runLength; i++)
					{
						contour.Add(z);
					}
					z = newZ;
				}
			}
			return contour;
		}

		class TODO : LayerGenerator.Shared
		{
			public required LayerCell[] Buffer { get; init; }
			public required PRNG prng { get; init; }
			public required IReadOnlyList<int> Contour { get; init; }
			public required Func<ReadOnlySpan<LayerCell>, bool> ShouldRejectRunFunc { get; init; }

			public int maxX => Buffer.Length;
			public IImmutableSet<int> PossibleRunLengths => Enumerable.Range(runLengthMin, runLengthRand).ToImmutableSortedSet();

			public required int MaxSteps { get; init; }
			private int stepsTaken = 0;

			public bool GetWritableBuffer(int xStart, int runLength, out Span<LayerCell> buffer)
			{
				int xEnd = xStart + runLength;
				if (enforceRunVsContour && xEnd < maxX && xEnd > 0)
				{
					// don't allow a run to end where the contour changes
					if (Contour[xEnd] != Contour[xEnd - 1])
					{
						buffer = default;
						return false;
					}
				}

				buffer = Buffer.AsSpan().Slice(xStart, runLength);
				return true;
			}

			public bool IncrementStepCounter()
			{
				stepsTaken++;
				return stepsTaken > MaxSteps;
			}

			public bool ShouldRejectRun(Span<LayerCell> run) => ShouldRejectRunFunc(run);
		}

		record struct LayerGenerator(LayerGenerator.Shared shared, int xStart, int prevY)
		{
			public enum Result
			{
				Success,

				/// <summary>
				/// A kind of failure that cannot be attributed to bad luck -- this node was doomed to fail.
				/// </summary>
				Dead,

				/// <summary>
				/// A failure that might be due to bad luck.
				/// </summary>
				Unlucky,

				/// <summary>
				/// Used when we've wasted too much time and should quit entirely.
				/// </summary>
				Aborted,
			}

			public interface Shared
			{
				/// <summary>
				/// Internal state of the PRNG is expected to always advance; no backtracking here.
				/// </summary>
				PRNG prng { get; }

				int maxX { get; }

				/// <summary>
				/// Returns a writable buffer into which the algorithm will attempt to write results.
				/// Return false to indicate "don't try that xEnd value again" -- this is used to
				/// avoid ending a run where the contour changes, for aesthetic reasons.
				/// </summary>
				bool GetWritableBuffer(int xStart, int runLength, out Span<LayerCell> buffer);

				/// <summary>
				/// Returns true when it's time to give up.
				/// </summary>
				bool IncrementStepCounter();

				IImmutableSet<int> PossibleRunLengths { get; }

				bool ShouldRejectRun(Span<LayerCell> run);

				IReadOnlyList<int> Contour { get; }
			}

			public Result Execute()
			{
				if (xStart >= shared.maxX)
				{
					return Result.Success;
				}
				else if (xStart > shared.maxX)
				{
					throw new Exception("assert fail");
				}

				var possibleRunLengths = shared.PossibleRunLengths;

				for (int i = 0; i < 50; i++)
				{
					int unsafeRunLength = shared.prng.RandomChoice(possibleRunLengths.ToArray());
					int runLength = Math.Min(unsafeRunLength, shared.maxX - xStart);
					if (!shared.GetWritableBuffer(xStart, runLength, out var buffer))
					{
						possibleRunLengths = possibleRunLengths.Remove(unsafeRunLength); // don't try it again, we know it will fail
						if (!possibleRunLengths.Any())
						{
							return Result.Dead; // no possible run lengths work
						}
						else
						{
							// this was cheap (no recursion), let's not count it towards our giveup counters
							i--;
							continue;
						}
					}

					if (shared.IncrementStepCounter())
					{
						return Result.Aborted;
					}

					int y = prevY + shared.prng.RandomChoice(-1, 1);
					WriteRun(buffer, ref y);

					if (!shared.ShouldRejectRun(buffer))
					{
						var recurseResult = new LayerGenerator(this.shared, xStart + runLength, y).Execute();
						switch (recurseResult)
						{
							case Result.Success:
							case Result.Aborted:
								return recurseResult;
							case Result.Unlucky:
								// Too bad, we'll keep looping.
								break;
							case Result.Dead:
								// hmm... Would it even help to try to detect "all possible recursions are dead"?
								// It's complicated, and it's not clear that it would actually save time in practice.
								break;
							default:
								throw new Exception($"Assert fail: {recurseResult}");
						}
					}
				}

				return Result.Unlucky; // loop finished without succeeding
			}

			private void WriteRun(Span<LayerCell> buffer, ref int cellY)
			{
				int runLength = buffer.Length;

				int prevZ = (xStart > 0) ? shared.Contour[xStart - 1] : shared.Contour[xStart];

				for (int j = 0; j < runLength; j++)
				{
					int currentX = xStart + j;
					int z = shared.Contour[currentX];
					int dz = prevZ - z;
					switch (dz)
					{
						case 0:
						case 1:
						case -1:
							cellY += dz;
							break;
						default:
							throw new Exception("contour Z cannot jump by more than 1!");
					}

					int nextZ = (currentX + 1 < shared.Contour.Count) ? shared.Contour[currentX + 1] : z;

					CornerInfo corner;
					if (z < prevZ) corner = CornerInfo.Unfilled(CornerMarker.Prev);
					else if (z < nextZ) corner = CornerInfo.Unfilled(CornerMarker.Next);
					else corner = CornerInfo.None;

					buffer[j] = new LayerCell(currentX, z, cellY, corner);
					prevZ = z;
				}
			}
		}

		private static List<LayerCell> ContourToLayerNEW(IReadOnlyList<int> contour, int y, Func<ReadOnlySpan<LayerCell>, bool> shouldRejectRun, PRNG prng)
		{
			var TODO = new TODO()
			{
				Buffer = new LayerCell[contour.Count],
				Contour = contour,
				MaxSteps = contour.Count * 100,
				prng = prng,
				ShouldRejectRunFunc = shouldRejectRun,
			};

			for (int attempts = 0; attempts < 25; attempts++)
			{
				var result = new LayerGenerator(TODO, 0, y).Execute();
				if (result == LayerGenerator.Result.Success)
				{
					return TODO.Buffer.ToList();
				}
			}

			throw new Exception("TOO MANY RETRIES?!?!");
		}

		private static List<LayerCell> FillCorners(List<LayerCell> layer)
		{
			if (layer.Count < 2) return new List<LayerCell>(layer);

			var newLayer = layer.Select(c => c.Clone()).ToList();

			for (int i = 0; i < newLayer.Count - 1; i++)
			{
				var a = newLayer[i];
				var b = newLayer[i + 1];

				if (a.Corner.Type == CornerType.Unfilled && a.Corner.Marker == CornerMarker.Next)
				{
					a.Corner = CornerInfo.Filled(b.Y);
				}

				if (b.Corner.Type == CornerType.Unfilled && b.Corner.Marker == CornerMarker.Prev)
				{
					b.Corner = CornerInfo.Filled(a.Y);
				}
			}
			return newLayer;
		}

		private static List<int> LayerToContour(List<LayerCell> layer)
		{
			return layer.Select(cell => cell.Z + (cell.Corner.Type == CornerType.Filled ? 2 : 1)).ToList();
		}

		private static bool ShouldRejectRun(IReadOnlyList<LayerCell> prevLayer, ReadOnlySpan<LayerCell> run)
		{
			for (int i = 0; i < run.Length; i++)
			{
				var cell = run[i];
				if (cell.X >= prevLayer.Count) return true; // Should not happen with valid contours
				var northCell = prevLayer[cell.X];
				int separation = northCell.Y - cell.Y;
				if (separation < minSeparation || separation > maxSeparation)
				{
					return true;
				}
			}
			return false;
		}
	}
}