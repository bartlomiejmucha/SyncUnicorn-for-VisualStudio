using EnvDTE;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using Project = EnvDTE.Project;
using Task = System.Threading.Tasks.Task;

namespace SyncUnicorn
{
    internal sealed class SyncUnicornCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("96b503c1-aebd-44ec-b134-f3eb1a5e037e");
        private readonly AsyncPackage _package;

        private SyncUnicornCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this._package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, menuCommandId);

            menuItem.BeforeQueryStatus += (sender, args) =>
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (sender is OleMenuCommand menuCommand)
                {
                    if (Package.GetGlobalService(typeof(SDTE)) is DTE dte && (dte.ActiveSolutionProjects is Array projects && projects.Length > 0))
                    {
                        if (projects.GetValue(0) is Project activeProject && !string.IsNullOrEmpty(activeProject.FullName))
                        {
                            menuCommand.Visible = ProjectCollection.GlobalProjectCollection.LoadedProjects.Any(x =>
                            {
                                ThreadHelper.ThrowIfNotOnUIThread();
                                return x.FullPath.Equals(activeProject.FullName) && x.Targets.ContainsKey("SyncUnicorn") && !VsShellUtilities.IsSolutionBuilding(package);
                            });
                        }
                    }
                }
            };

            commandService.AddCommand(menuItem);
        }

        public static SyncUnicornCommand Instance
        {
            get;
            private set;
        }

        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this._package;
            }
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync((typeof(IMenuCommandService))) as OleMenuCommandService;
            Instance = new SyncUnicornCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Package.GetGlobalService(typeof(SDTE)) is DTE dte && (dte.ActiveSolutionProjects is Array projects && projects.Length > 0))
            {
                if (projects.GetValue(0) is Project activeProject)
                {
                    var parameters = CreateBuildParameters();
                    var data = CreateBuildRequestData(activeProject, "SyncUnicorn", dte);

                    Task.Run(() =>
                    {
                        using (var buildManager = new BuildManager())
                        {
                            buildManager.Build(parameters, data);
                        }
                    });
                }
            }
        }

        private BuildParameters CreateBuildParameters()
        {
            var projectCollection = new ProjectCollection();
            var buildParameters = new BuildParameters(projectCollection)
            {
                Loggers = new List<ILogger>() { new SyncUnicornLogger() }
            };

            return buildParameters;
        }

        private BuildRequestData CreateBuildRequestData(Project proj, string target, DTE dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var globalProperties = new Dictionary<string, string>();
            if (proj.ConfigurationManager != null)
            {
                var config = proj.ConfigurationManager.ActiveConfiguration;
                globalProperties["Configuration"] = config.ConfigurationName;
                globalProperties["Platform"] = config.PlatformName.Replace(" ", "");
            }

            var solutionDir = Path.GetDirectoryName(dte.Solution.FullName);
            globalProperties["SolutionDir"] = solutionDir;

            return new BuildRequestData(proj.FullName, globalProperties, null, new[] { target }, null,
                BuildRequestDataFlags.ReplaceExistingProjectInstance);
        }
    }
}
