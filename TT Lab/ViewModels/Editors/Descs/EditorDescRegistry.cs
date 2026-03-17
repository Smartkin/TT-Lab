using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors.Descs;

using EDITOR_DESC_FACTORY = Func<DocumentViewModel, PropertyNode, EditorDesc>;

public static class EditorDescRegistry
{
    private static readonly Dictionary<Type, EDITOR_DESC_FACTORY> DescFactories = new();

    static EditorDescRegistry()
    {
        RegisterDefaultDescs();
    }

    public static void Register<T>(EDITOR_DESC_FACTORY factory) => Register(typeof(T), factory);
    
    public static void Register(Type propertyType, EDITOR_DESC_FACTORY factory)
    {
        DescFactories[propertyType] = factory;
    }

    public static EditorDesc GetDesc(DocumentViewModel document, PropertyNode node)
    {
        if (node.Metadata?.EditorDescType is { } metadataEditorType
            && typeof(EditorDesc).IsAssignableFrom(metadataEditorType))
        {
            var desc = (EditorDesc)Activator.CreateInstance(metadataEditorType)!;
            return desc with { Document = document, Node = node };
        }

        if (node.Metadata?.ContainedTypeConstructor != null)
        {
            var collectionDescFactory = DescFactories[typeof(CollectionMarker)];
            return collectionDescFactory(document, node);
        }

        if (node.PropertyType.IsEnum && node.PropertyType.GetCustomAttribute<FlagsAttribute>() != null)
        {
            var flagsDescFactory = DescFactories[typeof(FlagsMarker)];
            return flagsDescFactory(document, node);
        }

        if (node.PropertyType.IsEnum)
        {
            var enumsDescFactory = DescFactories[typeof(EnumMarker)];
            return enumsDescFactory(document, node);
        }

        if (DescFactories.TryGetValue(node.PropertyType, out var descFactory))
        {
            var desc = descFactory(document, node);
            return desc;
        }
        
        return new GenericEditorDesc { Document = document, Node = node };
    }

    private static void RegisterDefaultDescs()
    {
        Register<bool>((document, node) => new BoolEditorDesc { Document = document, Node = node });
        Register<CodeEditorDesc>((document, node) => new CodeEditorDesc { Document = document, Node = node });
        Register<EnumMarker>((document, node) => new EnumEditorDesc { Document = document, Node = node });
        Register<FlagsMarker>((document, node) => new FlagsEditorDesc { Document = document, Node = node });
        Register<Matrix4>((document, node) => new Matrix4EditorDesc { Document = document, Node = node });
        EDITOR_DESC_FACTORY textFieldFactory = (document, node) => new TextEditorDesc { Document = document, Node = node };
        Register<string>(textFieldFactory);
        Register<Byte>(textFieldFactory);
        Register<SByte>(textFieldFactory);
        Register<UInt16>(textFieldFactory);
        Register<Int16>(textFieldFactory);
        Register<UInt32>(textFieldFactory);
        Register<Int32>(textFieldFactory);
        Register<UInt64>(textFieldFactory);
        Register<Int64>(textFieldFactory);
        Register<UInt128>(textFieldFactory);
        Register<Int64>(textFieldFactory);
        Register<Single>(textFieldFactory);
        Register<Double>(textFieldFactory);
        Register<Decimal>(textFieldFactory);
        Register<LabURI>((document, node) => new UriLinkEditorDesc { Document = document, Node = node });
        Register<Vector2>((document, node) => new Vector2EditorDesc { Document = document, Node = node });
        Register<Vector3>((document, node) => new Vector3EditorDesc { Document = document, Node = node });
        Register<Vector4>((document, node) => new Vector4EditorDesc { Document = document, Node = node });
        Register<VectorCharacterData>((document, node) => new VectorCharacterDataEditorDesc { Document = document, Node = node });
        Register<CollectionMarker>((document, node) => new CollectionEditorDesc { Document = document, Node = node });
    }

    private class CollectionMarker;
    private class EnumMarker;
    private class FlagsMarker;
}