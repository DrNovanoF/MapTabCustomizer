using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MapTabCustomizer
{
    [StaticConstructorOnStartup]
    internal static class MapTabCustomizerBootstrap
    {
        static MapTabCustomizerBootstrap()
        {
            Harmony harmony = new Harmony("cleme.maptabcustomizer");
            harmony.PatchAll();
            LtoColonyGroupsCompatibility.TryPatch(harmony);
        }
    }

    [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.ColonistBarOnGUI))]
    internal static class ColonistBarOnGUIPatch
    {
        private static readonly AccessTools.FieldRef<ColonistBar, ColonistBarColonistDrawer> Drawer =
            AccessTools.FieldRefAccess<ColonistBar, ColonistBarColonistDrawer>("drawer");
        private static readonly FastInvokeHandler GroupFrameRect =
            MethodInvoker.GetHandler(AccessTools.Method(typeof(ColonistBarColonistDrawer), "GroupFrameRect"));

        private static bool Prefix()
        {
            return true;
        }

        private static void Postfix(ColonistBar __instance)
        {
            if (LtoColonyGroupsCompatibility.Active || Find.CurrentMap == null) return;

            IEnumerable<IGrouping<int, ColonistBar.Entry>> mapGroups = __instance.Entries
                .Where(entry => entry.map != null)
                .GroupBy(entry => entry.group);

            foreach (IGrouping<int, ColonistBar.Entry> group in mapGroups)
            {
                Map map = group.First().map;
                MapTabCustomizationComponent customization = map.GetComponent<MapTabCustomizationComponent>();
                Rect groupRect = (Rect)GroupFrameRect(Drawer(__instance), new object[] { group.Key });
                MapTabRenderer.DrawCustomization(map, customization, groupRect);
            }
        }
    }

    [HarmonyPatch(typeof(ColonistBar), "CheckRecacheEntries")]
    internal static class VanillaColonistBarRecachePatch
    {
        private static void Postfix(ColonistBar __instance)
        {
            if (LtoColonyGroupsCompatibility.Active || !MapTabRenderer.UseCompactMapTabs) return;
            MapTabRenderer.CompactEntries(__instance.Entries);
        }
    }

    internal static class MapTabRenderer
    {
        internal static bool UseCompactMapTabs => MapTabCustomizerMod.Settings != null &&
                                                  MapTabCustomizerMod.Settings.ReplacePawnPortraitsWithIcon;
        internal static bool ShowActiveMapPawns => MapTabCustomizerMod.Settings != null &&
                                                   MapTabCustomizerMod.Settings.ShowActiveMapPawns;

        internal static void CompactEntries(IList entries)
        {
            if (entries == null) return;
            HashSet<Map> seenMaps = new HashSet<Map>();
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                object entry = entries[index];
                FieldInfo mapField = AccessTools.Field(entry.GetType(), "map");
                Map map = mapField?.GetValue(entry) as Map;
                if (map == null || (ShowActiveMapPawns && map == Find.CurrentMap)) continue;
                if (!seenMaps.Add(map)) entries.RemoveAt(index);
            }
        }

        internal static void NotifyLayoutChanged()
        {
            Find.ColonistBar?.MarkColonistsDirty();
            LtoColonyGroupsCompatibility.MarkColonistsDirty();
        }

        internal static void DrawCompactMapTabs()
        {
            if (Find.Maps == null || Find.Maps.Count == 0) return;
            const float tabWidth = 74f;
            const float tabHeight = 58f;
            const float spacing = 6f;
            int mapCount = Find.Maps.Count;
            float totalWidth = mapCount * tabWidth + Mathf.Max(0, mapCount - 1) * spacing;
            float x = (UI.screenWidth - totalWidth) / 2f;
            float y = 21f;

            foreach (Map map in Find.Maps)
            {
                Rect tabRect = new Rect(x, y, tabWidth, tabHeight);
                DrawCompactMapTab(map, tabRect);
                x += tabWidth + spacing;
            }
        }

        private static void DrawCompactMapTab(Map map, Rect tabRect)
        {
            MapTabCustomizationComponent customization = map.GetComponent<MapTabCustomizationComponent>();
            Texture2D icon = MapTabIcons.Get(customization.IconIndex);
            bool hovered = Mouse.IsOver(tabRect);
            Color background = map == Find.CurrentMap
                ? new Color(0.28f, 0.34f, 0.40f, 0.98f)
                : hovered
                    ? new Color(0.20f, 0.20f, 0.20f, 0.98f)
                    : new Color(0.10f, 0.10f, 0.10f, 0.96f);
            Widgets.DrawBoxSolid(tabRect, background);
            Widgets.DrawBox(tabRect, map == Find.CurrentMap ? 2 : 1);

            if (icon != null)
            {
                GUI.DrawTexture(new Rect(tabRect.center.x - 18f, tabRect.y + 5f, 36f, 36f),
                    icon, ScaleMode.ScaleToFit, true);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(tabRect.x, tabRect.y + 4f, tabRect.width, 34f), "?");
                Text.Anchor = TextAnchor.UpperLeft;
            }

            bool showLabel = !customization.CustomLabel.NullOrEmpty() &&
                             (MapTabCustomizerMod.Settings == null ||
                              !MapTabCustomizerMod.Settings.ShowOnlyOnHover || hovered ||
                              (MapTabCustomizerMod.Settings.AlwaysShowActiveLabel && map == Find.CurrentMap));
            if (showLabel)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(tabRect.x + 3f, tabRect.yMax - 18f, tabRect.width - 6f, 16f),
                    customization.CustomLabel);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            if (hovered) TooltipHandler.TipRegion(tabRect, "MTC_CompactTabHint".Translate());
            Event current = Event.current;
            if (current.type != EventType.MouseDown || !tabRect.Contains(current.mousePosition)) return;
            if (current.button == 0)
            {
                Current.Game.CurrentMap = map;
                current.Use();
            }
            else if (current.button == 1)
            {
                Find.WindowStack.Add(new Dialog_EditMapTab(map));
                current.Use();
            }
        }

        internal static void DrawCustomization(Map map, MapTabCustomizationComponent customization, Rect groupRect)
        {
            bool hasLabel = !customization.CustomLabel.NullOrEmpty();
            Texture2D icon = MapTabIcons.Get(customization.IconIndex);
            float width = (icon != null ? 22f : 0f) + (hasLabel ? Mathf.Min(130f, Text.CalcSize(customization.CustomLabel).x + 10f) : 0f);
            Rect editArea = new Rect(groupRect.x, groupRect.yMax + 2f, Mathf.Max(groupRect.width, width), 22f);
            Rect clickArea = new Rect(groupRect.x, groupRect.y, Mathf.Max(groupRect.width, width), groupRect.height + 24f);
            bool hovered = Mouse.IsOver(clickArea);
            bool forceActiveLabel = MapTabCustomizerMod.Settings != null &&
                                    MapTabCustomizerMod.Settings.AlwaysShowActiveLabel &&
                                    map == Find.CurrentMap;
            bool showCustomization = MapTabCustomizerMod.Settings == null ||
                                     !MapTabCustomizerMod.Settings.ShowOnlyOnHover || hovered || forceActiveLabel;

            bool replaceThisMap = UseCompactMapTabs &&
                                  (!ShowActiveMapPawns || map != Find.CurrentMap);
            if (icon != null && replaceThisMap)
            {
                DrawIconReplacement(groupRect, icon);
            }

            if ((hasLabel || icon != null) && showCustomization)
            {
                Widgets.DrawBoxSolid(editArea, new Color(0.10f, 0.10f, 0.10f, 0.82f));
                float x = editArea.x + 3f;
                if (icon != null)
                {
                    GUI.DrawTexture(new Rect(x, editArea.y + 2f, 18f, 18f), icon);
                    x += 22f;
                }
                if (hasLabel)
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(new Rect(x, editArea.y, editArea.xMax - x - 2f, editArea.height), customization.CustomLabel);
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }

            if (hovered)
                TooltipHandler.TipRegion(clickArea, "MTC_RightClickHint".Translate());
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 1 && clickArea.Contains(current.mousePosition))
            {
                Find.WindowStack.Add(new Dialog_EditMapTab(map));
                current.Use();
            }
        }

        private static void DrawIconReplacement(Rect groupRect, Texture2D icon)
        {
            Widgets.DrawBoxSolid(groupRect, new Color(0.10f, 0.10f, 0.10f, 0.98f));
            Widgets.DrawBox(groupRect);
            float size = Mathf.Min(42f, Mathf.Min(groupRect.width - 8f, groupRect.height - 8f));
            if (size <= 0f) return;
            Rect iconRect = new Rect(
                groupRect.center.x - size / 2f,
                groupRect.center.y - size / 2f,
                size,
                size);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
        }
    }

    internal static class LtoColonyGroupsCompatibility
    {
        internal static bool Active { get; private set; }

        private static PropertyInfo entriesProperty;
        private static FieldInfo entryMapField;
        private static FieldInfo entryGroupField;
        private static FieldInfo drawerField;
        private static MethodInfo groupFrameRectMethod;
        private static MethodInfo markColonistsDirtyMethod;
        private static FieldInfo cachedEntriesField;

        internal static void TryPatch(Harmony harmony)
        {
            System.Type barType = AccessTools.TypeByName("TacticalGroups.TacticalColonistBar");
            System.Type entryType = AccessTools.TypeByName("TacticalGroups.TacticalColonistBar+Entry");
            System.Type drawerType = AccessTools.TypeByName("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer");
            if (barType == null || entryType == null || drawerType == null) return;

            entriesProperty = AccessTools.Property(barType, "Entries");
            entryMapField = AccessTools.Field(entryType, "map");
            entryGroupField = AccessTools.Field(entryType, "group");
            drawerField = AccessTools.Field(barType, "drawer");
            groupFrameRectMethod = AccessTools.Method(drawerType, "GroupFrameRect");
            MethodInfo onGui = AccessTools.Method(barType, "ColonistBarOnGUI");
            MethodInfo checkRecacheEntries = AccessTools.Method(barType, "CheckRecacheEntries");
            MethodInfo handleGroupingClicks = AccessTools.Method(barType, "HandleGroupingClicks");
            markColonistsDirtyMethod = AccessTools.Method(barType, "MarkColonistsDirty");
            cachedEntriesField = AccessTools.Field(barType, "cachedEntries");
            if (entriesProperty == null || entryMapField == null || entryGroupField == null ||
                drawerField == null || groupFrameRectMethod == null || onGui == null ||
                checkRecacheEntries == null || cachedEntriesField == null) return;

            harmony.Patch(onGui, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(DrawLtoMapTabs))),
                prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(ShouldRunLtoBar))));
            harmony.Patch(checkRecacheEntries, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(CompactLtoEntries))));
            if (handleGroupingClicks != null)
                harmony.Patch(handleGroupingClicks, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(ShouldHandleLtoGroupingClicks))));
            PatchLtoGroupDrawing(harmony, "TacticalGroups.ColonistGroup");
            PatchLtoGroupDrawing(harmony, "TacticalGroups.ColonyGroup");
            PatchLtoGroupDrawing(harmony, "TacticalGroups.PawnGroup");
            PatchLtoGroupDrawing(harmony, "TacticalGroups.CaravanGroup");
            PatchLtoGroupMetrics(harmony);
            Active = true;
            Log.Message("[Map Tab Customizer] [LTO] Colony Groups detected; compatibility renderer enabled.");
        }

        private static void PatchLtoGroupDrawing(Harmony harmony, string typeName)
        {
            System.Type groupType = AccessTools.TypeByName(typeName);
            if (groupType == null) return;
            HarmonyMethod prefix = new HarmonyMethod(
                AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(ShouldDrawLtoGroup)));
            MethodInfo draw = AccessTools.Method(groupType, "Draw", new[] { typeof(Rect) });
            MethodInfo drawOverlays = AccessTools.Method(groupType, "DrawOverlays", new[] { typeof(Rect) });
            if (draw != null && draw.DeclaringType == groupType) harmony.Patch(draw, prefix: prefix);
            if (drawOverlays != null && drawOverlays.DeclaringType == groupType)
                harmony.Patch(drawOverlays, prefix: prefix);
        }

        private static void PatchLtoGroupMetrics(Harmony harmony)
        {
            System.Type groupType = AccessTools.TypeByName("TacticalGroups.ColonistGroup");
            if (groupType == null) return;
            HarmonyMethod postfix = new HarmonyMethod(
                AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(AdjustLtoGroupMetric)));
            foreach (string propertyName in new[] { "GroupIconWidth", "GroupIconHeight", "GroupIconMargin" })
            {
                MethodInfo getter = AccessTools.PropertyGetter(groupType, propertyName);
                if (getter != null) harmony.Patch(getter, postfix: postfix);
            }
        }

        private static bool ShouldDrawLtoGroup(object __instance)
        {
            return ShouldShowLtoGroup(__instance);
        }

        private static void AdjustLtoGroupMetric(object __instance, ref float __result)
        {
            if (!ShouldShowLtoGroup(__instance)) __result = 0f;
        }

        private static bool ShouldShowLtoGroup(object group)
        {
            MapTabCustomizerSettings settings = MapTabCustomizerMod.Settings;
            if (settings == null) return true;
            if (settings.HideLtoButtons) return false;
            if (!settings.ShowOnlyActiveLtoButtons) return true;

            PropertyInfo mapProperty = AccessTools.Property(group.GetType(), "Map");
            Map map = mapProperty?.GetValue(group, null) as Map;
            return map != null && map == Find.CurrentMap;
        }

        private static bool ShouldHandleLtoGroupingClicks()
        {
            return MapTabCustomizerMod.Settings == null || !MapTabCustomizerMod.Settings.HideLtoButtons;
        }

        private static bool ShouldRunLtoBar()
        {
            return true;
        }

        private static void DrawLtoMapTabs(object __instance)
        {
            if (Find.CurrentMap == null) return;
            IEnumerable entries = entriesProperty.GetValue(__instance, null) as IEnumerable;
            object drawer = drawerField.GetValue(__instance);
            if (entries == null || drawer == null) return;

            Dictionary<int, Map> mapsByGroup = new Dictionary<int, Map>();
            foreach (object entry in entries)
            {
                Map map = entryMapField.GetValue(entry) as Map;
                if (map == null) continue;
                int group = (int)entryGroupField.GetValue(entry);
                if (!mapsByGroup.ContainsKey(group)) mapsByGroup.Add(group, map);
            }

            foreach (KeyValuePair<int, Map> pair in mapsByGroup)
            {
                Rect rect = (Rect)groupFrameRectMethod.Invoke(drawer, new object[] { pair.Key });
                MapTabRenderer.DrawCustomization(
                    pair.Value,
                    pair.Value.GetComponent<MapTabCustomizationComponent>(),
                    rect);
            }
        }

        private static void CompactLtoEntries(object __instance)
        {
            if (!MapTabRenderer.UseCompactMapTabs) return;
            IList entries = cachedEntriesField.GetValue(__instance) as IList;
            MapTabRenderer.CompactEntries(entries);
        }

        internal static void MarkColonistsDirty()
        {
            if (!Active || markColonistsDirtyMethod == null) return;
            System.Type tacticUtils = AccessTools.TypeByName("TacticalGroups.TacticUtils");
            FieldInfo barField = tacticUtils == null ? null : AccessTools.Field(tacticUtils, "TacticalColonistBar");
            object bar = barField?.GetValue(null);
            if (bar != null) markColonistsDirtyMethod.Invoke(bar, null);
        }
    }
}
