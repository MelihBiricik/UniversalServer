namespace UniversalServer.Model
{
    public interface ISettingsRepository
    {
        event SettingsSavedEventHandler DataSaved;
        void SaveSettings(string setting);
    }
}
