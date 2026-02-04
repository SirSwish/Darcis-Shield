using System.Collections.Generic;

namespace UrbanChaosMapEditor.Models
{
    public static class PrimCatalog
    {

        private static readonly Dictionary<int, string> _names = new()
        {
            { 0, "End of data" }, { 1, "Street Lamp" }, { 2, "Traffic Light" }, { 3, "Small Traffic Light" },
            { 4, "Single Gas Pump" }, { 5, "Double Gas Pump" }, { 6, "Awaken Sign" }, { 7, "Vote Bane Sign" },
            { 8, "Gas Price Sign" }, { 9, "Silvery Moon Sign" }, { 10, "Gas Sign" }, { 11, "Street Lamp" },
            { 12, "Motel Sign" }, { 13, "Metal Walkway 1" }, { 14, "Metal Walkway 2" }, { 15, "Metal Walkway 3" },
            { 16, "Metal Walkway 4" }, { 17, "Metal Walkway 5" }, { 18, "Tyres Sign" }, { 19, "Green Canopy 1" },
            { 20, "Damaged Green Canopy 1" }, { 21, "Green Canopy 2" }, { 22, "Damaged Green Canopy 2" },
            { 23, "Diner Sign" }, { 24, "Damaged Green Canopy 3" }, { 25, "Silvery Moon Sign 2" },
            { 26, "Motel Sign 2" }, { 27, "Wild Cat Van" }, { 28, "Wild Cat Crate" }, { 29, "Metal Walkway Corner" },
            { 30, "Pot Plant" }, { 31, "Liquors Sign" }, { 32, "Demon Gas Pipe" }, { 33, "Trash Can" },
            { 34, "Tree 1" }, { 35, "Tree 2" }, { 36, "Key" }, { 37, "Wooden Post" }, { 38, "Weed" },
            { 39, "Constitution (Treasure)" }, { 40, "Gas Cylinder" }, { 41, "Stairs" },
            { 42, "4-Way Street Lamp" }, { 43, "Side Step" }, { 44, "Metal Lift" }, { 45, "Terminal Sign" },
            { 46, "Bush 1" }, { 47, "Liberty Statue" }, { 48, "USA Flag" }, { 49, "Greenhouse" },
            { 50, "Estate Archway" }, { 51, "Wood Post & Cable" }, { 52, "Street Sign (Blue)" },
            { 53, "Speed Limit Sign" }, { 54, "Right Turn Sign" }, { 55, "Do Not Enter Sign" },
            { 56, "Le Grande Sign" }, { 57, "Rail Sign 2" }, { 58, "Tree 3" }, { 59, "Tree 4" },
            { 60, "Railway Stand" }, { 61, "Boombox" }, { 62, "Lion Statue" }, { 63, "Motomo Sign" },
            { 64, "Choke Sign" }, { 65, "Buy Cigarette Sign" }, { 66, "Take Away Sign" }, { 67, "Road Cone" },
            { 68, "Ala Mode Sign" }, { 69, "Door Awning" }, { 70, "Brown Pedestal" },
            { 71, "Stamina (Treasure)" }, { 72, "Bush 2" }, { 73, "Building Top 1" }, { 74, "Helicopter Body" },
            { 75, "Helicopter Rotor" }, { 76, "Soda Can" }, { 77, "Marble Archway" }, { 78, "Tree 5" },
            { 79, "Building Top 2" }, { 80, "Side Step" }, { 81, "Reflexes (Treasure)" }, { 82, "Nude Sign" },
            { 83, "Filing Cabinet" }, { 84, "Shock Sign" }, { 85, "Wrought Iron Gate" },
            { 86, "Wrought Iron Gate Door" }, { 87, "Van Tyre" }, { 88, "Civilian Van" }, { 89, "Park Bench" },
            { 90, "SUV" }, { 91, "Police SUV" }, { 92, "Building Top 3" }, { 93, "Corner Desk" },
            { 94, "Strength (Treasure)" }, { 95, "Café Chair" }, { 96, "Café Table" }, { 97, "Cross Tombstone" },
            { 98, "Round Tombstone" }, { 99, "Doors 1" }, { 100, "Fuel Meter" }, { 101, "Single Sofa" },
            { 102, "Brown Coffee Table" }, { 103, "TV" }, { 104, "Lamp" }, { 105, "Double Sofa" },
            { 106, "Building Top 4" }, { 107, "Choke Floor Sign" }, { 108, "Civilian Sedan" },
            { 109, "Metal Walkway 6" }, { 110, "Long Table" }, { 111, "Pool Sign" }, { 112, "Computer" },
            { 113, "Flood Light" }, { 114, "Tugboat" }, { 115, "Stone Archway" }, { 116, "Payphone" },
            { 117, "Doors 2" }, { 118, "Stone Archway Top" }, { 119, "Orange Sign" }, { 120, "M16 - Pickup" },
            { 121, "Revolver" }, { 122, "Car Shell" }, { 123, "Brown Canopy" }, { 124, "Colored Lights" },
            { 125, "Motel Sign" }, { 126, "Brown Office Chair" }, { 127, "Shotgun - Pickup" }, { 128, "Pool Table" },
            { 129, "Crate Large" }, { 130, "Crate Small" }, { 131, "Soft Top Sedan" }, { 132, "Satellite Dish" },
            { 133, "UCPD Sign" }, { 134, "Square Bin" }, { 135, "Police Flagpole" }, { 136, "Reflexes (Treasure)" },
            { 137, "Repent Sign" }, { 138, "Motorbike Wheel" }, { 139, "Book Case" }, { 140, "Bike Seat" },
            { 141, "Oil Barrel" }, { 142, "Med Pack" }, { 143, "Pistol" }, { 144, "Napkin" },
            { 145, "Explosive Barrel" }, { 146, "Phone box" }, { 147, "Target Practice Dummy" },
            { 148, "Police Roof Light" }, { 149, "Palm Tree" }, { 150, "Police Sedan" }, { 151, "Searchlight" },
            { 152, "Ivy" }, { 153, "Ivy 2" }, { 154, "Doors 3" }, { 155, "Taxi" }, { 156, "Danger Barrier" },
            { 157, "Green Switch" }, { 158, "Brown Stool" }, { 159, "Ambulance" }, { 160, "Funnel" },
            { 161, "Men Working Sign" }, { 162, "Stop Sign" }, { 163, "Dead End Sign" }, { 164, "Danger Sign" },
            { 165, "Road Barrier" }, { 166, "Knife" }, { 167, "Explosives - Pickup" }, { 168, "Grenade" },
            { 169, "Blue Mailbox" }, { 170, "Rail Pillar" }, { 171, "Rail Track" }, { 172, "Sniper Rifle" },
            { 173, "Curve Rail" }, { 174, "Straight Rail" }, { 175, "Rail barrier" }, { 176, "Railing" },
            { 177, "AK-47" }, { 178, "Laser Box" }, { 179, "Bar Sign" }, { 180, "Pool Chair" },
            { 181, "Bane Gate Left" }, { 182, "Bane Gate Right" }, { 183, "Cure Rail Barrier" }, { 184, "Bed" },
            { 185, "Double Brown Desk" }, { 186, "Single Brown desk" }, { 187, "Corner Brown Desk" }, { 188, "Mine" },
            { 189, "Male Statue 2" }, { 190, "Wall Light" }, { 191, "Female Statue" }, { 192, "Male Statue" },
            { 193, "Pipe 1" }, { 194, "Pipe 2" }, { 195, "Pipe 3" }, { 196, "Pipe 4" }, { 197, "Pipe 5" },
            { 198, "Pipe 6" }, { 199, "Pipe 7" }, { 200, "Parasol" }, { 201, "Roomba" },
            { 202, "Inflatable Chair" }, { 203, "Weeds 2" }, { 204, "Diving Board" }, { 205, "Baseball Bat" },
            { 206, "Aerial" }, { 207, "Water Tower 1" }, { 208, "Pot Plant 2" }, { 209, "Blue Canopy" },
            { 210, "Double Blue Canopy" }, { 211, "Corner Blue Canopy" }, { 212, "Pink Flowers" }, { 213, "Chimney" },
            { 214, "Vent" }, { 215, "Chimney 2" }, { 216, "Red Switch" }, { 217, "Washing Line" },
            { 218, "Powerline" }, { 219, "Winch" }, { 220, "Sedan Wheel" }, { 221, "Dumpster" },
            { 222, "Water Tower 2" }, { 223, "Hotel Sign" }, { 224, "Vent Long" }, { 225, "Vent 2" },
            { 226, "Skylight 1" }, { 227, "Skylight 2" }, { 228, "Fire Hydrant" }, { 229, "Wall Garden Light" },
            { 230, "Ground Garden Light" }, { 231, "Wildcat Topiary" }, { 232, "Red Folder" }, { 233, "Disk" },
            { 234, "Wall Mounted M16" }, { 235, "Disco Light" }, { 236, "Liquor Cabinet" }, { 237, "VHS Tape" },
            { 238, "Police Motorbike" }, { 239, "Weed Away" }, { 240, "Round Hedge" },
            { 241, "Brown Yellow Canopy" }, { 242, "Hot Dog Stand" }, { 243, "Rifle" }, { 244, "Doors 4" },
            { 245, "Missile" }, { 246, "Red Canopy" }, { 247, "Marble Pillar Top" }, { 248, "Xpresso Sign" },
            { 249, "Marble Pillar" }, { 250, "Marble Plant" }, { 251, "Marble Plant 2" },
            { 252, "Marble Pedestal" }, { 253, "Shotgun Ammo" }, { 254, "M16 Ammo" }, { 255, "Pistol Ammo" },
        };

        // Add a *small* category registry you can grow over time.
        // Anything not listed here is treated as Misc.
        private static readonly Dictionary<int, PrimButtonCategory> _categories = new()
        {
                // ===== City assets (street furniture, utilities, public fixtures) =====
                { 1,   PrimButtonCategory.CityAssets }, // Street Lamp
                { 11,  PrimButtonCategory.CityAssets }, // Street Lamp
                { 42,  PrimButtonCategory.CityAssets }, // 4-Way Street Lamp
                { 2,   PrimButtonCategory.CityAssets }, // Traffic Light
                { 3,   PrimButtonCategory.CityAssets }, // Small Traffic Light
                { 33,  PrimButtonCategory.CityAssets }, // Trash Can
                { 89,  PrimButtonCategory.CityAssets }, // Park Bench
                { 116, PrimButtonCategory.CityAssets }, // Payphone
                { 146, PrimButtonCategory.CityAssets }, // Phone box
                { 134, PrimButtonCategory.CityAssets }, // Square Bin
                { 169, PrimButtonCategory.CityAssets }, // Blue Mailbox
                { 221, PrimButtonCategory.CityAssets }, // Dumpster
                { 242, PrimButtonCategory.CityAssets }, // Hot Dog Stand
                { 141, PrimButtonCategory.CityAssets }, // Oil Barrel
                { 165, PrimButtonCategory.CityAssets }, // Road Barrier
                { 218, PrimButtonCategory.CityAssets }, // Powerline
                { 207, PrimButtonCategory.CityAssets }, // Water Tower 1
                { 222, PrimButtonCategory.CityAssets }, // Water Tower 2
                { 215, PrimButtonCategory.CityAssets }, // Chimney 2
                { 224, PrimButtonCategory.CityAssets }, // Vent Long
                { 228, PrimButtonCategory.CityAssets }, // Fire Hydrant
                { 162, PrimButtonCategory.CityAssets }, // Stop Sign (left per your earlier placement)
                { 67,  PrimButtonCategory.CityAssets }, // Road Cone
                { 76,  PrimButtonCategory.CityAssets }, // Soda Can
                { 145, PrimButtonCategory.CityAssets }, // Explosive Barrel
                { 156, PrimButtonCategory.CityAssets }, // Danger Barrier
                { 206, PrimButtonCategory.CityAssets },  /// Aerial

                // ===== Foliage (trees, plants, hedges, ivy, weeds) =====
                // Trees
                { 34,  PrimButtonCategory.Foliage }, // Tree 1
                { 35,  PrimButtonCategory.Foliage }, // Tree 2
                { 46,  PrimButtonCategory.Foliage }, // Tree 1
                { 72,  PrimButtonCategory.Foliage }, // Tree 2
                { 58,  PrimButtonCategory.Foliage }, // Tree 3
                { 59,  PrimButtonCategory.Foliage }, // Tree 4
                { 78,  PrimButtonCategory.Foliage }, // Tree 5
                { 149, PrimButtonCategory.Foliage }, // Palm Tree
                // Hedges / ivy / weeds
                { 240, PrimButtonCategory.Foliage }, // Round Hedge
                { 152, PrimButtonCategory.Foliage }, // Ivy
                { 153, PrimButtonCategory.Foliage }, // Ivy 2
                { 38,  PrimButtonCategory.Foliage }, // Weed
                { 203, PrimButtonCategory.Foliage }, // Weeds 2
                // Planters / flowers / topiary
                { 30,  PrimButtonCategory.Foliage }, // Pot Plant
                { 208, PrimButtonCategory.Foliage }, // Pot Plant 2
                { 212, PrimButtonCategory.Foliage }, // Pink Flowers
                { 231, PrimButtonCategory.Foliage }, // Wildcat Topiary
                { 250, PrimButtonCategory.Foliage }, // Marble Plant
                { 251, PrimButtonCategory.Foliage }, // Marble Plant 2

                // ===== Vehicles =====
                { 88,  PrimButtonCategory.Vehicles }, // Civilian Van
                { 90,  PrimButtonCategory.Vehicles }, // SUV
                { 108, PrimButtonCategory.Vehicles }, // Civilian Sedan
                { 150, PrimButtonCategory.Vehicles }, // Police Sedan
                { 155, PrimButtonCategory.Vehicles }, // Taxi
                { 159, PrimButtonCategory.Vehicles }, // Ambulance
                { 238, PrimButtonCategory.Vehicles }, // Police Motorbike
                { 114, PrimButtonCategory.Vehicles }, // Tugboat
                { 74,  PrimButtonCategory.Vehicles }, // Helicopter Body
                { 75,  PrimButtonCategory.Vehicles }, // Helicopter Rotor
                { 27,  PrimButtonCategory.Vehicles }, // Wild Cat Van
                { 87,  PrimButtonCategory.Vehicles }, // Van Tyre
                { 91,  PrimButtonCategory.Vehicles }, // Police SUV
                { 122, PrimButtonCategory.Vehicles }, // Car Shell
                { 131, PrimButtonCategory.Vehicles }, // Soft Top Sedan
                { 138, PrimButtonCategory.Vehicles }, // Motorbike Wheel
                { 140, PrimButtonCategory.Vehicles }, // Bike Seat
                { 160, PrimButtonCategory.Vehicles }, // Funnel
                { 220, PrimButtonCategory.Vehicles }, // Sedan Wheel

                // ===== Weapons & pickups =====
                // Firearms
                { 121, PrimButtonCategory.Weapons }, // Revolver
                { 143, PrimButtonCategory.Weapons }, // Pistol
                { 120, PrimButtonCategory.Weapons }, // M16 - Pickup
                { 172, PrimButtonCategory.Weapons }, // Sniper Rifle
                { 177, PrimButtonCategory.Weapons }, // AK-47
                { 243, PrimButtonCategory.Weapons }, // Rifle
                { 127, PrimButtonCategory.Weapons }, // Shotgun - Pickup
                // Ammo
                { 253, PrimButtonCategory.Weapons }, // Shotgun Ammo
                { 254, PrimButtonCategory.Weapons }, // M16 Ammo
                { 255, PrimButtonCategory.Weapons }, // Pistol Ammo
                // Explosives / thrown
                { 167, PrimButtonCategory.Weapons }, // Explosives - Pickup
                { 168, PrimButtonCategory.Weapons }, // Grenade
                { 188, PrimButtonCategory.Weapons }, // Mine
                { 245, PrimButtonCategory.Weapons }, // Missile
                // Melee / misc
                { 166, PrimButtonCategory.Weapons }, // Knife
                { 205, PrimButtonCategory.Weapons }, // Baseball Bat
                { 142, PrimButtonCategory.Weapons }, // Med Pack
                { 234, PrimButtonCategory.Weapons }, // Wall Mounted M16

                // ===== Structures (doors, gates, walkways, rails, canopies, rooftops, statues, pipes, skylights...) =====
                // Walkways, lifts, stairs
                { 13,  PrimButtonCategory.Structures }, // Metal Walkway 1
                { 14,  PrimButtonCategory.Structures }, // Metal Walkway 2
                { 15,  PrimButtonCategory.Structures }, // Metal Walkway 3
                { 16,  PrimButtonCategory.Structures }, // Metal Walkway 4
                { 17,  PrimButtonCategory.Structures }, // Metal Walkway 5
                { 29,  PrimButtonCategory.Structures }, // Walkway Corner
                { 109, PrimButtonCategory.Structures }, // Metal Walkway 6
                { 44,  PrimButtonCategory.Structures }, // Metal Lift
                { 41,  PrimButtonCategory.Structures }, // Stairs
                { 80,  PrimButtonCategory.Structures }, // Side Step
                // Doors / gates / barriers
                { 85,  PrimButtonCategory.Structures }, // Wrought Iron Gate
                { 86,  PrimButtonCategory.Structures }, // Wrought Iron Gate Door
                { 99,  PrimButtonCategory.Structures }, // Doors 1
                { 117, PrimButtonCategory.Structures }, // Doors 2
                { 154, PrimButtonCategory.Structures }, // Doors 3
                { 244, PrimButtonCategory.Structures }, // Doors 4
                { 69,  PrimButtonCategory.Structures }, // Door AWning
                { 181, PrimButtonCategory.Structures }, // Bane Gate Left
                { 182, PrimButtonCategory.Structures }, // Bane Gate Right
                { 183, PrimButtonCategory.Structures }, // Cure Rail Barrier
                // Rails
                { 170, PrimButtonCategory.Structures }, // Rail Pillar
                { 171, PrimButtonCategory.Structures }, // Rail Track
                { 173, PrimButtonCategory.Structures }, // Curve Rail
                { 174, PrimButtonCategory.Structures }, // Straight Rail
                { 175, PrimButtonCategory.Structures }, // Rail barrier
                { 176, PrimButtonCategory.Structures }, // Railing
                // Rooftops / skylights / vents / chimneys
                { 73,  PrimButtonCategory.Structures }, // Building Top 1
                { 79,  PrimButtonCategory.Structures }, // Building Top 2
                { 92,  PrimButtonCategory.Structures }, // Building Top 3
                { 106, PrimButtonCategory.Structures }, // Building Top 4
                { 226, PrimButtonCategory.Structures }, // Skylight 1
                { 227, PrimButtonCategory.Structures }, // Skylight 2
                { 214, PrimButtonCategory.Structures }, // Vent
                { 225, PrimButtonCategory.Structures }, // Vent 2
                { 213, PrimButtonCategory.Structures }, // Chimney
                // Archways / pillars / statues / dishes
                { 50,  PrimButtonCategory.Structures }, // Estate Archway
                { 77,  PrimButtonCategory.Structures }, // Marble Archway
                { 115, PrimButtonCategory.Structures }, // Stone Archway
                { 118, PrimButtonCategory.Structures }, // Stone Archway Top
                { 247, PrimButtonCategory.Structures }, // Marble Pillar Top
                { 249, PrimButtonCategory.Structures }, // Marble Pillar
                { 252, PrimButtonCategory.Structures }, // Marble Pedestal
                { 189, PrimButtonCategory.Structures }, // Male Statue 2
                { 191, PrimButtonCategory.Structures }, // Female Statue
                { 192, PrimButtonCategory.Structures }, // Male Statue
                { 132, PrimButtonCategory.Structures }, // Satellite Dish
                { 135, PrimButtonCategory.Structures }, // Police Flagpole
                // Canopies / greenhouse
                { 19,  PrimButtonCategory.Structures }, // Green Canopy 1
                { 20,  PrimButtonCategory.Structures }, // Damaged Green Canopy 1
                { 21,  PrimButtonCategory.Structures }, // Green Canopy 2
                { 22,  PrimButtonCategory.Structures }, // Damaged Green Canopy 2
                { 24,  PrimButtonCategory.Structures }, // Damaged Green Canopy 3
                { 123, PrimButtonCategory.Structures }, // Brown Canopy
                { 241, PrimButtonCategory.Structures }, // Brown Yellow Canopy
                { 246, PrimButtonCategory.Structures }, // Red Canopy
                { 209, PrimButtonCategory.Structures }, // Blue Canopy
                { 210, PrimButtonCategory.Structures }, // Double Blue Canopy
                { 211, PrimButtonCategory.Structures }, // Corner Blue Canopy
                { 49,  PrimButtonCategory.Structures }, // Greenhouse
                // Pipes
                { 193, PrimButtonCategory.Structures }, // Pipe 1
                { 194, PrimButtonCategory.Structures }, // Pipe 2
                { 195, PrimButtonCategory.Structures }, // Pipe 3
                { 196, PrimButtonCategory.Structures }, // Pipe 4
                { 197, PrimButtonCategory.Structures }, // Pipe 5
                { 198, PrimButtonCategory.Structures }, // Pipe 6
                { 199, PrimButtonCategory.Structures }, // Pipe 7
                // Structures (additions)
                { 37, PrimButtonCategory.Structures }, // Wooden Post
                { 43, PrimButtonCategory.Structures }, // Side Step
                { 47, PrimButtonCategory.Structures }, // Liberty Statue
                { 48, PrimButtonCategory.Structures }, // USA Flag
                { 51, PrimButtonCategory.Structures }, // Wood Post & Cable
                { 62, PrimButtonCategory.Structures }, // Lion Statue
                { 70, PrimButtonCategory.Structures }, // Brown Pedestal


                // ===== Signs =====
                { 6,   PrimButtonCategory.Signs }, // Awaken Sign
                { 7,   PrimButtonCategory.Signs }, // Vote Bane Sign
                { 8,   PrimButtonCategory.Signs }, // Gas Price Sign
                { 9,   PrimButtonCategory.Signs }, // Silvery Moon Sign
                { 10,  PrimButtonCategory.Signs }, // Gas Sign
                { 12,  PrimButtonCategory.Signs }, // Motel Sign
                { 18,  PrimButtonCategory.Signs }, // Tyres Sign
                { 23,  PrimButtonCategory.Signs }, // Diner Sign
                { 25,  PrimButtonCategory.Signs }, // Silvery Moon Sign 2
                { 26,  PrimButtonCategory.Signs }, // Motel Sign 2
                { 31,  PrimButtonCategory.Signs }, // Liquors Sign
                { 45,  PrimButtonCategory.Signs }, // Terminal Sign
                { 52,  PrimButtonCategory.Signs }, // Street Sign (Blue)
                { 53,  PrimButtonCategory.Signs }, // Speed Limit Sign
                { 54,  PrimButtonCategory.Signs }, // Right Turn Sign
                { 147,  PrimButtonCategory.Signs }, // Target Practice Dummy
                { 55,  PrimButtonCategory.Signs }, // Do Not Enter Sign
                { 56,  PrimButtonCategory.Signs }, // Le Grande Sign
                { 57,  PrimButtonCategory.Signs }, // Rail Sign 2
                { 60,  PrimButtonCategory.Signs }, // Railway Stand
                { 63,  PrimButtonCategory.Signs }, // Motomo Sign
                { 64,  PrimButtonCategory.Signs }, // Choke Sign
                { 65,  PrimButtonCategory.Signs }, // Buy Cigarette Sign
                { 66,  PrimButtonCategory.Signs }, // Take Away Sign
                { 68,  PrimButtonCategory.Signs }, // Ala Mode Sign
                { 82,  PrimButtonCategory.Signs }, // Nude Sign
                { 84,  PrimButtonCategory.Signs }, // Shock Sign
                { 107, PrimButtonCategory.Signs }, // Choke Floor Sign
                { 111, PrimButtonCategory.Signs }, // Pool Sign
                { 119, PrimButtonCategory.Signs }, // Orange Sign
                { 125, PrimButtonCategory.Signs }, // Motel Sign
                { 133, PrimButtonCategory.Signs }, // UCPD Sign
                { 137, PrimButtonCategory.Signs }, // Repent Sign
                { 161, PrimButtonCategory.Signs }, // Men Working Sign
                { 163, PrimButtonCategory.Signs }, // Dead End Sign
                { 164, PrimButtonCategory.Signs }, // Danger Sign
                { 179, PrimButtonCategory.Signs }, // Bar Sign
                { 223, PrimButtonCategory.Signs }, // Hotel Sign
                { 248, PrimButtonCategory.Signs }, // Xpresso Sign

                // ===== Furniture =====
                { 83,  PrimButtonCategory.Furniture }, // Filing Cabinet
                { 93,  PrimButtonCategory.Furniture }, // Corner Desk
                { 95,  PrimButtonCategory.Furniture }, // Café Chair
                { 96,  PrimButtonCategory.Furniture }, // Café Table
                { 101, PrimButtonCategory.Furniture }, // Single Sofa
                { 102, PrimButtonCategory.Furniture }, // Brown Coffee Table
                { 103, PrimButtonCategory.Furniture }, // TV
                { 104, PrimButtonCategory.Furniture }, // Lamp
                { 105, PrimButtonCategory.Furniture }, // Double Sofa
                { 110, PrimButtonCategory.Furniture }, // Long Table
                { 112, PrimButtonCategory.Furniture }, // Computer
                { 126, PrimButtonCategory.Furniture }, // Brown Office Chair
                { 128, PrimButtonCategory.Furniture }, // Pool Table
                { 139, PrimButtonCategory.Furniture }, // Book Case
                { 158, PrimButtonCategory.Furniture }, // Brown Stool
                { 184, PrimButtonCategory.Furniture }, // Bed
                { 185, PrimButtonCategory.Furniture }, // Double Brown Desk
                { 186, PrimButtonCategory.Furniture }, // Single Brown Desk
                { 187, PrimButtonCategory.Furniture }, // Corner Brown Desk
                { 200, PrimButtonCategory.Furniture }, // Parasol
                { 202, PrimButtonCategory.Furniture }, // Inflatable Chair
                { 236, PrimButtonCategory.Furniture }, // Liquor Cabinet
                { 180, PrimButtonCategory.Furniture }, // Pool Chair
                { 204, PrimButtonCategory.Furniture }, // Diving Board
                { 217, PrimButtonCategory.Furniture }, // Washing Line
                { 219, PrimButtonCategory.Furniture }, // Winch
                { 61, PrimButtonCategory.Furniture }, // Boombox

                // ===== Lights =====
                { 113, PrimButtonCategory.Lights }, // Flood Light
                { 124, PrimButtonCategory.Lights }, // Colored Lights
                { 148, PrimButtonCategory.Lights }, // Police Roof Light
                { 151, PrimButtonCategory.Lights }, // Searchlight
                { 190, PrimButtonCategory.Lights }, // Wall Light
                { 229, PrimButtonCategory.Lights }, // Wall Garden Light
                { 230, PrimButtonCategory.Lights }, // Ground Garden Light
                { 235, PrimButtonCategory.Lights }, // Disco Light

                // ===== Collectibles =====
                { 36,  PrimButtonCategory.Collectibles }, // Key
                { 39,  PrimButtonCategory.Collectibles }, // Constitution (Treasure)
                { 71,  PrimButtonCategory.Collectibles }, // Stamina (Treasure)
                { 81,  PrimButtonCategory.Collectibles }, // Reflexes (Treasure)
                { 94,  PrimButtonCategory.Collectibles }, // Strength (Treasure)
                { 136, PrimButtonCategory.Collectibles }, // Reflexes (Treasure)
                { 144, PrimButtonCategory.Collectibles }, // Napkin
                { 232, PrimButtonCategory.Collectibles }, // Red Folder
                { 233, PrimButtonCategory.Collectibles }, // Disk
                { 237, PrimButtonCategory.Collectibles }, // VHS Tape
                { 239, PrimButtonCategory.Collectibles }, // Weed Away


            
    };

        public static string GetName(int primNumber)
            => _names.TryGetValue(primNumber, out var name) ? name : $"Prim {primNumber}";

        public static PrimButtonCategory GetCategory(int primNumber)
            => _categories.TryGetValue(primNumber, out var cat) ? cat : PrimButtonCategory.Misc;

        // Helpers for palette building
        public static IEnumerable<int> AllIds() => _names.Keys.OrderBy(k => k);

        // Optional: quick way to tweak categories at runtime/startup if you want
        public static void SetCategory(int primNumber, PrimButtonCategory category) => _categories[primNumber] = category;
        public static bool TryGetCategory(int primNumber, out PrimButtonCategory category)
        {
            return _categories.TryGetValue(primNumber, out category); // define _categories similarly to _names
        }
    }
}
