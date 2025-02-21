using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace ChestOrganizer;
public static class ExtensionMethods {
    public static AssetLocation FindOpenSound(this BlockEntityOpenableContainer self) {
        Block block = self.Api.World.BlockAccessor.GetBlock(self.Pos);
        return block.Attributes?["openSound"]?.AsAssetLocation(block.Code.Domain) ?? self.OpenSound;
    }

    public static AssetLocation FindCloseSound(this BlockEntityOpenableContainer self) {
        Block block = self.Api.World.BlockAccessor.GetBlock(self.Pos);
        return block.Attributes?["closeSound"]?.AsAssetLocation(block.Code.Domain) ?? self.CloseSound;
    }

    public static string GetDialogTitle(this BlockEntityOpenableContainer self) {
        if (self is BlockEntityGenericContainer generic) {
            return Lang.Get(generic.dialogTitleLangCode);
        } else if (self is BlockEntityGenericTypedContainer typed) {
            return typed.DialogTitle;
        }
        self.Api.Logger.Warning($"Could not get dialog title for container entity of type {self.GetType().FullName}.");
        return null;
    }

    public static int FindColumns(this BlockEntity self) 
        => (self is BlockEntityGenericTypedContainer typed) ? typed.quantityColumns : 4;


    public static AssetLocation AsAssetLocation(this JsonObject self, string domain) {
        var value = self.AsString();
        if (value == null) return null;
        return AssetLocation.Create(value, domain);
    }

    public static (double, double) ClosestInside(this Rectangled rect, double x, double y) 
        => (x.ClosestInRange(rect.X, rect.Width), y.ClosestInRange(rect.Y, rect.Height));

    public static double ClosestInRange(this double value, double min, double length) {
        double max = min + length;
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    private static ScrolledBounds scrollBounds = null;

    public static GuiComposer BeginScroll(this GuiComposer composer, ScrolledBounds bounds, string key = null) {
        scrollBounds = bounds;
        return bounds.BeginScroll(composer, key);
    }

    public static GuiComposer EndScroll(this GuiComposer composer) {
        var bounds = scrollBounds;
        scrollBounds = null;
        return bounds?.EndScroll(composer) ?? composer;
    }

    public static bool ModifierDown(this ICoreClientAPI self, Modifier modifiers) {
        var keys = self.Input.KeyboardKeyStateRaw;
        bool and = IsSet(Modifier.And);
        if (Check(Modifier.Shift,   GlKeys.LShift,   GlKeys.RShift)  ) return !and;
        if (Check(Modifier.Control, GlKeys.LControl, GlKeys.RControl)) return !and;
        if (Check(Modifier.Alt,     GlKeys.LAlt,     GlKeys.RAlt)    ) return !and;
        return and;

        bool Check(Modifier m, GlKeys key1, GlKeys key2)
            => IsSet(m) && ((keys[(int) key1] || keys[(int) key2]) == !and);

        bool IsSet(Modifier m) 
            => (modifiers & m) != 0;
    }
}

public enum Modifier {
    Shift   = 1,
    Control = 2,
    Alt     = 4,

    Or      = 0,
    And     = 8,
}
