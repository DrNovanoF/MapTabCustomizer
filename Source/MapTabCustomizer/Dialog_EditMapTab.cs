using UnityEngine;
using Verse;

namespace MapTabCustomizer
{
    internal sealed class Dialog_EditMapTab : Window
    {
        private readonly MapTabCustomizationComponent customization;
        private string label;
        private int iconIndex;

        public override Vector2 InitialSize => new Vector2(430f, 250f);

        internal Dialog_EditMapTab(Map map)
        {
            customization = map.GetComponent<MapTabCustomizationComponent>();
            label = customization.CustomLabel ?? string.Empty;
            iconIndex = customization.IconIndex;
            doCloseX = true;
            closeOnAccept = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "MTC_EditTitle".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, 48f, 100f, 30f), "MTC_Label".Translate());
            GUI.SetNextControlName("MapTabLabel");
            label = Widgets.TextField(new Rect(105f, 45f, inRect.width - 105f, 32f), label, 40);

            Widgets.Label(new Rect(0f, 92f, 100f, 30f), "MTC_Icon".Translate());
            float x = 105f;
            for (int i = 0; i < MapTabIcons.Names.Length; i++)
            {
                Rect option = new Rect(x + i * 45f, 88f, 38f, 38f);
                if (iconIndex == i) Widgets.DrawHighlightSelected(option);
                if (Widgets.ButtonInvisible(option)) iconIndex = i;
                Texture2D texture = MapTabIcons.Get(i);
                if (texture != null) GUI.DrawTexture(option.ContractedBy(8f), texture);
                else Widgets.Label(option, "—");
                TooltipHandler.TipRegion(option, ("MTC_Icon_" + MapTabIcons.Names[i]).Translate());
            }

            Rect clearRect = new Rect(0f, inRect.height - 40f, 110f, 35f);
            if (Widgets.ButtonText(clearRect, "MTC_Clear".Translate()))
            {
                label = string.Empty;
                iconIndex = 0;
            }

            Rect saveRect = new Rect(inRect.width - 110f, inRect.height - 40f, 110f, 35f);
            if (Widgets.ButtonText(saveRect, "MTC_Save".Translate())) SaveAndClose();
        }

        public override void PostOpen()
        {
            base.PostOpen();
            UI.FocusControl("MapTabLabel", this);
        }

        private void SaveAndClose()
        {
            customization.CustomLabel = label.Trim();
            customization.IconIndex = iconIndex;
            Close();
        }
    }
}
