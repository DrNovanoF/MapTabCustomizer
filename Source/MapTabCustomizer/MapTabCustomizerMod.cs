using System.Collections.Generic;
using System.Collections;
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
            Harmony ltoHarmony = new Harmony("cleme.maptabcustomizer.lto");
            try
            {
                LtoColonyGroupsCompatibility.TryPatch(ltoHarmony);
            }
            catch (System.Exception exception)
            {
                ltoHarmony.UnpatchAll(ltoHarmony.Id);
                Log.Error("[Map Tab Customizer] [LTO] Compatibility could not be enabled. " +
                          "The vanilla colonist bar will remain available.\n" + exception);
            }
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
            return LtoColonyGroupsCompatibility.Active || MapTabRenderer.ShouldDrawBar;
        }

        private static void Postfix(ColonistBar __instance)
        {
            if (LtoColonyGroupsCompatibility.Active || Find.CurrentMap == null || !MapTabRenderer.ShouldDrawBar) return;

            Dictionary<int, Map> mapsByGroup = new Dictionary<int, Map>();
            foreach (ColonistBar.Entry entry in __instance.Entries)
                if (entry.map != null && !mapsByGroup.ContainsKey(entry.group))
                    mapsByGroup.Add(entry.group, entry.map);

            MapTabRenderer.BeginCustomizationPass();
            try
            {
                foreach (KeyValuePair<int, Map> pair in mapsByGroup)
                {
                    MapTabCustomizationComponent customization = pair.Value.GetComponent<MapTabCustomizationComponent>();
                    Rect groupRect = (Rect)GroupFrameRect(Drawer(__instance), new object[] { pair.Key });
                    MapTabRenderer.DrawCustomization(pair.Value, customization, groupRect);
                }
            }
            finally
            {
                MapTabRenderer.EndCustomizationPass();
            }
        }
    }

    [HarmonyPatch]
    internal static class VanillaColonistBarLayoutPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(ColonistBarDrawLocsFinder),
                "CalculateDrawLocs",
                new[] { typeof(List<Vector2>), typeof(float).MakeByRefType(), typeof(int) });
        }

        private static void Postfix(List<Vector2> __0)
        {
            if (LtoColonyGroupsCompatibility.Active || __0 == null) return;
            for (int index = 0; index < __0.Count; index++)
            {
                Vector2 position = __0[index];
                position.y += MapTabRenderer.BarVerticalOffset;
                __0[index] = position;
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

    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawColonist")]
    internal static class VanillaColonistPortraitPatch
    {
        private static bool Prefix(Map __2)
        {
            return !MapTabRenderer.ShouldReplaceMap(__2);
        }
    }

    [HarmonyPatch(typeof(ColonistBarColonistDrawer), "GroupFrameRect")]
    internal static class VanillaGroupFrameRectPatch
    {
        private static void Postfix(ref Rect __result)
        {
            if (!LtoColonyGroupsCompatibility.Active)
                MapTabRenderer.CorrectShiftedGroupFrame(ref __result);
        }
    }

    internal static class MapTabRenderer
    {
        private const float DefaultVerticalOffset = 28f;
        private const float AdditionalDevToolbarOffset = 48f;
        private static Map expandedHoverMap;
        private static bool hasPendingHoveredLabel;
        private static Rect pendingHoveredLabelRect;
        private static string pendingHoveredLabelText;
        private static Texture2D pendingHoveredLabelIcon;
        private static readonly Dictionary<System.Type, FieldInfo> EntryMapFields =
            new Dictionary<System.Type, FieldInfo>();

        internal static void BeginCustomizationPass()
        {
            hasPendingHoveredLabel = false;
            if (expandedHoverMap != null && (Find.Maps == null || !Find.Maps.Contains(expandedHoverMap)))
                expandedHoverMap = null;
        }

        internal static void EndCustomizationPass()
        {
            if (!hasPendingHoveredLabel) return;
            DrawLabelContents(pendingHoveredLabelRect, pendingHoveredLabelText, pendingHoveredLabelIcon);
            hasPendingHoveredLabel = false;
        }

        internal static bool UseCompactMapTabs => MapTabCustomizerMod.Settings != null &&
                                                  MapTabCustomizerMod.Settings.ReplacePawnPortraitsWithIcon;
        internal static bool ShowActiveMapPawns => MapTabCustomizerMod.Settings != null &&
                                                   MapTabCustomizerMod.Settings.ShowActiveMapPawns;
        internal static float BarVerticalOffset => DefaultVerticalOffset +
                                                   (Prefs.DevMode ? AdditionalDevToolbarOffset : 0f);

        internal static void CorrectShiftedGroupFrame(ref Rect rect)
        {
            float offset = BarVerticalOffset;
            rect.y += offset;
            rect.height = Mathf.Max(0f, rect.height - offset);
        }
        internal static bool ShouldDrawBar => MapTabCustomizerMod.Settings == null ||
                                              !MapTabCustomizerMod.Settings.ShowBarOnlyOnHover ||
                                              IsMouseInBarRevealArea();

        private static bool IsMouseInBarRevealArea()
        {
            Event current = Event.current;
            return current != null && current.mousePosition.y >= 0f && current.mousePosition.y <= 220f;
        }

        internal static bool ShouldReplaceMap(Map map)
        {
            return map != null && UseCompactMapTabs && (!ShowActiveMapPawns || map != Find.CurrentMap);
        }

        internal static void CompactEntries(IList entries)
        {
            if (entries == null) return;
            HashSet<Map> seenMaps = new HashSet<Map>();
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                object entry = entries[index];
                System.Type entryType = entry.GetType();
                if (!EntryMapFields.TryGetValue(entryType, out FieldInfo mapField))
                {
                    mapField = AccessTools.Field(entryType, "map");
                    EntryMapFields.Add(entryType, mapField);
                }
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

        internal static void DrawCustomization(Map map, MapTabCustomizationComponent customization, Rect groupRect)
        {
            bool hasLabel = !customization.CustomLabel.NullOrEmpty();
            Texture2D icon = MapTabIcons.Get(customization.IconIndex);
            Texture2D labelIcon = MapTabCustomizerMod.Settings != null &&
                                  MapTabCustomizerMod.Settings.HideIconInLabel
                ? null
                : icon;
            const float extraLabelWidth = 10f;
            float collapsedWidth = groupRect.width + extraLabelWidth;
            float naturalWidth = (labelIcon != null ? 22f : 0f) +
                                 (hasLabel ? Text.CalcSize(customization.CustomLabel).x + 10f : 0f);
            Rect clickArea = new Rect(groupRect.x, groupRect.y, collapsedWidth, groupRect.height + 24f);
            Rect expandedHoverArea = new Rect(
                groupRect.x,
                groupRect.y,
                Mathf.Max(collapsedWidth, naturalWidth),
                groupRect.height + 24f);
            bool hovered = Mouse.IsOver(clickArea) ||
                           (expandedHoverMap == map && Mouse.IsOver(expandedHoverArea));
            if (hovered) expandedHoverMap = map;
            else if (expandedHoverMap == map) expandedHoverMap = null;
            float displayedWidth = hovered ? Mathf.Max(collapsedWidth, naturalWidth) : collapsedWidth;
            Rect editArea = new Rect(groupRect.x, groupRect.yMax + 2f, displayedWidth, 22f);
            bool forceActiveLabel = MapTabCustomizerMod.Settings != null &&
                                    MapTabCustomizerMod.Settings.AlwaysShowActiveLabel &&
                                    map == Find.CurrentMap;
            bool showCustomization = MapTabCustomizerMod.Settings == null ||
                                     !MapTabCustomizerMod.Settings.ShowOnlyOnHover || hovered || forceActiveLabel;

            bool replaceThisMap = ShouldReplaceMap(map);
            if (icon != null && replaceThisMap)
            {
                DrawIconReplacement(groupRect, icon);
            }

            if ((hasLabel || labelIcon != null) && showCustomization)
            {
                if (hovered)
                {
                    hasPendingHoveredLabel = true;
                    pendingHoveredLabelRect = editArea;
                    pendingHoveredLabelText = customization.CustomLabel;
                    pendingHoveredLabelIcon = labelIcon;
                }
                else
                {
                    DrawLabelContents(editArea, customization.CustomLabel, labelIcon);
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

        private static void DrawLabelContents(Rect editArea, string label, Texture2D icon)
        {
            Widgets.DrawBoxSolid(editArea, DisplayLabelBackgroundColor);
            float x = editArea.x + 3f;
            if (icon != null)
            {
                DrawTintedIcon(new Rect(x, editArea.y + 2f, 18f, 18f), icon);
                x += 22f;
            }
            if (label.NullOrEmpty()) return;

            TextAnchor previousAnchor = Text.Anchor;
            bool previousWordWrap = Text.WordWrap;
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.WordWrap = false;
            Color previousGuiColor = GUI.color;
            GUI.color = DisplayTextColor;
            Widgets.Label(new Rect(x, editArea.y, editArea.xMax - x - 2f, editArea.height), label);
            GUI.color = previousGuiColor;
            Text.WordWrap = previousWordWrap;
            Text.Anchor = previousAnchor;
        }

        private static void DrawIconReplacement(Rect groupRect, Texture2D icon)
        {
            Widgets.DrawBoxSolid(groupRect, DisplayTabBackgroundColor);
            Rect compactRect = groupRect;
            compactRect.height *= 0.75f;
            compactRect.y = groupRect.center.y - compactRect.height / 2f;
            Widgets.DrawBoxSolid(compactRect, DisplayIconBackgroundColor);
            Widgets.DrawBox(compactRect);
            float size = Mathf.Min(42f, Mathf.Min(compactRect.width - 8f, compactRect.height - 8f));
            if (size <= 0f) return;
            Rect iconRect = new Rect(compactRect.center.x - size / 2f, compactRect.center.y - size / 2f, size, size);
            DrawTintedIcon(iconRect, icon);
        }

        private static Color DisplayTextColor => MapTabCustomizerMod.Settings?.TextColor ?? Color.white;
        private static Color DisplayIconColor => MapTabCustomizerMod.Settings?.IconColor ?? Color.white;
        private static Color DisplayLabelBackgroundColor => MapTabCustomizerMod.Settings?.LabelBackgroundColor ??
                                                            new Color(0.10f, 0.10f, 0.10f, 0.96f);
        private static Color DisplayIconBackgroundColor => MapTabCustomizerMod.Settings?.IconBackgroundColor ??
                                                           new Color(0.14f, 0.14f, 0.14f, 1f);
        private static Color DisplayTabBackgroundColor => MapTabCustomizerMod.Settings?.TabBackgroundColor ??
                                                          new Color(0.10f, 0.10f, 0.10f, 0.96f);

        private static void DrawTintedIcon(Rect rect, Texture2D icon)
        {
            Color previousColor = GUI.color;
            GUI.color = DisplayIconColor;
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);
            GUI.color = previousColor;
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
        private static FieldInfo tacticalColonistBarField;
        private static FieldInfo hideCreateGroupField;
        private static FieldInfo createGroupRectField;
        private static FieldInfo mappedRectField;
        private static FieldInfo mappedGroupField;
        private static readonly List<FieldInfo> MappedDrawLocFields = new List<FieldInfo>();
        private static readonly Dictionary<System.Type, PropertyInfo> GroupMapProperties =
            new Dictionary<System.Type, PropertyInfo>();

        internal static void TryPatch(Harmony harmony)
        {
            System.Type barType = AccessTools.TypeByName("TacticalGroups.TacticalColonistBar");
            System.Type entryType = AccessTools.TypeByName("TacticalGroups.TacticalColonistBar+Entry");
            System.Type drawerType = AccessTools.TypeByName("TacticalGroups.TacticalGroups_ColonistBarColonistDrawer");
            System.Type drawLocsFinderType = AccessTools.TypeByName("TacticalGroups.ColonistBarDrawLocsFinder");
            System.Type mappedValueType = AccessTools.TypeByName("TacticalGroups.MappedValue");
            System.Type settingsType = AccessTools.TypeByName("TacticalGroups.TacticalGroupsSettings");
            if (barType == null || entryType == null || drawerType == null ||
                drawLocsFinderType == null || mappedValueType == null) return;

            entriesProperty = AccessTools.Property(barType, "Entries");
            entryMapField = AccessTools.Field(entryType, "map");
            entryGroupField = AccessTools.Field(entryType, "group");
            drawerField = AccessTools.Field(barType, "drawer");
            groupFrameRectMethod = AccessTools.Method(drawerType, "GroupFrameRect");
            MethodInfo onGui = AccessTools.Method(barType, "ColonistBarOnGUI");
            MethodInfo checkRecacheEntries = AccessTools.Method(barType, "CheckRecacheEntries");
            MethodInfo handleGroupingClicks = AccessTools.Method(barType, "HandleGroupingClicks");
            MethodInfo drawLtoColonist = AccessTools.Method(drawerType, "DrawColonist");
            markColonistsDirtyMethod = AccessTools.Method(barType, "MarkColonistsDirty");
            cachedEntriesField = AccessTools.Field(barType, "cachedEntries");
            System.Type tacticUtils = AccessTools.TypeByName("TacticalGroups.TacticUtils");
            tacticalColonistBarField = tacticUtils == null
                ? null
                : AccessTools.Field(tacticUtils, "TacticalColonistBar");
            hideCreateGroupField = settingsType == null ? null : AccessTools.Field(settingsType, "HideCreateGroup");
            createGroupRectField = AccessTools.Field(drawLocsFinderType, "createGroupRect");
            mappedRectField = AccessTools.Field(mappedValueType, "rect");
            mappedGroupField = AccessTools.Field(mappedValueType, "colonistGroup");
            MappedDrawLocFields.Clear();
            foreach (string fieldName in new[] { "pawnGroupDrawLoc", "colonyGroupDrawLoc", "caravanGroupDrawLoc" })
            {
                FieldInfo field = AccessTools.Field(drawLocsFinderType, fieldName);
                if (field != null) MappedDrawLocFields.Add(field);
            }
            MethodInfo calculateDrawLocs = AccessTools.Method(
                drawLocsFinderType,
                "CalculateDrawLocs",
                new[] { typeof(List<Rect>), typeof(float).MakeByRefType() });
            if (entriesProperty == null || entryMapField == null || entryGroupField == null ||
                drawerField == null || groupFrameRectMethod == null || onGui == null ||
                checkRecacheEntries == null || cachedEntriesField == null || calculateDrawLocs == null ||
                createGroupRectField == null || mappedRectField == null || mappedGroupField == null) return;

            harmony.Patch(onGui, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(DrawLtoMapTabs))),
                prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(PrepareLtoBar))),
                finalizer: new HarmonyMethod(
                    AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(RestoreLtoBar))));
            harmony.Patch(checkRecacheEntries, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(CompactLtoEntries))));
            harmony.Patch(groupFrameRectMethod, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(CorrectLtoGroupFrame))));
            harmony.Patch(calculateDrawLocs, postfix: new HarmonyMethod(
                AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(NormalizeLtoLayout))));
            if (handleGroupingClicks != null)
                harmony.Patch(handleGroupingClicks, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(ShouldHandleLtoGroupingClicks))));
            if (drawLtoColonist != null)
                harmony.Patch(drawLtoColonist, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(LtoColonyGroupsCompatibility), nameof(ShouldDrawLtoColonist))));
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
            if (draw != null && draw.DeclaringType == groupType)
                harmony.Patch(draw, prefix: prefix);
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
            if (group == null) return false;
            MapTabCustomizerSettings settings = MapTabCustomizerMod.Settings;
            if (settings == null) return true;
            if (settings.HideLtoButtons) return false;
            if (!settings.ShowOnlyActiveLtoButtons) return true;

            System.Type groupType = group.GetType();
            if (!GroupMapProperties.TryGetValue(groupType, out PropertyInfo mapProperty))
            {
                mapProperty = AccessTools.Property(groupType, "Map");
                GroupMapProperties.Add(groupType, mapProperty);
            }
            Map map = mapProperty?.GetValue(group, null) as Map;
            return map != null && map == Find.CurrentMap;
        }

        private static bool ShouldHandleLtoGroupingClicks()
        {
            return MapTabCustomizerMod.Settings == null || !MapTabCustomizerMod.Settings.HideLtoButtons;
        }

        private static bool ShouldDrawLtoColonist(Map __2)
        {
            return !MapTabRenderer.ShouldReplaceMap(__2);
        }

        private static void CorrectLtoGroupFrame(ref Rect __result)
        {
            MapTabRenderer.CorrectShiftedGroupFrame(ref __result);
        }

        private static bool PrepareLtoBar(out bool __state)
        {
            __state = hideCreateGroupField != null && (bool)hideCreateGroupField.GetValue(null);
            if (hideCreateGroupField != null && MapTabCustomizerMod.Settings?.HideLtoButtons == true)
                hideCreateGroupField.SetValue(null, true);
            return MapTabRenderer.ShouldDrawBar;
        }

        private static System.Exception RestoreLtoBar(System.Exception __exception, bool __state)
        {
            if (hideCreateGroupField != null) hideCreateGroupField.SetValue(null, __state);
            return __exception;
        }

        private static void NormalizeLtoLayout(List<Rect> __0)
        {
            if (__0 == null || __0.Count == 0) return;

            bool suppressCreateGroup = MapTabCustomizerMod.Settings?.HideLtoButtons == true;
            Rect createGroupRect = (Rect)createGroupRectField.GetValue(null);
            if (suppressCreateGroup && createGroupRect.width > 0f)
            {
                float reservedWidth = createGroupRect.width + 20f;
                ShiftRectsAfter(__0, createGroupRect.x, -reservedWidth);
                foreach (FieldInfo field in MappedDrawLocFields)
                    ShiftMappedRectsAfter(field.GetValue(null) as IList, createGroupRect.x, -reservedWidth);
                createGroupRect = new Rect(-10000f, -10000f, 0f, 0f);
                createGroupRectField.SetValue(null, createGroupRect);
            }

            float verticalOffset = MapTabRenderer.BarVerticalOffset;
            ShiftRectsVertically(__0, verticalOffset);
            foreach (FieldInfo field in MappedDrawLocFields)
                ShiftMappedRectsVertically(field.GetValue(null) as IList, verticalOffset);
            if (!suppressCreateGroup) createGroupRect.y += verticalOffset;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            foreach (Rect rect in __0) IncludeBounds(rect, ref minX, ref maxX);
            foreach (FieldInfo field in MappedDrawLocFields)
            {
                IList mappedValues = field.GetValue(null) as IList;
                if (mappedValues == null) continue;
                foreach (object mappedValue in mappedValues)
                {
                    object group = mappedGroupField.GetValue(mappedValue);
                    if (ShouldShowLtoGroup(group))
                        IncludeBounds((Rect)mappedRectField.GetValue(mappedValue), ref minX, ref maxX);
                }
            }
            if (!suppressCreateGroup) IncludeBounds(createGroupRect, ref minX, ref maxX);
            if (minX == float.MaxValue) return;

            float offset = UI.screenWidth * 0.5f - (minX + maxX) * 0.5f;
            ShiftRects(__0, offset);
            foreach (FieldInfo field in MappedDrawLocFields)
                ShiftMappedRects(field.GetValue(null) as IList, offset);
            if (!suppressCreateGroup)
            {
                createGroupRect.x += offset;
                createGroupRectField.SetValue(null, createGroupRect);
            }
        }

        private static void IncludeBounds(Rect rect, ref float minX, ref float maxX)
        {
            if (rect.width <= 0f) return;
            minX = Mathf.Min(minX, rect.xMin);
            maxX = Mathf.Max(maxX, rect.xMax);
        }

        private static void ShiftRects(List<Rect> rects, float offset)
        {
            for (int index = 0; index < rects.Count; index++)
            {
                Rect rect = rects[index];
                rect.x += offset;
                rects[index] = rect;
            }
        }

        private static void ShiftRectsAfter(List<Rect> rects, float x, float offset)
        {
            for (int index = 0; index < rects.Count; index++)
            {
                Rect rect = rects[index];
                if (rect.x < x) continue;
                rect.x += offset;
                rects[index] = rect;
            }
        }

        private static void ShiftRectsVertically(List<Rect> rects, float offset)
        {
            for (int index = 0; index < rects.Count; index++)
            {
                Rect rect = rects[index];
                rect.y += offset;
                rects[index] = rect;
            }
        }

        private static void ShiftMappedRects(IList mappedValues, float offset)
        {
            ShiftMappedRectsAfter(mappedValues, float.MinValue, offset);
        }

        private static void ShiftMappedRectsAfter(IList mappedValues, float x, float offset)
        {
            if (mappedValues == null) return;
            for (int index = 0; index < mappedValues.Count; index++)
            {
                object mappedValue = mappedValues[index];
                Rect rect = (Rect)mappedRectField.GetValue(mappedValue);
                if (rect.x < x) continue;
                rect.x += offset;
                mappedRectField.SetValue(mappedValue, rect);
                if (mappedValue.GetType().IsValueType) mappedValues[index] = mappedValue;
            }
        }

        private static void ShiftMappedRectsVertically(IList mappedValues, float offset)
        {
            if (mappedValues == null) return;
            for (int index = 0; index < mappedValues.Count; index++)
            {
                object mappedValue = mappedValues[index];
                Rect rect = (Rect)mappedRectField.GetValue(mappedValue);
                rect.y += offset;
                mappedRectField.SetValue(mappedValue, rect);
                if (mappedValue.GetType().IsValueType) mappedValues[index] = mappedValue;
            }
        }

        private static void DrawLtoMapTabs(object __instance)
        {
            if (Find.CurrentMap == null || !MapTabRenderer.ShouldDrawBar) return;
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

            MapTabRenderer.BeginCustomizationPass();
            try
            {
                foreach (KeyValuePair<int, Map> pair in mapsByGroup)
                {
                    Rect rect = (Rect)groupFrameRectMethod.Invoke(drawer, new object[] { pair.Key });
                    MapTabRenderer.DrawCustomization(
                        pair.Value,
                        pair.Value.GetComponent<MapTabCustomizationComponent>(),
                        rect);
                }
            }
            finally
            {
                MapTabRenderer.EndCustomizationPass();
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
            object bar = tacticalColonistBarField?.GetValue(null);
            if (bar != null) markColonistsDirtyMethod.Invoke(bar, null);
        }
    }
}
