using HarmonyLib;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace ChestOrganizer;
public class BlockHighlight : IRenderer {
    private readonly ICoreClientAPI api;
    private readonly ClientMain game;
    private readonly WireframeCube wireframe;
    private BlockEntity entity;
    private bool registered = false;

    public BlockHighlight(ICoreClientAPI api) {
        this.api = api;
        game = api.World as ClientMain;
        wireframe = WireframeCube.CreateUnitCube(api, ColorUtil.WhiteArgb);
    }

    public BlockEntity Entity { 
        get => entity; 
        set { 
            entity = value;
            if (value == null) {
                Unregister();
            } else if (!registered) {
                Register();
            }
        } 
    }

    public double RenderOrder => 0.91;
    public int    RenderRange => 24;

    private void Register() {
        api.Event.RegisterRenderer(this, EnumRenderStage.AfterFinalComposition);
        registered = true;
    }

    private void Unregister() {
        api.Event.UnregisterRenderer(this, EnumRenderStage.AfterFinalComposition);
        registered = false;
    }

    public void Dispose() {
        entity = null;
        Unregister();
        wireframe.Dispose();
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage) {
        if (entity == null) return;

        var pos = entity.Pos;
        float thickness = 1.6f * ClientSettings.Wireframethickness;
        var block = entity.Block;
        Vec4f color = new(0.0f, 0.8f, 0.8f, 0.6f);
        Cuboidf[] array = block.GetSelectionBoxes(game.BlockAccessor, pos);
        if (!(array?.Length > 0)) return;

        foreach (var box in array) {
            float xscale = box.XSize;
            float yscale = box.YSize;
            float zscale = box.ZSize;
            double x = pos.X + box.X1;
            double y = pos.Y + box.Y1;
            double z = pos.Z + box.Z1;
            wireframe.Render(api, x, y, z, xscale, yscale, zscale, thickness, color);
        }
    }
}
