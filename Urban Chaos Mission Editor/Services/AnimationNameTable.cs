using System.Collections.Generic;

namespace UrbanChaosMissionEditor.Services;

/// <summary>
/// Provides animation name lookups for cutscene editing.
/// Animation indices come from the game's source code defines.
/// 
/// Character types (from CutsceneChannel.Index):
/// 1 = Darci, 2 = Roper, 3 = Cop, 4+ = various NPCs
/// </summary>
public static class AnimationNameTable
{
    // ========================================
    // Generic animation indices (shared)
    // These are the "logical" animation slots
    // ========================================
    public static readonly Dictionary<int, string> GenericAnimations = new()
    {
        { 1, "Walk" },
        { 2, "Run" },
        { 3, "Yomp" },
        { 4, "Punch 1" },
        { 5, "Punch 2" },
        { 6, "Punch 3" },
        { 7, "Kick Round 1" },
        { 8, "Kick 2" },
        { 9, "Kick 3" },
        { 10, "Fight Idle" },
        { 11, "Fight Ready" },
        { 12, "Hit Front Mid" },
        { 13, "Hit Front Hi" },
        { 14, "Fight Step N" },
        { 15, "Fight Step S" },
        { 16, "Fight Step E" },
        { 17, "Fight Step W" },
        { 18, "Standing Jump" },
        { 19, "Jump Up Grab" },
        { 20, "Fly Grabbing Ledge" },
        { 21, "Land Vert" },
        { 22, "Falling" },
        { 23, "Big Land" },
        { 24, "Stand Hip" },
        { 25, "Stand Ready" },
        { 26, "Idle Scratch 1" },
        { 27, "Idle Scratch 2" },
        { 28, "Breathe" },
        { 29, "Rotate Left" },
        { 30, "Rotate Right" },
        { 31, "Crouch Down" },
        { 32, "Idle Crouch" },
        { 33, "Crawl" },
        { 34, "Run Jump Left" },
        { 35, "Run Jump Right" },
        { 36, "Land Left" },
        { 37, "Land Right" },
        { 38, "Death Slide" },
        { 39, "Mount Ladder" },
        { 40, "On Ladder" },
        { 41, "Off Ladder Top" },
        { 42, "Traverse Left" },
        { 43, "Traverse Right" },
        { 44, "Dangle" },
        { 45, "Pull Up" },
        { 46, "Block Low" },
        { 47, "Block High" },
        { 48, "KO Back" },
        { 49, "Die Neck" },
        { 50, "Gut Hit Small" },
        { 51, "Gut Hit Big" },
        { 52, "Head Hit Small" },
        { 53, "Head Hit Big" },
    };

    // ========================================
    // Roper-specific animations (HERO.all)
    // From NROPER_ANIM_* and ROPER_* defines
    // ========================================
    public static readonly Dictionary<int, string> RoperAnimations = new()
    {
        { 1, "Yomp" },
        { 2, "Stand Ready" },
        { 3, "Breathe" },
        { 4, "Running Jump" },
        { 5, "Running Jump Fly" },
        { 6, "Running Jump Land" },
        { 7, "Running Jump Land Run" },
        { 8, "Draw Gun" },
        { 9, "Gun Aim" },
        { 10, "Gun Shoot" },
        { 11, "Listen" },
        { 12, "Jump Spot Takeoff" },
        { 13, "Jump Spot Land" },
        { 14, "Jump Spot Static" },
        { 15, "Tell" },
        { 16, "Shotgun Takeout" },
        { 17, "Shotgun Shoot" },
        { 19, "Swig Flask" },
        { 20, "Yomp Start" },
        { 21, "AK Takeout" },
        { 22, "AK Shoot" },
        { 23, "AK Aim" },
        { 24, "AK Aim Left" },
        { 25, "AK Aim Right" },
        { 26, "Shotgun Aim" },
        { 27, "Shotgun Aim Left" },
        { 28, "Shotgun Aim Right" },
        { 30, "Gun Aim Left" },
        { 31, "Gun Aim Right" },
        { 34, "Pickup Carry" },
        { 35, "Start Walk Carry" },
        { 36, "Walk Carry" },
        { 37, "Walk Stop Carry" },
        { 38, "Putdown Carry" },
        { 39, "Pickup Carry (Vertical)" },
        { 40, "Start Walk Carry (Vertical)" },
        { 41, "Walk Carry (Vertical)" },
        { 42, "Walk Stop Carry (Vertical)" },
        { 43, "Putdown Carry (Vertical)" },
        { 44, "Stand Carry" },
        { 45, "Stand Carry (Vertical)" },
        { 46, "Walk Backwards" },
        { 47, "Run With AK" },
        { 48, "Walk Backwards With AK" },
        { 49, "To Wall" },
        { 50, "To Wall Shotgun" },
        { 51, "Along Wall" },
        { 52, "Along Wall Back" },
        { 53, "Along Wall Shotgun" },
        { 54, "Along Wall Shotgun Back" },
        { 55, "Aim Wall Shotgun" },
        { 56, "Aim Wall Shotgun Back" },
        { 57, "Aim Wall Pistol" },
        { 60, "Fight Wall" },
        { 61, "Along Wall Shotgun C" },
        { 62, "Along Wall Shotgun D" },
        { 63, "Aim Wall Pistol B" },
        { 64, "Aim Wall Pistol C" },
        { 65, "To Wall Static" },
        { 72, "Two Pistol Run" },
        { 74, "Shotgun Run" },
        { 76, "Two Pistol Draw" },
        { 77, "Two Pistol Fire" },
        { 78, "Two Pistol Away" },
        { 79, "Two Pistol Aim Left" },
        { 80, "Two Pistol Aim Right" },
        { 82, "Shotgun Start Run" },
        { 84, "Shotgun Crouch" },
        { 85, "Shotgun Crouch Hold" },
        { 86, "Shotgun Crouch Stand Up" },
        { 87, "Two Pistol Crouch" },
        { 88, "Two Pistol Crouch Hold" },
        { 89, "Two Pistol Crouch Stand Up" },
        { 90, "Two Pistol Aim" },
        { 91, "Climb Over Fence" },
    };

    // ========================================
    // Darci-specific animations (DARCI1.all)
    // Most share indices with generic set
    // ========================================
    public static readonly Dictionary<int, string> DarciAnimations = new()
    {
        { 1, "Walk" },
        { 2, "Run" },
        { 3, "Yomp" },
        { 4, "Punch Jab" },
        { 5, "Punch Cross" },
        { 6, "Punch Hook" },
        { 7, "Kick Roundhouse" },
        { 8, "Kick Side" },
        { 9, "Kick Back" },
        { 10, "Fight Idle" },
        { 11, "To Fight Stance" },
        { 12, "Fight Recoil" },
        { 13, "Fight Block" },
        { 14, "Fight Step Forward" },
        { 15, "Fight Step Back" },
        { 16, "Fight Step Left" },
        { 17, "Fight Step Right" },
        { 18, "Standing Jump" },
        { 19, "Jump Up Grab" },
        { 20, "Fly Grabbing Ledge" },
        { 21, "Land Vertical" },
        { 22, "Falling" },
        { 23, "Roll Land" },
        { 24, "Stand Hip" },
        { 25, "Stand Ready" },
        { 26, "Idle Look Around" },
        { 27, "Idle Stretch" },
        { 28, "Breathe" },
        { 29, "Rotate Left" },
        { 30, "Rotate Right" },
        { 31, "Crouch Down" },
        { 32, "Idle Crouch" },
        { 33, "Crawl" },
        { 34, "Cartwheel Left" },
        { 35, "Cartwheel Right" },
        { 36, "Backflip" },
        { 37, "Front Flip" },
        { 38, "Wall Run" },
        { 39, "Wall Jump" },
        { 40, "Slide" },
        { 41, "Slide Attack" },
        { 42, "Sweep Kick" },
        { 43, "Flying Kick" },
        { 44, "Spin Kick" },
        { 45, "Uppercut" },
        { 46, "Elbow Strike" },
        { 47, "Knee Strike" },
        { 48, "Throw" },
        { 49, "Ground Pound" },
        { 50, "Gut Hit Small" },
        { 51, "Gut Hit Big" },
        { 52, "Head Hit Small" },
        { 53, "Head Hit Big" },
        { 54, "KO Back" },
        { 55, "KO Front" },
        { 56, "Get Up Front" },
        { 57, "Get Up Back" },
        { 58, "Climb Ladder" },
        { 59, "Climb Pipe" },
        { 60, "Shimmy Left" },
        { 61, "Shimmy Right" },
        { 62, "Pull Up" },
        { 63, "Hang Idle" },
        { 64, "Drop Down" },
        { 65, "Vault Over" },
        { 66, "Climb Fence" },
        { 67, "Balance Walk" },
        { 68, "Balance Wobble" },
        { 69, "Push Object" },
        { 70, "Pull Object" },
        { 71, "Carry Walk" },
        { 72, "Carry Idle" },
        { 73, "Throw Object" },
        { 74, "Pistol Draw" },
        { 75, "Pistol Aim" },
        { 76, "Pistol Fire" },
        { 77, "Pistol Reload" },
        { 78, "Pistol Run" },
        { 79, "Die" },
        { 80, "Death Fall" },
    };

    // ========================================
    // Cop animations (police1.all)
    // ========================================
    public static readonly Dictionary<int, string> CopAnimations = new()
    {
        { 1, "Walk" },
        { 2, "Run" },
        { 3, "Stand" },
        { 4, "Jab" },
        { 5, "Hit" },
        { 6, "Jab 2" },
        { 7, "Kick" },
        { 8, "Block" },
        { 9, "Idle" },
        { 10, "Idle 2" },
        { 11, "Idle 3" },
        { 12, "Breathe" },
        { 13, "Gut Hit Small" },
        { 14, "Head Hit Small" },
        { 15, "Gut Death" },
        { 16, "Die Neck" },
        { 17, "Jump Up Grab" },
        { 18, "Pull Up" },
        { 19, "Pistol Draw" },
        { 20, "Pistol Aim" },
        { 21, "Pistol Fire" },
        { 22, "Arrest" },
        { 23, "Radio Call" },
        { 24, "Point" },
        { 25, "Wave" },
    };

    // ========================================
    // Thug/Civilian animations
    // ========================================
    public static readonly Dictionary<int, string> ThugAnimations = new()
    {
        { 1, "Walk" },
        { 2, "Run" },
        { 3, "Stand" },
        { 4, "Punch 1" },
        { 5, "Punch 2" },
        { 6, "Kick" },
        { 7, "Block" },
        { 8, "Idle" },
        { 9, "Breathe" },
        { 10, "Hit Recoil" },
        { 11, "KO" },
        { 12, "Die" },
        { 13, "Threaten" },
        { 14, "Taunt" },
        { 15, "Weapon Swing" },
        { 16, "Weapon Stab" },
        { 17, "Gun Draw" },
        { 18, "Gun Aim" },
        { 19, "Gun Fire" },
        { 20, "Cower" },
        { 21, "Flee" },
        { 22, "Surrender" },
    };

    /// <summary>
    /// Get animation name for a specific person type and animation index
    /// </summary>
    /// <param name="personType">Person type (1=Darci, 2=Roper, 3=Cop, etc)</param>
    /// <param name="animIndex">Animation index from the cutscene packet</param>
    /// <returns>Human-readable animation name</returns>
    public static string GetAnimationName(int personType, int animIndex)
    {
        var table = personType switch
        {
            1 => DarciAnimations,
            2 => RoperAnimations,
            3 => CopAnimations,
            4 or 5 or 6 or 7 => ThugAnimations, // Various thug types
            _ => GenericAnimations
        };

        if (table.TryGetValue(animIndex, out var name))
            return name;

        // Fallback to generic table
        if (GenericAnimations.TryGetValue(animIndex, out name))
            return name;

        return $"Anim {animIndex}";
    }

    /// <summary>
    /// Get all animations for a person type (for browser UI)
    /// </summary>
    public static Dictionary<int, string> GetAnimationsForPerson(int personType)
    {
        return personType switch
        {
            1 => DarciAnimations,
            2 => RoperAnimations,
            3 => CopAnimations,
            4 or 5 or 6 or 7 => ThugAnimations,
            _ => GenericAnimations
        };
    }

    /// <summary>
    /// Get person type name
    /// </summary>
    public static string GetPersonTypeName(int personType)
    {
        return personType switch
        {
            1 => "Darci",
            2 => "Roper",
            3 => "Cop",
            4 => "Civilian",
            5 => "Rasta Thug",
            6 => "Grey Thug",
            7 => "Red Thug",
            8 => "Prostitute",
            9 => "Fat Prostitute",
            10 => "Hostage",
            11 => "Mechanic",
            12 => "Tramp",
            13 => "MIB 1",
            14 => "MIB 2",
            15 => "MIB 3",
            _ => $"Person {personType}"
        };
    }

    /// <summary>
    /// Search animations by name (partial match)
    /// </summary>
    public static List<(int Index, string Name)> SearchAnimations(int personType, string searchTerm)
    {
        var results = new List<(int, string)>();
        var table = GetAnimationsForPerson(personType);
        var search = searchTerm.ToLowerInvariant();

        foreach (var kvp in table)
        {
            if (kvp.Value.ToLowerInvariant().Contains(search))
            {
                results.Add((kvp.Key, kvp.Value));
            }
        }

        return results;
    }
}