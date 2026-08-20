using UnityEngine;
using Verse;

namespace MapTabCustomizer
{
    internal sealed class MapTabCustomizerSettings : ModSettings
    {
        internal bool ShowOnlyOnHover;
        internal bool ReplacePawnPortraitsWithIcon;
        internal bool ShowActiveMapPawns;
        internal bool AlwaysShowActiveLabel;
        internal bool HideLtoButtons;
        internal bool ShowOnlyActiveLtoButtons;
        internal bool HideIconInLabel;
        internal Color TextColor = Color.white;
        internal Color IconColor = Color.white;
        internal Color BackgroundColor = new Color(0.10f, 0.10f, 0.10f, 0.96f);
        internal Color LabelBackgroundColor = new Color(0.10f, 0.10f, 0.10f, 0.96f);
        internal Color IconBackgroundColor = new Color(0.14f, 0.14f, 0.14f, 1f);

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowOnlyOnHover, "showOnlyOnHover", false);
            Scribe_Values.Look(ref ReplacePawnPortraitsWithIcon, "replacePawnPortraitsWithIcon", false);
            Scribe_Values.Look(ref ShowActiveMapPawns, "showActiveMapPawns", false);
            Scribe_Values.Look(ref AlwaysShowActiveLabel, "alwaysShowActiveLabel", false);
            Scribe_Values.Look(ref HideLtoButtons, "hideLtoButtons", false);
            Scribe_Values.Look(ref ShowOnlyActiveLtoButtons, "showOnlyActiveLtoButtons", false);
            Scribe_Values.Look(ref HideIconInLabel, "hideIconInLabel", false);
            Scribe_Values.Look(ref TextColor, "textColor", Color.white);
            Scribe_Values.Look(ref IconColor, "iconColor", Color.white);
            Scribe_Values.Look(ref BackgroundColor, "backgroundColor", new Color(0.10f, 0.10f, 0.10f, 0.96f));
            Scribe_Values.Look(ref LabelBackgroundColor, "labelBackgroundColor", new Color(0.10f, 0.10f, 0.10f, 0.96f));
            Scribe_Values.Look(ref IconBackgroundColor, "iconBackgroundColor", new Color(0.14f, 0.14f, 0.14f, 1f));
            base.ExposeData();
        }
    }

    public sealed class MapTabCustomizerMod : Mod
    {
        internal static MapTabCustomizerSettings Settings;
        private Vector2 settingsScrollPosition;

        public MapTabCustomizerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MapTabCustomizerSettings>();
        }

        public override string SettingsCategory()
        {
            return "Map Tab Customizer";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, 1120f);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            listing.CheckboxLabeled(
                "MTC_ShowOnlyOnHover".Translate(),
                ref Settings.ShowOnlyOnHover,
                "MTC_ShowOnlyOnHoverDesc".Translate());
            listing.Gap();
            listing.CheckboxLabeled(
                "MTC_AlwaysShowActiveLabel".Translate(),
                ref Settings.AlwaysShowActiveLabel,
                "MTC_AlwaysShowActiveLabelDesc".Translate());
            listing.Gap();
            listing.CheckboxLabeled(
                "MTC_HideIconInLabel".Translate(),
                ref Settings.HideIconInLabel,
                "MTC_HideIconInLabelDesc".Translate());
            listing.Gap();
            bool previousCompactMode = Settings.ReplacePawnPortraitsWithIcon;
            listing.CheckboxLabeled(
                "MTC_ReplacePawnPortraits".Translate(),
                ref Settings.ReplacePawnPortraitsWithIcon,
                "MTC_ReplacePawnPortraitsDesc".Translate());
            if (previousCompactMode != Settings.ReplacePawnPortraitsWithIcon)
                MapTabRenderer.NotifyLayoutChanged();
            listing.Gap();
            bool previousActiveMapMode = Settings.ShowActiveMapPawns;
            listing.CheckboxLabeled(
                "MTC_ShowActiveMapPawns".Translate(),
                ref Settings.ShowActiveMapPawns,
                "MTC_ShowActiveMapPawnsDesc".Translate());
            if (previousActiveMapMode != Settings.ShowActiveMapPawns)
                MapTabRenderer.NotifyLayoutChanged();
            listing.GapLine();
            listing.Label("MTC_LtoOptions".Translate());
            bool previousHideLtoButtons = Settings.HideLtoButtons;
            listing.CheckboxLabeled(
                "MTC_HideLtoButtons".Translate(),
                ref Settings.HideLtoButtons,
                "MTC_HideLtoButtonsDesc".Translate());
            bool previousActiveLtoButtons = Settings.ShowOnlyActiveLtoButtons;
            listing.CheckboxLabeled(
                "MTC_ShowOnlyActiveLtoButtons".Translate(),
                ref Settings.ShowOnlyActiveLtoButtons,
                "MTC_ShowOnlyActiveLtoButtonsDesc".Translate());
            if (previousHideLtoButtons != Settings.HideLtoButtons ||
                previousActiveLtoButtons != Settings.ShowOnlyActiveLtoButtons)
                MapTabRenderer.NotifyLayoutChanged();
            listing.GapLine();
            listing.Label("MTC_DisplayColors".Translate());
            DrawColorControls(listing, "MTC_TextColor".Translate(), ref Settings.TextColor, false);
            DrawColorControls(listing, "MTC_IconColor".Translate(), ref Settings.IconColor, false);
            DrawColorControls(listing, "MTC_LabelBackgroundColor".Translate(), ref Settings.LabelBackgroundColor, true);
            DrawColorControls(listing, "MTC_IconBackgroundColor".Translate(), ref Settings.IconBackgroundColor, true);
            DrawColorControls(listing, "MTC_TabBackgroundColor".Translate(), ref Settings.BackgroundColor, true);
            listing.End();
            Widgets.EndScrollView();
        }

        private static void DrawColorControls(Listing_Standard listing, string title, ref Color color, bool showAlpha)
        {
            Rect header = listing.GetRect(28f);
            Widgets.Label(header, title);
            Rect preview = new Rect(header.xMax - 64f, header.y + 3f, 60f, 22f);
            Widgets.DrawBoxSolid(preview, color);
            Widgets.DrawBox(preview);

            DrawSliderRow(listing, "MTC_ColorRed".Translate(), ref color.r);
            DrawSliderRow(listing, "MTC_ColorGreen".Translate(), ref color.g);
            DrawSliderRow(listing, "MTC_ColorBlue".Translate(), ref color.b);
            if (showAlpha) DrawSliderRow(listing, "MTC_ColorAlpha".Translate(), ref color.a);
            else color.a = 1f;
            listing.Gap(8f);
        }

        private static void DrawSliderRow(Listing_Standard listing, string label, ref float value)
        {
            Rect row = listing.GetRect(28f);
            Widgets.Label(new Rect(row.x, row.y, 120f, row.height), label);
            Rect valueRect = new Rect(row.xMax - 48f, row.y, 48f, row.height);
            TextAnchor previousAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(valueRect, value.ToString("0.00"));
            Text.Anchor = previousAnchor;
            Rect sliderRect = new Rect(row.x + 128f, row.y + 4f, row.width - 184f, 20f);
            value = Widgets.HorizontalSlider(sliderRect, value, 0f, 1f, true, null, null, null, 0.01f);
        }
    }
}
