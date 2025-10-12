using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Blocktavius.AppDQB2;

/// <summary>
/// When placed on a property of a class that inherits from <see cref="ViewModelBaseWithCustomTypeDescriptor"/>,
/// this attribute causes the properties of the object assigned to this property to be
/// flattened into the parent object on the property grid.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FlattenPropertiesAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the category name to use for the flattened properties.
	/// If null or empty, the original categories of the child properties will be used.
	/// </summary>
	public string? CategoryName { get; set; }
}

/// <summary>
/// Implements ICustomTypeDescriptor to support certain custom attributes defined by this project.
/// </summary>
/// <remarks>
/// Using a [TypeDescriptionProvider] attribute would be nicer, but it doesn't work.
/// It seems that the type description is always based on the type alone and not the instance.
/// (Although the provider does create descriptors when instance!=null, those providers are never called.)
/// </remarks>
abstract class ViewModelBaseWithCustomTypeDescriptor : ViewModelBase, ICustomTypeDescriptor
{
	sealed record FlattenedProp
	{
		public required WeakReference<ViewModelBase> FromObjectRef { get; init; }
		public ViewModelBase? FromObject => FromObjectRef.TryGetTarget(out var vm) ? vm : null;
		public required string OriginalName { get; init; }
		public required string FlatName { get; init; }
	}

	private readonly object subscriptionKey = new();
	private readonly List<FlattenedProp> flattenedProps = new();

	PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
	{
		this.flattenedProps.Clear(); // we are about to rebuild this list

		// Get the default properties for this type, without using custom descriptors
		var defaultProps = TypeDescriptor.GetProperties(this, true);
		var allProps = new List<PropertyDescriptor>();

		foreach (PropertyDescriptor prop in defaultProps)
		{
			var flattenAttr = prop.Attributes.OfType<FlattenPropertiesAttribute>().FirstOrDefault();
			if (flattenAttr != null)
			{
				var childObject = prop.GetValue(this);
				if (childObject != null)
				{
					var childVM = childObject as ViewModelBase;
					if (childVM != null)
					{
						// subscribe so we can forward the PropertyChanged events
						childVM.Subscribe(subscriptionKey, this);
					}

					var childProperties = TypeDescriptor.GetProperties(childObject);
					foreach (PropertyDescriptor childProp in childProperties)
					{
						string flatName = $"{prop.Name}.{childProp.Name}";
						if (childVM != null)
						{
							this.flattenedProps.Add(new FlattenedProp
							{
								FromObjectRef = new WeakReference<ViewModelBase>(childVM),
								OriginalName = childProp.Name,
								FlatName = flatName,
							});
						}
						allProps.Add(new ForwardingPropertyDescriptor(flatName, childProp)
						{
							childComponent = childObject,
							customCategory = flattenAttr.CategoryName,
						});
					}
				}
			}
			else
			{
				allProps.Add(prop);
			}
		}

		return new PropertyDescriptorCollection(allProps.ToArray());
	}

	protected override void OnSubscribedPropertyChanged(ViewModelBase sender, PropertyChangedEventArgs e)
	{
		bool allProps = string.IsNullOrEmpty(e.PropertyName);
		var matches = flattenedProps
			.Where(p => p.FromObject == sender)
			.Where(p => allProps || p.OriginalName == e.PropertyName)
			.ToList();
		foreach (var match in matches)
		{
			OnPropertyChanged(match.FlatName);
		}
	}


	AttributeCollection ICustomTypeDescriptor.GetAttributes() => TypeDescriptor.GetAttributes(this, true);
	string? ICustomTypeDescriptor.GetClassName() => TypeDescriptor.GetClassName(this, true);
	string? ICustomTypeDescriptor.GetComponentName() => TypeDescriptor.GetComponentName(this, true);
	TypeConverter ICustomTypeDescriptor.GetConverter() => TypeDescriptor.GetConverter(this, true);
	EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent() => TypeDescriptor.GetDefaultEvent(this, true);
	PropertyDescriptor? ICustomTypeDescriptor.GetDefaultProperty() => TypeDescriptor.GetDefaultProperty(this, true);
	object? ICustomTypeDescriptor.GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(this, editorBaseType, true);
	EventDescriptorCollection ICustomTypeDescriptor.GetEvents() => TypeDescriptor.GetEvents(this, true);
	EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[]? attributes) => TypeDescriptor.GetEvents(this, attributes, true);
	object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) => this;
	PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[]? attributes) => TypeDescriptor.GetProperties(this, attributes, true);

	private sealed class ForwardingPropertyDescriptor : PropertyDescriptor
	{
		private readonly PropertyDescriptor childDescriptor;
		public required string? customCategory { get; init; }
		public required object childComponent { get; init; }

		public ForwardingPropertyDescriptor(string flatName, PropertyDescriptor childDescriptor)
			: base(flatName, childDescriptor.Attributes.Cast<Attribute>().ToArray())
		{
			this.childDescriptor = childDescriptor;
		}

		public override Type ComponentType => childComponent.GetType();
		public override bool IsReadOnly => childDescriptor.IsReadOnly;
		public override Type PropertyType => childDescriptor.PropertyType;
		public override string Category => string.IsNullOrWhiteSpace(customCategory) ? childDescriptor.Category : customCategory;
		public override string DisplayName => childDescriptor.DisplayName;
		public override bool CanResetValue(object component) => childDescriptor.CanResetValue(childComponent);
		public override object? GetValue(object? component) => childDescriptor.GetValue(childComponent);
		public override void ResetValue(object component) => childDescriptor.ResetValue(childComponent);
		public override bool ShouldSerializeValue(object component) => false; // hmm, this is probably what we want
		public override void SetValue(object? component, object? value) => childDescriptor.SetValue(childComponent, value);
	}
}
