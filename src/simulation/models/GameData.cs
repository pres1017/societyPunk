using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocietyPunk.Simulation.Models;

/// <summary>
/// Registry of all immutable game definitions loaded from JSON.
/// </summary>
public class GameData
{
    public Dictionary<string, Good> Goods { get; set; } = new();
    public Dictionary<string, Recipe> Recipes { get; set; } = new();
    public Dictionary<string, BuildingDef> Buildings { get; set; } = new();
    public Dictionary<string, Tech> Techs { get; set; } = new();
    public Dictionary<Race, RaceDef> Races { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static GameData LoadFromDirectory(string dataDir)
    {
        var data = new GameData();

        var goods = DeserializeList<Good>(Path.Combine(dataDir, "goods.json"));
        foreach (var g in goods) data.Goods[g.Id] = g;

        var recipes = DeserializeList<Recipe>(Path.Combine(dataDir, "recipes.json"));
        foreach (var r in recipes) data.Recipes[r.Id] = r;

        var buildings = DeserializeList<BuildingDef>(Path.Combine(dataDir, "buildings.json"));
        foreach (var b in buildings) data.Buildings[b.Id] = b;

        var techs = DeserializeList<Tech>(Path.Combine(dataDir, "techs.json"));
        foreach (var t in techs) data.Techs[t.Id] = t;

        var races = DeserializeList<RaceDef>(Path.Combine(dataDir, "races.json"));
        foreach (var r in races) data.Races[r.Race] = r;

        return data;
    }

    public static string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static List<T> DeserializeList<T>(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
    }
}
