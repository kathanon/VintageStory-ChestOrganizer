using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ChestOrganizer;
using CompareFunc = System.Func<ItemStack, ItemStack, int>;

public class Comparer : IComparer<ItemStack> {
    public static readonly Comparer NameAmount     = new(ByName, ByAmount);
    public static readonly Comparer TypeNameAmount = new(ByType, ByName, ByAmount);

    private static bool CompareNullableEnum<T>(T x, T y, out int res) {
        if (x == null) {
            res = (y == null) ? 0 : -1;
            return y != null;
        } else {
            res = (x as Enum).CompareTo(y);
            return true;
        }
    }

    private static int ComparePresence<T>(T x, T y) {
        int xval = (x != null) ? 1 : 0;
        int yval = (y != null) ? 1 : 0;
        return xval - yval;
    }

    private static int ByType(ItemStack x, ItemStack y) {
        int res = x.Class.CompareTo(y.Class);
        if (res != 0) return res;
        if (x.Class == EnumItemClass.Block) {
            return x.Block.BlockMaterial.CompareTo(y.Block.BlockMaterial);
        } else {
            // WIP
            if (CompareNullableEnum(x.Item.Tool, y.Item.Tool, out res)) return res;
            return ComparePresence(x.Item.NutritionProps, y.Item.NutritionProps);
        }
    }

    private static int ByName(ItemStack x, ItemStack y)
        => x.GetName().CompareTo(y.GetName());

    private static int ByAmount(ItemStack x, ItemStack y)
        => y.StackSize.CompareTo(x.StackSize);

    private readonly CompareFunc[] comparers;

    private Comparer(params CompareFunc[] comparers) 
        => this.comparers = comparers;

    public int Compare(ItemStack x, ItemStack y) {
        if (x == null || y == null) return (y != null ? 1 : 0) - (x != null ? 1 : 0);

        int res = 0;
        foreach (var cmp in comparers) {
            res = cmp(x, y);
            if (res != 0) break;
        }
        return res;
    }
}
