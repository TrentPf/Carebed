using Carebed.Infrastructure.Enums;

namespace Carebed.Managers
{
    internal class DisplayManager : IManager
    {
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call this when you render patient/sensor data to the GUI.
        /// It logs the same payload so file output matches what's shown.
        /// </summary>
        public void ShowPatientData<T>(T patientDto)
        {
            // TODO: update the UI here (this method is the hook point).
            // Log the payload using the project LoggingManager singleton.
            LoggingManager.Instance.Log(
                MessageOrigin.DisplayManager,
                MessageType.System,
                "Display updated",
                patientDto,
                Infrastructure.Enums.LogLevelEnum.Info
            );
        }
    }
}
