using System.Windows;
using System.Windows.Controls;

namespace Blocktavius.AppDQB2;

public class ScriptNodeTemplateSelector : DataTemplateSelector
{
	/// <summary>
	/// These names should all match a template defined in Application.Resources.
	/// </summary>
	public static class TemplateNames
	{
		public const string SCRIPT_NODE_LONG_STATUS_TEMPLATE = "SCRIPT_NODE_LONG_STATUS_TEMPLATE";
	}

	public DataTemplate? FallbackTemplate { get; set; }

	public Dictionary<Type, DataTemplate> KnownTemplates { get; set; } = new();

	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	{
		if (item == null)
		{
			return base.SelectTemplate(item, container);
		}

		if (item is ScriptNodeVM node && node.SelectDataTemplate(out var resourceKey))
		{
			if (container is FrameworkElement fe && fe.FindResource(resourceKey) is DataTemplate dt)
			{
				return dt;
			}

			// Log error here...
		}

		if (KnownTemplates.TryGetValue(item.GetType(), out var template))
		{
			return template;
		}

		return FallbackTemplate ?? base.SelectTemplate(item, container);
	}
}