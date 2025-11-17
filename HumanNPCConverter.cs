using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("HumanNPCConverter", "Copeman", "1.0.2")]
    [Description("Converts HumanNPC data to AdvancedHumanNPC format - Compatible with Oxide/uMod and Carbon")]
    class HumanNPCConverter : RustPlugin
    {
        private bool IsCarbon
        {
            get
            {
                // Check multiple ways to detect Carbon
                if (Interface.Oxide.RootDirectory.Contains("carbon")) return true;
                if (Interface.Oxide.DataDirectory.Contains("carbon")) return true;
                
                // Check for Carbon-specific assemblies
                try
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name.Contains("Carbon"));
                    if (assembly != null) return true;
                }
                catch { }
                
                return false;
            }
        }
        
        private string DataPath => IsCarbon ? "carbon/data/HumanNPC" : "oxide/data/HumanNPC";
        
        #region Data Classes
        
        // HumanNPC Data Structure
        public class NpcData
        {
            public List<NPCInfo> HumanNPCs { get; set; } = new List<NPCInfo>();
        }

        public class NPCInfo
        {
            public ulong userid { get; set; }
            public string displayName { get; set; }
            public bool invulnerability { get; set; }
            public float health { get; set; } = 100f;
            public bool respawn { get; set; } = true;
            public float respawnSeconds { get; set; } = 60f;
            public SpawnInfoData spawnInfo { get; set; }
            public string waypoint { get; set; }
            public float collisionRadius { get; set; } = 10f;
            public string spawnkit { get; set; }
            public float damageAmount { get; set; } = 10f;
            public float damageDistance { get; set; } = 3f;
            public float damageInterval { get; set; } = 2f;
            public float attackDistance { get; set; } = 100f;
            public float maxDistance { get; set; } = 200f;
            public bool hostile { get; set; }
            public float speed { get; set; } = 3f;
            public bool stopandtalk { get; set; }
            public float stopandtalkSeconds { get; set; } = 3f;
            public bool enable { get; set; } = true;
            public bool lootable { get; set; } = true;
            public float hitchance { get; set; } = 0.75f;
            public float reloadDuration { get; set; }
            public bool needsAmmo { get; set; }
            public bool defend { get; set; }
            public bool evade { get; set; }
            public bool follow { get; set; } = true;
            public float evdist { get; set; }
            public bool allowsit { get; set; }
            public string musician { get; set; }
            public bool playTune { get; set; }
            public bool SoundOnEnter { get; set; }
            public bool SoundOnUse { get; set; }
            public float band { get; set; }
            public string permission { get; set; }
            public string Sound { get; set; }
            public List<string> message_hello { get; set; }
            public List<string> message_bye { get; set; }
            public List<string> message_use { get; set; }
            public List<string> message_hurt { get; set; }
            public List<string> message_kill { get; set; }
            public Dictionary<string, float> protections { get; set; }
        }

        public class SpawnInfoData
        {
            public string position { get; set; }
            public string rotation { get; set; }
        }

        // AdvancedHumanNPC Data Structure
        public class AdvancedNPCData
        {
            public Dictionary<string, AdvancedNPCInfo> NPCs = new Dictionary<string, AdvancedNPCInfo>();
        }

        public class AdvancedNPCInfo
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
        }

        #endregion

        #region Commands

        [Command("humannpc.convert")]
        void ConvertCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.Reply("You must be an admin to use this command.");
                return;
            }

            string platform = IsCarbon ? "Carbon" : "Oxide/uMod/HumanNPC";
            player.Reply($"Starting HumanNPC to AdvancedHumanNPC conversion... (Platform: {platform})");
            player.Reply($"Data directory: {Interface.Oxide.DataDirectory}");
            
            var result = ConvertData();
            
            if (result.Success)
            {
                player.Reply($"✓ Conversion successful!");
                player.Reply($"  - Platform: {platform}");
                player.Reply($"  - Converted {result.ConvertedCount} NPCs");
                player.Reply($"  - Skipped {result.SkippedCount} NPCs (disabled or invalid)");
                player.Reply($"  - Output saved to: {DataPath}/AdvancedHumanNPC_Converted.json");
                
                player.Reply($"\nNext steps:");
                player.Reply($"1. Review the converted file in {DataPath}/");
                player.Reply($"2. Backup your current AdvancedHumanNPC.json (if exists)");
                player.Reply($"3. Rename AdvancedHumanNPC_Converted.json to AdvancedHumanNPC.json");
                
                if (IsCarbon)
                {
                    player.Reply($"4. Reload AdvancedHumanNPC plugin with: c.reload AdvancedHumanNPC");
                }
                else
                {
                    player.Reply($"4. Reload AdvancedHumanNPC plugin with: oxide.reload AdvancedHumanNPC");
                }
                
                if (result.Warnings.Count > 0)
                {
                    player.Reply($"\n⚠ Warnings:");
                    foreach (var warning in result.Warnings)
                    {
                        player.Reply($"  - {warning}");
                    }
                }
            }
            else
            {
                player.Reply($"✗ Conversion failed: {result.Error}");
                player.Reply($"Looking for NpcData.json in: {Interface.Oxide.DataDirectory}/HumanNPC/");
            }
        }

        #endregion

        #region Conversion Logic

        private ConversionResult ConvertData()
        {
            var result = new ConversionResult();

            try
            {
                // Load HumanNPC data
                var humanNPCData = LoadHumanNPCData();
                
                if (humanNPCData == null || humanNPCData.HumanNPCs == null || humanNPCData.HumanNPCs.Count == 0)
                {
                    result.Success = false;
                    result.Error = $"No HumanNPC data found. Make sure NpcData.json exists in {Interface.Oxide.DataDirectory}/HumanNPC/";
                    return result;
                }

                Puts($"Found {humanNPCData.HumanNPCs.Count} NPCs to convert");

                // Convert to AdvancedHumanNPC format
                var advancedData = new AdvancedNPCData();

                foreach (var oldNPC in humanNPCData.HumanNPCs)
                {
                    // Skip disabled NPCs
                    if (!oldNPC.enable)
                    {
                        result.SkippedCount++;
                        result.Warnings.Add($"Skipped disabled NPC: {oldNPC.displayName ?? "Unknown"}");
                        continue;
                    }

                    // Parse spawn info
                    var spawnData = ParseSpawnInfo(oldNPC.spawnInfo);
                    if (spawnData == null)
                    {
                        result.SkippedCount++;
                        result.Warnings.Add($"Skipped NPC with invalid spawn data: {oldNPC.displayName ?? "Unknown"}");
                        continue;
                    }

                    // Create new NPC
                    var newNPC = new AdvancedNPCInfo
                    {
                        Id = oldNPC.userid.ToString(),
                        Name = oldNPC.displayName ?? "Converted NPC",
                        Health = oldNPC.health,
                        Position = spawnData.Position,
                        Rotation = spawnData.Rotation,
                        Hostile = oldNPC.hostile,
                        AlwaysHostile = oldNPC.hostile,
                        Invulnerable = oldNPC.invulnerability,
                        Lootable = oldNPC.lootable,
                        Radius = oldNPC.collisionRadius,
                        AttackDistance = oldNPC.attackDistance,
                        MaxDistance = oldNPC.maxDistance,
                        Speed = oldNPC.speed,
                        DamageAmount = oldNPC.damageAmount,
                        DamageDistance = oldNPC.damageDistance,
                        DamageInterval = oldNPC.damageInterval,
                        Kit = oldNPC.spawnkit ?? "",
                        Respawn = oldNPC.respawn,
                        RespawnTime = oldNPC.respawnSeconds,
                        AllowSit = oldNPC.allowsit,
                        AllowRide = false, // Not in old format
                        NeedsAmmo = oldNPC.needsAmmo,
                        DropWeapon = false, // Not in old format
                        HelloMessages = oldNPC.message_hello ?? new List<string>(),
                        ByeMessages = oldNPC.message_bye ?? new List<string>(),
                        UseMessages = oldNPC.message_use ?? new List<string>(),
                        HurtMessages = oldNPC.message_hurt ?? new List<string>(),
                        KillMessages = oldNPC.message_kill ?? new List<string>()
                    };

                    // Convert USE messages to interaction system
                    if (newNPC.UseMessages != null && newNPC.UseMessages.Count > 0)
                    {
                        newNPC.AllowInteraction = true;
                        newNPC.InteractionDistance = 3f;
                        newNPC.InteractionText = "Press E to interact";
                        
                        // Convert use messages to commands
                        foreach (var msg in newNPC.UseMessages)
                        {
                            newNPC.InteractionCommands.Add($"say {msg}");
                        }
                    }

                    // Waypoints handling
                    if (!string.IsNullOrEmpty(oldNPC.waypoint))
                    {
                        result.Warnings.Add($"NPC '{newNPC.Name}' has waypoints '{oldNPC.waypoint}' - these need to be manually reconfigured");
                    }

                    // Sound handling
                    if (!string.IsNullOrEmpty(oldNPC.Sound))
                    {
                        result.Warnings.Add($"NPC '{newNPC.Name}' has sound '{oldNPC.Sound}' - sound system not yet implemented in AdvancedHumanNPC");
                    }

                    // Musician handling
                    if (!string.IsNullOrEmpty(oldNPC.musician))
                    {
                        result.Warnings.Add($"NPC '{newNPC.Name}' is a musician - this feature is not in AdvancedHumanNPC");
                    }

                    // Features not converted
                    if (oldNPC.stopandtalk)
                    {
                        result.Warnings.Add($"NPC '{newNPC.Name}' had 'stopandtalk' enabled - this feature is not in AdvancedHumanNPC");
                    }

                    if (oldNPC.defend || oldNPC.evade)
                    {
                        result.Warnings.Add($"NPC '{newNPC.Name}' had defend/evade settings - these are not in AdvancedHumanNPC");
                    }

                    if (oldNPC.hitchance > 0 || oldNPC.reloadDuration > 0)
                    {
                        result.Warnings.Add($"NPC '{newNPC.Name}' had combat tuning settings (hitchance: {oldNPC.hitchance}) - these are not in AdvancedHumanNPC");
                    }

                    if (oldNPC.protections != null && oldNPC.protections.Count > 0)
                    {
                        result.Warnings.Add($"NPC '{newNPC.Name}' had custom protections - these are not in AdvancedHumanNPC");
                    }

                    advancedData.NPCs[newNPC.Id] = newNPC;
                    result.ConvertedCount++;
                }

                // Save converted data
                SaveConvertedData(advancedData);
                
                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Exception during conversion: {ex.Message}";
                PrintError(ex.ToString());
                return result;
            }
        }

        private NpcData LoadHumanNPCData()
        {
            try
            {
                // Try multiple possible locations
                string[] possiblePaths = new string[]
                {
                    "HumanNPC/NpcData",
                    "HumanNPC\\NpcData",
                    "NpcData"
                };

                foreach (var path in possiblePaths)
                {
                    try
                    {
                        var data = Interface.Oxide.DataFileSystem.ReadObject<NpcData>(path);
                        if (data != null && data.HumanNPCs != null)
                        {
                            Puts($"Successfully loaded {data.HumanNPCs.Count} NPCs from: {path}");
                            return data;
                        }
                    }
                    catch (Exception ex)
                    {
                        Puts($"Failed to load from {path}: {ex.Message}");
                    }
                }

                // If all else fails, try reading the file directly
                string directPath = System.IO.Path.Combine(Interface.Oxide.DataDirectory, "HumanNPC", "NpcData.json");
                
                if (System.IO.File.Exists(directPath))
                {
                    string json = System.IO.File.ReadAllText(directPath);
                    var data = JsonConvert.DeserializeObject<NpcData>(json);
                    Puts($"Loaded via direct read: {data?.HumanNPCs?.Count ?? 0} NPCs");
                    return data;
                }

                return null;
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load NpcData: {ex.Message}");
                PrintError($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private void SaveConvertedData(AdvancedNPCData data)
        {
            // Save to HumanNPC subfolder to keep it organized
            Interface.Oxide.DataFileSystem.WriteObject("HumanNPC/AdvancedHumanNPC_Converted", data);
        }

        private SpawnData ParseSpawnInfo(SpawnInfoData spawnInfo)
        {
            if (spawnInfo == null || string.IsNullOrEmpty(spawnInfo.position))
                return null;

            try
            {
                // Parse position: "x y z"
                var posParts = spawnInfo.position.Split(' ');
                if (posParts.Length < 3)
                    return null;

                var result = new SpawnData
                {
                    Position = new SerializedVector3
                    {
                        X = float.Parse(posParts[0]),
                        Y = float.Parse(posParts[1]),
                        Z = float.Parse(posParts[2])
                    }
                };

                // Parse rotation: "x y z w" (quaternion)
                if (!string.IsNullOrEmpty(spawnInfo.rotation))
                {
                    var rotParts = spawnInfo.rotation.Split(' ');
                    if (rotParts.Length >= 4)
                    {
                        // Convert quaternion to euler angles (simplified - just use Y rotation)
                        // For now, we'll just extract the Y component which represents the heading
                        result.Rotation = new SerializedVector3
                        {
                            X = 0,
                            Y = float.Parse(rotParts[1]) * 360, // Rough conversion
                            Z = 0
                        };
                    }
                }
                else
                {
                    result.Rotation = new SerializedVector3 { X = 0, Y = 0, Z = 0 };
                }

                return result;
            }
            catch (Exception ex)
            {
                PrintError($"Failed to parse spawn info: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Helper Classes

        private class SpawnData
        {
            public SerializedVector3 Position { get; set; }
            public SerializedVector3 Rotation { get; set; }
        }

        private class ConversionResult
        {
            public bool Success { get; set; }
            public string Error { get; set; }
            public int ConvertedCount { get; set; }
            public int SkippedCount { get; set; }
            public List<string> Warnings { get; set; } = new List<string>();
        }

        #endregion

        #region Oxide Hooks

        void Init()
        {
            string platform = IsCarbon ? "Carbon" : "Oxide/uMod";
            Puts($"HumanNPC Converter loaded on {platform}. Use 'humannpc.convert' command to convert your data.");
        }

        #endregion
    }
}
