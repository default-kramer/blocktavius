using Blocktavius.AppDQB2.Resources;

namespace Blocktavius.AppDQB2.Persistence;

public sealed class ScriptDeserializationContext
{
	internal ScriptDeserializationContext(IReadOnlyList<ExtractedSnippetResourceVM> snippets)
	{
		this.Snippets = snippets;
	}

	public required IAreaManager AreaManager { get; init; }
	public required IBlockManager BlockManager { get; init; }
	internal IReadOnlyList<ExtractedSnippetResourceVM> Snippets { get; }
}
