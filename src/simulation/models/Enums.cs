namespace SocietyPunk.Simulation.Models;

public enum Era
{
    Agrarian,
    EarlyIndustrial,
    Industrial,
    Dieselpunk
}

public enum Race
{
    Human,
    Orc,
    Goblin,
    Elf,
    Gnoll
}

public enum ProfessionType
{
    // Building-bound
    Farmer,
    Miller,
    Baker,
    Miner,
    Smelter,
    Blacksmith,
    Weaver,
    Tailor,
    Scholar,
    Alchemist,

    // Roaming
    Hauler,
    Builder,
    Woodcutter,
    Hunter,
    Fisher,

    // General
    Laborer,
    Unemployed
}

public enum GoodCategory
{
    RawResource,
    ProcessedMaterial,
    ManufacturedGood,
    FuelGood,
    ConstructionGood,
    MagicalGood
}

public enum BuildingRole
{
    Production,
    Storage,
    Housing,
    Logistics,
    Research,
    Military,
    Magic
}

public enum PopState
{
    Idle,
    Working,
    Hauling,
    Eating,
    Sleeping,
    Constructing,
    Walking
}

public enum RoadType
{
    None,
    DirtPath,
    GravelRoad,
    Cobblestone,
    CartTrack,
    SteamRoad,
    Rail
}

public enum TerrainType
{
    Grass,
    Forest,
    Hills,
    Mountain,
    Desert,
    Wetland,
    Water,
    Coast
}

public enum WealthClass
{
    Destitute,
    Poor,
    WorkingClass,
    Artisan,
    MiddleClass,
    UpperClass,
    Elite
}
