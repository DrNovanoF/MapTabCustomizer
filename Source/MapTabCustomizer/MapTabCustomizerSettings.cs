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

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowOnlyOnHover, "showOnlyOnHover", false);
            Scribe_Values.Look(ref ReplacePawnPortraitsWithIcon, "replacePawnPortraitsWithIcon", false);
            Scribe_Values.Look(ref ShowActiveMapPawns, "showActiveMapPawns", false);
            Scribe_Values.Look(ref AlwaysShowActiveLabel, "alwaysShowActiveLabel", false);
            Scribe_Values.Look(ref HideLtoButtons, "hideLtoButtons", false);
            Scribe_Values.Look(ref ShowOnlyActiveLtoButtons, "showOnlyActiveLtoButtons", false);
            Scribe_Values.Look(ref HideIconInLabel, "hideIconInLabel", false);
            base.ExposeData();
        }
    }

    public sealed class MapTabCustomizerMod : Mod
    {
        internal static MapTabCustomizerSettings Settings;

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
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
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
            listing.End();
        }
    }
}
