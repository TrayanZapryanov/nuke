// Copyright 2020 Maintainers of NUKE.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities;
using Nuke.Common.Utilities.Collections;

namespace Nuke.Common.Tools.ReSharper
{
    public partial class ReSharperTasks
    {
        public const string ReSharperPluginLatest = null;

        private static void PreProcess<T>(ref T toolSettings) where T : ReSharperSettingsBase
        {
            if (toolSettings.Plugins.Count == 0)
                return;

            var wave = GetWave(toolSettings).NotNull("wave != null");
            var shadowDirectory = GetShadowDirectory(toolSettings, wave);

            FileSystemTasks.CopyDirectoryRecursively(
                Path.GetDirectoryName(toolSettings.ToolPath).NotNull(),
                shadowDirectory,
                DirectoryExistsPolicy.Merge,
                FileExistsPolicy.OverwriteIfNewer);

            toolSettings.Plugins.ForEach(x => HttpTasks.HttpDownloadFile(
                $"http://resharper-plugins.jetbrains.com/dotnet/api/v2/curated-feeds/Wave_v{wave}.0/Packages(Id='{x.Key}',Version='{x.Value}')/Download",
                Path.Combine(shadowDirectory, $"{x.Key}.nupkg")));
            
            toolSettings = toolSettings.SetToolPath(Path.Combine(shadowDirectory, Path.GetFileName(toolSettings.ToolPath)));
        }

        [CanBeNull]
        private static string GetWave(ReSharperSettingsBase toolSettings)
        {
            return Directory.GetParent(toolSettings.ToolPath)
                .DescendantsAndSelf(x => x.Parent)
                .Select(x => Path.Combine(x.FullName, "jetbrains.resharper.globaltools.nuspec"))
                .Where(File.Exists)
                .Select(x => new FileInfo(x).Directory.NotNull().Name)
                .Select(x => $"{x[2]}{x[3]}{x[5]}")
                .SingleOrDefault();
        }

        [CanBeNull]
        private static IProcess StartProcess(ReSharperSettingsBase toolSettings)
        {
            return ProcessTasks.StartProcess(toolSettings);
        }

        private static void PostProcess(ReSharperInspectCodeSettings toolSettings)
        {
            TeamCity.Instance?.ImportData(TeamCityImportType.ReSharperInspectCode, toolSettings.Output);
        }

        private static string GetShadowDirectory(ReSharperSettingsBase toolSettings, string wave)
        {
            var hashCode = toolSettings.ToolPath.Concat(toolSettings.Plugins.Select(x => x.Key + x.Value)).OrderBy(x => x).JoinComma().GetMD5Hash();
            return Path.Combine(NukeBuild.TemporaryDirectory, $"ReSharper-{wave}-{hashCode.Substring(startIndex: 0, length: 4)}");
        }
    }

    partial class ReSharperSettingsBase
    {
        public override Action<OutputType, string> CustomLogger => ProcessTasks.DefaultLogger;
    }
}
