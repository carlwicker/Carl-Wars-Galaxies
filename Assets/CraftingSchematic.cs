using UnityEngine;

[CreateAssetMenu(menuName = "Crafting/Schematic")]
public sealed class CraftingSchematic : ScriptableObject
{
    public string schematicId = "engine";
    public string displayName = "Engine Cube";
    public string firstResourceId = "metal";
    public int firstResourceCount = 5;
    public string secondResourceId = "petrochemicals";
    public int secondResourceCount = 3;
    public string prototypeId = "engine_prototype";
    public string outputId = "engine_cube";
    public bool usesExperimentation = true;

    public static CraftingSchematic LoadById(string id)
    {
        string path;
        switch (id)
        {
            case "engine": path = "EngineSchematic"; break;
            case "power": path = "PowerSchematic"; break;
            case "surveying_tool": path = "SurveyingToolSchematic"; break;
            default: return null;
        }

        CraftingSchematic schematic = Resources.Load<CraftingSchematic>(path);
        return schematic != null && schematic.schematicId == id ? schematic : null;
    }
}
