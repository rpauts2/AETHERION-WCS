using System;
using WcsInfinity.Systems;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Plugins;

// ╔══════════════════════════════════════════════════════════════════════╗
// ║  ЭФИРНЫЕ ПЕЧАТИ (AETHER SIGILS) — заклинания через ЖЕСТЫ движения.     ║
// ║  ПЕРВОЕ на CS2: игрок «рисует» печать последовательностью направлений ║
// ║  взгляда/движения (как паттерн-замок), и активирует мощное комбо.     ║
// ║                                                                        ║
// ║  Пример: зажал спец-клавишу → крутанул мышь Влево-Вверх-Вправо →       ║
// ║  сложилась печать «Громовая Дуга» → каст. Скилл руками, не кулдаунами. ║
// ╚══════════════════════════════════════════════════════════════════════╝
public enum Gesture { Up, Down, Left, Right }

public class Sigil
{
    public string Name = "";
    public string Desc = "";
    public List<Gesture> Pattern = new();   // последовательность жестов
    public int EtherCost;
    public Action<CCSPlayerController, IEngineApi>? Cast;
    public string Glyph = "✦";              // символ для HUD
}

public class AetherSigils
{
    private readonly BasePlugin _plugin;
    private readonly Func<ulong,int> _getEther;
    private readonly Action<ulong,int> _spendEther;
    private readonly IEngineApi _engine;

    public AetherSigils(BasePlugin plugin, IEngineApi engine,
        Func<ulong,int> getEther, Action<ulong,int> spendEther)
    {
        _plugin = plugin; _engine = engine;
        _getEther = getEther; _spendEther = spendEther;
    }

    // Состояние «рисования» печати на игрока
    private class DrawState
    {
        public bool Active;
        public List<Gesture> Buffer = new();
        public float LastYaw, LastPitch;
        public DateTime StartedUtc;
    }
    private readonly Dictionary<int, DrawState> _draw = new();

    // Колбэк после успешного каста печати (имя печати) — для Резонанса
    public Action<CounterStrikeSharp.API.Core.CCSPlayerController, string>? OnCast;

    // ── Каталог печатей ──
    public readonly List<Sigil> Sigils = new()
    {
        new Sigil{ Name="Громовая Дуга", Glyph="⚡", EtherCost=50,
            Pattern=new(){Gesture.Left,Gesture.Up,Gesture.Right},
            Desc="Дуга молнии перед игроком, цепной урон.",
            Cast=(p,e)=>{ var pos=e.GetPosition(p.Slot);
                e.SpawnParticle("particles/aether_thunder_arc.vpcf",pos.x,pos.y,pos.z+40);
                p.PrintToCenterHtml("<font color='#ffe14d'>⚡ ГРОМОВАЯ ДУГА!</font>"); } },

        new Sigil{ Name="Эфирный Рывок", Glyph="➤", EtherCost=35,
            Pattern=new(){Gesture.Down,Gesture.Up},
            Desc="Мгновенный рывок в направлении взгляда.",
            Cast=(p,e)=>{ var pos=e.GetPosition(p.Slot);
                var pawn=p.PlayerPawn?.Value; if(pawn==null)return;
                var f=Fwd(pawn.EyeAngles);
                e.Teleport(p.Slot,pos.x+f.X*250,pos.y+f.Y*250,pos.z+30);
                e.SpawnParticle("particles/aether_dash.vpcf",pos.x,pos.y,pos.z); } },

        new Sigil{ Name="Купол Эфира", Glyph="◐", EtherCost=70,
            Pattern=new(){Gesture.Left,Gesture.Down,Gesture.Right,Gesture.Up},
            Desc="Защитный купол: поглощает урон 4 сек.",
            Cast=(p,e)=>{ var pos=e.GetPosition(p.Slot);
                e.SpawnParticle("particles/aether_dome.vpcf",pos.x,pos.y,pos.z);
                e.AddHealth(p.Slot,50,150);
                p.PrintToCenterHtml("<font color='#46e0ff'>◐ КУПОЛ ЭФИРА!</font>"); } },

        new Sigil{ Name="Зов Бездны", Glyph="✸", EtherCost=120,
            Pattern=new(){Gesture.Up,Gesture.Up,Gesture.Down,Gesture.Down,Gesture.Left,Gesture.Right},
            Desc="Мифическая печать: взрыв тьмы, массовый урон+замедление.",
            Cast=(p,e)=>{ var pos=e.GetPosition(p.Slot);
                e.SpawnParticle("particles/aether_abyss.vpcf",pos.x,pos.y,pos.z);
                p.PrintToChat(" [SIGIL] ✸ ЗОВ БЕЗДНЫ — мифическая печать активирована!");
                p.PrintToCenterHtml("<font color='#a020f0'>✸ ЗОВ БЕЗДНЫ ✸</font>"); } },
    };

    // Начать рисование (по нажатию спец-клавиши, напр. R+зажать)
    public void BeginDraw(CCSPlayerController p)
    {
        var pawn=p.PlayerPawn?.Value; if(pawn==null)return;
        _draw[p.Slot]=new DrawState{
            Active=true, Buffer=new(),
            LastYaw=pawn.EyeAngles.Y, LastPitch=pawn.EyeAngles.X,
            StartedUtc=DateTime.UtcNow };
        p.PrintToCenterHtml("<font color='#7c5cff'>✎ Рисуй печать движением взгляда…</font>");
        TrackLoop(p);
    }

    private void TrackLoop(CCSPlayerController p)
    {
        _plugin.AddTimer(0.08f, ()=>{
            if(!p.IsValid || !_draw.TryGetValue(p.Slot,out var st) || !st.Active) return;
            var pawn=p.PlayerPawn?.Value; if(pawn==null)return;
            float yaw=pawn.EyeAngles.Y, pitch=pawn.EyeAngles.X;
            float dYaw=Norm180(yaw-st.LastYaw), dPitch=pitch-st.LastPitch;
            const float TH=12f; // порог распознавания жеста
            Gesture? g=null;
            if(MathF.Abs(dYaw)>MathF.Abs(dPitch)){
                if(dYaw>TH) g=Gesture.Right; else if(dYaw<-TH) g=Gesture.Left;
            } else {
                if(dPitch>TH) g=Gesture.Down; else if(dPitch<-TH) g=Gesture.Up;
            }
            if(g!=null && (st.Buffer.Count==0 || st.Buffer[^1]!=g)){
                st.Buffer.Add(g.Value);
                st.LastYaw=yaw; st.LastPitch=pitch;
                p.PrintToCenterHtml($"<font color='#7c5cff'>✎ {string.Concat(st.Buffer.Select(Arrow))}</font>");
            } else { st.LastYaw=yaw; st.LastPitch=pitch; }
            // авто-стоп через 3 сек
            if((DateTime.UtcNow-st.StartedUtc).TotalSeconds>3) { EndDraw(p); return; }
            TrackLoop(p);
        });
    }

    // Завершить рисование и попытаться скастовать совпавшую печать
    public void EndDraw(CCSPlayerController p)
    {
        if(!_draw.TryGetValue(p.Slot,out var st) || !st.Active) return;
        st.Active=false;
        var match=Sigils.FirstOrDefault(s=>s.Pattern.SequenceEqual(st.Buffer));
        if(match==null){ p.PrintToCenterHtml("<font color='#888'>✗ Печать не распознана</font>"); return; }
        int ether=_getEther(p.SteamID);
        if(ether<match.EtherCost){ p.PrintToCenterHtml($"<font color='#e06666'>Мало Эфира ({ether}/{match.EtherCost})</font>"); return; }
        _spendEther(p.SteamID, match.EtherCost);
        match.Cast?.Invoke(p,_engine);
        OnCast?.Invoke(p, match.Name);   // уведомить Резонанс
    }

    private static string Arrow(Gesture g)=>g switch{
        Gesture.Up=>"↑",Gesture.Down=>"↓",Gesture.Left=>"←",_=>"→"};
    private static float Norm180(float a){ while(a>180)a-=360; while(a<-180)a+=360; return a; }
    private static Vector Fwd(QAngle a){
        float pp=a.X*MathF.PI/180f,yy=a.Y*MathF.PI/180f;
        return new Vector(MathF.Cos(pp)*MathF.Cos(yy),MathF.Cos(pp)*MathF.Sin(yy),-MathF.Sin(pp)); }

    public void CleanupSlot(int slot) => _draw.Remove(slot);
}
