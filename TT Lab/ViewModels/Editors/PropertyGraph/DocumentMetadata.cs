using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public record DocumentMetadata : EditorMetadata
{
    public PropertyMetadata[] Properties { get; }

    public DocumentMetadata(Type type, bool searchAllAttributes = false)
    {
        Editable = type.GetCustomAttribute<EditableAttribute>();
        EditorDescType = Editable?.EditorDescType;
        EditorParams = type.GetCustomAttributes<EditorParamAttribute>().ToDictionary(x => x.Param, x => x.Value);
        EditorParamWrappers = type.GetCustomAttributes<EditorParamWrapperBaseAttribute>().ToArray();
        FieldReactors = GetFieldReactors(type);
        
        var editableProps = new List<PropertyMetadata>();

        foreach (var property in type.GetProperties())
        {
            var editableAttribute = property.GetCustomAttribute<EditableAttribute>();
            if (editableAttribute == null && !searchAllAttributes)
            {
                continue;
            }

            Func<object>? containedTypeConstructor = null;
            if (property.PropertyType.IsGenericType || property.PropertyType.IsArray)
            {
                var containedType = property.PropertyType.GetElementType() ?? property.PropertyType.GetGenericArguments().FirstOrDefault();
                if (containedType != null)
                {
                    containedTypeConstructor = GetTypeFactory(containedType);
                }
            }

            if (Editable?.IncludeAllProperties == true)
            {
                // Circular dependency handling
                if (type == property.PropertyType)
                {
                    editableProps.Add(new PropertyMetadata
                    {
                        PropertyInfo = property,
                        ContainedTypeConstructor = containedTypeConstructor,
                        EditorDescType = EditorDescType,
                        Editable = Editable,
                        EditorParams = EditorParams,
                        EditorParamWrappers = EditorParamWrappers,
                        FieldReactors = FieldReactors
                    });
                    continue;
                }
                
                var metadata = DocumentMetadataCache.Get(property.PropertyType, true);
                editableProps.Add(new PropertyMetadata
                {
                    PropertyInfo = property,
                    ContainedTypeConstructor = containedTypeConstructor,
                    EditorDescType = metadata.EditorDescType,
                    Editable = metadata.Editable,
                    EditorParams = metadata.EditorParams,
                    EditorParamWrappers = metadata.EditorParamWrappers,
                    FieldReactors = metadata.FieldReactors
                });
            }
            else
            {
                editableProps.Add(new PropertyMetadata
                {
                    PropertyInfo = property,
                    ContainedTypeConstructor = containedTypeConstructor,
                    EditorDescType = editableAttribute!.EditorDescType,
                    Editable = editableAttribute,
                    EditorParams = property.GetCustomAttributes<EditorParamAttribute>().ToDictionary(x => x.Param, x => x.Value),
                    EditorParamWrappers = property.GetCustomAttributes<EditorParamWrapperBaseAttribute>().ToArray(),
                    FieldReactors = GetFieldReactors(property)
                });
            }
            
        }
        
        Properties = editableProps.ToArray();
    }
    
    private static Dictionary<string, List<IFieldChange>> GetFieldReactors(MemberInfo provider)
    {
        var links = provider.GetCustomAttributes<EditorLinkedFieldAttribute>();
        var result = new Dictionary<string, List<IFieldChange>>();
        foreach (var link in links)
        {
            if (!result.TryGetValue(link.LinkedField, out var linkList))
            {
                result[link.LinkedField] = [];
                linkList = result[link.LinkedField];
            }
            
            linkList.Add((IFieldChange)Activator.CreateInstance(link.ActionChange)!);
        }
        return result;
    }

    private static readonly Dictionary<Type, Func<object>> ConstructorCache = new();
    private static Func<object> GetTypeFactory(Type type)
    {
        if (ConstructorCache.TryGetValue(type, out var result))
        {
            return result;
        }
        
        if (type.IsValueType || type == typeof(string) || type == typeof(decimal))
        {
            result = Expression.Lambda<Func<object>>(
                    Expression.Convert(Expression.Default(type),
                        typeof(object))
                ).Compile();
        }
        else
        {
            var constructor = type.GetConstructor(Type.EmptyTypes)!;
            var newExpression = Expression.New(constructor);
            result = Expression.Lambda<Func<object>>(newExpression).Compile();
        }
        
        ConstructorCache[type] = result;
        return result;
    }
}