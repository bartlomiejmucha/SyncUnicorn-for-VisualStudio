using Microsoft.VisualStudio.Shell.Interop;
using System;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

namespace SyncUnicorn
{
    public static class LoggerAsync
    {
        private static IVsOutputWindowPane _pane;
        private static IVsOutputWindow _output;
        private static IAsyncServiceProvider _provider;
        private static Guid _guid;
        private static string _name;
        private static readonly object SyncRoot = new object();

        public static async void Initialize(IAsyncServiceProvider provider, string name)
        {
            _provider = provider;
            _name = name;

            _output = await _provider.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
        }

        public static void ActivatePane()
        {
            try
            {
                if (EnsurePane())
                {
                    _pane.Activate();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                if (EnsurePane())
                {
                    _pane.OutputStringThreadSafe(message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        public static void Log(Exception ex)
        {
            if (ex != null)
            {
                Log(ex.ToString());
            }
        }

        public static void Clear()
        {
            _pane?.Clear();
        }

        public static void DeletePane()
        {
            if (_pane != null)
            {
                try
                {
                    _output.DeletePane(ref _guid);
                    _pane = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write(ex);
                }
            }
        }

        private static bool EnsurePane()
        {
            if (_pane == null)
            {
                lock (SyncRoot)
                {
                    if (_pane == null)
                    {
                        _guid = Guid.NewGuid();

                        _output.CreatePane(ref _guid, _name, 1, 1);
                        _output.GetPane(ref _guid, out _pane);
                    }
                }
            }

            return _pane != null;
        }
    }
}
