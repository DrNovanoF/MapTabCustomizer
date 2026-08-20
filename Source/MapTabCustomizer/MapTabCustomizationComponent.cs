using Verse;

namespace MapTabCustomizer
{
    public sealed class MapTabCustomizationComponent : MapComponent
    {
        public string CustomLabel = string.Empty;
        public int IconIndex;

        public MapTabCustomizationComponent(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref CustomLabel, "customLabel", string.Empty);
            Scribe_Values.Look(ref IconIndex, "iconIndex", 0);
        }
    }
}
