using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Systems;

public static class AetherionConst
{
    public static readonly object? DisabledMenuOption = AetherionConstResolve.TryResolveDisabledOption();
    private static class AetherionConstResolve
    {
        public static object? TryResolveDisabledOption()
        {
            try
            {
                var type = Type.GetType("CS2MenuManager.API.Enum.DisableOption, CS2MenuManager") ?? Type.GetType("CS2MenuManager.Enum.DisableOption, CS2MenuManager");
                if (type == null) return null;
                var field = type.GetField("DisableHideNumber") ?? type.GetField("DisableOptionHide") ?? type.GetField("Hide");
                return field?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }
    }
}
