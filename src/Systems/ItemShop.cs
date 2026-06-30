using System;
using System.Collections.Generic;
using System.Linq;
using WcsInfinity.Models;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  ITEM SHOP — магазин предметов за золото (как в старых WCS).        ║
// ║  Предметы дают тактические эффекты, НЕ ломающие баланс (anti-P2W). ║
// ║  Покупка за золото с киллов; часть предметов одноразовые (раунд),  ║
// ║  часть — пассивные на сессию.                                     ║
// ╚══════════════════════════════════════════════════════════════════╝
public enum ItemKind { Consumable, RoundPassive, SessionPassive }
public enum ItemSlot { Boots, Gloves, Amulet, Potion, Relic, Special }

public class ShopItem
{
    public int Id;
    public string Name = "";
    public string Desc = "";
    public ItemKind Kind;
    public ItemSlot Slot;
    public long Price;
    public string Rarity = "Обычный";
    public int MaxStack = 1;
    // эффект-теги, читаемые боевым движком
    public Dictionary<string,float> Effects = new();
}

public class OwnedItem { public int ItemId; public int Count; public bool Active; }

public static class ItemShop
{
    // Каталог. Сбалансирован: бонусы маленькие, цены кусаются.
    public static readonly List<ShopItem> Catalog = new()
    {
        new ShopItem{ Id=1, Name=L10n.Get("Systems_ItemShop_Name_Boots", "Сапоги Эфира"), Desc=L10n.Get("Systems_ItemShop_Boot_Speed", "+8% скорость на раунд"), Kind=ItemKind.RoundPassive,
            Slot=ItemSlot.Boots, Price=600, Rarity="Обычный", Effects={["speed"]=0.08f} },
        new ShopItem{ Id=2, Name=L10n.Get("Systems_ItemShop_Name_Gloves", "Перчатки Хвата"), Desc=L10n.Get("Systems_ItemShop_Gloves_KnifeDmg", "+10% урон ножом"), Kind=ItemKind.RoundPassive,
            Slot=ItemSlot.Gloves, Price=750, Rarity="Обычный", Effects={["knife_dmg"]=0.10f} },
        new ShopItem{ Id=3, Name=L10n.Get("Systems_ItemShop_Name_Amulet", "Амулет Жизни"), Desc=L10n.Get("Systems_ItemShop_Amulet_HP", "+15 HP при спавне"), Kind=ItemKind.RoundPassive,
            Slot=ItemSlot.Amulet, Price=900, Rarity="Редкий", Effects={["bonus_hp"]=15f} },
        new ShopItem{ Id=4, Name=L10n.Get("Systems_ItemShop_Name_BerserkPotion", "Зелье Берсерка"), Desc=L10n.Get("Systems_ItemShop_BerserkPotion", "+20% урон 8 сек (актив)"), Kind=ItemKind.Consumable,
            Slot=ItemSlot.Potion, Price=500, Rarity="Редкий", MaxStack=3, Effects={["berserk"]=0.20f,["dur"]=8f} },
        new ShopItem{ Id=5, Name=L10n.Get("Systems_ItemShop_Name_PhantomPotion", "Зелье Фантома"), Desc=L10n.Get("Systems_ItemShop_PhantomPotion", "невидимость 4 сек (актив)"), Kind=ItemKind.Consumable,
            Slot=ItemSlot.Potion, Price=700, Rarity="Эпик", MaxStack=2, Effects={["invis"]=1f,["dur"]=4f} },
        new ShopItem{ Id=6, Name=L10n.Get("Systems_ItemShop_Name_EtherRelic", "Реликвия Эфира"), Desc=L10n.Get("Systems_ItemShop_EtherRelic", "+15% накопление Эфира"), Kind=ItemKind.SessionPassive,
            Slot=ItemSlot.Relic, Price=2500, Rarity="Эпик", Effects={["ether_gain"]=0.15f} },
        new ShopItem{ Id=7, Name=L10n.Get("Systems_ItemShop_Name_RecallStone", "Камень Возврата"), Desc=L10n.Get("Systems_ItemShop_RecallStone", "1 раз/раунд: телепорт на спавн"), Kind=ItemKind.Consumable,
            Slot=ItemSlot.Special, Price=400, Rarity="Обычный", MaxStack=1, Effects={["recall"]=1f} },
        new ShopItem{ Id=8, Name=L10n.Get("Systems_ItemShop_Name_DragonHeart", "Сердце Дракона"), Desc=L10n.Get("Systems_ItemShop_DragonHeart", "+25 HP, +5% урон (сессия)"), Kind=ItemKind.SessionPassive,
            Slot=ItemSlot.Relic, Price=5000, Rarity="Легендарный", Effects={["bonus_hp"]=25f,["dmg"]=0.05f} },
        new ShopItem{ Id=9, Name=L10n.Get("Systems_ItemShop_Name_EternityShard", "Осколок Вечности"), Desc=L10n.Get("Systems_ItemShop_EternityShard", "-10% КД ультимейта (сессия)"), Kind=ItemKind.SessionPassive,
            Slot=ItemSlot.Relic, Price=8000, Rarity="Мифик", Effects={["ult_cdr"]=0.10f} },
    };

    public static ShopItem? Get(int id) => Catalog.FirstOrDefault(i => i.Id == id);

    public static (bool ok, string msg) Buy(PlayerData p, int itemId)
    {
        var item = Get(itemId);
        if (item == null) return (false, L10n.Get("Systems_ItemShop_NotFound", "Предмет не найден."));
        if (p.Gold < item.Price) return (false, L10n.GetF("Systems_ItemShop_NotEnoughGold", "Не хватает золота ({0}/{1}).",
            ("p.Gold", p.Gold), ("item.Price", item.Price)));

        var owned = p.Inventory.FirstOrDefault(o => o.ItemId == itemId);
        if (owned != null && owned.Count >= item.MaxStack)
            return (false, L10n.GetF("Systems_ItemShop_MaxStack", "Достигнут лимит ({0}).",
                ("item.MaxStack", item.MaxStack)));

        p.Gold -= item.Price;
        if (owned == null) p.Inventory.Add(new OwnedItem { ItemId = itemId, Count = 1 });
        else owned.Count++;
        return (true, L10n.GetF("Systems_ItemShop_BuySuccess", "Куплено: {0} ({1}).",
            ("item.Name", item.Name), ("item.Rarity", item.Rarity)));
    }

    // Суммарные пассивные эффекты игрока (для боевого движка)
    public static Dictionary<string,float> AggregateEffects(PlayerData p)
    {
        var acc = new Dictionary<string,float>();
        foreach (var o in p.Inventory)
        {
            var it = Get(o.ItemId);
            if (it == null || it.Kind == ItemKind.Consumable) continue;
            foreach (var (k,v) in it.Effects)
                acc[k] = acc.GetValueOrDefault(k) + v * o.Count;
        }
        return acc;
    }
}
