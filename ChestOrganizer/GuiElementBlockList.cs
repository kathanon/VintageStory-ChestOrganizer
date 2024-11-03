using Cairo;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ChestOrganizer;
using BlockAction = Action<int, double, Rectangled, double, double, bool>;

public class GuiElementBlockList : GuiElement {
    private struct HoverIcon {
        private readonly ICoreClientAPI api;
        private bool tooltipRendered;

        public Rectangled Rect;
        public LoadedTexture Icon;
        public LoadedTexture Hover;
        public readonly HoverText Tooltip;

        public HoverIcon(ICoreClientAPI capi, string tooltip) {
            Icon    = new(capi);
            Hover   = new(capi);
            Tooltip = new(capi, tooltip);
            api = capi;
        }

        public void EnsureTooltipRendered(float deltaTime) {
            if (!tooltipRendered) {
                Tooltip.Render(false, deltaTime);
            }
            // Prepare for next frame.
            tooltipRendered = false;
        }

        public void Render(double x, double y, float deltaTime) {
            RenderTexture(Icon, x, y);
            if (Rect.PointInside(api.Input.MouseX - x, api.Input.MouseY - y)) {
                RenderTexture(Hover, x, y);
                Tooltip.Render(true, deltaTime);
                tooltipRendered = true;
            }
        }

        public void DisposeTextures() {
            Icon .Dispose();
            Hover.Dispose();
        }

        private readonly void RenderTexture(LoadedTexture texture, double x, double y) 
            => api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId,
                                                            x + Rect.X,
                                                            y + Rect.Y,
                                                            Rect.Width,
                                                            Rect.Height,
                                                            200f);
    }

    public static readonly double IconSize     = 1.6 * GuiElementPassiveItemSlot.unscaledSlotSize;
    public static readonly double Padding      = GuiElementItemSlotGridBase.unscaledSlotPadding;
    public static readonly double DragDistance = 6.0;
    public static readonly double DragWidthPct = 0.9;

    private readonly List<(ItemStack stack, BlockEntity entity)> blocks;
    private readonly MergedInventory inventory;
    private readonly DummySlot dummySlot = new();
    private readonly Rectangled boundsRect = new();
    private readonly BlockHighlight highlight;
    private HoverIcon close;
    private HoverIcon split;
    private Rectangled dragRect;
    private LoadedTexture dragLine;
    private int hoveredIndex = -1;
    private int dragFrom = -1;
    private int dragTo = -1;
    private double mouseDownX = -1.0;
    private double mouseDownY = -1.0;
    private float deltaTime;

    public event Action<int> OnBlockHover;

    public GuiElementBlockList(ICoreClientAPI capi, ElementBounds bounds, MergedInventory inventory) : base(capi, bounds) {
        blocks = inventory.ChestPositions
            .Select(capi.World.BlockAccessor.GetBlockEntity)
            .Select(MakeStack)
            .ToList();
        close     = new(capi, Lang.Get("chestorganizer:close"));
        split     = new(capi, Lang.Get("chestorganizer:detach"));
        dragLine  = new(capi);
        highlight = new(capi);
        this.inventory = inventory;

        static (ItemStack, BlockEntity) MakeStack(BlockEntity entity) {
            var res = new ItemStack(entity.Block, 1);
            if (entity is BlockEntityGenericTypedContainer container) {
                res.Attributes.SetString("type", container.type);
            }
            return (res, entity);
        }
    }

    public override void ComposeElements(Context unusedCtx, ImageSurface unusedSurface) {
        // Dispose of any stale textures.
        DisposeTextures();

        double size = scaled(IconSize);
        int iconSize = (int) Math.Round(size / 8 + 4);
        double pad = scaled(2);

        close.Rect = new(size        - iconSize - pad, pad, iconSize, iconSize);
        Icons.MakeTexture(ref close.Icon,  Icons.Draw,      Icons.Cross, iconSize, false);
        Icons.MakeTexture(ref close.Hover, Icons.DrawHover, Icons.Cross, iconSize, false);

        split.Rect = new(close.Rect.X - iconSize - pad, pad, iconSize, iconSize);
        Icons.MakeTexture(ref split.Icon,  Icons.Draw,      Icons.Split, iconSize, false);
        Icons.MakeTexture(ref split.Hover, Icons.DrawHover, Icons.Split, iconSize, false);

        double width = Bounds.OuterWidth * DragWidthPct;
        double margin = Bounds.OuterWidth * (1.0 - DragWidthPct) / 2.0;
        dragRect = new(margin - 2.0, -0.05 * width - 2.0, width + 4.0, 0.1 * width + 4.0);
        Icons.MakeTexture(ref dragLine, Icons.Draw, Icons.Divider, (int) dragRect.Width, (int) dragRect.Height, false);
    }

    private void ForEachBlock(BlockAction action, double mx = -1.0, double my = -1.0) {
        if (mx < 0.0) mx = api.Input.MouseX;
        if (my < 0.0) my = api.Input.MouseY;
        double size = scaled(IconSize);
        Rectangled rect = new(Bounds.renderX, Bounds.renderY, size, 0.8 * size);

        for (int i = 0; i < blocks.Count; i++) {
            bool inside = rect.PointInside(mx, my);
            action(i, size, rect, mx, my, inside);
            rect.Y += rect.Height;
        }
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args) {
        base.OnMouseMove(api, args);
        if (mouseDownX >= 0.0) {
            double dx = api.Input.MouseX - mouseDownX;
            double dy = api.Input.MouseY - mouseDownY;
            if (dx * dx + dy * dy >= DragDistance * DragDistance) {
                ForEachBlock(SetDragging, mouseDownX, mouseDownY);
                mouseDownX = mouseDownY = -1.0;
            }
        }
    }

    private void SetDragging(int i, double size, Rectangled rect, double mx, double my, bool inside) {
        if (inside) {
            dragFrom = i;
        }
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args) {
        base.OnMouseUpOnElement(api, args);
        ForEachBlock(OnMouseUp);
        if (dragFrom >= 0 && dragTo >= 0 && dragFrom != dragTo) {
            inventory.Reorder(dragFrom, dragTo);
        }
        dragFrom = dragTo = -1;
        mouseDownX = mouseDownY = -1.0;
    }

    private void OnMouseUp(int i, double size, Rectangled rect, double mx, double my, bool inside) {
        if (dragFrom >= 0) {
            (double x, double y) = boundsRect.ClosestInside(mx, my);
            if (IsOverBlock(rect, i, x, y)) {
                bool above = y <= rect.Y + rect.Height / 2;
                dragTo = i + (above ? 0 : 1);
                if (dragTo > dragFrom) dragTo--;
            }
        } else {
            bool closeHit = close.Rect.PointInside(mx - rect.X, my - rect.Y);
            bool splitHit = split.Rect.PointInside(mx - rect.X, my - rect.Y);

            if (closeHit || splitHit) {
                inventory.Remove(i, splitHit);
            }
        }
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args) {
        base.OnMouseUpOnElement(api, args);
        dragFrom = dragTo = -1;
        ForEachBlock(OnMouseDown);
    }

    private void OnMouseDown(int i, double size, Rectangled rect, double mx, double my, bool inside) {
        if (!inside) return;

        bool closeHit = close.Rect.PointInside(mx - rect.X, my - rect.Y);
        bool splitHit = split.Rect.PointInside(mx - rect.X, my - rect.Y);

        if (!closeHit && !splitHit) {
            mouseDownX = mx;
            mouseDownY = my;
        }
    }

    public override void RenderInteractiveElements(float deltaTime) {
        base.RenderInteractiveElements(deltaTime);
        boundsRect.X = Bounds.renderX;
        boundsRect.Y = Bounds.renderY;
        boundsRect.Width = Bounds.OuterWidth;
        boundsRect.Height = Bounds.OuterHeight;

        int prevHovered = hoveredIndex;
        hoveredIndex = -1;
        this.deltaTime = deltaTime;

        ForEachBlock(Render);

        if (prevHovered != hoveredIndex) {
            OnBlockHover?.Invoke(hoveredIndex);
            highlight.Entity = (hoveredIndex >= 0) ? blocks[hoveredIndex].entity : null;
        }
        close.EnsureTooltipRendered(deltaTime);
        split.EnsureTooltipRendered(deltaTime);
    }

    private void Render(int i, double size, Rectangled rect, double mx, double my, bool inside) {
        bool hover = inside && dragFrom < 0;

        // Highlight hovered one
        float blockSize = (float) (size / 2);
        if (hover) {
            hoveredIndex = i;
            blockSize *= 1.25f;
        }

        // Render position and drag line
        (double x, double y) = boundsRect.ClosestInside(mx, my);
        double z = 200.0;
        if (dragFrom != i) { 
            if (dragFrom >= 0 && IsOverBlock(rect, i, x, y)) {
                bool above = y <= rect.Y + rect.Height / 2;
                double lineY = above ? rect.Y : rect.Y + rect.Height;
                double adjust = (i == 0 && above) ? 0.0 : -0.08;
                api.Render.Render2DTexturePremultipliedAlpha(dragLine.TextureId,
                                                             rect.X + dragRect.X,
                                                             lineY + adjust * size,
                                                             dragRect.Width,
                                                             dragRect.Height);
            }

            x = rect.X + 0.48 * size;
            y = rect.Y + 0.40 * size;
            z = 100.0;
        }

        // Render block
        dummySlot.Itemstack = blocks[i].stack;
        api.Render.RenderItemstackToGui(dummySlot, x, y, z, blockSize, -1, showStackSize: false);

        // Icons
        if (hover) {
            close.Render(rect.X, rect.Y, deltaTime);
            split.Render(rect.X, rect.Y, deltaTime);
        }
    }

    private bool IsOverBlock(Rectangled rect, int index, double x, double y) {
        var yMaxRect = (index == blocks.Count - 1) ? boundsRect : rect;
        return x >= rect.X && x <= rect.X + rect.Width
            && y >= rect.Y && y <= yMaxRect.Y + yMaxRect.Height;
    }

    public override void Dispose() {
        base.Dispose();
        highlight.Dispose();
        close.Tooltip.Dispose();
        split.Tooltip.Dispose();
        DisposeTextures();
    }

    private void DisposeTextures() {
        close.DisposeTextures();
        split.DisposeTextures();
        dragLine.Dispose();
    }
}
