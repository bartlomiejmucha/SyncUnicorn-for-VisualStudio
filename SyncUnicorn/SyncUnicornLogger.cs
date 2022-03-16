using Microsoft.Build.Framework;

namespace SyncUnicorn
{
    public class SyncUnicornLogger : ILogger
    {
        private bool _loggingActive;

        public SyncUnicornLogger()
        {
            LoggerAsync.Clear();
        }

        public void Initialize(IEventSource eventSource)
        {
            eventSource.TaskStarted += (sender, args) =>
            {
                if (args.TaskName == "SyncUnicorn")
                {
                    _loggingActive = true;

                    LoggerAsync.ActivatePane();

                    LoggerAsync.Log("========== SyncUnicorn started ==========");
                }
            };

            eventSource.TaskFinished += (sender, args) =>
            {
                if (args.TaskName == "SyncUnicorn")
                {
                    _loggingActive = false;

                    LoggerAsync.Log("========== SyncUnicorn finished ==========");
                }
            };

            eventSource.AnyEventRaised += (sender, args) =>
            {
                if (_loggingActive)
                {
                    LoggerAsync.Log(args.Message);
                }
            };
        }

        public void Shutdown()
        {
        }

        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }
    }
}
