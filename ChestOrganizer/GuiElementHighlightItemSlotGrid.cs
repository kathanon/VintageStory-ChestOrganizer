using Cairo;
using HarmonyLib;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace ChestOrganizer;
public class GuiElementHighlightItemSlotGrid : GuiElementItemSlotGrid {
    private const int Margin = 4;

    private readonly int[] boundaries;
    private readonly LoadedTexture[] textures;
    private readonly OrderedDictionary<int, ItemSlot> rendered;

    private int highlight = -1;

    public GuiElementHighlightItemSlotGrid(ICoreClientAPI capi,
                                           IInventory inventory,
                                           Action<object> SendPacketHandler,
                                           int cols,
                                           ElementBounds bounds,
                                           int[] boundaries) 
            : base(capi, inventory, SendPacketHandler, cols, null, bounds) {
        this.boundaries = boundaries;
        textures = boundaries.Select(_ => new LoadedTexture(capi)).ToArray();

        // Reflection stuff
        var handle = Traverse.Create(this);
        // Both isLastSlotGridInComposite and UpdateLastSlotGridFlag are inaccessible
        handle.Field<bool>("isLastSlotGridInComposite").Value = true;
        // renderedSlots & availableSlots are for some reason internal rather than protected
        rendered = handle.Field<OrderedDictionary<int, ItemSlot>>("renderedSlots").Value;
    }

    public void SetHighlight(int i) {
        highlight = i;
    }

    public override void ComposeElements(Context unusedCtx, ImageSurface unusedSurface) {
        base.ComposeElements(unusedCtx, unusedSurface);
        ComposeOutlines();
    }

    public void ComposeOutlines() {
        DisposeTextures();

        int width  = (int) Bounds.InnerWidth  + 2 * Margin;
        int height = (int) Bounds.InnerHeight + 2 * Margin;
        int start = 0;
        int nRendered = 0;
        for (int i = 0, n = boundaries.Length; i < n; i++) {
            int next = (i < n - 1) ? boundaries[i + 1] : inventory.Count;
            Icons.MakeTexture(ref textures[i], ctx => DrawOutline(ctx, start, next - 1, ref nRendered), width, height);
            start = next;
        }
    }

    private void DrawOutline(Context context, int start, int end, ref int nRendered) {
        int start2 = -1, end2 = -1;
        for (int i = start; i <= end; i++) {
            if (rendered.ContainsKey(i)) {
                if (start2 < 0 && i >= start) start2 = nRendered;
                end2 = nRendered;
                nRendered++;
            }
        }
        start = start2;
        end = end2;

        int n = SlotBounds.Length;
        if (start < 0 || end < 0 || start >= n || end >= n) return;
        var startBounds  = SlotBounds[start];
        var endBounds    = SlotBounds[end];
        var rowEndBounds = SlotBounds[(end < cols) ? end : cols - 1];
        double x1 = scaled(startBounds.fixedX - 1.0) + Margin;
        double y1 = scaled(startBounds.fixedY - 1.0) + Margin;
        double x2 = scaled(endBounds.fixedX + endBounds.fixedWidth + 1.0) + Margin;
        double y2 = scaled(endBounds.fixedY + endBounds.fixedHeight + 1.0) + Margin;
        double xr = scaled(rowEndBounds.fixedX + rowEndBounds.fixedWidth + 1.0) + Margin;
        double x0 = scaled(-1.0) + Margin;
        double h = scaled(startBounds.fixedHeight + 2.0);
        double r = scaled(4.0);

        if (y2 - y1 < 1.1 * h) {
            // Within single row
            RoundRectangle(context, x1, y1, x2 - x1, y2 - y1, r);
        } else {
            // Spanning multiple rows
            double angle = Math.PI / 2;
            context.NewPath();
            context.Arc(x1 + r, y1 + r, r, -2.0 * angle, -1.0 * angle);
            context.Arc(xr - r, y1 + r, r, -1.0 * angle, 0.0);
            if (xr - x2 < 0.1 * h) {
                context.Arc(x2 - r, y2 - r, r, 0.0, 1.0 * angle);
            } else {
                context.Arc(xr - r, y2 - h - r, r, 0.0, 1.0 * angle);
                context.ArcNegative(x2 + r, y2 - h + r, r, -1.0 * angle, -2.0 * angle);
                context.Arc(x2 - r, y2 - r, r, 0.0, 1.0 * angle);
            }
            context.Arc(x0 + r, y2 - r, r, 1.0 * angle, 2.0 * angle);
            if (x1 > 0.1 * h) {
                context.Arc(x0 + r, y1 + h + r, r, 2.0 * angle, 3.0 * angle);
                context.ArcNegative(x1 - r, y1 + h - r, r, 1.0 * angle, 0.0);
            }
            context.ClosePath();
        }

        context.SetSourceRGBA(new double[] { 1.0, 1.0, 1.0, 0.8 });
        context.LineWidth = scaled(2.0);
        context.Operator = Operator.Source;
        context.Stroke();
    }

    public override void RenderInteractiveElements(float deltaTime) {
        base.RenderInteractiveElements(deltaTime);

        if (highlight >= 0 && highlight < boundaries.Length && boundaries.Length > 1) {
            api.Render.Render2DTexturePremultipliedAlpha(textures[highlight].TextureId,
                                                         Bounds.renderX - Margin,
                                                         Bounds.renderY - Margin,
                                                         Bounds.OuterWidth + 2 * Margin,
                                                         Bounds.OuterHeight + 2 * Margin);
        }
    }

    public override void Dispose() {
        base.Dispose();
        DisposeTextures();
    }

    private void DisposeTextures() {
        foreach (var texture in textures) {
            texture.Dispose();
        }
    }
}
