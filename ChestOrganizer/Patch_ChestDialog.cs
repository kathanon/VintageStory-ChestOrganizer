using Cairo;
using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace ChestOrganizer;
[HarmonyPatch]
public static class Patch_ChestDialog {
    private static ICoreClientAPI api;

    public static void Setup(ICoreClientAPI api) {
        Patch_ChestDialog.api = api;
    }

    private static GuiDialog current = null;
    private static bool allowCloseInventory = true;

    public static bool BlockCloseInventory(Func<bool> action) {
        allowCloseInventory = false;
        bool result = action();
        allowCloseInventory = true;
        return result;
    }


    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiDialogBlockEntityInventory), MethodType.Constructor, 
        typeof(string), typeof(InventoryBase), typeof(BlockPos), typeof(int), typeof(ICoreClientAPI))]
    public static void InventoryDialog_Ctor(GuiDialogBlockEntityInventory __instance) 
        => current = __instance;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiDialogInventory), "ComposeSurvivalInvDialog")]
    public static void PlayerInventoryDialogCompose(GuiDialogInventory __instance) 
        => current = __instance;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementDialogTitleBar), MethodType.Constructor, 
        typeof(ICoreClientAPI), typeof(string), typeof(GuiComposer), typeof(Action), typeof(CairoFont), typeof(ElementBounds))]
    public static void TitleBar_Ctor(GuiElementDialogTitleBar __instance) {
        if (current != null) {
            TitleBarAdditions.For(__instance).Activate(api, current);
            current = null;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementDialogTitleBar), nameof(GuiElementDialogTitleBar.ComposeTextElements))]
    public static void TitleBar_Compose(GuiElementDialogTitleBar __instance, Context ctx, ImageSurface surface, Rectangled ___menuIconRect) 
        => TitleBarAdditions.For(__instance).Compose(ctx, surface, ___menuIconRect);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementDialogTitleBar), nameof(GuiElementDialogTitleBar.RenderInteractiveElements))]
    public static void TitleBar_Render(GuiElementDialogTitleBar __instance, float deltaTime) 
        => TitleBarAdditions.For(__instance).Render(deltaTime);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementDialogTitleBar), nameof(GuiElementDialogTitleBar.OnMouseUp))]
    public static void TitleBar_MouseUp(GuiElementDialogTitleBar __instance, MouseEvent args) 
        => TitleBarAdditions.For(__instance).OnMouseUp(args);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementDialogTitleBar), nameof(GuiElementDialogTitleBar.OnMouseDown))]
    public static void TitleBar_MouseDown(GuiElementDialogTitleBar __instance, MouseEvent args) 
        => TitleBarAdditions.For(__instance).OnMouseDown(args);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementDialogTitleBar), nameof(GuiElementDialogTitleBar.OnMouseMove))]
    public static void TitleBar_MouseMove(GuiElementDialogTitleBar __instance, MouseEvent args) 
        => TitleBarAdditions.For(__instance).OnMouseMove(args);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementDialogTitleBar), nameof(GuiElementDialogTitleBar.OnKeyDown))]
    public static void TitleBar_KeyDown(GuiElementDialogTitleBar __instance, KeyEvent args) 
        => TitleBarAdditions.For(__instance).OnKeyDown(args);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElement), nameof(GuiElement.OnKeyPress))]
    public static void TitleBar_KeyPress(GuiElement __instance, KeyEvent args) {
        if (__instance is GuiElementDialogTitleBar bar) {
            TitleBarAdditions.For(bar).OnKeyPress(args);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GuiElementDialogTitleBar), nameof(GuiElementDialogTitleBar.Dispose))]
    public static void TitleBar_Dispose(GuiElementDialogTitleBar __instance) 
        => TitleBarAdditions.For(__instance).Dispose();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(GuiDialogBlockEntity), nameof(GuiDialogBlockEntity.OnGuiClosed))]
    public static bool EntityDialog_OnGuiClosed() 
        => allowCloseInventory;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(BlockEntityOpenableContainer), nameof(BlockEntityOpenableContainer.OnReceivedServerPacket))]
    public static bool GenericContainer_OnReceivedPacket(BlockEntityOpenableContainer __instance, int packetid) 
        => !MergedInventory.OnServerPacket(__instance, packetid);
}
