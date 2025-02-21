using Cairo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;

namespace ChestOrganizer;
using IconFunc = Action<Context, double, double, double, double, double[]>;
public class TitleBarAdditions {
    private static readonly ConditionalWeakTable<GuiElementDialogTitleBar, TitleBarAdditions> table = new();

    public static TitleBarAdditions For(GuiElementDialogTitleBar titleBar) 
        => table.GetValue(titleBar, x => new(x));


    private readonly GuiElementDialogTitleBar bar;
    private List<Icon> icons;
    private Search search; 
    private ICoreClientAPI api;
    private InventoryBase inventory;
    private GuiDialogBlockEntityInventory dialog;

    public TitleBarAdditions(GuiElementDialogTitleBar bar) {
        this.bar = bar;
    }

    public void Activate(ICoreClientAPI api, GuiDialog dialog) {
        if (dialog is GuiDialogBlockEntityInventory dialogInv) { 
            this.dialog = dialogInv;
            Activate(api, dialogInv.Inventory, dialog);
            icons.Insert(0, new(Icons.Merge, Merge, api, Lang.Get("chestorganizer:merge")));
        } else if (dialog is GuiDialogInventory) {
            var backpack = api.World.Player.InventoryManager.GetOwnInventory("backpack");
            if (backpack is not InventoryBase inventory) return;
            Activate(api, inventory, dialog);
        }
    }

    public void Activate(ICoreClientAPI api, InventoryBase inventory, GuiDialog dialog) {
        this.api = api;
        this.inventory = inventory;

        search = new Search(dialog, inventory, api);

        icons = new() {
            new(Icons.Sort, Sort, api, Lang.Get("chestorganizer:sort")),
            new(Icons.Find, Find, api, Lang.Get("chestorganizer:find")),
        };
    }

    private void Merge() {
        if (api.ModifierDown(Modifier.Shift)) {
            api.OpenedGuis
                .OfType<GuiDialogBlockEntityInventory>()
                .ToList()
                .Foreach(MoveToMerged);
        } else {
            MoveToMerged(dialog);
        }
    }

    private void Sort() {
        var comparer = api.ModifierDown(Modifier.Shift) ? Comparer.Name : Comparer.Code;
        inventory.Sort(comparer, api);
    }

    private void Find() 
        => search.Toggle();

    private void MoveToMerged(GuiDialogBlockEntityInventory dialog) {
        if (Patch_ChestDialog.BlockCloseInventory(dialog.TryClose)) {
            MergedInventory.MergeFromDialog(dialog, api);
        }
    }

    public void Compose(Context ctx, ImageSurface surface, Rectangled menuIconRect) {
        if (api == null) return;

        double size = Math.Round(0.8 * GuiElement.scaled(GuiElementDialogTitleBar.unscaledCloseIconSize));
        double x = menuIconRect.X - size - GuiElement.scaled(8);
        double y = menuIconRect.Y + (menuIconRect.Height + 1 - size) / 2;
        size += 4;
        foreach (var icon in icons) {
            icon.Place(ref x, y, size);
            icon.Compose(ctx);
        }
        search.Compose(bar.Bounds, surface, icons[^1]);
    }

    public void Dispose() {
        if (api == null) return;
        foreach (var icon in icons) {
            icon.Dispose();
        }
        search.Dispose();
    }

    public void Render(float deltaTime) {
        if (api == null) return;
        var bounds = bar.Bounds;
        CheckMouseHit(icon => icon.Hover(bounds, api), deltaTime);
        search.Render(deltaTime);
    }

    public void OnMouseUp(MouseEvent args) {
        if (api == null) return;
        if (CheckMouseHit(icon => icon.OnMouseUp())) {
            args.Handled = true;
        }
        search.OnMouseUp(args);
    }

    public void OnMouseDown(MouseEvent args) {
        if (api == null) return;
        search.OnMouseDown(args);
    }

    public void OnMouseMove(MouseEvent args) {
        if (api == null) return;
        search.OnMouseMove(args);
    }

    public void OnKeyDown(KeyEvent args) {
        if (api == null) return;
        search.OnKeyDown(args);
    }

    public void OnKeyPress(KeyEvent args) {
        if (api == null) return;
        search.OnKeyPress(args);
    }

    private bool CheckMouseHit(Action<Icon> onHit, float? deltaTime = null) {
        var bounds = bar.Bounds;
        double x = api.Input.MouseX - bounds.absX;
        double y = api.Input.MouseY - bounds.absY;

        foreach (var icon in icons) {
            if (icon.Hit(x, y, deltaTime)) {
                onHit(icon);
                return true;
            }
        }

        return false;
    }


    private class Icon {
        public readonly Action OnMouseUp;
        public readonly Rectangled rect = new();

        private readonly IconFunc icon;
        private readonly HoverText tooltip = null;
        private LoadedTexture hover;

        public Icon(IconFunc icon, Action onMouseUp, ICoreClientAPI api, string tooltip = null) {
            this.icon = icon;
            OnMouseUp = onMouseUp;
            hover = new(api);
            if (tooltip != null) {
                this.tooltip = new(api, tooltip);
            }
        }

        public void Place(ref double x, double y, double size) {
            rect.X = x;
            rect.Y = y;
            rect.Width = size;
            rect.Height = size;
            x -= size + GuiElement.scaled(4);
        }

        public void Compose(Context ctx) {
            Icons.Draw(ctx, icon, rect);
            Icons.MakeTexture(ref hover, Icons.DrawHover, icon, (int) rect.Width);
        }

        public bool Hit(double x, double y, float? deltaTime) {
            bool hit = rect.PointInside(x, y);
            if (deltaTime != null) tooltip?.Render(hit, deltaTime.Value);
            return hit;
        }

        public void Hover(ElementBounds bounds, ICoreClientAPI api) 
            => api.Render.Render2DTexturePremultipliedAlpha(hover.TextureId,
                                                            bounds.absX + rect.X,
                                                            bounds.absY + rect.Y,
                                                            rect.Width,
                                                            rect.Height,
                                                            200f);
        public void Dispose() {
            hover.Dispose();
            tooltip?.Dispose();
        }
    }

    private class Search {
        private readonly GuiDialog dialog;
        private readonly InventoryBase inventory;
        private readonly ICoreClientAPI api;
        private GuiElementTextInput textField;
        private LoadedTexture textStatic;
        private bool visible = false;
        private readonly Dictionary<int, string> dummyDict = new();

        public Search(GuiDialog dialog, InventoryBase inventory, ICoreClientAPI api) {
            this.dialog = dialog;
            this.inventory = inventory;
            this.api = api;
            textStatic = new(api);
        }

        public void TextChanged(string text) {
            for (int i = dummyDict.Count; i < inventory.Count; i++) {
                dummyDict[i] = null;
            }

            var composer = dialog.SingleComposer ?? dialog.Composers["maininventory"];
            var grid = composer.GetElement("slotgrid") as GuiElementItemSlotGridBase;
            if (text != null && text.Length > 0) {
                grid?.FilterItemsBySearchText(text ?? "", searchCacheNames: dummyDict);
            } else {
                (grid as GuiElementItemSlotGrid)?.DetermineAvailableSlots();
                (grid as GuiElementItemSlotGridExcl)?.ComposeElements(null, null);
            }
            (grid as GuiElementHighlightItemSlotGrid)?.ComposeOutlines();
        }

        public void Compose(ElementBounds parent, ImageSurface parentSurface, Icon find) {
            float scale = RuntimeEnv.GUIScale;
            double findHeight = find.rect.Height / scale;
            double height = Math.Round(1.4 * findHeight);
            double width = 4 * height;
            double x = find.rect.X / scale - width - 3.0;
            double y = find.rect.Y / scale - (height - findHeight) / 2 + 1.0;
            var bounds = ElementBounds.Fixed(0.0, 0.0, width, height).WithParent(parent);
            textField = new(api, bounds, TextChanged, CairoFont.WhiteSmallText());
            textField.SetMaxLines(1);

            Icons.MakeContext((int) width, (int) height, out var context, out var surface);
            var pattern = Icons.CopyFrom(parentSurface, x, y);
            context.SetSource(pattern);
            context.Paint();
            surface.BlurFull(1.7);
            pattern.Dispose();
            textField.ComposeElements(context, surface);
            api.Gui.LoadOrUpdateCairoTexture(surface, true, ref textStatic);

            bounds.fixedX = x;
            bounds.fixedY = y;
            textField.ComposeElements(context, surface);

            surface.Dispose();
            context.Dispose();
        }

        public void Toggle() {
            visible = !visible;
            if (visible) {
                textField.OnFocusGained();
            } else {
                textField.SetValue("");
                textField.OnFocusGained();
            }
        }

        public void OnMouseUp(MouseEvent args) {
            if (visible) {
                textField.OnMouseUp(api, args);
            }
        }

        public void OnMouseDown(MouseEvent args) {
            if (visible) {
                textField.OnMouseDown(api, args);
                if (textField.IsPositionInside(args.X, args.Y)) {
                    textField.OnFocusGained();
                } else {
                    textField.OnFocusLost();
                }
            }
        }

        public void OnMouseMove(MouseEvent args) {
            if (visible) {
                textField.OnMouseMove(api, args);
            }
        }

        public void OnKeyDown(KeyEvent args) {
            if (visible) {
                textField.OnKeyDown(api, args);
            }
        }

        public void OnKeyPress(KeyEvent args) {
            if (visible) {
                textField.OnKeyPress(api, args);
            }
        }

        public void Render(float deltaTime) {
            if (visible) {
                var bounds = textField.Bounds;
                api.Render.Render2DTexturePremultipliedAlpha(textStatic.TextureId,
                                                             bounds.renderX,
                                                             bounds.renderY,
                                                             bounds.OuterWidth,
                                                             bounds.OuterHeight);
                textField.RenderInteractiveElements(deltaTime);
            }
        }

        public void Dispose() {
            textStatic.Dispose();
            textField .Dispose();
        }
    }
}
