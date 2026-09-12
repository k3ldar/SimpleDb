using PluginManager.Abstractions;

namespace SimpleDB.Console.Internal
{
    internal class SettingsProvider : ISettingsProvider
    {
        private readonly SimpleDBSettings _simpleDBSettings;

        public SettingsProvider(SimpleDBSettings simpleDBSettings)
        {
            _simpleDBSettings = simpleDBSettings;
        }

        public T GetSettings<T>(in string storage, in string sectionName)
        {
            if (sectionName == "SimpleDBSettings")
            {
                return (T)(object)_simpleDBSettings;
            }

            return default;
        }

        public T GetSettings<T>(in string sectionName)
        {
            return GetSettings<T>("test", sectionName);
        }
    }
}
