using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace ChestOrganizer;
public class MergedInventory : InventoryBase {
    private static int lastID = 0;

    private static MergedInventory current = null;
    private static GuiDialogMergedInventory dialog = null;

    private readonly ICoreClientAPI api;
    private readonly List<IncludedInventory> parts;
    private int count;

    public static void MergeFromDialog(GuiDialogBlockEntityInventory source, ICoreClientAPI api) {
        current ??= new(api);
        current.AddFromDialog(source);
        UpdateDialog(api);
    }

    private static void UpdateDialog(ICoreClientAPI api) {
        if (dialog == null) {
            dialog = new(Lang.Get("chestorganizer:title"), current, api);
            dialog.TryOpen();
        } else {
            dialog.Compose();
        }
    }

    public static bool OnServerPacket(BlockEntityOpenableContainer container, int id) {
        var api = container.Api as ICoreClientAPI;
        bool invert = api?.ModifierDown(Modifier.Shift | Modifier.Control) ?? false;
        if (current != null) {
            return !invert && current.HandleServerPacket(container, id);
        } else if (invert) {
            current ??= new(api);
            if (current.HandleServerPacket(container, id)) {
                UpdateDialog(api);
                return true;
            }
        }
        return false;
    }

    public MergedInventory(ICoreClientAPI api) 
        : base("mergedinventory", $"{++lastID}", api) {
        parts = new();
        count = 0;
        this.api = api;
    }

    public void AddFromDialog(GuiDialogBlockEntityInventory source) 
        => AddPart(new(this, source, api, count), false);

    public void Add(BlockEntityOpenableContainer container) 
        => AddPart(new(this, container, api, count), true);

    private void AddPart(IncludedInventory part, bool open) {
        if (Find(part.Inventory) >= 0) return;

        parts.Add(part);
        count += part.Count;

        if (open) part.Open();
        UpdateDialog(api);
    }

    public void Remove(int index, bool reopen) {
        var removed = parts[index];
        parts.RemoveAt(index);
        if (parts.Count == 0) {
            dialog?.TryClose();
            api.World.Player.InventoryManager.CloseInventory(this);
        } else { 
            UpdateCount();
            dialog?.Compose();
        }
        if (reopen) {
            removed.ReopenDialog();
        } else { 
            removed.Close(); 
        }
    }

    private bool HandleServerPacket(BlockEntityOpenableContainer container, int id) {
        if (id == (int) EnumBlockContainerPacketId.OpenInventory) {
            id = (int) (container.Inventory.HasOpened(api.World.Player) 
                ? EnumBlockEntityPacketId.Close
                : EnumBlockEntityPacketId.Open);
        }
        switch (id) {
            case (int) EnumBlockEntityPacketId.Open:
                Add(container);
                return true;
            case (int) EnumBlockEntityPacketId.Close:
                return CloseIfPresent(container.Inventory);
            default:
                return false;
        }
    }

    private bool CloseIfPresent(InventoryBase inventory) {
        int i = Find(inventory);
        bool present = i >= 0;
        if (present) {
            Remove(i, false);
        }
        return present;
    }

    public void Reorder(int from, int to) {
        var temp = parts[from];
        if (to > from) {
            // Move other elements backward
            for (int i = from; i < to; i++) {
                parts[i] = parts[i + 1];
            }
        } else {
            // Move other elements forward
            for (int i = from; i > to; i--) {
                parts[i] = parts[i - 1];
            }
        }
        parts[to] = temp;
        UpdateCount();
        UpdateDialog(api);
    }

    private void UpdateCount() {
        count = 0;
        foreach (var part in parts) {
            count = part.UpdateCount(count);
        }
    }

    private int Find(InventoryBase inventory) {
        for (int i = 0; i < parts.Count; i++) {
            if (parts[i].Inventory == inventory) {
                return i;
            }
        }
        return -1;
    }

    private IncludedInventory Find(ref int slotId) {
        for (int low = 0, high = parts.Count - 1; low <= high; ) {
            int i = (low + high) / 2;
            var part = parts[i];
            if (slotId < part.Start) {
                high = i - 1;
            } else if (slotId >= part.End) {
                low = i + 1;
            } else {
                slotId -= part.Start;
                return part;
            }
        }
        return null;
    }

    public override ItemSlot this[int slotId] {
        get => Find(ref slotId)?.Inventory[slotId];
        set {}
    }

    public IEnumerable<BlockPos> ChestPositions
        => parts.Select(x => x.Position);

    public int[] Boundaries 
        => parts.Select(x => x.Start).ToArray();

    public override bool TakeLocked => false;

    public override bool PutLocked => false;

    public override int Count => count;

    public int PartsCount => parts.Count;

    public override bool IsDirty => parts.Any(x => x.Inventory.DirtySlots.Count > 0);

    public override int GetSlotId(ItemSlot slot) 
        => parts
            .Select(x => x.GetSlotId(slot))
            .Where(x => x >= 0)
            .Append(-1)
            .First();

    public override object ActivateSlot(int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op) {
        return Find(ref slotId)?.Inventory.ActivateSlot(slotId, sourceSlot, ref op);
    }

    public override void MarkSlotDirty(int slotId) {
        base.MarkSlotDirty(slotId);
        Find(ref slotId)?.Inventory.MarkSlotDirty(slotId);
    }

    public override object Close(IPlayer player) {
        current = null;
        dialog = null;
        parts.ForEach(x => x.Close());
        base.Close(player);
        return null;
    }

    // This inventory is ephemeral and can not be saved or loaded.
    public override void FromTreeAttributes(ITreeAttribute tree) {}
    public override void ToTreeAttributes(ITreeAttribute tree) {}


    private class IncludedInventory {
        private readonly MergedInventory parent;
        private readonly ICoreClientAPI api;
        private readonly AssetLocation closeSound;
        private readonly AssetLocation openSound;
        private readonly string title;
        private readonly int cols;

        public readonly InventoryBase Inventory;
        public readonly BlockPos Position;
        public int Start;
        public int Count;

        public IncludedInventory(MergedInventory parent,
                                 BlockEntityOpenableContainer source,
                                 ICoreClientAPI api,
                                 int start) {
            this.parent = parent;
            this.api = api;
            Inventory = source.Inventory;
            Position = source.Pos;
            closeSound = source.FindCloseSound();
            openSound = source.FindOpenSound();
            Start = start;
            Count = Inventory.Count;
            title = source.GetDialogTitle();
            cols = source.FindColumns();

            Inventory.SlotModified += SlotModified;
            Inventory.SlotNotified += SlotNotified;
        }

        public IncludedInventory(MergedInventory parent,
                                 GuiDialogBlockEntityInventory source,
                                 ICoreClientAPI api,
                                 int start) {
            this.parent = parent;
            this.api = api;
            Inventory = source.Inventory;
            Position = source.BlockEntityPosition;
            closeSound = source.CloseSound;
            Start = start;
            Count = Inventory.Count;
            title = source.DialogTitle;
            cols = api.World.BlockAccessor.GetBlockEntity(Position).FindColumns();
        }


        public int End 
            => Start + Count;

        public int GetSlotId(ItemSlot slot) {
            int i = Inventory.GetSlotId(slot);
            return (i >= 0) ? i + Start : -1;
        }

        public void Open() {
            api.World.Player.InventoryManager.OpenInventory(Inventory);
            api.Gui.PlaySound(openSound, randomizePitch: true);
        }

        public void Close() {
            Detach();
            api.World.Player.InventoryManager.CloseInventory(Inventory);
            SendPacket(api, Position, open: false);
            api.Gui.PlaySound(closeSound, randomizePitch: true);
        }

        private void Detach() {
            Inventory.SlotModified -= SlotModified;
            Inventory.SlotNotified -= SlotNotified;
            api.World.Player.InventoryManager.CloseInventory(Inventory);
        }

        public int UpdateCount(int start) {
            Count = Inventory.Count;
            Start = start;
            return End;
        }

        public void ReopenDialog() {
            if (title == null) return;
            Detach();
            var dialog = new GuiDialogBlockEntityInventory(title, Inventory, Position, cols, api);
            SetEntityDialog(api, Position, dialog);
            dialog.CloseSound = closeSound;
            dialog.TryOpen();
        }

        private void SlotModified(int slotId) 
            => parent.DidModifyItemSlot(Inventory[slotId]);

        private void SlotNotified(int slotId) 
            => parent.PerformNotifySlot(slotId);
    }

    private static void SetEntityDialog(ICoreClientAPI api, BlockPos pos, GuiDialogBlockEntity dialog) {
        if (api.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityOpenableContainer container) {
            Traverse.Create(container).Field<GuiDialogBlockEntity>("invDialog").Value = dialog;
        }
    }

    private static void SendPacket(ICoreClientAPI api, BlockPos pos, bool open) {
        int id = (int) (open ? EnumBlockEntityPacketId.Open : EnumBlockEntityPacketId.Close);
        api.Network.SendBlockEntityPacket(pos, id);
    }
}
