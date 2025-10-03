using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Blocktavius.Tests;

/// <summary>
/// Helper for creating readable assertions on I2DSampler results.
/// Provides ASCII-art style visualization for test readability.
/// </summary>
public static class SamplerAssert
{
	/// <summary>
	/// Creates a multi-line string representation of a 2D sampler.
	/// Each position is formatted using the provided formatter function.
	/// </summary>
	public static string Print<T>(I2DSampler<T> sampler, Func<T, string> formatter, int cellWidth = 3)
	{
		var sb = new StringBuilder();
		var bounds = sampler.Bounds;

		// Print with Z increasing downward (standard screen coordinates)
		for (int z = bounds.start.Z; z < bounds.end.Z; z++)
		{
			for (int x = bounds.start.X; x < bounds.end.X; x++)
			{
				var value = sampler.Sample(new XZ(x, z));
				string formatted = formatter(value);

				// Pad or truncate to cellWidth
				if (formatted.Length < cellWidth)
				{
					formatted = formatted.PadLeft(cellWidth);
				}
				else if (formatted.Length > cellWidth)
				{
					formatted = formatted.Substring(0, cellWidth);
				}

				sb.Append(formatted);
			}
			sb.AppendLine();
		}

		return sb.ToString();
	}

	/// <summary>
	/// Print an Elevation sampler showing Y values.
	/// </summary>
	public static string PrintElevations(I2DSampler<Elevation> sampler, int cellWidth = 3)
	{
		return Print(sampler, e => e.Y == -1 ? "." : e.Y.ToString(), cellWidth);
	}

	/// <summary>
	/// Asserts that the sampler matches the expected ASCII pattern.
	/// Pattern should be a multi-line string where each character represents an expected value.
	/// Use a parser function to convert characters to expected values.
	/// </summary>
	public static void MatchesPattern<T>(
		I2DSampler<T> sampler,
		string expectedPattern,
		Func<char, T> charToValue,
		Func<T, T, bool>? comparer = null)
		where T : IEquatable<T>
	{
		comparer ??= (a, b) => a?.Equals(b) ?? (b is null);

		var lines = expectedPattern.Split('\n', StringSplitOptions.RemoveEmptyEntries)
			.Select(line => line.TrimEnd('\r'))
			.ToArray();

		if (lines.Length == 0)
		{
			throw new ArgumentException("Pattern cannot be empty");
		}

		int expectedHeight = lines.Length;
		int expectedWidth = lines.Max(l => l.Length);

		var bounds = sampler.Bounds;
		int actualHeight = bounds.Size.Z;
		int actualWidth = bounds.Size.X;

		if (actualWidth != expectedWidth || actualHeight != expectedHeight)
		{
			Assert.Fail(
				$"Size mismatch: expected {expectedWidth}x{expectedHeight}, got {actualWidth}x{actualHeight}");
		}

		var errors = new List<string>();

		for (int z = 0; z < expectedHeight; z++)
		{
			string line = z < lines.Length ? lines[z] : "";
			for (int x = 0; x < expectedWidth; x++)
			{
				char expectedChar = x < line.Length ? line[x] : ' ';
				T expectedValue = charToValue(expectedChar);
				T actualValue = sampler.Sample(new XZ(bounds.start.X + x, bounds.start.Z + z));

				if (!comparer(actualValue, expectedValue))
				{
					errors.Add($"  At ({bounds.start.X + x},{bounds.start.Z + z}): expected {expectedValue}, got {actualValue}");
				}
			}
		}

		if (errors.Any())
		{
			var actual = Print(sampler, v => v?.ToString() ?? "?", 1);
			Assert.Fail(
				$"Pattern mismatch:\n\nExpected:\n{expectedPattern}\n\nActual:\n{actual}\n\nErrors:\n{string.Join("\n", errors)}");
		}
	}

	/// <summary>
	/// Asserts that elevations at given positions match expected values.
	/// More flexible than pattern matching for spot-checking specific coordinates.
	/// </summary>
	public static void ElevationsAt(
		I2DSampler<Elevation> sampler,
		params (int x, int z, int expectedY)[] expectations)
	{
		var errors = new List<string>();

		foreach (var (x, z, expectedY) in expectations)
		{
			var actualY = sampler.Sample(new XZ(x, z)).Y;
			if (actualY != expectedY)
			{
				errors.Add($"  At ({x},{z}): expected {expectedY}, got {actualY}");
			}
		}

		if (errors.Any())
		{
			Assert.Fail(
				$"Elevation mismatches:\n{string.Join("\n", errors)}\n\nActual:\n{PrintElevations(sampler)}");
		}
	}

	/// <summary>
	/// Asserts that all positions in a region satisfy a predicate.
	/// Useful for checking gradients, ranges, or other properties.
	/// </summary>
	public static void AllSatisfy<T>(
		I2DSampler<T> sampler,
		Func<XZ, T, bool> predicate,
		string failureMessage = "Predicate failed")
	{
		var errors = new List<string>();

		foreach (var xz in sampler.Bounds.Enumerate())
		{
			var value = sampler.Sample(xz);
			if (!predicate(xz, value))
			{
				errors.Add($"  At {xz}: {value}");
			}
		}

		if (errors.Any())
		{
			Assert.Fail(
				$"{failureMessage}\n{string.Join("\n", errors.Take(10))}" +
				(errors.Count > 10 ? $"\n  ... and {errors.Count - 10} more" : ""));
		}
	}
}
