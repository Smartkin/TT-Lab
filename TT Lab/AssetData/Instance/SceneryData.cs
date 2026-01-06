using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetData.Instance
{
    [ReferencesAssets]
    public class SceneryData : AbstractAssetData
    {
        private static readonly Dictionary<ITwinScenery.SceneryType, Type> ScIndexToType = new();

        static SceneryData()
        {
            ScIndexToType.Add(ITwinScenery.SceneryType.Root, typeof(SceneryRootData));
            ScIndexToType.Add(ITwinScenery.SceneryType.Leaf, typeof(SceneryLeafData));
            ScIndexToType.Add(ITwinScenery.SceneryType.Node, typeof(SceneryNodeData));
        }

        public SceneryData(IAsset asset) : base(asset)
        {
            ChunkPath = LabURI.Empty;
            SkydomeID = LabURI.Empty;
            HasLighting = false;
            AmbientLights = new List<AmbientLight>();
            DirectionalLights = new List<DirectionalLight>();
            PointLights = new List<PointLight>();
            NegativeLights = new List<NegativeLight>();
            Sceneries = new List<SceneryBaseData>();
        }

        public SceneryData(IAsset asset, ITwinScenery scenery) : this(asset)
        {
            SetTwinItem(scenery);
        }

        [JsonProperty(Required = Required.Always)]
        public LabURI ChunkPath { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt32 FogColor { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Byte UnkByte { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI SkydomeID { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Boolean HasLighting { get; set; }
        [JsonProperty(Required = Required.AllowNull)]
        public List<AmbientLight> AmbientLights { get; set; }
        [JsonProperty(Required = Required.AllowNull)]
        public List<DirectionalLight> DirectionalLights { get; set; }
        [JsonProperty(Required = Required.AllowNull)]
        public List<PointLight> PointLights { get; set; }
        [JsonProperty(Required = Required.AllowNull)]
        public List<NegativeLight> NegativeLights { get; set; }
        [JsonProperty(Required = Required.AllowNull)]
        public List<SceneryBaseData> Sceneries { get; set; }

        protected override void Dispose(Boolean disposing)
        {
            AmbientLights.Clear();
            DirectionalLights.Clear();
            PointLights.Clear();
            NegativeLights.Clear();
            Sceneries.Clear();
        }

        private void ExportGltf(string path)
        {
            var scene = new SharpGLTF.Scenes.SceneBuilder($"TwinsanityScenery_{Owner.Name}");
            var root = new SharpGLTF.Scenes.NodeBuilder("scenery_root");
            scene.AddNode(root);

            // TODO: Figure out lighting
            if (HasLighting)
            {
                foreach (var ambientLight in AmbientLights)
                {
                }

                foreach (var directionalLight in DirectionalLights)
                {
                }

                foreach (var pointLight in PointLights)
                {
                }

                foreach (var negativeLight in NegativeLights)
                {
                }
            }

            var sceneryRoot = (SceneryRootData)Sceneries[0];
            var sceneryList = Sceneries.Skip(1).ToList();
            BuildSceneryRenderTreeForNode(scene, root, sceneryRoot, ref sceneryList);
            ExportSceneryNodesToGltf(scene, root, sceneryRoot);
            
            var resultModel = scene.ToGltf2();
            resultModel.SaveGLB(path);
        }
        
        private void BuildSceneryRenderTreeForNode(SharpGLTF.Scenes.SceneBuilder scene, SharpGLTF.Scenes.NodeBuilder parentNode, SceneryNodeData sceneryNode, ref List<SceneryBaseData> sceneryTree)
        {
            foreach (var sceneryType in sceneryNode.SceneryTypes)
            {
                if (sceneryType == ITwinScenery.SceneryType.Node)
                {
                    var childNode = parentNode.CreateNode();
                    var data = (SceneryNodeData)sceneryTree[0];
                    sceneryTree = sceneryTree.Skip(1).ToList();
                    ExportSceneryNodesToGltf(scene, childNode, data);
                    BuildSceneryRenderTreeForNode(scene, childNode, data, ref sceneryTree);
                }
                else if (sceneryType == ITwinScenery.SceneryType.Leaf)
                {
                    var data = sceneryTree[0];
                    sceneryTree = sceneryTree.Skip(1).ToList();
                    ExportSceneryNodesToGltf(scene, parentNode, data);
                }
            }
        }

        private void ExportSceneryNodesToGltf(SharpGLTF.Scenes.SceneBuilder scene, SharpGLTF.Scenes.NodeBuilder parentNode, SceneryBaseData sceneryData)
        {
            var assetManager = AssetManager.Get();
            var index = 0;
            foreach (var meshId in sceneryData.MeshIDs)
            {
                var meshData = assetManager.GetAssetData<MeshData>(meshId);
                var model = assetManager.GetAssetData<ModelData>(meshData.Model);
                var meshMatrix = sceneryData.MeshModelMatrices[index];
                var meshNode = parentNode.CreateNode();
                meshNode.LocalMatrix = meshMatrix.ToSystem();
                var meshes = model.GetMeshes(parentNode, meshData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
                foreach (var mesh in meshes)
                {
                    scene.AddRigidMesh(mesh.Mesh, meshNode);
                }

                index++;
            }

            index = 0;
            foreach (var lodId in sceneryData.LodIDs)
            {
                var lodData = assetManager.GetAssetData<LodModelData>(lodId);
                var lodNode = parentNode.CreateNode();
                var lodMatrix = sceneryData.LodModelMatrices[index];
                lodNode.LocalMatrix = lodMatrix.ToSystem();
                foreach (var meshId in lodData.Meshes)
                {
                    var meshData = assetManager.GetAssetData<MeshData>(meshId);
                    var model = assetManager.GetAssetData<ModelData>(meshData.Model);
                    var meshes = model.GetMeshes(parentNode, meshData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
                    foreach (var mesh in meshes)
                    {
                        scene.AddRigidMesh(mesh.Mesh, lodNode);
                    }
                }

                index++;
            }
        }

        protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            };
            base.SaveInternal(dataPath, settings);
            
            ExportGltf(dataPath + ".glb");
        }

        protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            };
            base.LoadInternal(dataPath, settings);
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            var assetManager = AssetManager.Get();
            ITwinScenery scenery = GetTwinItem<ITwinScenery>();
            ChunkPath = assetManager.GetAllAssetsOf<LevelChunk>().First(c => c.GetChunkPath().Equals(scenery.Name.Replace('\\', Path.DirectorySeparatorChar), StringComparison.InvariantCultureIgnoreCase)).URI;
            FogColor = scenery.FogColor;
            UnkByte = scenery.UnkByte;
            if (scenery.SkydomeID != 0)
            {
                SkydomeID = AssetManager.Get().GetUriByTwinId<Skydome>(Owner, scenery.SkydomeID);
            }
            HasLighting = scenery.HasLighting;
            if (HasLighting)
            {
                AmbientLights = CloneUtils.DeepClone(scenery.AmbientLights);
                DirectionalLights = CloneUtils.DeepClone(scenery.DirectionalLights);
                PointLights = CloneUtils.DeepClone(scenery.PointLights);
                NegativeLights = CloneUtils.DeepClone(scenery.NegativeLights);
            }
            Sceneries = new List<SceneryBaseData>();
            foreach (var sc in scenery.Sceneries)
            {
                Sceneries.Add((SceneryBaseData)Activator.CreateInstance(ScIndexToType[sc.GetObjectIndex()], Owner, sc)!);
            }
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            var assetManager = AssetManager.Get();
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(ChunkPath == LabURI.Empty ? string.Empty : assetManager.GetAsset<LevelChunk>(ChunkPath).GetChunkPath().Replace(Path.DirectorySeparatorChar, '\\'));
            writer.Write(FogColor);
            writer.Write(UnkByte);
            writer.Write(SkydomeID == LabURI.Empty ? 0 : assetManager.GetAsset(SkydomeID).ID);
            writer.Write(HasLighting);
            if (HasLighting)
            {
                writer.Write(AmbientLights.Count);
                foreach (var ambient in AmbientLights)
                {
                    ambient.Write(writer);
                }

                writer.Write(DirectionalLights.Count);
                foreach (var directional in DirectionalLights)
                {
                    directional.Write(writer);
                }

                writer.Write(PointLights.Count);
                foreach (var point in PointLights)
                {
                    point.Write(writer);
                }

                writer.Write(NegativeLights.Count);
                foreach (var negative in NegativeLights)
                {
                    negative.Write(writer);
                }
            }
            writer.Write(Sceneries.Count);
            foreach (var scenery in Sceneries)
            {
                writer.Write((Int32)scenery.GetSceneryType());
                scenery.Write(writer);
            }

            writer.Flush();
            ms.Position = 0;
            return factory.GenerateScenery(ms);
        }

        public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
        {
            var assetManager = AssetManager.Get();
            var graphicsSection = section.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
            var skydomeSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_SKYDOMES_SECTION);
            if (SkydomeID != LabURI.Empty)
            {
                assetManager.GetAsset(SkydomeID).ResolveChunkResources(factory, skydomeSection);
            }

            foreach (var scenery in Sceneries)
            {
                scenery.ResolveChunkResouces(factory, graphicsSection);
            }

            return base.ResolveChunkResources(factory, section, id, layoutID);
        }
    }
}
