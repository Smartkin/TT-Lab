using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

/// <summary>
/// Puts copied values into a property and everything under it, as one step of the document's history. Values of the same kind go over
/// the property's as they were copied, linked fields under it included, while the ones following it from outside work theirs out the way
/// an edit does. Asks before values of another kind replace what's there (a camera subtype of another kind, an empty slot), whether values
/// going into a list go after its last item or over its items from the one pasted onto (and on past its end), and before the property's
/// rules limit what's pasted: a list's most items, a field's range and length, a sub type its object's type can't have (unless the
/// paste changes the type too), particle system names, which get a free one
/// </summary>
public static class ValuesPaste
{
    /// <summary>
    /// Asks the one pasting: a title, what's about to happen and the answers besides cancelling (none for a message to close), the index
    /// of the answer picked or none
    /// </summary>
    internal static Func<string, string, IReadOnlyList<string>, Task<int?>> Ask { get; set; } = ChoiceDialogue.Ask;

    /// <summary>
    /// Why the copied values can't go into the node, none when they can
    /// </summary>
    /// <param name="isAssetRoot">The node is a whole asset's (a document's top, the inspected asset's), which takes a whole asset's values</param>
    /// <param name="isReadOnly">Its editor is grayed out</param>
    public static string? WhyNot(CopiedValues? copied, PropertyNode target, bool isAssetRoot, bool isReadOnly)
    {
        if (copied == null)
        {
            return "No values are copied";
        }

        if (isAssetRoot)
        {
            if (!copied.IsAsset)
            {
                return $"The copied values are {copied.From}'s, not a whole asset's";
            }

            return target.Target.GetType() == copied.Type ? null : $"The copied values are a whole {Friendly(copied.Type)}, this is a {Friendly(target.Target.GetType())}";
        }

        if (copied.IsAsset)
        {
            return $"The copied values are a whole {Friendly(copied.Type)}, they go into an asset's header";
        }

        if (isReadOnly)
        {
            return "This value is grayed out";
        }

        if (ListOf(target) is { } list && GoesIntoList(copied, list.List))
        {
            return WhyNotLinks(copied, list.List);
        }

        if (CopiedValues.IsListType(target.PropertyType) || CopiedValues.IsListType(copied.Type))
        {
            return $"The copied values are {Describe(copied.Type)}, this is {Describe(target.PropertyType)}";
        }

        if (!Fits(copied.Type, target.PropertyType))
        {
            return $"The copied values are {Describe(copied.Type)}, this is {Describe(target.PropertyType)}";
        }

        return target.PropertyType == typeof(LabURI) ? WhyNotLinks(copied, target) : null;
    }

    /// <summary>
    /// Pastes what the clipboard holds into the node, asking what the values need asked. Whether anything got pasted
    /// </summary>
    public static async Task<bool> PasteAsync(DocumentViewModel document, PropertyNode target, bool isAssetRoot, bool isReadOnly)
    {
        var copied = CopiedValues.Read(await ValuesClipboard.ReadTextAsync());
        if (WhyNot(copied, target, isAssetRoot, isReadOnly) is { } reason)
        {
            await Ask("Can't paste the values", reason, []);
            return false;
        }

        var planner = new Planner();
        PropertyNode root;
        if (isAssetRoot)
        {
            planner.PlanAsset(copied!.Values, target);
            root = target;
        }
        else if (ListOf(target) is { } list && GoesIntoList(copied!, list.List))
        {
            var items = ItemsOf(copied!);
            var count = ((IList)list.List.GetValue()!).Count;
            var over = list.List == target ? "from its first item" : $"from item {list.Start}";
            var answer = await Ask("Paste values into a list",
                $"{Count(items.Count, "copied item")} go into {UndoHistory.NameOf(list.List)}, which has {Count(count, "item")}. Append adds them after its last item, " +
                $"Replace goes over its items {over} and adds the ones left over after its last item.", ["Append", "Replace"]);
            if (answer == null)
            {
                return false;
            }

            planner.PlanItems(items, list.List, answer == 0 ? count : list.Start);
            root = list.List;
        }
        else
        {
            planner.PlanValue(copied!.Values, target, copied.Type);
            root = target;
        }

        if (planner.Overwrites.Count > 0 && await Ask("Overwrite values", $"These copied values aren't the same kind as what's there:\n{Lines(planner.Overwrites)}\nOverwrite them?", ["Overwrite"]) != 0)
        {
            return false;
        }

        if (planner.Limits.Count > 0 && await Ask("Some values get limited", $"Pasting limits some of the values to what this takes:\n{Lines(planner.Limits)}\nPaste anyway?", ["Paste anyway"]) != 0)
        {
            return false;
        }

        var graph = document.PropertyGraph;
        using (document.History.BeginGroup($"Pasted values into '{UndoHistory.NameOf(target)}'"))
        {
            graph.PasteRoot = root;
            try
            {
                foreach (var step in planner.Steps)
                {
                    step();
                }
            }
            finally
            {
                graph.PasteRoot = null;
            }
        }

        return true;
    }

    // The list values pasted onto it go into, and where Replace starts: a list from its first item, an element from its own place
    private static (PropertyNode List, int Start)? ListOf(PropertyNode target)
    {
        if (IsList(target))
        {
            return (target, 0);
        }

        if (target.Index is { } index && target.Parent is { } parent && IsList(parent))
        {
            return (parent, index);
        }

        return null;
    }

    private static bool IsList(PropertyNode node) => CopiedValues.IsListType(node.PropertyType) && node.GetValue() is IList;

    // Items of the list's kind, or a list of them
    private static bool GoesIntoList(CopiedValues copied, PropertyNode list)
    {
        var elementType = CopiedValues.ElementTypeOf(list.GetValue()!.GetType());
        return Fits(copied.Type, elementType) || (CopiedValues.IsListType(copied.Type) && Fits(CopiedValues.ElementTypeOf(copied.Type), elementType));
    }

    private static List<(JsonElement Json, Type Type)> ItemsOf(CopiedValues copied)
    {
        if (!CopiedValues.IsListType(copied.Type) || copied.Values.ValueKind != JsonValueKind.Array)
        {
            return [(copied.Values, copied.Type)];
        }

        var elementType = CopiedValues.ElementTypeOf(copied.Type);
        return copied.Values.EnumerateArray().Select(item => (item, CopiedValues.TypeOf(item, elementType))).ToList();
    }

    // Numbers, text and links only go where the same type does, other values where their type is taken
    private static bool Fits(Type copied, Type declared)
    {
        var target = Nullable.GetUnderlyingType(declared) ?? declared;
        var source = Nullable.GetUnderlyingType(copied) ?? copied;
        if (CopiedValues.IsLeafType(target) || CopiedValues.IsLeafType(source) || target == typeof(LabURI) || source == typeof(LabURI))
        {
            return source == target;
        }

        if (CopiedValues.IsListType(target) || CopiedValues.IsListType(source))
        {
            return CopiedValues.IsListType(target) && CopiedValues.IsListType(source) && Fits(CopiedValues.ElementTypeOf(source), CopiedValues.ElementTypeOf(target));
        }

        return target.IsAssignableFrom(source);
    }

    // A link field takes links to its kind of asset
    private static string? WhyNotLinks(CopiedValues copied, PropertyNode link)
    {
        if (link.Metadata?.EditorParams.TryGetValue(UriLinkViewModel.BrowseType, out var browse) != true || browse is not Type browseType || browseType == typeof(IAsset))
        {
            return null;
        }

        var links = copied.Values.ValueKind == JsonValueKind.Array ? copied.Values.EnumerateArray().ToList() : [copied.Values];
        var assets = AssetManager.Get();
        foreach (var json in links)
        {
            if (!CopiedValues.TryReadLink(json, out var uri) || uri == LabURI.Empty || !assets.DoesAssetExist(uri))
            {
                continue;
            }

            var asset = assets.GetAsset(uri);
            if (!browseType.IsInstanceOfType(asset))
            {
                return $"The copied link is to a {Friendly(asset.GetType())}, this links {Friendly(browseType)} assets";
            }
        }

        return null;
    }

    private static string Describe(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return CopiedValues.IsListType(type) ? $"a list of {Friendly(CopiedValues.ElementTypeOf(type))}" : $"a {Friendly(type)}";
    }

    private static string Friendly(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return CopiedValues.IsListType(type) ? $"list of {Friendly(CopiedValues.ElementTypeOf(type))}" : type.Name;
    }

    private static string Count(int count, string what) => count == 1 ? $"1 {what}" : $"{count} {what}s";

    private static string Lines(List<string> lines)
    {
        const int Shown = 12;
        var shown = lines.Take(Shown).Select(line => $"• {line}");
        return string.Join("\n", lines.Count > Shown ? shown.Append($"• and {lines.Count - Shown} more") : shown);
    }

    /// <summary>
    /// What a paste does, worked out before anything changes: the changes in order, what it asks first and what the property's rules
    /// limit
    /// </summary>
    private sealed class Planner
    {
        public List<Action> Steps { get; } = [];
        public List<string> Overwrites { get; } = [];
        public List<string> Limits { get; } = [];

        // Particle system names this paste gives, two systems pasted at once can't get the same one
        private readonly HashSet<string> _givenNames = [];
        // Whether the object values are pasted into gets another type with them, which drops its type's rules
        private bool _typeChanges;

        public void PlanAsset(JsonElement json, PropertyNode asset)
        {
            foreach (var child in asset.Children)
            {
                if (child.Name != nameof(SerializableAsset.Alias) && CopiedValues.IsValue(child) && json.ValueKind == JsonValueKind.Object && json.TryGetProperty(child.Name, out var value))
                {
                    PlanValue(value, child);
                }
            }
        }

        public void PlanValue(JsonElement json, PropertyNode node, Type? copiedType = null)
        {
            if (node.PropertyType == typeof(LabURI))
            {
                PlanLink(json, node);
                return;
            }

            if (CopiedValues.IsLeafType(node.PropertyType))
            {
                PlanLeaf(json, node);
                return;
            }

            if (IsList(node))
            {
                if (json.ValueKind == JsonValueKind.Array)
                {
                    var elementType = CopiedValues.ElementTypeOf(node.GetValue()!.GetType());
                    PlanItems(json.EnumerateArray().Select(item => (item, CopiedValues.TypeOf(item, elementType))).ToList(), node, 0);
                }

                return;
            }

            if (CopiedValues.IsComputed(node))
            {
                PlanComputed(json, node);
                return;
            }

            PlanObject(json, node, copiedType);
        }

        /// <summary>
        /// The items go over the list's from the start on, the ones left over after its last item are added, as many as it takes
        /// </summary>
        public void PlanItems(List<(JsonElement Json, Type Type)> items, PropertyNode listNode, int start)
        {
            var list = (IList)listNode.GetValue()!;
            var count = list.Count;
            var max = listNode.MaxElements;
            var added = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var index = start + i;
                if (index < count)
                {
                    if (listNode.FindChild($"[{index}]") is { } element)
                    {
                        PlanElement(items[i].Json, items[i].Type, element);
                    }

                    continue;
                }

                var left = items.Count - i;
                if (list.IsFixedSize)
                {
                    Limits.Add($"{UndoHistory.NameOf(listNode)} always has {Count(count, "item")}, {Count(left, "copied item")} after its last aren't pasted");
                    break;
                }

                if (max is { } most && count + added >= most)
                {
                    Limits.Add($"{UndoHistory.NameOf(listNode)} takes {most} items at most, {Count(left, "copied item")} aren't pasted");
                    break;
                }

                if (Build(items[i].Json, items[i].Type, listNode.Metadata?.ForParts(), $"{UndoHistory.NameOf(listNode)} [{index}]", DocumentViewModel.GetOwningAsset(listNode)) is not { } value)
                {
                    continue;
                }

                added++;
                Steps.Add(() => listNode.InsertElement(((IList)listNode.GetValue()!).Count, value));
            }
        }

        private void PlanElement(JsonElement json, Type type, PropertyNode element)
        {
            var current = element.GetValue();
            if (element.PropertyType == typeof(LabURI) || CopiedValues.IsLeafType(element.PropertyType) || CopiedValues.IsLeafType(type))
            {
                PlanValue(json, element, type);
                return;
            }

            if (current == null || current.GetType() != type || type.IsValueType)
            {
                if (current != null && current.GetType() != type)
                {
                    Overwrites.Add($"{UndoHistory.NameOf(element)}: {Friendly(current.GetType())} becomes {Friendly(type)}");
                }

                var value = Build(json, type, element.Metadata, UndoHistory.NameOf(element), DocumentViewModel.GetOwningAsset(element));
                if (value != null)
                {
                    Steps.Add(() => element.SetValue(value));
                }

                return;
            }

            PlanValue(json, element, type);
        }

        private void PlanLeaf(JsonElement json, PropertyNode node)
        {
            if (!CopiedValues.TryReadLeaf(json, node.PropertyType, out var value))
            {
                Limits.Add($"{UndoHistory.NameOf(node)} isn't pasted, the copied value isn't one of its type");
                return;
            }

            value = Constrain(value, node.Metadata, node.Target, UndoHistory.NameOf(node), DocumentViewModel.GetOwningAsset(node));
            Steps.Add(() => node.SetValue(value));
        }

        private void PlanLink(JsonElement json, PropertyNode node)
        {
            var name = UndoHistory.NameOf(node);
            if (!CopiedValues.TryReadLink(json, out var uri))
            {
                Limits.Add($"{name} isn't pasted, the copied value isn't a link");
                return;
            }

            if (WhyNotLink(uri, node.Metadata, node) is { } why)
            {
                Limits.Add($"{name} {why}, it keeps its link");
                return;
            }

            Steps.Add(() => node.SetValue(uri));
        }

        private void PlanComputed(JsonElement json, PropertyNode node)
        {
            if (json.ValueKind != JsonValueKind.Object || node.GetValue() is not { } whole)
            {
                return;
            }

            SetComputedParts(json, node, whole);
            Steps.Add(() => node.SetValue(whole));
        }

        // The getter's value is a copy already, its parts' values too (the bounds' middle and half size)
        private static void SetComputedParts(JsonElement json, PropertyNode node, object whole)
        {
            foreach (var part in node.Children)
            {
                if (part.Metadata == null || !json.TryGetProperty(part.Name, out var partJson))
                {
                    continue;
                }

                if (CopiedValues.TryReadLeaf(partJson, part.PropertyType, out var partValue))
                {
                    part.Metadata.PropertyInfo.SetValue(whole, partValue);
                }
                else if (partJson.ValueKind == JsonValueKind.Object && part.Metadata.PropertyInfo.GetValue(whole) is { } partWhole)
                {
                    SetComputedParts(partJson, part, partWhole);
                    part.Metadata.PropertyInfo.SetValue(whole, partWhole);
                }
            }
        }

        private void PlanObject(JsonElement json, PropertyNode node, Type? copiedType)
        {
            var current = node.GetValue();
            var name = UndoHistory.NameOf(node);
            if (json.ValueKind == JsonValueKind.Null)
            {
                if (current != null)
                {
                    Overwrites.Add($"{name}: {Friendly(current.GetType())} becomes nothing");
                    Steps.Add(() => node.SetValue(null));
                }

                return;
            }

            if (json.ValueKind != JsonValueKind.Object)
            {
                Limits.Add($"{name} isn't pasted, the copied value isn't one of its type");
                return;
            }

            var type = json.TryGetProperty(CopiedValues.TypeKey, out _) ? CopiedValues.TypeOf(json, node.PropertyType) : copiedType ?? CopiedValues.TypeOf(json, node.PropertyType);
            if (!node.PropertyType.IsAssignableFrom(type) && current?.GetType() != type)
            {
                Limits.Add($"{name} isn't pasted, a {Friendly(type)} doesn't go there");
                return;
            }

            // Another kind of value has other values in it, a struct's parts are set as a whole
            if (current == null || current.GetType() != type || type.IsValueType)
            {
                if (current == null || current.GetType() != type)
                {
                    Overwrites.Add($"{name}: {(current == null ? "nothing" : Friendly(current.GetType()))} becomes {Friendly(type)}");
                }

                var value = Build(json, type, node.Metadata, name, DocumentViewModel.GetOwningAsset(node));
                if (value != null)
                {
                    Steps.Add(() => node.SetValue(value));
                }

                return;
            }

            var typeChanges = _typeChanges;
            if (current is GameObjectData gameObject)
            {
                _typeChanges = json.TryGetProperty(nameof(GameObjectData.Type), out var pastedType) && CopiedValues.TryReadLeaf(pastedType, typeof(ITwinObject.ObjectType), out var objectType)
                               && !Equals(objectType, gameObject.Type);
            }

            foreach (var child in node.Children)
            {
                if (CopiedValues.IsValue(child) && json.TryGetProperty(child.Name, out var childJson))
                {
                    PlanValue(childJson, child);
                }
            }

            _typeChanges = typeChanges;
        }

        // A value made of the JSON outside the graph, to go in as a whole: an item added to a list, a value of another kind
        private object? Build(JsonElement json, Type type, PropertyMetadata? metadata, string name, IAsset? owner)
        {
            if (json.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(LabURI))
            {
                if (!CopiedValues.TryReadLink(json, out var uri))
                {
                    Limits.Add($"{name} isn't pasted, the copied value isn't a link");
                    return null;
                }

                if (WhyNotLink(uri, metadata, null, owner?.Package) is { } why)
                {
                    Limits.Add($"{name} {why}, it links nothing");
                    return LabURI.Empty;
                }

                return uri;
            }

            if (CopiedValues.IsLeafType(type))
            {
                if (!CopiedValues.TryReadLeaf(json, type, out var leaf))
                {
                    Limits.Add($"{name} isn't pasted, the copied value isn't one of its type");
                    return null;
                }

                return Constrain(leaf, metadata, null, name, owner);
            }

            if (CopiedValues.IsListType(type))
            {
                return BuildList(json, type, metadata, name, owner);
            }

            if (json.ValueKind != JsonValueKind.Object)
            {
                Limits.Add($"{name} isn't pasted, the copied value isn't one of its type");
                return null;
            }

            type = CopiedValues.TypeOf(json, type);
            object instance;
            try
            {
                instance = (metadata?.TypeConstructors.TryGetValue(type, out var make) == true ? make() : null)
                           ?? DocumentMetadata.FactoryOf(type)?.Invoke()
                           ?? Activator.CreateInstance(type, true)!;
            }
            catch (Exception exception) when (exception is MissingMethodException or MemberAccessException or ArgumentException or NotSupportedException)
            {
                Limits.Add($"{name} isn't pasted, TT Lab can't make a {Friendly(type)}");
                return null;
            }

            foreach (var property in DocumentMetadataCache.Get(type).Properties)
            {
                var info = property.PropertyInfo;
                if (!json.TryGetProperty(info.Name, out var propertyJson))
                {
                    continue;
                }

                var value = Build(propertyJson, info.PropertyType, property, $"{name} › {info.Name}", owner);
                if (value == null && propertyJson.ValueKind != JsonValueKind.Null)
                {
                    continue;
                }

                if (info.CanWrite)
                {
                    info.SetValue(instance, value);
                }
                else if (info.GetValue(instance) is IList { IsFixedSize: false } kept && value is IList items)
                {
                    kept.Clear();
                    foreach (var item in items)
                    {
                        kept.Add(item);
                    }
                }
            }

            if (instance is ParticleSystem system && owner != null)
            {
                system.Name = UniqueName(system.Name, owner, system, name);
            }

            return instance;
        }

        private IList? BuildList(JsonElement json, Type type, PropertyMetadata? metadata, string name, IAsset? owner)
        {
            if (json.ValueKind != JsonValueKind.Array)
            {
                Limits.Add($"{name} isn't pasted, the copied value isn't a list");
                return null;
            }

            var elementType = CopiedValues.ElementTypeOf(type);
            var items = json.EnumerateArray().ToList();
            if (metadata?.EditorParams.TryGetValue(DocumentCollectionViewModel.MaxCount, out var max) == true && Convert.ToInt32(max, CultureInfo.InvariantCulture) is var most && items.Count > most)
            {
                Limits.Add($"{name} takes {most} items at most, {Count(items.Count - most, "copied item")} aren't pasted");
                items = items.Take(most).ToList();
            }

            var built = items.Select((item, index) => Build(item, CopiedValues.TypeOf(item, elementType), metadata?.ForParts(), $"{name} [{index}]", owner)).ToList();
            if (type.IsArray)
            {
                var array = Array.CreateInstance(elementType, built.Count);
                for (var index = 0; index < built.Count; index++)
                {
                    array.SetValue(built[index], index);
                }

                return array;
            }

            if (Activator.CreateInstance(type) is not IList list)
            {
                return null;
            }

            foreach (var item in built.Where(item => item != null))
            {
                list.Add(item);
            }

            return list;
        }

        // A field's rules: the range of numbers and length of text it takes, a sub type its object's type can have (unless the paste
        // gives the object another type too), a particle system name no other system has
        private object? Constrain(object? value, PropertyMetadata? metadata, object? owner, string name, IAsset? asset)
        {
            var parameters = metadata?.EditorParams;
            if (value is IConvertible and not (string or bool or char or Enum) && parameters?.TryGetValue(TextFieldViewModel.TextFieldNumberRange, out var range) == true && range is IEnumerable bounds)
            {
                var limits = bounds.Cast<object>().Select(bound => Convert.ToDouble(bound, CultureInfo.InvariantCulture)).ToArray();
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (limits.Length == 2 && (number < limits[0] || number > limits[1]))
                {
                    var clamped = Convert.ChangeType(Math.Clamp(number, limits[0], limits[1]), value.GetType(), CultureInfo.InvariantCulture);
                    Limits.Add($"{name} takes {limits[0]} to {limits[1]}, the copied {value} becomes {clamped}");
                    value = clamped;
                }
            }

            if (value is string pasted && parameters?.TryGetValue(TextFieldViewModel.TextFieldAsciiOnly, out var asciiOnly) == true && asciiOnly is true
                && !NameRules.IsAscii(pasted))
            {
                value = NameRules.ToAscii(pasted);
                Limits.Add($"{name} only takes plain ASCII, the copied \"{pasted}\" becomes \"{value}\"");
            }

            if (value is string text && parameters?.TryGetValue(TextFieldViewModel.TextFieldStringLength, out var length) == true
                && Convert.ToInt32(length, CultureInfo.InvariantCulture) is var longest && text.Length > longest)
            {
                value = text[..longest];
                Limits.Add($"{name} takes {longest} characters at most, the copied \"{text}\" becomes \"{value}\"");
            }

            switch (owner)
            {
                case ParticleSystem system when metadata?.PropertyInfo.Name == nameof(ParticleSystem.Name) && value is string systemName && asset != null:
                    value = UniqueName(systemName, asset, system, name);
                    break;
                case GameObjectData gameObject when metadata?.PropertyInfo.Name == nameof(GameObjectData.SubType) && value is Byte subType && !_typeChanges
                                                   && !ObjectTypes.AllowsSubType(gameObject.Type, subType):
                    var allowed = ObjectTypes.DefaultSubTypeOf(gameObject.Type);
                    Limits.Add($"{name} {subType} ({ObjectTypes.FindSubType(gameObject.Type, subType).Name}) isn't one a {gameObject.Type} has, it becomes {allowed} ({ObjectTypes.FindSubType(gameObject.Type, allowed).Name})");
                    value = allowed;
                    break;
            }

            return value;
        }

        private string UniqueName(string systemName, IAsset asset, ParticleSystem system, string name)
        {
            var unique = ParticleSystemNames.MakeUnique(systemName, asset, system, _givenNames);
            if (unique != systemName)
            {
                Limits.Add($"{name}: another particle system is named {systemName}, the pasted one becomes {unique}");
            }

            _givenNames.Add(unique);
            return unique;
        }

        // A link field's rules: an asset the project has, not one the field never takes (its own chunk, one of a kind it leaves out), of its
        // own version for game objects, their instances and behaviours
        private static string? WhyNotLink(LabURI uri, PropertyMetadata? metadata, PropertyNode? node, LabURI? package = null)
        {
            if (uri == LabURI.Empty)
            {
                return null;
            }

            var assets = AssetManager.Get();
            if (!assets.DoesAssetExist(uri))
            {
                return "links an asset this project doesn't have";
            }

            var keeping = node != null ? UriLinkViewModel.PackageKeeping(node)?.URI : package;
            if (AssetVersions.WhyNotUsableBy(keeping, assets.GetAsset(uri)) is { } otherVersion)
            {
                return $"can't link it ({otherVersion})";
            }

            var parameters = metadata?.EditorParams;
            if (node != null && parameters?.TryGetValue(UriLinkViewModel.BrowseExcludeOwnerChunk, out var excludesOwner) == true && excludesOwner is true
                && UriLinkViewModel.OwnerChunkOf(node) == uri)
            {
                return "can't link its own chunk";
            }

            if (parameters?.TryGetValue(UriLinkViewModel.BrowseExcludeWhen, out var condition) == true && condition is string property
                && UriLinkViewModel.IsExcludedFromBrowsing(uri, property))
            {
                return $"can't link {assets.GetAsset(uri).Alias}";
            }

            return null;
        }
    }
}
