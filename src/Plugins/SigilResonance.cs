using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WcsInfinity.Plugins;

// ╔══════════════════════════════════════════════════════════════════════╗
// ║  РЕЗОНАНС ПЕЧАТЕЙ (SIGIL RESONANCE) — система комбо заклинаний.        ║
// ║  Если скастовать 2 печати в окне резонанса (3.5 сек), они сливаются   ║
// ║  в УСИЛЕННОЕ комбо-заклинание. Глубина мастерства: знать не только    ║
// ║  жесты, но и какие печати резонируют друг с другом.                   ║
// ║                                                                        ║
// ║  Никто на CS2 не делал цепочки-комбо из жестовых заклинаний.          ║
// ╚══════════════════════════════════════════════════════════════════════╝
public class ResonanceCombo
{
    public string SigilA = "";
    public string SigilB = "";
    public string Name = "";
    public string Desc = "";
    public string Glyph = "✦✦";
    public Action<CCSPlayerController, Systems.IEngineApi>? Unleash;
}

public class SigilResonance
{
    private readonly Systems.IEngineApi _engine;
    public SigilResonance(Systems.IEngineApi engine) { _engine = engine; }

    // Последняя печать игрока и время каста (для окна резонанса)
    private readonly Dictionary<int, (string sigil, DateTime at)> _last = new();
    private const double WindowSec = 3.5;

    // ── Таблица резонансов (комбо двух печатей) ──
    public readonly List<ResonanceCombo> Combos = new()
    {
        new ResonanceCombo{
            SigilA="Эфирный Рывок", SigilB="Громовая Дуга", Glyph="⚡➤",
            Name="МОЛНИЕНОСНЫЙ КЛИНОК",
            Desc="Рывок сквозь врага оставляет грозовой след — урон всем на линии.",
            Unleash=(p,e)=>{ var pos=e.GetPosition(p.Slot);
                e.SpawnParticle("particles/aether_lightning_blade.vpcf",pos.x,pos.y,pos.z+30);
                e.PlaySound(p.Slot,"aether/resonance_blade");
                p.PrintToCenterHtml("<font color='#ffe14d'>⚡➤ МОЛНИЕНОСНЫЙ КЛИНОК!</font>"); } },

        new ResonanceCombo{
            SigilA="Купол Эфира", SigilB="Громовая Дуга", Glyph="◐⚡",
            Name="ГРОЗОВАЯ КРЕПОСТЬ",
            Desc="Купол бьёт молниями по всем, кто подходит — 6 сек.",
            Unleash=(p,e)=>{ var pos=e.GetPosition(p.Slot);
                e.SpawnParticle("particles/aether_storm_fortress.vpcf",pos.x,pos.y,pos.z);
                e.AddHealth(p.Slot,40,200);
                p.PrintToCenterHtml("<font color='#46e0ff'>◐⚡ ГРОЗОВАЯ КРЕПОСТЬ!</font>"); } },

        new ResonanceCombo{
            SigilA="Эфирный Рывок", SigilB="Купол Эфира", Glyph="➤◐",
            Name="ФАНТОМНЫЙ ПОБЕГ",
            Desc="Рывок + щит + кратковременная невидимость. Идеальный отход.",
            Unleash=(p,e)=>{ var pos=e.GetPosition(p.Slot);
                e.SetInvisible(p.Slot,true);
                e.SpawnParticle("particles/aether_phantom.vpcf",pos.x,pos.y,pos.z);
                p.PrintToCenterHtml("<font color='#b388ff'>➤◐ ФАНТОМНЫЙ ПОБЕГ!</font>"); } },

        new ResonanceCombo{
            SigilA="Зов Бездны", SigilB="Громовая Дуга", Glyph="✸⚡",
            Name="АПОКАЛИПСИС ЭФИРА",
            Desc="Мифик-комбо: буря тьмы и молний накрывает всю зону. Экран дрожит.",
            Unleash=(p,e)=>{ var pos=e.GetPosition(p.Slot);
                e.SpawnParticle("particles/aether_apocalypse.vpcf",pos.x,pos.y,pos.z);
                e.PlaySound(p.Slot,"aether/apocalypse");
                p.PrintToChat(" \x0B[РЕЗОНАНС] \x01✸⚡ АПОКАЛИПСИС ЭФИРА — мифическое комбо!");
                p.PrintToCenterHtml("<font color='#ff3b6b'>✸⚡ АПОКАЛИПСИС ЭФИРА ✸⚡</font>"); } },
    };

    // Вызывается ПОСЛЕ успешного каста печати.
    // Возвращает true, если сработал резонанс (комбо) — тогда обычный эффект можно усилить/заменить.
    public bool OnSigilCast(CCSPlayerController p, string sigilName)
    {
        var now = DateTime.UtcNow;
        if (_last.TryGetValue(p.Slot, out var prev) && (now - prev.at).TotalSeconds <= WindowSec)
        {
            // ищем комбо (в любом порядке)
            var combo = Combos.FirstOrDefault(c =>
                (c.SigilA == prev.sigil && c.SigilB == sigilName) ||
                (c.SigilA == sigilName && c.SigilB == prev.sigil));
            if (combo != null)
            {
                combo.Unleash?.Invoke(p, _engine);
                _last.Remove(p.Slot);          // комбо «съедает» цепочку
                return true;
            }
        }
        // запоминаем как первую печать цепочки
        _last[p.Slot] = (sigilName, now);
        // подсказка о возможном резонансе
        HintResonance(p, sigilName);
        return false;
    }

    // Подсказать игроку, что можно зарезонировать
    private void HintResonance(CCSPlayerController p, string sigilName)
    {
        var options = Combos
            .Where(c => c.SigilA == sigilName || c.SigilB == sigilName)
            .Select(c => c.Name).Distinct().ToList();
        if (options.Count > 0)
            p.PrintToChat($" \x0B[РЕЗОНАНС] \x01Окно открыто! Скастуй вторую печать для комбо.");
    }

    public void ClearPlayer(int slot) => _last.Remove(slot);
}
