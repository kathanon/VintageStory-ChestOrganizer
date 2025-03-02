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
    private static long lastPacketTime = 0L;
    private static int lastPacketId = 0;

    private static MergedInventory current = null;
    private static GuiDialogMergedInventory dialog = null;

    private readonly ICoreClientAPI api;
    private readonly List<IncludedInventory> parts;
    private int count;

    public static void MergeFromDialog(GuiDialogBlockEntityInventory source, ICoreClientAPI api) {
        current ??= new(api);
        current.AddFromDialog(source);
    }

    public static void MergeRange(IEnumerable<BlockEntityOpenableContainer> containers, ICoreClientAPI api) {
        current ??= new(api);
        current.AddRange(containers);
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

    public void AddFromDialog(GuiDialogBlockEntityInventory source) {
        AddPart(new(this, source, api, count), false);
        UpdateDialog(api);
    }

    public void Add(BlockEntityOpenableContainer container) {
        AddPart(new(this, container, api, count), true);
        UpdateDialog(api);
    }

    public void AddRange(IEnumerable<BlockEntityOpenableContainer> containers) {
        int n = count;
        foreach (var container in containers) {
            AddPart(new(this, container, api, count), true);
        }
        if (count != n) { 
            UpdateDialog(api);
        }
    }

    private void AddPart(IncludedInventory part, bool open) {
        if (Find(part.Inventory) >= 0) return;

        parts.Add(part);
        count += part.Count;

        if (open) part.Open();
    }

    public void Remove(int index, bool reopen) {
        var removed = parts[index];
        parts.RemoveAt(index);
        UpdateParts();
        if (reopen) {
            removed.ReopenDialog();
        } else {
            removed.Close();
        }
    }

    private void Remove(IncludedInventory removed) {
        parts.Remove(removed);
        UpdateParts();
        removed.Detach();
    }

    private void UpdateParts() {
        if (parts.Count == 0) {
            dialog?.TryClose();
            api.World.Player.InventoryManager.CloseInventory(this);
        } else {
            UpdateCount();
            dialog?.Compose();
        }
    }

    private bool HandleServerPacket(BlockEntityOpenableContainer container, int id) {
        long time = api.InWorldEllapsedMilliseconds;
        if (time - lastPacketTime < 10L && id == lastPacketId) return true;
        lastPacketTime = time;
        lastPacketId   = id;

        if (id == (int) EnumBlockContainerPacketId.OpenInventory) {
            id = (int) (container.Inventory.HasOpened(api.World.Player) 
                ? EnumBlockEntityPacketId.Close
                : EnumBlockEntityPacketId.Open);
        }

        bool result;
        switch (id) {
            case (int) EnumBlockEntityPacketId.Open:
                Add(container);
                result = true;
                break;
            case (int) EnumBlockEntityPacketId.Close:
                result = CloseIfPresent(container.Inventory);
                break;
            default:
                result = false;
                break;
        }
        if (!result) lastPacketTime = 0L;
        return result;
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

    public IEnumerable<BlockEntity> ChestEntities
        => parts.Select(x => x.Entity);

    public int ChestCount 
        => parts.Count;

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

    public void Split() {
        parts.ForEach(x => x.ReopenDialog());
        parts.Clear();
        UpdateParts();
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

    private static void SetEntityDialog(BlockEntity entity, GuiDialogBlockEntity dialog) {
        if (entity is BlockEntityOpenableContainer container) {
            Traverse.Create(container).Field<GuiDialogBlockEntity>("invDialog").Value = dialog;
        }
    }

    private static void SendPacket(ICoreClientAPI api, BlockPos pos, bool open) {
        int id = (int) (open ? EnumBlockEntityPacketId.Open : EnumBlockEntityPacketId.Close);
        api.Network.SendBlockEntityPacket(pos, id);
    }


    private class IncludedInventory {
        private readonly MergedInventory parent;
        private readonly ICoreClientAPI api;
        private readonly AssetLocation closeSound;
        private readonly AssetLocation openSound;
        private readonly string title;
        private readonly int cols;

        public readonly InventoryBase Inventory;
        public readonly BlockEntity Entity;
        public int Start;
        public int Count;

        public IncludedInventory(MergedInventory parent,
                                 BlockEntityOpenableContainer source,
                                 ICoreClientAPI api,
                                 int start) {
            this.parent = parent;
            this.api = api;
            Entity = source;
            Inventory = source.Inventory;
            closeSound = source.FindCloseSound();
            openSound = source.FindOpenSound();
            Start = start;
            Count = Inventory.Count;
            title = source.GetDialogTitle();
            cols = source.FindColumns();
            Attach();
        }

        public IncludedInventory(MergedInventory parent,
                                 GuiDialogBlockEntityInventory source,
                                 ICoreClientAPI api,
                                 int start) {
            this.parent = parent;
            this.api = api;
            Inventory = source.Inventory;
            Entity = api.World.BlockAccessor.GetBlockEntity(source.BlockEntityPosition);
            closeSound = source.CloseSound;
            Start = start;
            Count = Inventory.Count;
            title = source.DialogTitle;
            cols = Entity.FindColumns();
            Attach();
        }


        private void Attach() {
            Inventory.SlotModified += SlotModified;
            Inventory.SlotNotified += SlotNotified;
            Inventory.OnInventoryClosed += InventoryClosed;
        }

        public void Detach() {
            Inventory.SlotModified -= SlotModified;
            Inventory.SlotNotified -= SlotNotified;
            Inventory.OnInventoryClosed -= InventoryClosed;
            api.World.Player.InventoryManager.CloseInventory(Inventory);
        }

        public int End 
            => Start + Count;

        public BlockPos Position
            => Entity.Pos;

        public int GetSlotId(ItemSlot slot) {
            int i = Inventory.GetSlotId(slot);
            return (i >= 0) ? i + Start : -1;
        }

        public void Open() {
            var player = api.World.Player;
            if (!Inventory.HasOpened(player)) {
                player.InventoryManager.OpenInventory(Inventory);
                SendPacket(api, Position, open: true);
            }
            api.Gui.PlaySound(openSound, randomizePitch: true);
        }

        public void Close() {
            Detach();
            // We need to do this again for lid to close... why??
            api.World.Player.InventoryManager.CloseInventory(Inventory);
            SendPacket(api, Position, open: false);
            api.Gui.PlaySound(closeSound, randomizePitch: true);
        }

        public int UpdateCount(int start) {
            Count = Inventory.Count;
            Start = start;
            return End;
        }

        public void ReopenDialog() {
            Detach();
            var dialog = new GuiDialogBlockEntityInventory(title, Inventory, Position, cols, api);
            SetEntityDialog(Entity, dialog);
            dialog.CloseSound = closeSound;
            dialog.TryOpen();
        }

        private void SlotModified(int slotId) 
            => parent.DidModifyItemSlot(Inventory[slotId]);

        private void SlotNotified(int slotId) 
            => parent.PerformNotifySlot(slotId);

        private void InventoryClosed(IPlayer player) {
            if (player == api.World.Player) {
                parent.Remove(this);
            }
        }
    }
}
