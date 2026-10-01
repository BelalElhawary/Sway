using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.Logging;
using Sway.Core.Dom;

namespace Sway.Core.Rendering;

/// <summary>
/// Applies Blazor render batches to the <see cref="Document"/> node tree.
/// The edit-application algorithm mirrors the one in Blazor WebAssembly's BrowserRenderer.
/// </summary>
public sealed class SkiaRenderer : Renderer
{
    readonly Document _document;
    readonly ILogger _logger;
    readonly Dictionary<int, ContainerNode> _containers = new();

    public SkiaRenderer(Document document, IServiceProvider services, ILoggerFactory loggerFactory)
        : base(services, loggerFactory)
    {
        _document = document;
        _logger = loggerFactory.CreateLogger<SkiaRenderer>();
    }

    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    public async Task AddRootComponentAsync<TComponent>() where TComponent : IComponent
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            var component = InstantiateComponent(typeof(TComponent));
            int id = AssignRootComponentId(component);

            lock (_document.SyncRoot)
            {
                var container = new ComponentNode(_document);
                _document.Root.Append(container);
                _containers[id] = container;
            }

            await RenderRootComponentAsync(id, ParameterView.Empty);
        });
    }

    public Task DispatchAsync(ulong handlerId, EventArgs args) =>
        Dispatcher.InvokeAsync(() => DispatchEventAsync(handlerId, null, args));

    protected override void HandleException(Exception exception) =>
        _logger.LogError(exception, "Unhandled exception in a component");

    protected override Task UpdateDisplayAsync(in RenderBatch batch)
    {
        lock (_document.SyncRoot)
        {
            var frames = batch.ReferenceFrames.Array;
            var updated = batch.UpdatedComponents;

            for (int i = 0; i < updated.Count; i++)
            {
                ref readonly var diff = ref updated.Array[i];
                if (!_containers.TryGetValue(diff.ComponentId, out var container))
                    throw new InvalidOperationException($"Component {diff.ComponentId} was updated before it was inserted.");
                ApplyEdits(container, diff.Edits, frames);
            }

            var disposed = batch.DisposedComponentIDs;
            for (int i = 0; i < disposed.Count; i++)
                _containers.Remove(disposed.Array[i]);
        }

        return Task.CompletedTask;
    }

    void ApplyEdits(ContainerNode root, ArrayBuilderSegment<RenderTreeEdit> edits, RenderTreeFrame[] frames)
    {
        var parent = root;
        List<(int from, int to)>? permutation = null;

        for (int i = 0; i < edits.Count; i++)
        {
            var edit = edits.Array[edits.Offset + i];
            int index = edit.SiblingIndex; // components always start at child 0, so no offset is needed

            switch (edit.Type)
            {
                case RenderTreeEditType.PrependFrame:
                {
                    int childIndex = index;
                    InsertFrame(parent, ref childIndex, frames, edit.ReferenceFrameIndex);
                    break;
                }
                case RenderTreeEditType.RemoveFrame:
                    parent.RemoveAt(index);
                    break;
                case RenderTreeEditType.SetAttribute:
                    ApplyAttribute((ElementNode)parent.Children[index], in frames[edit.ReferenceFrameIndex]);
                    break;
                case RenderTreeEditType.RemoveAttribute:
                    RemoveAttribute((ElementNode)parent.Children[index], edit.RemovedAttributeName!);
                    break;
                case RenderTreeEditType.UpdateText:
                    ((TextNode)parent.Children[index]).Text = frames[edit.ReferenceFrameIndex].TextContent ?? "";
                    break;
                case RenderTreeEditType.UpdateMarkup:
                {
                    var block = (ComponentNode)parent.Children[index];
                    while (block.Children.Count > 0) block.RemoveAt(block.Children.Count - 1);
                    HtmlFragment.ParseInto(block, frames[edit.ReferenceFrameIndex].MarkupContent ?? "");
                    break;
                }
                case RenderTreeEditType.StepIn:
                    parent = (ContainerNode)parent.Children[index];
                    break;
                case RenderTreeEditType.StepOut:
                    parent = parent.Parent ?? throw new InvalidOperationException("StepOut past the root.");
                    break;
                case RenderTreeEditType.PermutationListEntry:
                    (permutation ??= new()).Add((index, edit.MoveToSiblingIndex));
                    break;
                case RenderTreeEditType.PermutationListEnd:
                    Permute(parent, permutation!);
                    permutation = null;
                    break;
                default:
                    throw new NotSupportedException($"Unknown edit type {edit.Type}.");
            }
        }
    }

    /// <summary>Inserts a frame subtree at <paramref name="childIndex"/> and returns how many frames it consumed.</summary>
    int InsertFrame(ContainerNode parent, ref int childIndex, RenderTreeFrame[] frames, int frameIndex)
    {
        ref readonly var frame = ref frames[frameIndex];
        switch (frame.FrameType)
        {
            case RenderTreeFrameType.Element:
            {
                var element = new ElementNode(_document, frame.ElementName);
                parent.Insert(childIndex++, element);

                int end = frameIndex + frame.ElementSubtreeLength;
                int next = frameIndex + 1;
                int nested = 0;
                while (next < end)
                    next += InsertFrame(element, ref nested, frames, next);
                return frame.ElementSubtreeLength;
            }
            case RenderTreeFrameType.Text:
                parent.Insert(childIndex++, new TextNode(_document, frame.TextContent ?? ""));
                return 1;
            case RenderTreeFrameType.Attribute:
                ApplyAttribute((ElementNode)parent, in frame);
                return 1;
            case RenderTreeFrameType.Component:
            {
                var container = new ComponentNode(_document);
                parent.Insert(childIndex++, container);
                _containers[frame.ComponentId] = container;
                return frame.ComponentSubtreeLength;
            }
            case RenderTreeFrameType.Region:
            {
                int end = frameIndex + frame.RegionSubtreeLength;
                int next = frameIndex + 1;
                while (next < end)
                    next += InsertFrame(parent, ref childIndex, frames, next);
                return frame.RegionSubtreeLength;
            }
            case RenderTreeFrameType.Markup:
            {
                var block = new ComponentNode(_document);
                parent.Insert(childIndex++, block);
                HtmlFragment.ParseInto(block, frame.MarkupContent ?? "");
                return 1;
            }
            default:
                // ElementReferenceCapture, ComponentReferenceCapture, NamedEvent: no node.
                return 1;
        }
    }

    static void ApplyAttribute(ElementNode element, in RenderTreeFrame frame)
    {
        string name = frame.AttributeName;

        if (frame.AttributeEventHandlerId != 0)
        {
            if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                element.Handlers[name[2..]] = frame.AttributeEventHandlerId;
            return;
        }

        switch (frame.AttributeValue)
        {
            case null:
            case false:
                element.RemoveAttribute(name);
                break;
            case true:
                element.SetAttribute(name, "");
                break;
            default:
                element.SetAttribute(name, Convert.ToString(frame.AttributeValue, System.Globalization.CultureInfo.InvariantCulture) ?? "");
                break;
        }
    }

    static void RemoveAttribute(ElementNode element, string name)
    {
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase) && element.Handlers.Remove(name[2..])) return;
        element.RemoveAttribute(name);
    }

    static void Permute(ContainerNode parent, List<(int from, int to)> entries)
    {
        var order = parent.Children.ToArray();
        foreach (var (from, to) in entries)
            order[to] = parent.Children[from];
        parent.Reorder(order);
    }
}
