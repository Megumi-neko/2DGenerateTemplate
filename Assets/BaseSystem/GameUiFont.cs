using UnityEngine;

namespace Game.BaseSystem
{
    public static class GameUiFont
    {
        public const string ResourcePath = "Fond/AaShiLaiMu-2";
        private const string FallbackBuiltinFont = "LegacyRuntime.ttf";

        public static Font Load()
        {
            Font font = Resources.Load<Font>(ResourcePath);
            if (font != null)
            {
                return font;
            }

            return Resources.GetBuiltinResource<Font>(FallbackBuiltinFont);
        }
    }
}
