using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace KingdomIdle.UGUI.Editor
{
    public sealed class AuthoredGlyphBuildValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1900;

        public void OnPreprocessBuild(BuildReport report)
        {
            try { MagePolishPreparation.ValidateAuthoredGlyphs(); }
            catch (Exception error)
            {
                throw new BuildFailedException("Cannot verify authored UI glyphs before build. " + error.Message);
            }
        }
    }
}
