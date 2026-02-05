using Blocktavius.Core;
using Blocktavius.DQB2;
using Blocktavius.DQB2.Mutations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blocktavius.AppDQB2;

static class TERRAGEN
{
	public static void DropTheHammer(IMutableStage stage)
	{
		/*
(define (chisel->mask chisel)
  (case chisel
    [(none) 0]
    ; * 1/3/5/7 - diagonal chisel N/E/S/W, matches (blueprint.chisel_status << 4)
    ; * 2/4/6/8 - diagonal chisel SW/SE/NW/NE
    ; * 9/a/b/c - concave chisel NW/SW/SE/NE
    [(flat-lo) #xe000]
    [(flat-hi) #xf000]))
		*/

		var availableSpace = new Rect(new XZ(900, 900), new XZ(1200, 1200));

		const int groundMinY = 13;
		stage.Mutate(new ClearEverythingMutation() { StartY = groundMinY });

		var prng = PRNG.Deserialize("1-2-3-67-67-67");

		stage.Mutate(MakeGround(availableSpace, groundMinY));

		List<ITerraformComponent> components = new();

		components.AddRange(HillRequests().Select(r => new HillComponent { HillRequest = r }));

		var arranged = Arrange(components, availableSpace, prng);

		foreach (var (position, component) in arranged)
		{
			var context = new TerraformMutationContext
			{
				ArrangedPosition = position,
				PRNG = prng,
				Stage = stage,
			};
			component.Mutate(context);
		}
	}

	private static PutHillMutation MakeGround(Rect rect, int y)
	{
		var sampler = new ConstantSampler<int> { Bounds = rect, Value = y };
		return new PutHillMutation()
		{
			Block = 3,
			Sampler = sampler,
			YFloor = 1,
		};
	}

	sealed record HillSpec
	{
		public required int Elevation { get; init; }
		public required int Size { get; init; }
	}

	interface IArrangable
	{
		Rect SeedSize { get; }
	}

	sealed record TerraformMutationContext
	{
		public required IMutableStage Stage { get; init; }
		public required PRNG PRNG { get; init; }
		public required Rect ArrangedPosition { get; init; }
	}

	interface ITerraformComponent : IArrangable
	{
		void Mutate(TerraformMutationContext context);
	}

	class HillComponent : ITerraformComponent
	{
		public required WIP.HillRequest HillRequest { get; init; }

		public Rect SeedSize => HillRequest.SeedSize;

		public void Mutate(TerraformMutationContext context)
		{
			var hill = WIP.Blah(context.PRNG.AdvanceAndClone(), this.HillRequest);
			var hill2 = hill.Project(item =>
			{
				ushort blockId;
				if (item.Kind == WIP.HillItemKind.Plateau)
				{
					blockId = 4;
				}
				else if (item.Kind == WIP.HillItemKind.Chisel)
				{
					blockId = 21 | 0xe000;
				}
				else if (item.Kind == WIP.HillItemKind.Cliff)
				{
					blockId = 21;
				}
				else
				{
					blockId = 0;
					return (-1, blockId);
				}
				return (item.Elevation, blockId);
			});

			var mut = new PutHillMutation2()
			{
				Sampler = hill2.TranslateTo(context.ArrangedPosition.start),
				YFloor = 1,
			};

			context.Stage.Mutate(mut);
		}
	}

	private static IEnumerable<WIP.HillRequest> HillRequests()
	{
		// random rotation should be applied here, or at least before Arrange()
		yield return new WIP.HillRequest { Elevation = 50, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(120, 80)) };
		yield return new WIP.HillRequest { Elevation = 48, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(80, 120)) };
		yield return new WIP.HillRequest { Elevation = 40, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(60, 45)) };
		yield return new WIP.HillRequest { Elevation = 32, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(80, 150)) };
		yield return new WIP.HillRequest { Elevation = 30, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(150, 80)) };
		yield return new WIP.HillRequest { Elevation = 26, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(100, 100)) };
		yield return new WIP.HillRequest { Elevation = 24, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(70, 140)) };
		yield return new WIP.HillRequest { Elevation = 20, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(150, 150)) };
		yield return new WIP.HillRequest { Elevation = 18, SeedSize = new Rect(XZ.Zero, XZ.Zero.Add(150, 150)) };
	}

	private static List<(Rect translatedPosition, TComponent request)> Arrange<TComponent>(IEnumerable<TComponent> requests, Rect fullSpace, PRNG prng)
		where TComponent : IArrangable
	{
		var positions = requests.Select(request => (position: request.SeedSize, request)).ToList();
		var best = (positions, overlap: int.MaxValue); // lower values are better, zero is perfect

		for (int attempt = 0; attempt < 5; attempt++)
		{
			int totalOverlap = 0;

			for (int i = 0; i < positions.Count; i++)
			{
				var item = positions[i];
				var range = new Rect(fullSpace.start, fullSpace.end.Subtract(item.request.SeedSize.Size));
				var x = prng.NextInt32(range.start.X, range.end.X);
				var z = prng.NextInt32(range.start.Z, range.end.Z);
				var start = new XZ(x, z);
				var pos = new Rect(start, start.Add(item.request.SeedSize.Size));

				var overlappers = positions.Take(i).Select(x => x.position).ToList();

				var current = (overlap: int.MaxValue, pos);
				foreach (var dir in Direction.CardinalDirections())
				{
					if (current.overlap == 0) { break; }
					var pushed = BestPush(pos, overlappers, dir.Step, fullSpace);
					if (pushed.Item1 < current.overlap)
					{
						current = pushed;
					}
				}

				positions[i] = (current.pos, item.request);
				totalOverlap += current.overlap;
			}

			if (totalOverlap < best.overlap)
			{
				best = (positions.ToList(), totalOverlap);
			}
		}

		return best.positions;
	}

	private static (int, Rect) BestPush(Rect rect, IReadOnlyList<Rect> overlappers, XZ pushDir, Rect fullSpace)
	{
		int overlap(Rect rect)
		{
			int o = 0;
			foreach (var other in overlappers)
			{
				var i = other.Intersection(rect);
				o += i.Size.X * i.Size.Z;
			}
			return o;
		}

		var best = (overlap(rect), rect);
		while (best.Item1 > 0 && fullSpace.Intersection(rect).Size == rect.Size)
		{
			rect = new Rect(rect.start.Add(pushDir), rect.end.Add(pushDir));
			var o = overlap(rect);
			if (o < best.Item1)
			{
				best = (o, rect);
			}
		}

		return best;
	}
}
