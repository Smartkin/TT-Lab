using Caliburn.Micro;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.Instance.Particles;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace TT_Lab.ViewModels.Editors.Instance;

public class ParticlesViewModel : InstanceSectionResourceEditorViewModel
{
    public ParticlesViewModel()
    {
        DirtyTracker.AddBindableCollection(ParticleTypes);
        DirtyTracker.AddBindableCollection(ParticleInstances);
    }
        
    protected override void Save()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<ParticleData>();
        data.ParticleSystems.Clear();
        foreach (var type in ParticleTypes)
        {
            var t = new TwinParticleSystem();
            type.Save(t);
            data.ParticleSystems.Add(t);
        }
        data.ParticleEmitters.Clear();
        foreach (var inst in ParticleInstances)
        {
            var i = new TwinParticleEmitter();
            inst.Save(i);
            data.ParticleEmitters.Add(i);
        }
            
        base.Save();
    }

    public override void LoadData()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<ParticleData>();
        
        ParticleTypes = [];
        foreach (var type in data.ParticleSystems)
        {
            ParticleTypes.Add(new ParticleTypeViewModel(type));
        }
        
        ParticleInstances = [];
        foreach (var inst in data.ParticleEmitters)
        {
            ParticleInstances.Add(new ParticleInstanceViewModel(inst));
        }
        
        DirtyTracker.AddBindableCollection(ParticleTypes);
        DirtyTracker.AddBindableCollection(ParticleInstances);
    }

    public BindableCollection<ParticleTypeViewModel> ParticleTypes
    {
        get;
        private set;
    } = [];

    public BindableCollection<ParticleInstanceViewModel> ParticleInstances
    {
        get;
        private set;
    } = [];
}