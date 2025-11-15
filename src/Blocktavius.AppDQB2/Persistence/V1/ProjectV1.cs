using Blocktavius.Core;
using Blocktavius.DQB2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Blocktavius.AppDQB2.Persistence.V1;

sealed record ProjectV1
{
	public required string? ProfileVerificationHash { get; init; }
	public required SlotReferenceV1? SourceSlot { get; init; }
	public required SlotReferenceV1? DestSlot { get; init; }
	public required string? SourceStgdatFilename { get; init; }

	private IContentEqualityList<ChunkOffsetV1>? _chunkExpansion = null;
	public required IReadOnlyList<ChunkOffsetV1>? ChunkExpansion
	{
		get => _chunkExpansion;
		set => _chunkExpansion = value.EmptyIfNull()
			.OrderBy(o => o.OffsetZ)
			.ThenBy(o => o.OffsetX)
			.ToContentEqualityList();
	}

	public required string? Notes { get; init; }

	private IContentEqualityList<ImageReferenceV1>? _images = null;
	public required IReadOnlyList<ImageReferenceV1>? Images
	{
		get => _images;
		set => _images = value?.ToContentEqualityList();
	}

	public required bool? MinimapVisible { get; init; }
	public required bool? ChunkGridVisible { get; init; }

	public ProjectV1 VerifyProfileHash(ProfileSettings profile)
	{
		if (profile.VerificationHash == ProfileVerificationHash)
		{
			return this;
		}

		// This project comes from a different profile so clear out the slots
		// which will force this user to re-select the slots they want to use.
		return this with
		{
			ProfileVerificationHash = profile.VerificationHash,
			SourceSlot = null,
			DestSlot = null,
			SourceStgdatFilename = null,
		};
	}

	private static readonly JsonSerializerOptions jsonOptions = new()
	{
		TypeInfoResolver = NullablePropertiesNotRequiredResolver.Instance,
	};

	public static ProjectV1? Load(string json)
	{
		return JsonSerializer.Deserialize<ProjectV1>(json, jsonOptions);
	}

	public void Save(Stream stream)
	{
		JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
	}
}

/// <summary>
/// We prefer to match on SlotNumber, but SlotName can be used as a fallback.
/// </summary>
sealed record SlotReferenceV1
{
	public required int? SlotNumber { get; init; }
	public required string? SlotName { get; init; }
}

sealed record ImageReferenceV1
{
	public required string? RelativePath { get; init; }
	public required bool? IsVisible { get; init; }
}

sealed record ChunkOffsetV1
{
	public required int OffsetX { get; init; }
	public required int OffsetZ { get; init; }

	public ChunkOffset ToCore() => new ChunkOffset(OffsetX, OffsetZ);

	public static ChunkOffsetV1 FromCore(ChunkOffset offset) => new ChunkOffsetV1
	{
		OffsetX = offset.OffsetX,
		OffsetZ = offset.OffsetZ,
	};
}
