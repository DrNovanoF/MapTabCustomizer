using UnityEngine;
using Verse;

namespace MapTabCustomizer
{
    internal sealed class MapTabCustomizerSettings : ModSettings
    {
        internal bool ShowOnlyOnHover;
        internal bool ReplacePawnPortraitsWithIcon;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShowOnlyOnHover, "showOnlyOnHover", false);
            Scribe_Values.Look(ref ReplacePawnPortraitsWithIcon, "replacePawnPortraitsWithIcon", false);
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
            bool previousCompactMode = Settings.ReplacePawnPortraitsWithIcon;
            listing.CheckboxLabeled(
                "MTC_ReplacePawnPortraits".Translate(),
                ref Settings.ReplacePawnPortraitsWithIcon,
                "MTC_ReplacePawnPortraitsDesc".Translate());
            if (previousCompactMode != Settings.ReplacePawnPortraitsWithIcon)
                MapTabRenderer.NotifyLayoutChanged();
            listing.End();
        }
    }
}
