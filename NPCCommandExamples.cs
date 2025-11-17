using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("NPCCommandExamples", "Copeman", "1.0.0")]
    [Description("Example commands for AdvancedHumanNPC")]
    class NPCCommandExamples : RustPlugin
    {
        [PluginReference]
        private Plugin AdvancedHumanNPC;

        void OnServerInitialized()
        {
            // Wait a moment for AdvancedHumanNPC to fully load
            timer.Once(2f, () => RegisterCommands());
        }

        void Unload()
        {
            // Clean up - unregister all commands
            if (AdvancedHumanNPC != null)
            {
                AdvancedHumanNPC.Call("UnregisterNPCCommand", "NPCCommandExamples", "heal");
                AdvancedHumanNPC.Call("UnregisterNPCCommand", "NPCCommandExamples", "inventory.giveto");
                AdvancedHumanNPC.Call("UnregisterNPCCommand", "NPCCommandExamples", "oxide.grant");
                AdvancedHumanNPC.Call("UnregisterNPCCommand", "NPCCommandExamples", "teleportpos");
            }
        }

        void RegisterCommands()
        {
            if (AdvancedHumanNPC == null)
            {
                PrintWarning("AdvancedHumanNPC plugin not found! Make sure it's loaded.");
                return;
            }

            // Example 1: Simple heal command - no parameters
            AdvancedHumanNPC.Call("RegisterNPCCommand",
                "NPCCommandExamples",              // Plugin name
                "heal {player.id}",                // Command to execute
                "Heal Player",                     // Display name
                "Fully heals the interacting player", // Description
                null                               // No parameters
            );

            // Example 2: Give items with customizable amount
            var giveParams = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "player",
                    ["type"] = "player",
                    ["default"] = "{player.id}",
                    ["placeholder"] = true
                },
                new Dictionary<string, object>
                {
                    ["name"] = "item",
                    ["type"] = "text",
                    ["default"] = "wood",
                    ["placeholder"] = false
                },
                new Dictionary<string, object>
                {
                    ["name"] = "amount",
                    ["type"] = "number",
                    ["default"] = "1000",
                    ["placeholder"] = false
                }
            };

            AdvancedHumanNPC.Call("RegisterNPCCommand",
                "NPCCommandExamples",
                "inventory.giveto",
                "Give Items",
                "Gives items to the player",
                giveParams
            );

            // Example 3: Grant permission
            var permParams = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "type",
                    ["type"] = "text",
                    ["default"] = "user",
                    ["placeholder"] = false
                },
                new Dictionary<string, object>
                {
                    ["name"] = "player",
                    ["type"] = "player",
                    ["default"] = "{player.id}",
                    ["placeholder"] = true
                },
                new Dictionary<string, object>
                {
                    ["name"] = "permission",
                    ["type"] = "text",
                    ["default"] = "vip.access",
                    ["placeholder"] = false
                }
            };

            AdvancedHumanNPC.Call("RegisterNPCCommand",
                "NPCCommandExamples",
                "oxide.grant",
                "Grant Permission",
                "Grants a permission to the player",
                permParams
            );

            // Example 4: Teleport to coordinates
            var tpParams = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["name"] = "player",
                    ["type"] = "player",
                    ["default"] = "{player.name}",
                    ["placeholder"] = true
                },
                new Dictionary<string, object>
                {
                    ["name"] = "x",
                    ["type"] = "number",
                    ["default"] = "0",
                    ["placeholder"] = false
                },
                new Dictionary<string, object>
                {
                    ["name"] = "y",
                    ["type"] = "number",
                    ["default"] = "50",
                    ["placeholder"] = false
                },
                new Dictionary<string, object>
                {
                    ["name"] = "z",
                    ["type"] = "number",
                    ["default"] = "0",
                    ["placeholder"] = false
                }
            };

            AdvancedHumanNPC.Call("RegisterNPCCommand",
                "NPCCommandExamples",
                "teleportpos",
                "Teleport to Position",
                "Teleports player to specific coordinates",
                tpParams
            );

            Puts($"Successfully registered 4 commands with AdvancedHumanNPC!");
        }
    }
}