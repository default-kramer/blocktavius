using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Blocktavius.AppDQB2;

/// <summary>
/// A nullable required property (like `public required string?`) should only be required
/// from the C# compiler's perspective.
/// The JSON serializer shouldn't care if it's missing and just set it to null.
/// </summary>
public class NullablePropertiesNotRequiredResolver : DefaultJsonTypeInfoResolver
{
	protected NullablePropertiesNotRequiredResolver() { }
	public static readonly NullablePropertiesNotRequiredResolver Instance = new();

	public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
	{
		JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

		if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object)
		{
			foreach (var propertyInfo in jsonTypeInfo.Properties)
			{
				if (propertyInfo.IsSetNullable)
				{
					propertyInfo.IsRequired = false;
				}
			}
		}

		return jsonTypeInfo;
	}
}
