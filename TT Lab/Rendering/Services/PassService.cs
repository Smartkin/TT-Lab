using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using org.ogre;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Rendering.Passes;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Services;

public enum PassPriority
{
    SkydomeOpaque = -10,
    SkydomeTransparent = -8,
    Opaque = 1,
    Transparent = 10000,
    Primitive = int.MaxValue
}

public class PassService
{
    public const string EVERY_PASS = "EveryPass";
    
    private readonly Dictionary<string, RenderPass> _passes = [];
    private readonly SortedList<PassPriority, RenderPass> _sortedPasses = new(new DuplicateKeyComparer<PassPriority>());
    private readonly Dictionary<string, SortedList<int, Renderable>> _passRenderables = [];

    public PassService(RenderContext context)
    {
        foreach (var passType in Enum.GetValues<TwinShader.Type>())
        {
            var passName = passType.ToString();
            var passPriorityOpaque = PassPriority.Opaque;
            var passPriorityTransparent = PassPriority.Transparent;
            switch (passType)
            {
                case TwinShader.Type.StandardUnlit:
                    break;
                case TwinShader.Type.StandardLit:
                    break;
                case TwinShader.Type.LitSkinnedModel:
                    break;
                case TwinShader.Type.UnlitSkydome:
                    passPriorityOpaque = PassPriority.SkydomeOpaque;
                    passPriorityTransparent = PassPriority.SkydomeTransparent;
                    break;
                case TwinShader.Type.ColorOnly:
                    break;
                case TwinShader.Type.LitEnvironmentMap:
                    break;
                case TwinShader.Type.UiShader:
                    break;
                case TwinShader.Type.LitMetallic:
                    break;
                case TwinShader.Type.LitReflectionSurface:
                    break;
                case TwinShader.Type.SHADER_17:
                    break;
                case TwinShader.Type.Particle:
                    break;
                case TwinShader.Type.Decal:
                    break;
                case TwinShader.Type.SHADER_20:
                    break;
                case TwinShader.Type.UnlitGlossy:
                    break;
                case TwinShader.Type.UnlitEnvironmentMap:
                    break;
                case TwinShader.Type.UnlitClothDeformation:
                    break;
                case TwinShader.Type.SHADER_25:
                    break;
                case TwinShader.Type.UnlitClothDeformation2:
                    break;
                case TwinShader.Type.UnlitBillboard:
                    break;
                case TwinShader.Type.SHADER_30:
                    break;
                case TwinShader.Type.SHADER_31:
                    break;
                case TwinShader.Type.SHADER_32:
                    break;
            }
            
            RenderPass renderPass = new GenericPass(context, passName, context.GetProgram("Generic"), passType);
            RegisterPass(passType.ToString(), renderPass, passPriorityOpaque);
            RenderPass renderPassTransparent = new GenericPass(context, passName + "Transparent", context.GetProgram("Generic"), passType);
            RegisterPass(passType + "Transparent", renderPassTransparent, passPriorityTransparent);
        }
    }

    public void RegisterRenderableInPasses(Renderable renderable, (string, int)[] passPriorities)
    {
        if (passPriorities.Length == 1)
        {
            var passPriority = passPriorities[0];
            if (passPriority.Item1 == EVERY_PASS)
            {
                foreach (var renderPass in _passes.Values)
                {
                    _passRenderables[renderPass.Name].Add(passPriority.Item2, renderable);
                }

                return;
            }
        }
        
        foreach (var passPriority in passPriorities)
        {
            _passRenderables[passPriority.Item1].Add(passPriority.Item2, renderable);
        }
    }

    public void UnregisterRenderableInPasses(Renderable renderable)
    {
        foreach (var passRenderablesValue in _passRenderables.Values)
        {
            var index = passRenderablesValue.IndexOfValue(renderable);
            if (index == -1)
            {
                continue;
            }
            
            passRenderablesValue.RemoveAt(index);
        }
    }

    public IList<RenderPass> GetSkydomeOpaquePasses()
    {
        return _sortedPasses.Where(kv => kv.Key == PassPriority.SkydomeOpaque).Select(kv => kv.Value).ToList();
    }

    public IList<RenderPass> GetSkydomeTransparentPasses()
    {
        return _sortedPasses.Where(kv => kv.Key == PassPriority.SkydomeTransparent).Select(kv => kv.Value).ToList();
    }

    public IList<RenderPass> GetTransparentPasses()
    {
        return _sortedPasses.Where(kv => kv.Key is >= PassPriority.Transparent and < PassPriority.Primitive).Select(kv => kv.Value).ToList();
    }

    public IList<RenderPass> GetPasses()
    {
        return _sortedPasses.Where(kv => kv.Key is > PassPriority.SkydomeTransparent and < PassPriority.Transparent).Select(kv => kv.Value).ToList();
    }

    public IList<RenderPass> GetPrimitivePasses()
    {
        return _sortedPasses.Where(kv => kv.Key == PassPriority.Primitive).Select(kv => kv.Value).ToList();
    }

    public RenderPass GetRenderPass(string name)
    {
        Debug.Assert(_passes.ContainsKey(name), $"Attempting to get unregistered pass {name}!");
        return _passes[name];
    }

    public IList<Renderable> GetRenderablesInPass(string passName)
    {
        return _passRenderables[passName].Values;
    }
    
    private void RegisterPass(string name, RenderPass renderPass, PassPriority priority)
    {
        Debug.Assert(!_passes.ContainsKey(name), $"Attempting to add existing pass {name}!");
        _passes.Add(name, renderPass);
        _sortedPasses.Add(priority, renderPass);
        _passRenderables.Add(name, new SortedList<Int32, Renderable>(new DuplicateKeyComparer<Int32>()));
    }
    
    
    // https://stackoverflow.com/questions/5716423/c-sharp-sortable-collection-which-allows-duplicate-keys
    private class DuplicateKeyComparer<TKey>
        :
            IComparer<TKey> where TKey : IComparable
    {
        #region IComparer<TKey> Members

        public int Compare(TKey? x, TKey? y)
        {
            var result = x?.CompareTo(y);

            if (result == 0)
                return -1; // Handle equality as being greater. Note: this will break Remove(key) or
            else          // IndexOfKey(key) since the comparer never returns 0 to signal key equality
                return result!.Value;
        }

        #endregion
    }
}