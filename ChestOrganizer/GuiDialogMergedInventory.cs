using System;
using System.Linq;
using Vintagestory.API.Client;

namespace ChestOrganizer;

public class GuiDialogMergedInventory : GuiDialogGeneric {
    private readonly MergedInventory inventory;

    public GuiDialogMergedInventory(string title, MergedInventory inventory, ICoreClientAPI capi) 
        : base(title, capi) {
        this.inventory = inventory;
        Compose();
    }

    public override void OnFinalizeFrame(float dt) {
        base.OnFinalizeFrame(dt);

        // Check for inventories that are out of range.
        var player = capi.World.Player;
        float range = player.WorldData.PickingRange + 1;
        float rangesq = range * range;
        var eyePos = player.Entity.Pos.XYZ.Add(player.Entity.LocalEyePos);
        var toRemove = inventory.ChestPositions
            .Where(p => p.DistanceSqTo(eyePos.X, eyePos.Y, eyePos.Z) > rangesq)
            .Select((p, i) => i)
            .Reverse()
            .ToArray();
        if (toRemove.Length > 0) {
            capi.Event.EnqueueMainThreadTask(delegate { 
                for (int i = 0; i < toRemove.Length; i++) {
                    inventory.Remove(toRemove[i], false);
                }
            }, "chestorganizer-closechests");
        }
    }

    public override bool PrefersUngrabbedMouse => false;

    public void Compose() {
        double edgePad = GuiStyle.ElementToDialogPadding;
        double slotPad = GuiElementItemSlotGridBase.unscaledSlotPadding;
        double blockSize = GuiElementBlockList.IconSize;

        int n = inventory.Count;
        int cols = (n > 9 * 9) ? 18 : 9;
        int rows = (n + cols - 1) / cols;
        int visibleRows = Math.Min(rows, 9);
        bool withScroll = visibleRows < rows;
        double blockHeight = inventory.PartsCount * 0.8 * blockSize + slotPad;

        var viewBounds = ElementStdBounds
            .SlotGrid(EnumDialogArea.None, slotPad, slotPad, cols, visibleRows);
        var innerBounds = ElementStdBounds
            .SlotGrid(EnumDialogArea.None, 0.0, 0.0, cols, rows);
        ScrolledBounds gridBounds = new(viewBounds, innerBounds, 6.0);
        ScrolledBounds blockBounds = new(slotPad, slotPad, blockSize, blockHeight, gridBounds.Height, 0.0);
        var dialogBounds = gridBounds.Outer
            .ForkBoundingParent(edgePad + blockBounds.Width + 10.0, edgePad + 30.0, edgePad, edgePad)
            .WithFixedAlignmentOffset(0.0, -90.0)
            .WithAlignment(EnumDialogArea.CenterBottom);
        blockBounds.Outer
            .ForkBoundingParent(edgePad, edgePad + 30.0)
            .WithParent(dialogBounds);

        var composer = SingleComposer = capi.Gui.CreateCompo(inventory.InventoryID, dialogBounds);

        var titleBar = new GuiElementDialogTitleBar(capi, DialogTitle, composer, CloseAction);
        var slotGrid = new GuiElementHighlightItemSlotGrid(
            capi, inventory, capi.Network.SendPacketClient, cols, gridBounds.Inner, inventory.Boundaries);
        var blocklist = new GuiElementBlockList(capi, blockBounds.Inner, inventory);
        blocklist.OnBlockHover += slotGrid.SetHighlight;

        TitleBarAdditions.For(titleBar).Activate(capi, inventory, this);

        composer
            .AddShadedDialogBG(ElementBounds.Fill)
            .AddInteractiveElement(titleBar)
            .BeginScroll(blockBounds, "blockscroll")
            .AddInteractiveElement(blocklist, "blocklist")
            .EndScroll()
            .BeginScroll(gridBounds, "scrollbar")
            .AddInteractiveElement(slotGrid, "slotgrid")
            .EndScroll()
            .Compose()
            .UnfocusOwnElements();

        blockBounds.SetupScrollbar();
        gridBounds.SetupScrollbar();
    }

    public override double DrawOrder => 0.21;

    public override void OnGuiClosed() {
        inventory.Close(capi.World.Player);
        Dispose();
    }

    public override void OnGuiOpened() 
        => inventory.Open(capi.World.Player);

    private void CloseAction()
        => TryClose();
}
