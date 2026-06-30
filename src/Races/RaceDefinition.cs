using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WcsInfinity.Races;

// Тиры рас (сила/редкость). Раньше жил в IRace.cs (легаси).
public enum RaceTier { T1_Spark = 1, T2_Combo = 2, T3_Power = 3, Guild = 4, Mythic = 5 }

// Data-driven описание расы из races.json. Позволяет растить контент до тысяч рас
// без перекомпиляции — новые расы = новые записи в JSON + теги эффектов.
//
// ВАЖНО: ключи JSON сокращены (n/t/d/max/cd/ether) для компактности файла рас.
// Поля Index/Effect/Value связывают навык с боевым движком (RaceRuntime).
public class AbilityDef
{
    [JsonPropertyName("n")]      public string Name { get; set; } = "";
    [JsonPropertyName("d")]      public string Description { get; set; } = "";
    [JsonPropertyName("t")]      public string Type { get; set; } = "Passive"; // Passive/Active/Ultimate
    [JsonPropertyName("max")]    public int MaxLevel { get; set; } = 8;
    [JsonPropertyName("cd")]     public float Cooldown { get; set; } = 0f;
    [JsonPropertyName("ether")]  public int EtherCost { get; set; } = 0;

    // Связь с движком (опциональные теги; если пусто — навык чисто пассивно-числовой)
    [JsonPropertyName("effect")] public string Effect { get; set; } = "";   // тег из EffectLibrary
    [JsonPropertyName("value")]  public float Value { get; set; } = 0f;     // параметр эффекта

    // Назначается автоматически при загрузке (позиция в списке способностей)
    [JsonIgnore] public int Index { get; set; }
}

public class RaceDefinition
{
    [JsonPropertyName("id")]        public int Id { get; set; }
    [JsonPropertyName("name")]      public string Name { get; set; } = "";
    [JsonPropertyName("lore")]      public string Lore { get; set; } = "";
    [JsonPropertyName("archetype")] public string Archetype { get; set; } = "";
    [JsonPropertyName("tier")]      public int Tier { get; set; } = 1;
    [JsonPropertyName("division")]  public int Division { get; set; } = 1;
    [JsonPropertyName("unlock")]    public int RequiredLevelToUnlock { get; set; } = 0;
    [JsonPropertyName("guild")]     public bool IsGuildRace { get; set; } = false;
    [JsonPropertyName("abilities")] public List<AbilityDef> Abilities { get; set; } = new();

    public RaceTier TierEnum => (RaceTier)Tier;

    // Проставить Index каждой способности (вызывается после десериализации)
    public void IndexAbilities()
    {
        for (int i = 0; i < Abilities.Count; i++) Abilities[i].Index = i;
    }
}
