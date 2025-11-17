
<img width="1328" height="903" alt="Screenshot 2025-11-15 200643" src="https://github.com/user-attachments/assets/eade44a0-be3d-4a09-9beb-2e69b6bd377a" /> <img width="1304" height="957" alt="Screenshot 2025-11-15 200652" src="https://github.com/user-attachments/assets/58b1f2c7-a818-4180-a64d-f1d8d232c809" /> <img width="1183" height="851" alt="Screenshot 2025-11-15 200659" src="https://github.com/user-attachments/assets/2ae83394-0234-4cb2-83ba-082bf0fa6984" /> <img width="1204" height="888" alt="Screenshot 2025-11-16 204610" src="https://github.com/user-attachments/assets/ec39a9c4-d3ac-43f6-89a3-fcec706e5168" />


# AdvancedHumanNPC Integration Guide for Plugin Developers



## Overview

This guide shows you how to integrate your existing Rust plugin with the **AdvancedHumanNPC** system in just a few minutes. By registering your commands, users can easily add them to NPCs through a visual UI without manually typing commands.

## Benefits

✅ **No UI coding required** - AdvancedHumanNPC handles all the interface  
✅ **Increased plugin visibility** - Users discover your commands in the NPC UI  
✅ **Automatic placeholder support** - `{player.id}`, `{player.name}`, etc. work automatically  
✅ **10-20 lines of code** - Minimal integration effort  
✅ **Optional** - Works alongside your existing command system  

---

## Quick Start (5 Minutes)

### Step 1: Add Plugin Reference

Add this to your plugin's fields section:

```csharp
[PluginReference]
private Plugin AdvancedHumanNPC;
```

### Step 2: Register Your Commands

Add this method to your plugin:

```csharp
void OnServerInitialized()
{
    // ... your existing code ...
    
    // Register with NPC system (wait 3 seconds to ensure it's loaded)
    timer.Once(3f, () => RegisterNPCCommands());
}

void RegisterNPCCommands()
{
    if (AdvancedHumanNPC == null)
    {
        Puts("AdvancedHumanNPC not found - skipping command registration");
        return;
    }

    // Register your commands here (see examples below)
    
    Puts("Successfully registered commands with AdvancedHumanNPC");
}
```

### Step 3: Clean Up on Unload (Optional but Recommended)

```csharp
void Unload()
{
    if (AdvancedHumanNPC != null)
    {
        // Unregister your commands
        AdvancedHumanNPC.Call("UnregisterNPCCommand", "YourPluginName", "yourcommand");
    }
    
    // ... your existing unload code ...
}
```

---

## API Reference

### RegisterNPCCommand

Registers a command with the NPC system.

```csharp
AdvancedHumanNPC.Call("RegisterNPCCommand",
    string pluginName,        // Your plugin's name
    string commandName,       // The actual command to execute
    string displayName,       // Friendly name shown in UI
    string description,       // What the command does
    List<object> parameters   // Optional: parameter definitions (can be null)
);
```

**Returns:** `bool` - true if successful, false if failed

### UnregisterNPCCommand

Removes a previously registered command.

```csharp
AdvancedHumanNPC.Call("UnregisterNPCCommand",
    string pluginName,        // Your plugin's name
    string commandName        // Command to remove (first word only)
);
```

**Returns:** `bool` - true if found and removed, false otherwise

### GetRegisteredCommands

Gets all registered commands (mainly for debugging).

```csharp
var commands = AdvancedHumanNPC.Call("GetRegisteredCommands") as List<object>;
```

---

## Available Placeholders

These placeholders are automatically replaced when a player interacts with the NPC:

| Placeholder | Replaced With | Example |
|-------------|---------------|---------|
| `{player.id}` | Player's Steam ID | `76561198012345678` |
| `{player.name}` | Player's display name | `John Doe` |
| `{npc.name}` | NPC's name | `Shop Keeper` |
| `{npc.id}` | NPC's unique ID | `abc123-def456` |

---

## Real-World Examples

### Example 1: Kits Plugin

```csharp
void RegisterNPCCommands()
{
    if (AdvancedHumanNPC == null) return;

    // Get all your kits (using your existing method)
    var allKits = GetAllKits();
    
    if (allKits == null || allKits.Length == 0) return;

    // Register a command for each kit
    foreach (var kitName in allKits)
    {
        AdvancedHumanNPC.Call("RegisterNPCCommand",
            "Kits",                                      // Plugin name
            $"kit {kitName} {{player.id}}",             // Command
            $"Give Kit: {kitName}",                     // Display name
            $"Gives the {kitName} kit to the player",   // Description
            null                                         // No custom parameters
        );
    }

    Puts($"Registered {allKits.Length} kit commands with AdvancedHumanNPC");
}

void Unload()
{
    if (AdvancedHumanNPC != null)
    {
        AdvancedHumanNPC.Call("UnregisterNPCCommand", "Kits", "kit");
    }
}
```

### Example 2: Economics Plugin

```csharp
void RegisterNPCCommands()
{
    if (AdvancedHumanNPC == null) return;

    // Give money
    AdvancedHumanNPC.Call("RegisterNPCCommand",
        "Economics",
        "deposit {player.id} 1000",
        "Give $1,000",
        "Deposits $1,000 into player's bank account",
        null
    );

    // Take money
    AdvancedHumanNPC.Call("RegisterNPCCommand",
        "Economics",
        "withdraw {player.id} 500",
        "Charge $500",
        "Withdraws $500 from player's bank account",
        null
    );

    // Show balance (NPC speaks it)
    AdvancedHumanNPC.Call("RegisterNPCCommand",
        "Economics",
        "balance {player.id}",
        "Check Balance",
        "Shows the player their account balance",
        null
    );
}
```

### Example 3: Teleportation Plugin

```csharp
void RegisterNPCCommands()
{
    if (AdvancedHumanNPC == null) return;

    // Teleport to locations
    var locations = new Dictionary<string, string>
    {
        { "Spawn", "teleportpos {player.id} 0 500 0" },
        { "Outpost", "teleportpos {player.id} -500 0 500" },
        { "Bandit Camp", "teleportpos {player.id} 500 0 -500" }
    };

    foreach (var location in locations)
    {
        AdvancedHumanNPC.Call("RegisterNPCCommand",
            "NTeleportation",
            location.Value,
            $"Teleport to {location.Key}",
            $"Instantly teleports player to {location.Key}",
            null
        );
    }
}
```

### Example 4: ServerRewards Plugin

```csharp
void RegisterNPCCommands()
{
    if (AdvancedHumanNPC == null) return;

    // Reward points presets
    var rewards = new[] { 10, 50, 100, 500, 1000 };

    foreach (var amount in rewards)
    {
        AdvancedHumanNPC.Call("RegisterNPCCommand",
            "ServerRewards",
            $"sr add {{player.id}} {amount}",
            $"Give {amount} RP",
            $"Awards {amount} reward points to the player",
            null
        );
    }
}
```

### Example 5: Quest System

```csharp
void RegisterNPCCommands()
{
    if (AdvancedHumanNPC == null) return;

    // Get all available quests from your system
    var quests = GetAllQuests(); // Your existing method

    foreach (var quest in quests)
    {
        // Start quest
        AdvancedHumanNPC.Call("RegisterNPCCommand",
            "QuestSystem",
            $"quest start {{player.id}} {quest.ID}",
            $"Start Quest: {quest.Name}",
            quest.Description,
            null
        );

        // Complete quest
        AdvancedHumanNPC.Call("RegisterNPCCommand",
            "QuestSystem",
            $"quest complete {{player.id}} {quest.ID}",
            $"Complete Quest: {quest.Name}",
            $"Marks {quest.Name} as completed and gives rewards",
            null
        );
    }
}
```

### Example 6: Custom Shop

```csharp
void RegisterNPCCommands()
{
    if (AdvancedHumanNPC == null) return;

    // Shop items
    var shopItems = new Dictionary<string, int>
    {
        { "rifle.ak", 1000 },
        { "pistol.revolver", 250 },
        { "wood", 100 }
    };

    foreach (var item in shopItems)
    {
        AdvancedHumanNPC.Call("RegisterNPCCommand",
            "MyShop",
            $"shop.buy {{player.id}} {item.Key} {item.Value}",
            $"Buy {item.Key}",
            $"Purchase {item.Key} for ${item.Value}",
            null
        );
    }
}
```

---

## Advanced: Custom Parameters

For more complex commands with configurable parameters:

```csharp
void RegisterNPCCommands()
{
    if (AdvancedHumanNPC == null) return;

    var parameters = new List<object>
    {
        new Dictionary<string, object>
        {
            ["name"] = "player",              // Parameter name
            ["type"] = "player",              // Type: "player", "text", "number"
            ["default"] = "{player.id}",      // Default value
            ["placeholder"] = true            // Auto-replace with player data
        },
        new Dictionary<string, object>
        {
            ["name"] = "amount",
            ["type"] = "number",
            ["default"] = "100",
            ["placeholder"] = false           // User must configure this
        }
    };

    AdvancedHumanNPC.Call("RegisterNPCCommand",
        "YourPlugin",
        "give",                               // Base command
        "Give Item",
        "Gives an item to the player",
        parameters                            // Pass parameter list
    );
}
```

---

## Testing Your Integration

1. **Load your plugin** with the new code
2. **Check console** for "Successfully registered commands with AdvancedHumanNPC"
3. **Open NPC UI** with `/npc` command
4. **Edit or create an NPC**
5. **Click "📋 Edit Commands"**
6. **Click "🔌 Plugin Commands"**
7. **Find your plugin** in the list and expand it
8. **Click "Add"** on any command

Your command should appear in the NPC's interaction list!

---

## Troubleshooting

### Commands Not Showing Up

**Problem:** Your commands don't appear in the UI

**Solutions:**
- Ensure AdvancedHumanNPC is installed and loaded
- Check console for error messages
- Verify the 3-second delay in `timer.Once(3f, ...)`
- Make sure you're calling `RegisterNPCCommand` correctly

### Commands Not Executing

**Problem:** Commands appear but don't work when players interact

**Solutions:**
- Test the command manually in console first
- Check placeholder syntax: `{player.id}` not `{playerid}`
- Ensure your command handler accepts the parameters
- Look for errors in the F1 console when testing

### Duplicate Commands

**Problem:** Commands appear multiple times

**Solutions:**
- Make sure you unregister commands in `Unload()`
- Only call `RegisterNPCCommands()` once
- Use `timer.Once()` not `timer.Repeat()`

---

## Best Practices

✅ **Use descriptive display names** - "Give Starter Kit" not just "kit"  
✅ **Write clear descriptions** - Users need to understand what the command does  
✅ **Always check for null** - `if (AdvancedHumanNPC == null) return;`  
✅ **Clean up on unload** - Unregister your commands  
✅ **Test your commands** - Make sure they work via console first  
✅ **Use existing commands** - Don't create new ones just for NPCs  

❌ **Don't register commands that require player input** - NPC commands are automatic  
❌ **Don't hard-code player IDs** - Always use `{player.id}` placeholder  
❌ **Don't register admin-only commands** - Users might expose them unintentionally  

---

## Support

For questions or issues:
- Check the AdvancedHumanNPC plugin page
- Post in the plugin's discussion thread
- Include your plugin name and code snippet

---

## Complete Example Template

Copy and paste this template to get started quickly:

```csharp
using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("YourPlugin", "YourName", "1.0.0")]
    class YourPlugin : RustPlugin
    {
        [PluginReference]
        private Plugin AdvancedHumanNPC;

        void OnServerInitialized()
        {
            // Your existing initialization code...
            
            // Register with NPC system
            timer.Once(3f, () => RegisterNPCCommands());
        }

        void RegisterNPCCommands()
        {
            if (AdvancedHumanNPC == null)
            {
                Puts("AdvancedHumanNPC not found - skipping command registration");
                return;
            }

            // Example 1: Simple command
            AdvancedHumanNPC.Call("RegisterNPCCommand",
                "YourPlugin",                           // Your plugin name
                "yourcommand {player.id}",              // Command to execute
                "Your Command Name",                    // Display name
                "Description of what this does",        // Description
                null                                    // No custom parameters
            );

            // Example 2: Another command
            AdvancedHumanNPC.Call("RegisterNPCCommand",
                "YourPlugin",
                "anothercommand {player.name} somevalue",
                "Another Command",
                "Does something else",
                null
            );

            Puts("Registered commands with AdvancedHumanNPC");
        }

        void Unload()
        {
            // Clean up
            if (AdvancedHumanNPC != null)
            {
                AdvancedHumanNPC.Call("UnregisterNPCCommand", "YourPlugin", "yourcommand");
                AdvancedHumanNPC.Call("UnregisterNPCCommand", "YourPlugin", "anothercommand");
            }
            
            // Your existing unload code...
        }
    }
}
```

---

## License & Attribution

This integration is free to use in any plugin. Attribution to AdvancedHumanNPC is appreciated but not required.

**Happy coding! 🚀**
