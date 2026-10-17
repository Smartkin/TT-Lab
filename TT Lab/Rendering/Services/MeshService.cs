using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Rendering.Services;

public class MeshService(MeshFactory factory)
{
    private readonly RenderCache<Mesh> _meshes = new();

    public MeshInfo GetMesh(LabURI uri, bool createNewInstance = false)
    {
        if (_meshes.TryGet(uri, out var mesh))
        {
            var newMeshInstance = mesh?.Clone();
            return new MeshInfo(newMeshInstance);
        }

        mesh = factory.CreateMesh(uri);
        _meshes.Add(uri, mesh);
        return new MeshInfo(mesh);
    }

    /// <summary>
    /// The models' meshes are made again the next time they're asked for (their parts got other materials, their data was replaced), the
    /// ones made before are released once nothing draws them (<see cref="ReleaseUndrawn"/>)
    /// </summary>
    public IReadOnlyList<Mesh> Forget(IEnumerable<LabURI> uris) => _meshes.Forget(uris);

    /// <summary>
    /// The models' meshes stay while something draws them and are forgotten and released once nothing does: the parts of data replaced
    /// whole, their URIs are of their content and the ones the new data still has are drawn again
    /// </summary>
    public void ForgetWhenUndrawn(IEnumerable<LabURI> uris) => _meshes.ForgetWhenUndrawn(uris);

    /// <summary>
    /// Goes up whenever more meshes are forgotten: the ones forgotten before only get undrawn when a mesh leaves the scene
    /// </summary>
    public int ForgottenVersion => _meshes.Version;

    /// <summary>
    /// Releases what the forgotten meshes nothing draws any more were drawn from and gives their models, whose batches go. On the render
    /// thread, between frames
    /// </summary>
    public IReadOnlyList<ModelBuffer> ReleaseUndrawn(Func<ModelBuffer, bool> isDrawn)
    {
        if (!_meshes.HasForgotten)
        {
            return [];
        }

        var released = new List<ModelBuffer>();
        foreach (var mesh in _meshes.TakeUndrawn(mesh => mesh.GetModels().Any(isDrawn)))
        {
            released.AddRange(mesh.GetModels());
            mesh.ReleaseBuffers();
        }

        return released;
    }
}

public record MeshInfo(Mesh? Model);
