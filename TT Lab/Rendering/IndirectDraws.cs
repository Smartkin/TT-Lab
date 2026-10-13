using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Passes;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering;

/// <summary>
/// The batches of the passes drawn with glMultiDrawArraysIndirect: a draw call for every batch with instances cost more than the rest of the
/// frame. Batches that follow each other in a pass and whose materials set the same GL state are one call, every draw of it reading its
/// material entry and its texture's bindless handle at DrawBase + gl_DrawID (<c>MainPass.vert</c>) from a buffer made once a frame. The
/// draws keep the passes' order. What can't be drawn so (the camera, billboards, skins, previews, models with a vertex array of their own,
/// materials without a texture) is drawn the way it always was, in its place. Only with bindless textures (<see cref="BindlessTextures"/>):
/// without them every texture would end a call, and the draws hardly ever share one in a row
/// </summary>
public sealed class IndirectDraws(RenderContext context) : IDisposable
{
    private const uint EntriesBinding = 7;
    private static readonly (int, int, int, int) NotMade = (-1, -1, -1, -1);

    // DrawArraysIndirectCommand
    [StructLayout(LayoutKind.Sequential)]
    private struct Command
    {
        public uint Count;
        public uint InstanceCount;
        public uint First;
        public uint BaseInstance;
    }

    // DrawEntry of TwinMaterial.glsl
    [StructLayout(LayoutKind.Sequential)]
    private struct DrawEntry
    {
        public int Material;
        public int Unused;
        public ulong Texture;
    }

    // Draws of the same GL state in one call (its run), or an entry of the pass drawn on its own
    private readonly record struct Step(PassEntry Entry, TwinMaterial? Material, int Run);

    // The draws of a call and how many of them have instances this frame
    private struct Run
    {
        public int First;
        public int Count;
        public int Drawn;
    }

    private readonly Dictionary<RenderPass, List<Step>> _steps = [];
    private Command[] _commands = new Command[1024];
    private DrawEntry[] _entries = new DrawEntry[1024];
    private RenderBatch[] _commandBatches = new RenderBatch[1024];
    private TextureBuffer?[] _commandTextures = new TextureBuffer?[1024];
    private int[] _commandRuns = new int[1024];
    private Run[] _runs = new Run[64];
    private int _count;
    private int _runCount;
    private uint _commandBuffer;
    private uint _entryBuffer;
    // What the calls were made from, they're made again when any of it changed
    private (int Passes, int Materials, int Textures, int Batches) _madeFrom = NotMade;

    /// <summary>
    /// The calls of the frame's passes, after the batches gathered their instances and before the first pass: made again when what's in
    /// the passes changed, otherwise only the draws of the batches whose instances were written this frame change (a batch without
    /// instances this frame is a draw of none)
    /// </summary>
    public void Build(PassService passes, IReadOnlyList<RenderBatch> written)
    {
        var madeFrom = (passes.Version, context.Materials.Generation, TextureBuffer.HandlesReleased, RenderBatch.DrawnAloneVersion);
        var made = madeFrom != _madeFrom;
        if (made)
        {
            // Only made once they're uploaded: a frame failing on the way leaves them half made, the next one makes them again
            _madeFrom = NotMade;
            Make(passes);
            for (var command = 0; command < _count; command++)
            {
                Refresh(command);
            }
        }
        else
        {
            foreach (var batch in written)
            {
                foreach (var command in batch.Commands)
                {
                    Refresh(command);
                }
            }
        }

        Upload(made);
        if (made)
        {
            _madeFrom = madeFrom;
        }
    }

    private void Refresh(int command)
    {
        var batch = _commandBatches[command];
        var instances = (uint)(batch.DrawsIndirectly ? batch.InstanceCount : 0);
        ref var draw = ref _commands[command];
        if ((draw.InstanceCount > 0) != (instances > 0))
        {
            _runs[_commandRuns[command]].Drawn += instances > 0 ? 1 : -1;
        }

        draw.InstanceCount = instances;
        draw.BaseInstance = (uint)batch.BaseInstance;
    }

    // The texture the last draw of the run with instances draws with, which it leaves on unit 0
    private TextureBuffer? LastTexture(in Run run)
    {
        for (var command = run.First + run.Count - 1; command >= run.First; command--)
        {
            if (_commands[command].InstanceCount > 0)
            {
                return _commandTextures[command];
            }
        }

        return null;
    }

    private void Make(PassService passes)
    {
        for (var command = 0; command < _count; command++)
        {
            _commandBatches[command].Commands.Clear();
        }

        _count = 0;
        _runCount = 0;
        foreach (var pass in passes.DrawnPasses)
        {
            if (!_steps.TryGetValue(pass, out var steps))
            {
                steps = [];
                _steps[pass] = steps;
            }

            Make(passes.GetEntries(pass), steps);
        }
    }

    private void Make(IReadOnlyList<PassEntry> entries, List<Step> steps)
    {
        steps.Clear();
        var states = new List<TwinMaterial>();
        var runState = -1;
        foreach (var entry in entries)
        {
            // A material taking a copy of the frame first draws on its own
            if (!entry.CopiesScreen && entry.Shader >= 0 && entry.Renderable is RenderBatch { CanDrawIndirectly: true, DrawsAlone: false } batch
                && RunMaterialOf(batch, entry.Shader) is { } material && material.TextureHandle is var handle and not 0)
            {
                var state = states.FindIndex(other => other.DrawsLike(material));
                if (state < 0)
                {
                    state = states.Count;
                    states.Add(material);
                }

                if (state != runState)
                {
                    runState = state;
                    AddRun();
                    steps.Add(new Step(default, material, _runCount - 1));
                }

                Add(batch, material, handle);
                _runs[_runCount - 1].Count++;
                continue;
            }

            runState = -1;
            steps.Add(new Step(entry, null, -1));
        }
    }

    // The material a batch draws with in the pass when it's one a run can draw with: textured, its fragments failing the alpha test drawn
    // with the passing ones (a material drawing them apart takes two draws per instance). The batches of the passes are made into calls
    // whether they have instances or not, and a material can fail to be made (its texture gone from the project or unreadable): the batch
    // then draws on its own, which only makes its material once it has instances to draw, as every batch did before the calls
    private static TwinMaterial? RunMaterialOf(RenderBatch batch, int shader)
    {
        try
        {
            return batch.Buffer.MaterialOf(shader) is { HasTexture: true, FailsApart: false } material ? material : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void AddRun()
    {
        if (_runCount == _runs.Length)
        {
            Array.Resize(ref _runs, _runs.Length * 2);
        }

        _runs[_runCount++] = new Run { First = _count };
    }

    private void Add(RenderBatch batch, TwinMaterial material, ulong handle)
    {
        if (_count == _commands.Length)
        {
            Array.Resize(ref _commands, _commands.Length * 2);
            Array.Resize(ref _entries, _entries.Length * 2);
            Array.Resize(ref _commandBatches, _commandBatches.Length * 2);
            Array.Resize(ref _commandTextures, _commandTextures.Length * 2);
            Array.Resize(ref _commandRuns, _commandRuns.Length * 2);
        }

        _commands[_count] = new Command { Count = batch.Buffer.IndexCount, First = (uint)batch.Buffer.FirstVertex };
        _entries[_count] = new DrawEntry { Material = material.Entry, Texture = handle };
        _commandBatches[_count] = batch;
        _commandTextures[_count] = material.Texture;
        _commandRuns[_count] = _runCount - 1;
        batch.Commands.Add(_count);
        _count++;
    }

    private unsafe void Upload(bool made)
    {
        var gl = context.Gl;
        if (_commandBuffer == 0)
        {
            _commandBuffer = gl.CreateBuffer();
            _entryBuffer = gl.CreateBuffer();
        }

        // A storage of its own every frame: the frame before may still be drawing from the old one. The entries only change with the calls
        var count = Math.Max(_count, 1);
        fixed (Command* commands = _commands)
        {
            gl.NamedBufferData(_commandBuffer, (nuint)(count * sizeof(Command)), commands, VertexBufferObjectUsage.StreamDraw);
        }

        if (made)
        {
            fixed (DrawEntry* entries = _entries)
            {
                gl.NamedBufferData(_entryBuffer, (nuint)(count * sizeof(DrawEntry)), entries, VertexBufferObjectUsage.StaticDraw);
            }
        }

        gl.BindBuffer(BufferTargetARB.DrawIndirectBuffer, _commandBuffer);
        gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, EntriesBinding, _entryBuffer);
    }

    /// <summary>
    /// Draws what the pass has: the calls of its batches and the rest in between, in the pass's order
    /// </summary>
    public unsafe void Draw(RenderPass pass, float delta)
    {
        if (!_steps.TryGetValue(pass, out var steps))
        {
            return;
        }

        var gl = context.Gl;
        var state = context.State;
        var program = pass.Program;
        foreach (var step in steps)
        {
            if (step.Material is { } material)
            {
                var run = _runs[step.Run];
                if (run.Drawn == 0)
                {
                    continue;
                }

                material.ApplyState();
                state.BindVertexArray(context.VertexPool.VertexArray);
                program.SetDrawBase(run.First);
                gl.MultiDrawArraysIndirect(PrimitiveType.Triangles, (void*)(run.First * sizeof(Command)), (uint)run.Count, 0);
                // The last of the draws would have left its texture on unit 0, what a material without one draws with, and unit 0 active
                state.SetPendingTexture0(LastTexture(run)?.Handler ?? 0);
                state.SetActiveTexture(0);
                continue;
            }

            if (step.Entry.CopiesScreen)
            {
                context.ScreenCopy.Take();
            }

            // Whatever it draws, a material without a texture reads unit 0
            program.SetDrawBase(-1);
            state.SettleTexture0();
            step.Entry.Draw(delta);
        }
    }

    public void Dispose()
    {
        if (_commandBuffer == 0)
        {
            return;
        }

        context.Gl.DeleteBuffer(_commandBuffer);
        context.Gl.DeleteBuffer(_entryBuffer);
        _commandBuffer = 0;
        _entryBuffer = 0;
    }
}
