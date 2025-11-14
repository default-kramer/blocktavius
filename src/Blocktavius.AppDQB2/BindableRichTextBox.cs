using Blocktavius.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Blocktavius.AppDQB2;

public sealed record BindableRichText
{
	public required IContentEqualityList<Run> Contents { get; init; }

	public static readonly BindableRichText Empty = new BindableRichText
	{
		Contents = Enumerable.Empty<Run>().ToContentEqualityList()
	};

	public FlowDocument BuildDocument()
	{
		var doc = new FlowDocument();
		var paragraph = new Paragraph();

		foreach (var runData in Contents)
		{
			var run = new System.Windows.Documents.Run(runData.Text)
			{
				Foreground = new SolidColorBrush(runData.Color)
			};
			paragraph.Inlines.Add(run);
		}

		doc.Blocks.Add(paragraph);
		return doc;
	}

	public sealed record Run
	{
		public required string Text { get; init; }
		public required Color Color { get; init; } // Added Color property
	}
}

public sealed class BindableRichTextBuilder()
{
	private readonly List<BindableRichText.Run> runs = new();

	public BindableRichText Build() => new BindableRichText { Contents = runs.ToContentEqualityList() };

	public BindableRichTextBuilder Append(string? text)
	{
		if (text == null)
		{
			return this;
		}
		runs.Add(new BindableRichText.Run
		{
			Text = text,
			Color = Colors.Black,
		});
		return this;
	}

	public BindableRichTextBuilder AppendLine(string? text) => Append(text + Environment.NewLine);

	public BindableRichTextBuilder AppendLine() => AppendLine(null);

	public BindableRichTextBuilder FallbackIfNull(string fallbackText, params string?[] strings)
	{
		foreach (var str in strings)
		{
			if (str != null)
			{
				return Append(str);
			}
		}
		runs.Add(new BindableRichText.Run
		{
			Text = fallbackText,
			Color = Colors.Red,
		});
		return this;
	}
}

public class BindableRichTextBox : RichTextBox
{
	public static readonly DependencyProperty BindableRichTextProperty =
		DependencyProperty.Register(
			"BindableRichText",
			typeof(BindableRichText),
			typeof(BindableRichTextBox),
			new PropertyMetadata(null, OnBindableRichTextBuilderChanged));

	public BindableRichText BindableRichText
	{
		get { return (BindableRichText)GetValue(BindableRichTextProperty); }
		set { SetValue(BindableRichTextProperty, value); }
	}

	private static void OnBindableRichTextBuilderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is BindableRichTextBox me)
		{
			if (e.NewValue is BindableRichText newText)
			{
				if (newText != e.OldValue as BindableRichText)
				{
					me.Document = newText.BuildDocument();
				}
			}
			else
			{
				me.Document = BindableRichText.Empty.BuildDocument();
			}
		}
	}
}
