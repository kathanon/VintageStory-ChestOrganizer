using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ChestOrganizer;
public class Main : ModSystem {
    public const string ID = "chestorganizer";
    public const string Hotkey = ID + ".openall";

    private static Harmony harmony;

    private ICoreClientAPI api;

    public override void StartPre(ICoreAPI api) 
        => (harmony ??= new Harmony(ID)).PatchAll();

    public override void Dispose() 
        => harmony?.UnpatchAll(ID);

    public override void StartClientSide(ICoreClientAPI api) {
        this.api = api;
        Patch_ChestDialog.Setup(api);
        Icons.Setup(api);

        api.Input.RegisterHotKey(
            Hotkey,
            Lang.Get("chestorganizer:openall"),
            GlKeys.R,
            HotkeyType.CharacterControls);
        api.Input.SetHotKeyHandler(Hotkey, OpenAll);
    }

    public bool OpenAll(KeyCombination _) {
        var player = api.World.Player;
        if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) return false;

        float range = player.WorldData.PickingRange + 1;
        float rangesq = range * range;
        var eyePos = player.Entity.Pos.XYZ.Add(player.Entity.LocalEyePos - 0.5f);
        var accessor = api.World.BlockAccessor;
        List<BlockEntityGenericTypedContainer> chests = new();

        accessor.WalkBlocks((eyePos - range).AsBlockPos, (eyePos + (range + 1.0f)).AsBlockPos, Step);
        MergedInventory.MergeRange(chests, api);

        return true;


        void Step(Block b, int x, int y, int z) {
            if (eyePos.SquareDistanceTo(x, y, z) > rangesq) return;
            var entity = accessor.GetBlockEntity<BlockEntityGenericTypedContainer>(new(x, y, z));
            if (!entity?.Inventory.HasOpened(player) ?? false) {
                chests.Add(entity);
            }
        }
    }
}
