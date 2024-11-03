using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ChestOrganizer;
public static class Sorter {
    public static void Sort(this IInventory inventory, IComparer<ItemStack> comparer, ICoreClientAPI api, bool merge = true) {
        int n = inventory.Count;
        var current = Enumerable
            .Range(0, n)
            .ToArray();
        var from = current
            .ToArray();
        var order = current
            .Order(new Comparer(inventory, comparer))
            .ToArray();
        var player = api.World.Player;
        var manager = player.InventoryManager;

        // Move items to desired order.
        for (int i = 0; i < n; i++) {
            int j = order[i];
            int k = current[j];
            if (k == i) continue;

            var targetSlot = inventory[i];
            var sourceSlot = inventory[k];

            if (targetSlot.Empty) {
                if (!sourceSlot.Empty) {
                    Move(sourceSlot, targetSlot);
                }
            } else if (sourceSlot.Empty) {
                Move(targetSlot, sourceSlot);
            } else {
                Flip(sourceSlot, targetSlot);
            }

            (from[i], from[k]) = (from[k], from[i]);
            current[from[i]] = i;
            current[from[k]] = k;
        }

        if (!merge) return;

        // Merge stacks.
        for (int i = 0, j = 1; i < n - 1 && j < n; ) {
            var target = inventory[i];
            var source = inventory[j];
            if (target.CanTakeFrom(source)) {
                Move(source, target);
                if (source.Empty) j++;
            } else { 
                i++;
                if (i >= j) j = i + 1;
            }
        }

        void Move(ItemSlot from, ItemSlot to) {
            int n = from.GetRemainingSlotSpace(to.Itemstack);
            ItemStackMoveOperation op = new(api.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, n);
            op.ActingPlayer = player;
            SendPacket(manager.TryTransferTo(from, to, ref op));
        }

        void Flip(ItemSlot a, ItemSlot b) {
            if (a.TryFlipWith(b)) {
                SendPacket(a.Inventory.InvNetworkUtil.GetFlipSlotsPacket(b.Inventory, SlotId(b), SlotId(a)));
            }
        }

        static int SlotId(ItemSlot slot) 
            => slot.Inventory.GetSlotId(slot);

        void SendPacket(object obj) {
            if (obj is Packet_Client packet) {
                api.Network.SendPacketClient(packet);
            }
        }
    }

    private class Comparer : IComparer<int> {
        private readonly IInventory inventory;
        private readonly IComparer<ItemStack> comparer;

        public Comparer(IInventory inventory, IComparer<ItemStack> comparer) {
            this.inventory = inventory;
            this.comparer = comparer;
        }

        public int Compare(int x, int y) 
            => comparer.Compare(inventory[x].Itemstack, inventory[y].Itemstack);
    }
}
