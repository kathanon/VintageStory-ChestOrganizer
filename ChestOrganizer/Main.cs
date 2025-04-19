using HarmonyLib;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace ChestOrganizer;

using ChestList = List<BlockEntityGenericTypedContainer>;
public class Main : ModSystem {
    public const string ID = "chestorganizer";
    public const string Hotkey = ID + ".openall";

    private static Harmony harmony;

    private ICoreClientAPI api;
    private RoomRegistry roomSystem;
    private ModSystemBlockReinforcement reinforcementSystem;

    public override void StartPre(ICoreAPI api) 
        => (harmony ??= new Harmony(ID)).PatchAll();

    public override void Dispose() 
        => harmony?.UnpatchAll(ID);

    public override void StartClientSide(ICoreClientAPI api) {
        this.api = api;
        Patch_ChestDialog.Setup(api);
        Icons.Setup(api);
        
        roomSystem = api.ModLoader.GetModSystem<RoomRegistry>();
        reinforcementSystem = api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();

        api.Input.RegisterHotKey(
            Hotkey,
            Lang.Get("chestorganizer:openall"),
            GlKeys.R,
            HotkeyType.CharacterControls);
        api.Input.SetHotKeyHandler(Hotkey, OpenAll);
    }
    
    private bool HasLineOfSightTo(IPlayer player, Vec3d targetPoint)
    {
      Vec3d playerEyePos = player.Entity.Pos.XYZ.Add(player.Entity.LocalEyePos);

      // Define a more sophisticated block filter for line of sight
      BlockFilter blockFilter = (pos, block) => {
          // Blocks that are air don't need to be considered
          if (block == null || block.Id == 0)
              return false;
          
          // Target position is always visible
          if (pos.X == (int)targetPoint.X && pos.Y == (int)targetPoint.Y && pos.Z == (int)targetPoint.Z)
              return false;
          
          // Other containers don't block 
          if (block is BlockGenericTypedContainer)
              return false;

          // Allow seeing through transparent blocks
          if (block.RenderPass == EnumChunkRenderPass.Transparent ||
              block.RenderPass == EnumChunkRenderPass.BlendNoCull ||
              block.Replaceable >= 6000)
          {
              return false;
          }

          // If no collision boxes, allow seeing through
          if (block.CollisionBoxes == null || block.CollisionBoxes.Length == 0)
              return false;

          // Check collision box volume; if the volume is small (50% or less), allow seeing
          // through it. This handles chiseled blocks, furniture, fences, etc.
          var totalVolume = block.CollisionBoxes.Sum(box => (box.X2 - box.X1) * (box.Y2 - box.Y1) * (box.Z2 - box.Z1));
          if (totalVolume < 0.5f) // 50% threshold
              return false;

          // Block sight if it's a solid block with substantial collision
          return true;
      };

      // Perform the actual raycast
      var selection = player.Entity.World.InteresectionTester.GetSelectedBlock(
          playerEyePos,
          targetPoint,
          blockFilter
      );

      // If nothing blocks the ray or it's the target block itself
      return selection == null ||
             (selection.Position.X == (int)targetPoint.X &&
              selection.Position.Y == (int)targetPoint.Y &&
              selection.Position.Z == (int)targetPoint.Z);
  }
  

    public bool OpenAll(KeyCombination _) {
        api.Logger.Audit("Open all blocks");
        var player = api.World.Player;
        if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) return false;
        
        List<BlockEntityGenericTypedContainer> chests = new();
        var accessor = api.World.BlockAccessor;

        BlockPos startPos;
        BlockPos endPos;
        
        var eyePos = player.Entity.Pos.XYZ.Add(player.Entity.LocalEyePos - 0.5f);

        // If the player is in a room, use the room to bound scanning and skip the heavy
        // line of sight checking. If there is a column, wall, etc. in that room that
        // obscures the storage, we'll still be able to access it. 
        // 
        // If the player is NOT in a room, we use a range scan and line of sight checking
        // to determine what can be opened. This is both more costly and more unpredictable
        // when in crowded spaces - we are checking visibility to the center of the block,
        // so if the center of the block is just slightly out of view, it will not open it.
        var strictCheck = true;
        var room = roomSystem.GetRoomForPosition(player.Entity.Pos.AsBlockPos);
        if (room is { ExitCount: 0 })
        {
            api.Logger.Debug($"Scanning room for chests: {room} {room.ExitCount}");
            startPos = room.Location.Start.AsBlockPos;
            endPos = room.Location.End.AsBlockPos;
            strictCheck = false;
        }
        else
        {
            // Not in an enclosed room; use a ranged scan
            api.Logger.Debug("Scanning range for chests");
            var range = player.WorldData.PickingRange + 1;
            startPos = (eyePos - range).AsBlockPos;
            endPos = (eyePos + range + 1.0f).AsBlockPos;
        }
        
        // Now that we have our area to scan, do the scan - taking into account anything that
        // might be blocking the player's ability to interact with the storage
        var stopwatch = Stopwatch.StartNew();
        accessor.WalkBlocks(startPos, endPos, (block, x, y, z) =>
        {
            var blockPos = new BlockPos(x, y, z);

            // Don't bother with any blocks that aren't a container
            if (block is not BlockGenericTypedContainer) return;
            
            // Don't bother with any containers that are out of reach
            var blockCenter = new Vec3d(x + 0.5, y + 0.5, z + 0.5);
            if (player.Entity.Pos.DistanceTo(blockCenter) > 5.1) return;
            
            // Try multiple points on the container to check visibility
            if (strictCheck && !HasLineOfSightTo(player, blockCenter)) return;

            // Check that there is a block entity of correct type and that reinforcement system
            // permits access
            var entity = accessor.GetBlockEntity<BlockEntityGenericTypedContainer>(blockPos);
            bool locked = reinforcementSystem.IsLockedForInteract(blockPos, player);
            if (!locked && (!entity?.Inventory.HasOpened(player) ?? false))
            {
                chests.Add(entity);
            }
        });

        stopwatch.Stop();
        api.Logger.Debug($"Open all blocks finished in {stopwatch.ElapsedMilliseconds} - Strict mode: {strictCheck}");

        if (chests.Count > 0) MergedInventory.MergeRange(chests, api);
        return true;
    }
}
