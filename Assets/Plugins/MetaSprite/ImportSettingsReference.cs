using UnityEngine;

namespace MetaSprite
{
    public class ImportSettingsReference : ScriptableObject
    {
        public ImportSettings ImporterSettings;
    }

    public class MetaSpriteImportData
    {
        public string metaSpriteSettingsGuid;
    }
}