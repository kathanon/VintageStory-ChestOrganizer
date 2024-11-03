using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace ChestOrganizer;
public class Mod : ModSystem {
    public const string ID = "chestorganizer";

    public override void StartClientSide(ICoreClientAPI api) {
        Patch_ChestDialog.Setup(api);
        Icons.Setup(api);

        new Harmony(ID).PatchAll();
    }

}
