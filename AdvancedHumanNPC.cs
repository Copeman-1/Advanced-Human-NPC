using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("AdvancedHumanNPC", "Copeman", "1.0.0")]
    [Description("Advanced Human NPC management with UI")]
    class AdvancedHumanNPC : RustPlugin
    {
       #region Fields
private const string PermissionUse = "advancedhumannpc.use";
private const string PermissionAdmin = "advancedhumannpc.admin";
private Dictionary<ulong, NPCData> activeNPCs = new Dictionary<ulong, NPCData>();
private Dictionary<ulong, string> openUIs = new Dictionary<ulong, string>();
private Dictionary<string, RegisteredCommand> registeredCommands = new Dictionary<string, RegisteredCommand>();
private Dictionary<ulong, HashSet<string>> expandedPlugins = new Dictionary<ulong, HashSet<string>>();
[PluginReference]
private Plugin Kits;
#endregion
        #region Data Classes
        public class NPCData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public float Health { get; set; } = 100f;
            public SerializedVector3 Position { get; set; }
            public SerializedVector3 Rotation { get; set; }
            public bool Hostile { get; set; }
            public bool AlwaysHostile { get; set; }
            public bool Invulnerable { get; set; }
            public bool Lootable { get; set; } = true;
            public float Radius { get; set; } = 10f;
            public float AttackDistance { get; set; } = 30f;
            public float MaxDistance { get; set; } = 40f;
            public float Speed { get; set; } = 5f;
            public float DamageAmount { get; set; } = 10f;
            public float DamageDistance { get; set; } = 5f;
            public float DamageInterval { get; set; } = 2f;
            public string Kit { get; set; } = "";
            public bool Respawn { get; set; } = true;
            public float RespawnTime { get; set; } = 60f;
            public List<string> HelloMessages { get; set; } = new List<string>();
            public List<string> ByeMessages { get; set; } = new List<string>();
            public List<string> UseMessages { get; set; } = new List<string>();
            public List<string> HurtMessages { get; set; } = new List<string>();
            public List<string> KillMessages { get; set; } = new List<string>();
            public bool AllowSit { get; set; }
            public bool AllowRide { get; set; }
            public bool NeedsAmmo { get; set; }
            public bool DropWeapon { get; set; }
            public List<SerializedVector3> Waypoints { get; set; } = new List<SerializedVector3>();
            public ulong ActiveEntityId { get; set; }
			public bool AllowInteraction { get; set; } = false;
            public float InteractionDistance { get; set; } = 3f;
            public string InteractionText { get; set; } = "Press E to interact";
            public List<string> InteractionCommands { get; set; } = new List<string>();
        }

        public class SerializedVector3
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }

            public Vector3 ToVector3() => new Vector3(X, Y, Z);
            public static SerializedVector3 FromVector3(Vector3 v) => new SerializedVector3 { X = v.x, Y = v.y, Z = v.z };
        }

        public class StoredData
        {
            public Dictionary<string, NPCData> NPCs = new Dictionary<string, NPCData>();
        }

        private StoredData storedData;
		
		public class RegisteredCommand
{
    public string PluginName { get; set; }
    public string CommandName { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public List<CommandParameter> Parameters { get; set; } = new List<CommandParameter>();
}

public class CommandParameter
{
    public string Name { get; set; }
    public string Type { get; set; } // "player", "text", "number"
    public string DefaultValue { get; set; }
    public bool IsPlaceholder { get; set; } // If true, will be replaced with player data
}


        #endregion

        
	#region Hooks
void Init()
{
    permission.RegisterPermission(PermissionUse, this);
    permission.RegisterPermission(PermissionAdmin, this);
    LoadData();
}

void OnServerInitialized()
{
    if (Kits == null)
    {
        PrintWarning("Kits plugin not found! Kit functionality will be disabled.");
    }
    else
    {
        Puts("Kits plugin found! Kit selection enabled.");
    }
    SpawnAllNPCs();
}

void Unload()
{
    foreach (var playerId in openUIs.Keys.ToList())
    {
        var player = BasePlayer.FindByID(playerId);
        if (player != null)
            DestroyUI(player);
    }
    DespawnAllNPCs();
}

object OnEntityTakeDamage(BasePlayer npc, HitInfo info)
{
    if (npc == null || info == null) return null;
    
    NPCData npcData;
    if (activeNPCs.TryGetValue(npc.net.ID.Value, out npcData))
    {
        if (npcData.Invulnerable)
        {
            return true;
        }
    }
    return null;
}

string OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
{
    if (activeNPCs.ContainsKey(player.net.ID.Value))
        return null;
    
    return null;
}

object OnPlayerDeath(BasePlayer npc, HitInfo info)
{
    if (npc == null) return null;

    NPCData npcData;
    if (activeNPCs.TryGetValue(npc.net.ID.Value, out npcData))
    {
        if (npcData.Respawn)
        {
            timer.Once(npcData.RespawnTime, () => SpawnNPC(npcData));
        }
        return null;
    }
    return null;
}

object OnPlayerInput(BasePlayer player, InputState input)
{
    if (player == null || input == null) return null;
    
    // Check if player pressed USE button (E key)
    if (input.WasJustPressed(BUTTON.USE))
    {
        // Find nearest NPC within interaction distance
        foreach (var kvp in activeNPCs)
        {
            var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(kvp.Key));
            if (entity == null || entity.IsDestroyed) continue;
            
            var npcPlayer = entity as BasePlayer;
            if (npcPlayer == null) continue;
            
            var npcData = kvp.Value;
            if (!npcData.AllowInteraction) continue;
            
            float distance = Vector3.Distance(player.transform.position, npcPlayer.transform.position);
            
            if (distance <= npcData.InteractionDistance)
            {
                // Execute all commands
                if (npcData.InteractionCommands != null && npcData.InteractionCommands.Count > 0)
                {
                    foreach (var cmd in npcData.InteractionCommands)
                    {
                        if (string.IsNullOrEmpty(cmd)) continue;
                        
                        // Replace placeholders
                        string processedCmd = cmd
                            .Replace("{player.id}", player.UserIDString)
                            .Replace("{player.name}", player.displayName)
                            .Replace("{npc.name}", npcData.Name)
                            .Replace("{npc.id}", npcData.Id);
                        
                        // Check if it's a say/chat command to make NPC speak
                        if (processedCmd.StartsWith("say "))
                        {
                            string message = processedCmd.Substring(4); // Remove "say "
                            SendNPCChat(npcPlayer, npcData.Name, message);
                        }
                        else if (processedCmd.StartsWith("chat.say "))
                        {
                            string message = processedCmd.Substring(9); // Remove "chat.say "
                            SendNPCChat(npcPlayer, npcData.Name, message);
                        }
                        else
                        {
                            // Execute as regular command
                            rust.RunServerCommand(processedCmd);
                        }
                    }
                }
                else
                {
                    SendReply(player, $"Interacted with {npcData.Name} (No commands configured)");
                }
                
                return true; // Block the USE action
            }
        }
    }
    
    return null;
}

void SendNPCChat(BasePlayer npc, string npcName, string message)
{
    if (npc == null || string.IsNullOrEmpty(message)) return;
    
    // Broadcast chat message to all players with NPC's name
    Server.Broadcast(message, npcName, npc.userID);
}
#endregion
       #region Plugin API
/// <summary>
/// Allows other plugins to register commands for NPCs
/// </summary>
/// <param name="pluginName">Name of the plugin registering the command</param>
/// <param name="commandName">The actual command to execute</param>
/// <param name="displayName">Friendly name shown in UI</param>
/// <param name="description">Description of what the command does</param>
/// <param name="parameters">List of parameters (optional)</param>
[HookMethod("RegisterNPCCommand")]
public bool RegisterNPCCommand(string pluginName, string commandName, string displayName, string description, List<object> parameters = null)
{
    if (string.IsNullOrEmpty(commandName))
    {
        PrintWarning($"Cannot register command with empty command name from {pluginName}");
        return false;
    }

    var cmd = new RegisteredCommand
    {
        PluginName = pluginName,
        CommandName = commandName,
        DisplayName = displayName,
        Description = description
    };

    if (parameters != null)
    {
        foreach (var param in parameters)
        {
            if (param is Dictionary<string, object> dict)
            {
                cmd.Parameters.Add(new CommandParameter
                {
                    Name = dict.ContainsKey("name") ? dict["name"].ToString() : "",
                    Type = dict.ContainsKey("type") ? dict["type"].ToString() : "text",
                    DefaultValue = dict.ContainsKey("default") ? dict["default"].ToString() : "",
                    IsPlaceholder = dict.ContainsKey("placeholder") && (bool)dict["placeholder"]
                });
            }
        }
    }

    // Use the first word of command as the key identifier instead of full command
    string commandIdentifier = commandName.Split(' ')[0];
    string key = $"{pluginName}_{commandIdentifier}";
    registeredCommands[key] = cmd;
    Puts($"Registered command: {displayName} from {pluginName} with key: {key}");
    return true;
}

/// <summary>
/// Unregister a command
/// </summary>
[HookMethod("UnregisterNPCCommand")]
public bool UnregisterNPCCommand(string pluginName, string commandName)
{
    string key = $"{pluginName}_{commandName}";
    if (registeredCommands.ContainsKey(key))
    {
        registeredCommands.Remove(key);
        Puts($"Unregistered command: {commandName} from {pluginName}");
        return true;
    }
    return false;
}

/// <summary>
/// Get all registered commands (for UI display)
/// </summary>
[HookMethod("GetRegisteredCommands")]
public List<RegisteredCommand> GetRegisteredCommands()
{
    return registeredCommands.Values.ToList();
}

/// <summary>
/// Build a command string with parameters filled in
/// </summary>
string BuildCommand(RegisteredCommand cmd, BasePlayer player, NPCData npc)
{
    string command = cmd.CommandName;
    
    foreach (var param in cmd.Parameters)
    {
        string value = param.DefaultValue;
        
        if (param.IsPlaceholder)
        {
            value = value
                .Replace("{player.id}", player.UserIDString)
                .Replace("{player.name}", player.displayName)
                .Replace("{npc.name}", npc.Name)
                .Replace("{npc.id}", npc.Id);
        }
        
        command += $" {value}";
    }
    
    return command;
}
#endregion

        #region Commands
        [ChatCommand("npc")]
        void NPCCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            if (args.Length == 0)
            {
                ShowMainUI(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "create":
                    CreateNPCAtPlayer(player, args.Length > 1 ? string.Join(" ", args.Skip(1)) : "New NPC");
                    break;
                case "delete":
                    if (args.Length > 1)
                        DeleteNPC(player, args[1]);
                    break;
                case "list":
                    ListNPCs(player);
                    break;
                case "tp":
                    if (args.Length > 1)
                        TeleportToNPC(player, args[1]);
                    break;
                default:
                    SendReply(player, "Usage: /npc [create|delete|list|tp] [name/id]");
                    break;
            }
        }
        #endregion

        #region NPC Management
        void CreateNPCAtPlayer(BasePlayer player, string name)
        {
            var npcData = new NPCData
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Position = SerializedVector3.FromVector3(player.transform.position),
                Rotation = SerializedVector3.FromVector3(player.viewAngles)
            };

            storedData.NPCs[npcData.Id] = npcData;
            SaveData();
            SpawnNPC(npcData);
            SendReply(player, $"Created NPC '{name}' with ID: {npcData.Id}");
        }

        void SpawnNPC(NPCData data)
{
    var position = data.Position.ToVector3();
    var rotation = Quaternion.Euler(data.Rotation.ToVector3());
    
    var npc = (BasePlayer)GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", position, rotation);
    
    if (npc == null) return;

    ulong npcUserId = 76561198000000000uL + (ulong)Math.Abs(data.Id.GetHashCode());
    npc.userID = npcUserId;
    
    npc.Spawn();
    
    npc.UserIDString = npcUserId.ToString();
    
    npc.displayName = data.Name;
    npc._name = data.Name;
    
    npc.startHealth = data.Health;
    npc.health = data.Health;
    npc.InitializeHealth(data.Health, data.Health);
    
    data.ActiveEntityId = npc.net.ID.Value;
    activeNPCs[npc.net.ID.Value] = data;

    if (data.Invulnerable)
    {
        npc.baseProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
    }

    // Add interaction text if enabled
    if (data.AllowInteraction && !string.IsNullOrEmpty(data.InteractionText))
    {
        npc.SetInfo("interaction", data.InteractionText);
    }

    if (!string.IsNullOrEmpty(data.Kit) && Kits != null)
    {
        timer.Once(1f, () =>
        {
            if (npc == null || npc.IsDestroyed) return;
            
            if (npc.userID == 0)
            {
                npc.userID = npcUserId;
            }
            if (string.IsNullOrEmpty(npc.UserIDString))
            {
                npc.UserIDString = npcUserId.ToString();
            }
            
            permission.GrantUserPermission(npc.UserIDString, $"kits.{data.Kit.ToLower()}", this);
            
            Kits.Call("SetPlayerCooldown", npc.userID, data.Kit, 0);
            Kits.Call("SetPlayerKitUses", npc.userID, data.Kit, 0);
            
            var result = Kits.Call("GiveKit", npc, data.Kit);
            
            if (result == null || (result is bool && !(bool)result))
            {
                Puts($"Regular GiveKit failed, trying TryClaimKit...");
                result = Kits.Call("TryClaimKit", npc, data.Kit, false);
            }
            
            if (result == null)
            {
                PrintWarning($"Failed to give kit '{data.Kit}' to NPC '{data.Name}' - Check kit configuration");
            }
            else if (result is bool && !(bool)result)
            {
                PrintWarning($"Failed to give kit '{data.Kit}' to NPC '{data.Name}' - Kit may have Economy cost or requirements");
            }
            else
            {
                Puts($"Successfully gave kit '{data.Kit}' to NPC '{data.Name}'");
            }
        });
    }

    npc.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
}

        void DespawnNPC(ulong entityId)
        {
            if (entityId == 0) return;
            
            var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(entityId));
            if (entity != null && !entity.IsDestroyed)
            {
                entity.Kill();
            }
            
            activeNPCs.Remove(entityId);
        }

        void SpawnAllNPCs()
        {
            foreach (var npcData in storedData.NPCs.Values)
            {
                SpawnNPC(npcData);
            }
        }

        void DespawnAllNPCs()
        {
            foreach (var entityId in activeNPCs.Keys.ToList())
            {
                DespawnNPC(entityId);
            }
        }

        void DeleteNPC(BasePlayer player, string idOrName)
        {
            var npc = storedData.NPCs.Values.FirstOrDefault(n => n.Id == idOrName || n.Name.Contains(idOrName));
            if (npc == null)
            {
                SendReply(player, "NPC not found.");
                return;
            }

            if (npc.ActiveEntityId != 0)
            {
                DespawnNPC(npc.ActiveEntityId);
            }
            
            storedData.NPCs.Remove(npc.Id);
            SaveData();
            SendReply(player, $"Deleted NPC '{npc.Name}'");
        }

        void ListNPCs(BasePlayer player)
        {
            if (storedData.NPCs.Count == 0)
            {
                SendReply(player, "No NPCs found.");
                return;
            }

            SendReply(player, "=== NPC List ===");
            foreach (var npc in storedData.NPCs.Values)
            {
                SendReply(player, $"- {npc.Name} (ID: {npc.Id}) - Health: {npc.Health}");
            }
        }

        void TeleportToNPC(BasePlayer player, string idOrName)
        {
            var npc = storedData.NPCs.Values.FirstOrDefault(n => n.Id == idOrName || n.Name.Contains(idOrName));
            if (npc == null)
            {
                SendReply(player, "NPC not found.");
                return;
            }

            player.Teleport(npc.Position.ToVector3());
            SendReply(player, $"Teleported to NPC '{npc.Name}'");
        }
        #endregion

   #region UI
void ShowMainUI(BasePlayer player)
{
    DestroyUI(player);

    var container = new CuiElementContainer();
    
    // Main panel directly on Overlay
    var mainPanel = container.Add(new CuiPanel
    {
        Image = { Color = "0.15 0.15 0.18 0.98" },
        RectTransform = { AnchorMin = "0.25 0.15", AnchorMax = "0.75 0.85" },
        CursorEnabled = true
    }, "Overlay", "NPCMainPanel");

    // Top accent bar
    container.Add(new CuiPanel
    {
        Image = { Color = "0.26 0.51 0.82 1" },
        RectTransform = { AnchorMin = "0 0.96", AnchorMax = "1 1" }
    }, mainPanel);

    // Header with icon
    container.Add(new CuiLabel
    {
        Text = { Text = "⚙ ADVANCED NPC MANAGER", FontSize = 26, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
        RectTransform = { AnchorMin = "0 0.91", AnchorMax = "1 0.96" }
    }, mainPanel);

    // Subtitle
    container.Add(new CuiLabel
    {
        Text = { Text = $"Managing {storedData.NPCs.Count} NPCs", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.7 0.7 0.7 1" },
        RectTransform = { AnchorMin = "0 0.88", AnchorMax = "1 0.91" }
    }, mainPanel);

    // Close button (styled)
    container.Add(new CuiButton
    {
        Button = { Command = "npcui.close", Color = "0.8 0.2 0.2 0.9" },
        RectTransform = { AnchorMin = "0.94 0.93", AnchorMax = "0.99 0.99" },
        Text = { Text = "✕", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, mainPanel);

    // Create NPC button (gradient style)
    container.Add(new CuiButton
    {
        Button = { Command = "npcui.create", Color = "0.2 0.7 0.3 0.9" },
        RectTransform = { AnchorMin = "0.03 0.82", AnchorMax = "0.2 0.87" },
        Text = { Text = "➕ CREATE NPC", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, mainPanel);

    // List header background
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.12 1" },
        RectTransform = { AnchorMin = "0.03 0.75", AnchorMax = "0.97 0.80" }
    }, mainPanel, "ListHeader");

    // Column headers
    container.Add(new CuiLabel
    {
        Text = { Text = "NAME", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
        RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.25 1" }
    }, "ListHeader");

    container.Add(new CuiLabel
    {
        Text = { Text = "DETAILS", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
        RectTransform = { AnchorMin = "0.27 0", AnchorMax = "0.7 1" }
    }, "ListHeader");

    container.Add(new CuiLabel
    {
        Text = { Text = "ACTIONS", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
        RectTransform = { AnchorMin = "0.72 0", AnchorMax = "0.98 1" }
    }, "ListHeader");

    // NPC List
    float yPos = 0.73f;
    int index = 0;
    foreach (var npc in storedData.NPCs.Values.Take(10))
    {
        // Card background
        var npcPanel = container.Add(new CuiPanel
        {
            Image = { Color = index % 2 == 0 ? "0.2 0.2 0.22 0.95" : "0.18 0.18 0.2 0.95" },
            RectTransform = { AnchorMin = $"0.03 {yPos - 0.065}", AnchorMax = $"0.97 {yPos}" }
        }, mainPanel);

        // Left border accent
        container.Add(new CuiPanel
        {
            Image = { Color = npc.Hostile ? "0.8 0.3 0.3 1" : (npc.AllowInteraction ? "0.3 0.7 0.8 1" : "0.5 0.5 0.5 1") },
            RectTransform = { AnchorMin = "0 0", AnchorMax = "0.008 1" }
        }, npcPanel);

        // NPC Name with icon
        string icon = npc.Hostile ? "⚔" : (npc.AllowInteraction ? "💬" : "👤");
        container.Add(new CuiLabel
        {
            Text = { Text = $"{icon} {npc.Name}", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
            RectTransform = { AnchorMin = "0.03 0", AnchorMax = "0.28 1" }
        }, npcPanel);

        // Details with badges
        string hostileText = npc.Hostile ? "<color=#ff6b6b>Hostile</color>" : "<color=#6bff6b>Peaceful</color>";
        string kitText = string.IsNullOrEmpty(npc.Kit) ? "<color=#888>No Kit</color>" : $"<color=#4ecdc4>Kit: {npc.Kit}</color>";
        container.Add(new CuiLabel
        {
            Text = { Text = $"HP: {npc.Health} | {hostileText} | {kitText}", FontSize = 11, Align = TextAnchor.MiddleLeft, Color = "0.9 0.9 0.9 1" },
            RectTransform = { AnchorMin = "0.3 0", AnchorMax = "0.7 1" }
        }, npcPanel);

        // Edit button (blue)
        container.Add(new CuiButton
        {
            Button = { Command = $"npcui.edit {npc.Id}", Color = "0.26 0.51 0.82 0.9" },
            RectTransform = { AnchorMin = "0.73 0.2", AnchorMax = "0.84 0.8" },
            Text = { Text = "✎ Edit", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
        }, npcPanel);

        // Delete button (red)
        container.Add(new CuiButton
        {
            Button = { Command = $"npcui.delete {npc.Id}", Color = "0.8 0.25 0.25 0.9" },
            RectTransform = { AnchorMin = "0.86 0.2", AnchorMax = "0.97 0.8" },
            Text = { Text = "🗑 Delete", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
        }, npcPanel);

        yPos -= 0.07f;
        index++;
    }

    // Empty state message
    if (storedData.NPCs.Count == 0)
    {
        container.Add(new CuiLabel
        {
            Text = { Text = "No NPCs created yet.\nClick 'CREATE NPC' to get started!", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" },
            RectTransform = { AnchorMin = "0.3 0.35", AnchorMax = "0.7 0.45" }
        }, mainPanel);
    }

    // Footer
    container.Add(new CuiLabel
    {
        Text = { Text = "TIP: Hover over an NPC and press 'E' if interaction is enabled", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.5 0.5 0.5 1" },
        RectTransform = { AnchorMin = "0 0.01", AnchorMax = "1 0.04" }
    }, mainPanel);

    CuiHelper.AddUi(player, container);
    openUIs[player.userID] = "NPCMainPanel";
}
void ShowEditUI(BasePlayer player, string npcId)
{
    DestroyUI(player);

    if (!storedData.NPCs.ContainsKey(npcId))
    {
        SendReply(player, "NPC not found.");
        return;
    }

    var npc = storedData.NPCs[npcId];
    var container = new CuiElementContainer();

    // Background overlay
    container.Add(new CuiPanel
    {
        Image = { Color = "0 0 0 0.8" },
        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
        CursorEnabled = true
    }, "Overlay", "NPCEditBackdrop");

    // Main panel
    var mainPanel = container.Add(new CuiPanel
    {
        Image = { Color = "0.15 0.15 0.18 0.98" },
        RectTransform = { AnchorMin = "0.2 0.08", AnchorMax = "0.8 0.92" },
        CursorEnabled = true
    }, "NPCEditBackdrop", "NPCEditPanel");

    // Top accent bar
    container.Add(new CuiPanel
    {
        Image = { Color = "0.26 0.51 0.82 1" },
        RectTransform = { AnchorMin = "0 0.97", AnchorMax = "1 1" }
    }, mainPanel);

    // Header
    container.Add(new CuiLabel
    {
        Text = { Text = $"✎ EDITING: {npc.Name}", FontSize = 22, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
        RectTransform = { AnchorMin = "0 0.94", AnchorMax = "1 0.97" }
    }, mainPanel);

    // Back button
    container.Add(new CuiButton
    {
        Button = { Command = "npcui.back", Color = "0.3 0.3 0.35 0.9" },
        RectTransform = { AnchorMin = "0.02 0.95", AnchorMax = "0.12 0.99" },
        Text = { Text = "← Back", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, mainPanel);

    float yPos = 0.90f;
    float spacing = 0.075f;

    // LEFT COLUMN - Basic Settings
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.12 0.8" },
        RectTransform = { AnchorMin = "0.02 0.12", AnchorMax = "0.48 0.92" }
    }, mainPanel, "LeftPanel");

    container.Add(new CuiLabel
    {
        Text = { Text = "⚙ BASIC SETTINGS", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.26 0.51 0.82 1" },
        RectTransform = { AnchorMin = "0.03 0.94", AnchorMax = "0.97 0.98" }
    }, "LeftPanel");

    float leftYPos = 0.90f;
    AddStyledInputField(container, "LeftPanel", "Name:", npc.Name, $"npcui.update {npcId} name", ref leftYPos, spacing);
    AddStyledInputField(container, "LeftPanel", "Health:", npc.Health.ToString(), $"npcui.update {npcId} health", ref leftYPos, spacing);
    AddStyledInputField(container, "LeftPanel", "Speed:", npc.Speed.ToString(), $"npcui.update {npcId} speed", ref leftYPos, spacing);

    AddStyledCheckbox(container, "LeftPanel", "Hostile:", npc.Hostile, $"npcui.toggle {npcId} hostile", ref leftYPos, spacing);
    AddStyledCheckbox(container, "LeftPanel", "Invulnerable:", npc.Invulnerable, $"npcui.toggle {npcId} invulnerable", ref leftYPos, spacing);
    AddStyledCheckbox(container, "LeftPanel", "Respawn:", npc.Respawn, $"npcui.toggle {npcId} respawn", ref leftYPos, spacing);

    // Interaction Section
    leftYPos -= 0.03f;
    container.Add(new CuiLabel
    {
        Text = { Text = "💬 INTERACTION", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.26 0.51 0.82 1" },
        RectTransform = { AnchorMin = $"0.03 {leftYPos - 0.03}", AnchorMax = $"0.97 {leftYPos}" }
    }, "LeftPanel");
    leftYPos -= 0.05f;

    AddStyledCheckbox(container, "LeftPanel", "Allow Interaction:", npc.AllowInteraction, $"npcui.toggle {npcId} interaction", ref leftYPos, spacing);
    AddStyledInputField(container, "LeftPanel", "Interaction Distance:", npc.InteractionDistance.ToString(), $"npcui.update {npcId} interactdist", ref leftYPos, spacing);
    AddStyledInputField(container, "LeftPanel", "Interaction Text:", npc.InteractionText, $"npcui.update {npcId} interacttext", ref leftYPos, spacing);

    // Commands button (styled)
    container.Add(new CuiButton
    {
        Button = { Command = $"npcui.editcommands {npcId}", Color = "0.26 0.51 0.82 0.9" },
        RectTransform = { AnchorMin = $"0.03 {leftYPos - spacing}", AnchorMax = $"0.97 {leftYPos - 0.01}" },
        Text = { Text = $"📋 Edit Commands ({npc.InteractionCommands.Count})", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, "LeftPanel");

    // RIGHT COLUMN - Kit Selection
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.12 0.8" },
        RectTransform = { AnchorMin = "0.52 0.12", AnchorMax = "0.98 0.92" }
    }, mainPanel, "RightPanel");

    container.Add(new CuiLabel
    {
        Text = { Text = "🎒 KIT SELECTION", FontSize = 16, Align = TextAnchor.MiddleLeft, Color = "0.26 0.51 0.82 1" },
        RectTransform = { AnchorMin = "0.03 0.94", AnchorMax = "0.97 0.98" }
    }, "RightPanel");

    // Current kit badge
    string currentKitDisplay = string.IsNullOrEmpty(npc.Kit) ? "None" : npc.Kit;
    string kitColor = string.IsNullOrEmpty(npc.Kit) ? "0.5 0.5 0.5" : "0.3 0.7 0.3";
    
    container.Add(new CuiPanel
    {
        Image = { Color = $"{kitColor} 0.3" },
        RectTransform = { AnchorMin = "0.03 0.88", AnchorMax = "0.97 0.92" }
    }, "RightPanel", "CurrentKitBadge");

    container.Add(new CuiLabel
    {
        Text = { Text = $"Current: {currentKitDisplay}", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
    }, "CurrentKitBadge");

    if (Kits != null)
    {
        var kits = Kits.Call("GetAllKits") as string[];
        if (kits != null && kits.Length > 0)
        {
            // Kit list scrollable area
            var kitListPanel = container.Add(new CuiPanel
            {
                Image = { Color = "0.05 0.05 0.07 0.9" },
                RectTransform = { AnchorMin = "0.03 0.14", AnchorMax = "0.97 0.86" }
            }, "RightPanel");

            float kitBtnY = 0.97f;
            int maxKits = 15;
            for (int i = 0; i < Math.Min(kits.Length, maxKits); i++)
            {
                var kitName = kits[i];
                bool isSelected = npc.Kit == kitName;

                container.Add(new CuiButton
                {
                    Button = { Command = $"npcui.selectkit {npcId} {kitName}", Color = isSelected ? "0.2 0.7 0.3 0.9" : "0.25 0.25 0.28 0.9" },
                    RectTransform = { AnchorMin = $"0.02 {kitBtnY - 0.055}", AnchorMax = $"0.98 {kitBtnY}" },
                    Text = { Text = isSelected ? $"✓ {kitName}" : kitName, FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                }, kitListPanel);

                kitBtnY -= 0.06f;
            }

            if (kits.Length > maxKits)
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = $"... and {kits.Length - maxKits} more kits", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" },
                    RectTransform = { AnchorMin = $"0.02 {kitBtnY - 0.04}", AnchorMax = $"0.98 {kitBtnY}" }
                }, kitListPanel);
            }
        }
        else
        {
            container.Add(new CuiLabel
            {
                Text = { Text = "📦\n\nNo kits available", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" },
                RectTransform = { AnchorMin = "0.03 0.4", AnchorMax = "0.97 0.5" }
            }, "RightPanel");
        }

        // Clear kit button
        container.Add(new CuiButton
        {
            Button = { Command = $"npcui.clearkit {npcId}", Color = "0.7 0.3 0.3 0.9" },
            RectTransform = { AnchorMin = "0.03 0.08", AnchorMax = "0.48 0.12" },
            Text = { Text = "✕ Clear Kit", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
        }, "RightPanel");
    }
    else
    {
        container.Add(new CuiLabel
        {
            Text = { Text = "⚠\n\nKits plugin not found!\nInstall the Kits plugin\nto use this feature.", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.8 0.4 0.4 1" },
            RectTransform = { AnchorMin = "0.03 0.4", AnchorMax = "0.97 0.6" }
        }, "RightPanel");
    }

    // Save & Respawn button (bottom, full width, prominent)
    container.Add(new CuiButton
    {
        Button = { Command = $"npcui.save {npcId}", Color = "0.2 0.7 0.3 0.95" },
        RectTransform = { AnchorMin = "0.52 0.02", AnchorMax = "0.98 0.09" },
        Text = { Text = "💾 SAVE & RESPAWN NPC", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, mainPanel);

    CuiHelper.AddUi(player, container);
    openUIs[player.userID] = "NPCEditPanel";
}
void ShowCommandsUI(BasePlayer player, string npcId)
{
    DestroyUI(player);

    if (!storedData.NPCs.ContainsKey(npcId))
    {
        SendReply(player, "NPC not found.");
        return;
    }

    var npc = storedData.NPCs[npcId];
    var container = new CuiElementContainer();

    // Background overlay
    container.Add(new CuiPanel
    {
        Image = { Color = "0 0 0 0.8" },
        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
        CursorEnabled = true
    }, "Overlay", "NPCCommandsBackdrop");

    var mainPanel = container.Add(new CuiPanel
    {
        Image = { Color = "0.15 0.15 0.18 0.98" },
        RectTransform = { AnchorMin = "0.25 0.15", AnchorMax = "0.75 0.85" },
        CursorEnabled = true
    }, "NPCCommandsBackdrop", "NPCCommandsPanel");

    // Top accent bar
    container.Add(new CuiPanel
    {
        Image = { Color = "0.26 0.51 0.82 1" },
        RectTransform = { AnchorMin = "0 0.96", AnchorMax = "1 1" }
    }, mainPanel);

    // Header
    container.Add(new CuiLabel
    {
        Text = { Text = $"📋 INTERACTION COMMANDS", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
        RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 0.96" }
    }, mainPanel);

    container.Add(new CuiLabel
    {
        Text = { Text = $"NPC: {npc.Name}", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "0.7 0.7 0.7 1" },
        RectTransform = { AnchorMin = "0 0.89", AnchorMax = "1 0.92" }
    }, mainPanel);

    // Back button
    container.Add(new CuiButton
    {
        Button = { Command = $"npcui.edit {npcId}", Color = "0.3 0.3 0.35 0.9" },
        RectTransform = { AnchorMin = "0.02 0.93", AnchorMax = "0.15 0.98" },
        Text = { Text = "← Back", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, mainPanel);

    // Info panel
    container.Add(new CuiPanel
    {
        Image = { Color = "0.2 0.4 0.6 0.2" },
        RectTransform = { AnchorMin = "0.05 0.82", AnchorMax = "0.95 0.88" }
    }, mainPanel, "InfoPanel");

    container.Add(new CuiLabel
    {
        Text = { Text = "💡 Commands execute when player presses E near NPC\n🔧 Placeholders: {player.id} {player.name} {npc.name} {npc.id}", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.9 0.9 0.9 1" },
        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
    }, "InfoPanel");

    // Add command button
    container.Add(new CuiButton
    {
        Button = { Command = $"npcui.addcommand {npcId}", Color = "0.2 0.7 0.3 0.9" },
        RectTransform = { AnchorMin = "0.05 0.75", AnchorMax = "0.3 0.81" },
        Text = { Text = "➕ Add Command", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, mainPanel);

    // Plugin Commands button
    container.Add(new CuiButton
    {
        Button = { Command = $"npcui.showtemplates {npcId}", Color = "0.4 0.26 0.82 0.9" },
        RectTransform = { AnchorMin = "0.32 0.75", AnchorMax = "0.58 0.81" },
        Text = { Text = $"🔌 Plugin Commands ({registeredCommands.Count})", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, mainPanel);

    // Command count badge
    container.Add(new CuiLabel
    {
        Text = { Text = $"Total: {npc.InteractionCommands.Count} command(s)", FontSize = 11, Align = TextAnchor.MiddleRight, Color = "0.7 0.7 0.7 1" },
        RectTransform = { AnchorMin = "0.7 0.75", AnchorMax = "0.95 0.81" }
    }, mainPanel);

    // Command list area
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.12 0.8" },
        RectTransform = { AnchorMin = "0.05 0.08", AnchorMax = "0.95 0.73" }
    }, mainPanel, "CommandListArea");

    float yPos = 0.95f;
    for (int i = 0; i < npc.InteractionCommands.Count; i++)
    {
        // Command card
        var cmdPanel = container.Add(new CuiPanel
        {
            Image = { Color = i % 2 == 0 ? "0.18 0.18 0.2 0.95" : "0.2 0.2 0.22 0.95" },
            RectTransform = { AnchorMin = $"0.02 {yPos - 0.11}", AnchorMax = $"0.98 {yPos}" }
        }, "CommandListArea");

        // Command number badge
        container.Add(new CuiPanel
        {
            Image = { Color = "0.26 0.51 0.82 0.9" },
            RectTransform = { AnchorMin = "0.01 0.3", AnchorMax = "0.08 0.7" }
        }, cmdPanel, $"CmdBadge{i}");

        container.Add(new CuiLabel
        {
            Text = { Text = $"{i + 1}", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
        }, $"CmdBadge{i}");

        // Command text input with background
        container.Add(new CuiPanel
        {
            Image = { Color = "0.08 0.08 0.1 0.9" },
            RectTransform = { AnchorMin = "0.1 0.15", AnchorMax = "0.83 0.85" }
        }, cmdPanel, $"InputBg{i}");

        container.Add(new CuiElement
        {
            Parent = $"InputBg{i}",
            Components =
            {
                new CuiInputFieldComponent
                {
                    Text = npc.InteractionCommands[i],
                    FontSize = 11,
                    Align = TextAnchor.MiddleLeft,
                    Command = $"npcui.updatecommand {npcId} {i} ",
                    Color = "0.9 0.9 0.9 1"
                },
                new CuiRectTransformComponent { AnchorMin = "0.02 0.1", AnchorMax = "0.98 0.9" }
            }
        });

        // Delete button
        container.Add(new CuiButton
        {
            Button = { Command = $"npcui.deletecommand {npcId} {i}", Color = "0.8 0.25 0.25 0.9" },
            RectTransform = { AnchorMin = "0.86 0.2", AnchorMax = "0.98 0.8" },
            Text = { Text = "🗑", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
        }, cmdPanel);

        yPos -= 0.12f;
    }

    // Empty state
    if (npc.InteractionCommands.Count == 0)
    {
        container.Add(new CuiLabel
        {
            Text = { Text = "📝\n\nNo commands added yet\n\nClick 'Add Command' to create your first command", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" },
            RectTransform = { AnchorMin = "0.2 0.35", AnchorMax = "0.8 0.55" }
        }, "CommandListArea");
    }

    // Example commands panel
    container.Add(new CuiPanel
    {
        Image = { Color = "0.15 0.3 0.15 0.3" },
        RectTransform = { AnchorMin = "0.05 0.01", AnchorMax = "0.95 0.06" }
    }, mainPanel, "ExamplesPanel");

    container.Add(new CuiLabel
    {
        Text = { Text = "Examples: say Hello {player.name}! | give {player.id} wood 1000 | oxide.grant user {player.id} vip", FontSize = 9, Align = TextAnchor.MiddleCenter, Color = "0.7 0.9 0.7 1" },
        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
    }, "ExamplesPanel");

    CuiHelper.AddUi(player, container);
    openUIs[player.userID] = "NPCCommandsPanel";
}
void ShowCommandTemplatesUI(BasePlayer player, string npcId)
{
    DestroyUI(player);

    if (!storedData.NPCs.ContainsKey(npcId))
    {
        SendReply(player, "NPC not found.");
        return;
    }

    var npc = storedData.NPCs[npcId];
    var container = new CuiElementContainer();

    // Background overlay
    container.Add(new CuiPanel
    {
        Image = { Color = "0 0 0 0.8" },
        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
        CursorEnabled = true
    }, "Overlay", "NPCTemplatesBackdrop");

    var mainPanel = container.Add(new CuiPanel
    {
        Image = { Color = "0.15 0.15 0.18 0.98" },
        RectTransform = { AnchorMin = "0.25 0.15", AnchorMax = "0.75 0.85" },
        CursorEnabled = true
    }, "NPCTemplatesBackdrop", "NPCTemplatesPanel");

    // Top accent bar
    container.Add(new CuiPanel
    {
        Image = { Color = "0.4 0.26 0.82 1" },
        RectTransform = { AnchorMin = "0 0.96", AnchorMax = "1 1" }
    }, mainPanel);

    // Header
    container.Add(new CuiLabel
    {
        Text = { Text = "🔌 PLUGIN COMMANDS", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
        RectTransform = { AnchorMin = "0 0.92", AnchorMax = "1 0.96" }
    }, mainPanel);

    container.Add(new CuiLabel
    {
        Text = { Text = "Select a command template to add to your NPC", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.7 0.7 0.7 1" },
        RectTransform = { AnchorMin = "0 0.89", AnchorMax = "1 0.92" }
    }, mainPanel);

    // Back button
    container.Add(new CuiButton
    {
        Button = { Command = $"npcui.editcommands {npcId}", Color = "0.3 0.3 0.35 0.9" },
        RectTransform = { AnchorMin = "0.02 0.93", AnchorMax = "0.15 0.98" },
        Text = { Text = "← Back", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, mainPanel);

    // Commands list area
    container.Add(new CuiPanel
    {
        Image = { Color = "0.1 0.1 0.12 0.8" },
        RectTransform = { AnchorMin = "0.05 0.08", AnchorMax = "0.95 0.87" }
    }, mainPanel, "TemplateListArea");

    if (registeredCommands.Count > 0)
    {
        // Group commands by plugin
        var pluginGroups = registeredCommands.Values
            .GroupBy(c => c.PluginName)
            .OrderBy(g => g.Key);

        // Initialize expanded plugins set if needed
        if (!expandedPlugins.ContainsKey(player.userID))
        {
            expandedPlugins[player.userID] = new HashSet<string>();
        }

        float yPos = 0.97f;
        
        foreach (var pluginGroup in pluginGroups)
        {
            string pluginName = pluginGroup.Key;
            bool isExpanded = expandedPlugins[player.userID].Contains(pluginName);
            int commandCount = pluginGroup.Count();
            
            // Plugin header panel
            var pluginHeaderPanel = container.Add(new CuiPanel
            {
                Image = { Color = "0.25 0.25 0.27 0.95" },
                RectTransform = { AnchorMin = $"0.02 {yPos - 0.07}", AnchorMax = $"0.98 {yPos}" }
            }, "TemplateListArea");

            // Plugin color badge
            container.Add(new CuiPanel
            {
                Image = { Color = "0.4 0.26 0.82 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.01 1" }
            }, pluginHeaderPanel);

            // Expand/Collapse icon
            container.Add(new CuiLabel
            {
                Text = { Text = isExpanded ? "▼" : "▶", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.08 1" }
            }, pluginHeaderPanel);

            // Plugin name
            container.Add(new CuiLabel
            {
                Text = { Text = pluginName, FontSize = 15, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.1 0", AnchorMax = "0.7 1" }
            }, pluginHeaderPanel);

            // Command count badge
            container.Add(new CuiPanel
            {
                Image = { Color = "0.4 0.26 0.82 0.7" },
                RectTransform = { AnchorMin = "0.72 0.2", AnchorMax = "0.85 0.8" }
            }, pluginHeaderPanel, $"CountBadge_{pluginName}");

            container.Add(new CuiLabel
            {
                Text = { Text = $"{commandCount} cmd{(commandCount != 1 ? "s" : "")}", FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, $"CountBadge_{pluginName}");

            // Toggle button (invisible clickable area)
            container.Add(new CuiButton
            {
                Button = { Command = $"npcui.toggleplugin {npcId} {pluginName}", Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "", FontSize = 1, Align = TextAnchor.MiddleCenter }
            }, pluginHeaderPanel);

            yPos -= 0.075f;

            // Show commands if expanded
            if (isExpanded)
            {
                int cmdIndex = 0;
                var pluginCommands = new List<KeyValuePair<string, RegisteredCommand>>();
                foreach (var kvp in registeredCommands)
                {
                    if (kvp.Value.PluginName == pluginName)
                    {
                        pluginCommands.Add(kvp);
                    }
                }
                
                foreach (var kvp in pluginCommands)
                {
                    var cmd = kvp.Value;
                    
                    // Command card (indented)
                    var cmdPanel = container.Add(new CuiPanel
                    {
                        Image = { Color = cmdIndex % 2 == 0 ? "0.18 0.18 0.2 0.95" : "0.2 0.2 0.22 0.95" },
                        RectTransform = { AnchorMin = $"0.04 {yPos - 0.12}", AnchorMax = $"0.98 {yPos}" }
                    }, "TemplateListArea");

                    // Command display name
                    container.Add(new CuiLabel
                    {
                        Text = { Text = cmd.DisplayName, FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                        RectTransform = { AnchorMin = "0.02 0.65", AnchorMax = "0.85 0.95" }
                    }, cmdPanel);

                    // Description
                    container.Add(new CuiLabel
                    {
                        Text = { Text = cmd.Description, FontSize = 9, Align = TextAnchor.MiddleLeft, Color = "0.7 0.7 0.7 1" },
                        RectTransform = { AnchorMin = "0.02 0.35", AnchorMax = "0.85 0.6" }
                    }, cmdPanel);

                    // Command preview
                    string commandPreview = cmd.CommandName;
                    if (cmd.Parameters.Count > 0)
                    {
                        commandPreview += " " + string.Join(" ", cmd.Parameters.Select(p => p.IsPlaceholder ? p.DefaultValue : $"<{p.Name}>"));
                    }
                    
                    container.Add(new CuiPanel
                    {
                        Image = { Color = "0.08 0.08 0.1 0.9" },
                        RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.85 0.3" }
                    }, cmdPanel, $"CmdPreview_{cmdIndex}_{pluginName}");

                    container.Add(new CuiLabel
                    {
                        Text = { Text = commandPreview, FontSize = 8, Align = TextAnchor.MiddleLeft, Color = "0.5 0.8 0.5 1" },
                        RectTransform = { AnchorMin = "0.02 0", AnchorMax = "0.98 1" }
                    }, $"CmdPreview_{cmdIndex}_{pluginName}");

                    // Add button
                    container.Add(new CuiButton
                    {
                        Button = { Command = $"npcui.addtemplate {npcId} {kvp.Key}", Color = "0.2 0.7 0.3 0.9" },
                        RectTransform = { AnchorMin = "0.87 0.25", AnchorMax = "0.98 0.75" },
                        Text = { Text = "➕\nAdd", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
                    }, cmdPanel);

                    yPos -= 0.125f;
                    cmdIndex++;
                }
            }

            yPos -= 0.01f; // Small gap between plugin sections
        }
    }
    else
    {
        // Empty state
        container.Add(new CuiLabel
        {
            Text = { Text = "🔌\n\nNo plugin commands registered\n\nOther plugins can register commands\nusing the API", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.6 0.6 0.6 1" },
            RectTransform = { AnchorMin = "0.2 0.35", AnchorMax = "0.8 0.55" }
        }, "TemplateListArea");
    }

    CuiHelper.AddUi(player, container);
    openUIs[player.userID] = "NPCTemplatesPanel";
}
void AddStyledInputField(CuiElementContainer container, string parent, string label, string value, string command, ref float yPos, float spacing)
{
    // Label
    container.Add(new CuiLabel
    {
        Text = { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "0.9 0.9 0.9 1" },
        RectTransform = { AnchorMin = $"0.03 {yPos - spacing + 0.04}", AnchorMax = $"0.35 {yPos}" }
    }, parent);

    // Input background
    container.Add(new CuiPanel
    {
        Image = { Color = "0.08 0.08 0.1 0.9" },
        RectTransform = { AnchorMin = $"0.37 {yPos - spacing + 0.01}", AnchorMax = $"0.97 {yPos - 0.01}" }
    }, parent, $"InputBg_{yPos}");

    // Input field
    container.Add(new CuiElement
    {
        Parent = $"InputBg_{yPos}",
        Components =
        {
            new CuiInputFieldComponent
            {
                Text = value,
                FontSize = 12,
                Align = TextAnchor.MiddleLeft,
                Command = command + " ",
                Color = "0.9 0.9 0.9 1"
            },
            new CuiRectTransformComponent { AnchorMin = "0.03 0.1", AnchorMax = "0.97 0.9" }
        }
    });

    yPos -= spacing;
}

void AddStyledCheckbox(CuiElementContainer container, string parent, string label, bool isChecked, string command, ref float yPos, float spacing)
{
    // Label
    container.Add(new CuiLabel
    {
        Text = { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Color = "0.9 0.9 0.9 1" },
        RectTransform = { AnchorMin = $"0.03 {yPos - spacing}", AnchorMax = $"0.6 {yPos}" }
    }, parent);

    // Checkbox button with border
    string checkboxColor = isChecked ? "0.2 0.7 0.3 0.9" : "0.3 0.3 0.35 0.9";
    container.Add(new CuiButton
    {
        Button = { Command = command, Color = checkboxColor },
        RectTransform = { AnchorMin = $"0.65 {yPos - spacing + 0.01}", AnchorMax = $"0.75 {yPos - 0.01}" },
        Text = { Text = isChecked ? "✓" : "", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
    }, parent);

    yPos -= spacing;
}

void DestroyUI(BasePlayer player)
{
    if (player == null) return;
    
    // Destroy all known UI panels
    CuiHelper.DestroyUi(player, "NPCMainPanel");
    CuiHelper.DestroyUi(player, "NPCBackdrop");
    CuiHelper.DestroyUi(player, "NPCEditPanel");
    CuiHelper.DestroyUi(player, "NPCEditBackdrop");
    CuiHelper.DestroyUi(player, "NPCCommandsPanel");
    CuiHelper.DestroyUi(player, "NPCCommandsBackdrop");
    CuiHelper.DestroyUi(player, "NPCTemplatesPanel");
    CuiHelper.DestroyUi(player, "NPCTemplatesBackdrop");
    
    if (openUIs.ContainsKey(player.userID))
    {
        openUIs.Remove(player.userID);
    }
}
#endregion

#region Console Commands
[ConsoleCommand("npcui.close")]
void CloseUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player != null)
        DestroyUI(player);
}

[ConsoleCommand("npcui.back")]
void BackCommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player != null)
        ShowMainUI(player);
}

[ConsoleCommand("npcui.create")]
void CreateUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player != null)
    {
        CreateNPCAtPlayer(player, "New NPC");
        ShowMainUI(player);
    }
}

[ConsoleCommand("npcui.delete")]
void DeleteUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    if (string.IsNullOrEmpty(npcId)) return;

    DeleteNPC(player, npcId);
    ShowMainUI(player);
}

[ConsoleCommand("npcui.edit")]
void EditUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    if (string.IsNullOrEmpty(npcId)) return;

    ShowEditUI(player, npcId);
}

[ConsoleCommand("npcui.toggle")]
void ToggleUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    var property = arg.GetString(1);

    if (!storedData.NPCs.ContainsKey(npcId)) return;

    var npc = storedData.NPCs[npcId];

    switch (property.ToLower())
    {
        case "hostile":
            npc.Hostile = !npc.Hostile;
            break;
        case "invulnerable":
            npc.Invulnerable = !npc.Invulnerable;
            break;
        case "respawn":
            npc.Respawn = !npc.Respawn;
            break;
        case "interaction":
            npc.AllowInteraction = !npc.AllowInteraction;
            break;
    }

    SaveData();
    ShowEditUI(player, npcId);
}

[ConsoleCommand("npcui.update")]
void UpdateUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    var property = arg.GetString(1);
    var value = arg.GetString(2);

    if (!storedData.NPCs.ContainsKey(npcId)) return;

    var npc = storedData.NPCs[npcId];

    switch (property.ToLower())
    {
        case "name":
            npc.Name = value;
            break;
        case "health":
            if (float.TryParse(value, out float health))
                npc.Health = health;
            break;
        case "speed":
            if (float.TryParse(value, out float speed))
                npc.Speed = speed;
            break;
        case "kit":
            npc.Kit = value;
            break;
        case "interactdist":
            if (float.TryParse(value, out float interactDist))
                npc.InteractionDistance = interactDist;
            break;
        case "interacttext":
            npc.InteractionText = value;
            break;
    }

    SaveData();
}

[ConsoleCommand("npcui.save")]
void SaveUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    if (!storedData.NPCs.ContainsKey(npcId)) return;

    var npc = storedData.NPCs[npcId];
    
    if (npc.ActiveEntityId != 0)
    {
        DespawnNPC(npc.ActiveEntityId);
    }
    SpawnNPC(npc);
    
    SaveData();
    SendReply(player, $"Saved and respawned NPC '{npc.Name}'");
    ShowMainUI(player);
}

[ConsoleCommand("npcui.selectkit")]
void SelectKitCommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    var kitName = arg.GetString(1);

    if (!storedData.NPCs.ContainsKey(npcId)) return;

    var npc = storedData.NPCs[npcId];
    npc.Kit = kitName;
    
    SaveData();
    ShowEditUI(player, npcId);
}

[ConsoleCommand("npcui.clearkit")]
void ClearKitCommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    if (!storedData.NPCs.ContainsKey(npcId)) return;

    var npc = storedData.NPCs[npcId];
    npc.Kit = "";
    
    SaveData();
    ShowEditUI(player, npcId);
}

[ConsoleCommand("npcui.editcommands")]
void EditCommandsUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    if (string.IsNullOrEmpty(npcId)) return;

    ShowCommandsUI(player, npcId);
}

[ConsoleCommand("npcui.addcommand")]
void AddCommandUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    if (!storedData.NPCs.ContainsKey(npcId)) return;

    var npc = storedData.NPCs[npcId];
    npc.InteractionCommands.Add("say Hello {player.name}!");
    
    SaveData();
    ShowCommandsUI(player, npcId);
}

[ConsoleCommand("npcui.deletecommand")]
void DeleteCommandUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    var index = arg.GetInt(1);
    
    if (!storedData.NPCs.ContainsKey(npcId)) return;

    var npc = storedData.NPCs[npcId];
    if (index >= 0 && index < npc.InteractionCommands.Count)
    {
        npc.InteractionCommands.RemoveAt(index);
        SaveData();
    }
    
    ShowCommandsUI(player, npcId);
}

[ConsoleCommand("npcui.updatecommand")]
void UpdateCommandUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    var index = arg.GetInt(1);
    
    if (!storedData.NPCs.ContainsKey(npcId)) return;

    var npc = storedData.NPCs[npcId];
    if (index >= 0 && index < npc.InteractionCommands.Count)
    {
        if (arg.Args != null && arg.Args.Length > 2)
        {
            string fullCommand = string.Join(" ", arg.Args.Skip(2));
            npc.InteractionCommands[index] = fullCommand;
            SaveData();
        }
    }
}

[ConsoleCommand("npcui.showtemplates")]
void ShowTemplatesUICommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    if (string.IsNullOrEmpty(npcId)) return;

    ShowCommandTemplatesUI(player, npcId);
}

[ConsoleCommand("npcui.addtemplate")]
void AddTemplateCommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    if (arg.Args == null || arg.Args.Length < 2)
    {
        Puts("AddTemplateCommand: Invalid arguments");
        return;
    }

    var npcId = arg.GetString(0);
    var templateKey = arg.GetString(1);
    
    Puts($"AddTemplateCommand called: npcId={npcId}, templateKey={templateKey}");
    
    if (string.IsNullOrEmpty(npcId))
    {
        Puts("AddTemplateCommand: npcId is null or empty");
        return;
    }
    
    if (string.IsNullOrEmpty(templateKey))
    {
        Puts("AddTemplateCommand: templateKey is null or empty");
        return;
    }
    
    if (!storedData.NPCs.ContainsKey(npcId))
    {
        Puts($"AddTemplateCommand: NPC not found with ID: {npcId}");
        return;
    }
    
    if (!registeredCommands.ContainsKey(templateKey))
    {
        Puts($"AddTemplateCommand: Template not found with key: {templateKey}");
        Puts($"Available keys: {string.Join(", ", registeredCommands.Keys)}");
        return;
    }

    var npc = storedData.NPCs[npcId];
    var cmd = registeredCommands[templateKey];
    
    // Build the command string with default parameters
    string commandString = cmd.CommandName;
    foreach (var param in cmd.Parameters)
    {
        commandString += $" {param.DefaultValue}";
    }
    
    npc.InteractionCommands.Add(commandString);
    SaveData();
    
    Puts($"Successfully added command: {commandString}");
    SendReply(player, $"Added command: {cmd.DisplayName}");
    ShowCommandsUI(player, npcId);
}
[ConsoleCommand("npcui.toggleplugin")]
void TogglePluginCommand(ConsoleSystem.Arg arg)
{
    var player = arg.Player();
    if (player == null) return;

    var npcId = arg.GetString(0);
    var pluginName = arg.GetString(1);
    
    if (!expandedPlugins.ContainsKey(player.userID))
    {
        expandedPlugins[player.userID] = new HashSet<string>();
    }
    
    if (expandedPlugins[player.userID].Contains(pluginName))
    {
        expandedPlugins[player.userID].Remove(pluginName);
    }
    else
    {
        expandedPlugins[player.userID].Add(pluginName);
    }
    
    ShowCommandTemplatesUI(player, npcId);
}
#endregion

        #region Data Management
void LoadData()
{
    try
    {
        storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
    }
    catch
    {
        storedData = new StoredData();
    }
}

void SaveData()
{
    Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
}
#endregion
    }
}