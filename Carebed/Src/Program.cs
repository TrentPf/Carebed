using Carebed.Infrastructure.EventBus;

namespace Carebed.src
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // Create and initialize shared services
            var eventBus = new BasicEventBus();
            eventBus.Initialize();

            // Optional: global exception hooks for UI/background threads
            Application.ThreadException += (s, e) =>
            {
                // TODO: log e.Exception
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                // TODO: log e.Exception
                e.SetObserved();
            };

            // Pass dependencies into the main form
            using var mainDashboard = new MainDashboard(eventBus);

            // When the form closes we can shutdown services
            mainDashboard.FormClosed += (s, e) => eventBus.Shutdown();

            Application.Run(mainDashboard);
        }
    }
}