using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Rendering.Services;

public class MeshService(MeshFactory factory)
{
    private readonly Dictionary<LabURI, Mesh?> _meshes = [];

    public MeshInfo GetMesh(LabURI uri, bool createNewInstance = false)
    {
        if (_meshes.TryGetValue(uri, out var mesh))
        {
            var newMeshInstance = mesh?.Clone();
            return new MeshInfo(newMeshInstance);
        }
        
        mesh = factory.CreateMesh(uri);
        _meshes[uri] = mesh;
        return new MeshInfo(mesh);
    }

    // The meshes of the models are made again the next time they're asked for, their parts got other materials
    public void Forget(IEnumerable<LabURI> uris)
    {
        foreach (var uri in uris)
        {
            _meshes.Remove(uri);
        }
    }
}

public record MeshInfo(Mesh? Model);