﻿using Resurgent.UtilityBelt.Library.Utilities;
using Resurgent.UtilityBelt.Library.Utilities.ImageInput;
using Resurgent.UtilityBelt.Library.Utilities.XbeModels;
// using Resurgent.UtilityBelt.Library.Utilities.Xiso;
// using Repackinator;
using Repackinator.Localization.Language;
using System.Diagnostics;
using Repackinator.Core.Models;
using Repackinator.Core.Logging;

namespace Repackinator.Core.Actions
{
    public class Scanner
    {
        private Action<LogMessage>? Logger { get; set; }

        private Action<ProgressInfo>? Progress { get; set; }

        private ProgressInfo CurrentProgress = new ProgressInfo();

        public GameData[]? GameDataList { get; set; }

        private void SendProgress()
        {
            if (Progress == null)
            {
                return;
            }
            Progress(CurrentProgress);
        }

        private void Log(LogMessageLevel level, string message)
        {
            if (Logger == null)
            {
                return;
            }
            var logMessage = new LogMessage(level, message);
            Logger(logMessage);
            File.AppendAllText("ScanLog.txt", logMessage.ToLogFormat());
        }

        private void ProcessFolder(string folder, Config config, Stopwatch procesTime, CancellationToken cancellationToken)
        {
            if (GameDataList == null)
            {
                Log(LogMessageLevel.Error, "GameData should not be null.");
                return;
            }

            try
            {
                CurrentProgress.Progress2 = 0;
                CurrentProgress.Progress2Text = folder;
                SendProgress();

                var isoToProcess = Directory.GetFiles(folder, "*.iso").OrderBy(o => o).ToArray();
                var cciToProcess = Directory.GetFiles(folder, "*.cci").OrderBy(o => o).ToArray();
                var csoToProcess = Directory.GetFiles(folder, "*.cso").OrderBy(o => o).ToArray();

                var mixedCount = 0;
                if (isoToProcess.Length > 0)
                {
                    mixedCount++;
                }
                if (cciToProcess.Length > 0)
                {
                    mixedCount++;
                }
                if (csoToProcess.Length > 0)
                {
                    mixedCount++;
                }

                if (mixedCount == 0)
                {
                    return;
                }
                else if (mixedCount > 1)
                {
                    Log(LogMessageLevel.Error, $"Folder '{folder}' contains mixed ISO and CCI.");
                    return;
                }

                var xbeData = Array.Empty<byte>();
                if (isoToProcess.Length > 0)
                {
                    using var xisoInput = new XisoInput(isoToProcess);
                    if (!XisoUtility.TryGetDefaultXbeFromXiso(xisoInput, ref xbeData))
                    {
                        Log(LogMessageLevel.Error, $"Unable to extract default.xbe.");
                        return;
                    }
                }
                else if (cciToProcess.Length > 0)
                {
                    using var xisoInput = new CciInput(cciToProcess);
                    if (!XisoUtility.TryGetDefaultXbeFromXiso(xisoInput, ref xbeData))
                    {
                        Log(LogMessageLevel.Error, $"Unable to extract default.xbe.");
                        return;
                    }
                }
                else if (csoToProcess.Length > 0)
                {
                    using var xisoInput = new CsoInput(csoToProcess);
                    if (!XisoUtility.TryGetDefaultXbeFromXiso(xisoInput, ref xbeData))
                    {
                        Log(LogMessageLevel.Error, $"Unable to extract default.xbe.");
                        return;
                    }
                }

                if (!XbeUtility.TryGetXbeCert(xbeData, out var cert) || cert == null)
                {
                    Log(LogMessageLevel.Error, $"Unable to get data from default.xbe.");
                    return;
                }

                var titleId = cert.Title_Id.ToString("X8");
                var gameRegion = XbeCertificate.GameRegionToString(cert.Game_Region);
                var version = cert.Version.ToString("X8");
                var xbeTitle = string.Empty;

                bool found = false;
                for (int i = 0; i < GameDataList.Length; i++)
                {
                    if (!config.Section.Equals("[AllSections]") && GameDataList[i].Section.Equals(config.Section, StringComparison.CurrentCultureIgnoreCase) == false)
                    {
                        continue;
                    }
                    if (GameDataList[i].TitleID == titleId && GameDataList[i].Region == gameRegion && GameDataList[i].Version == version)
                    {
                        found = true;
                        GameDataList[i].Process = "N";
                        xbeTitle = GameDataList[i].XBETitle;
                        break;
                    }
                }

                if (found)
                {
                    Log(LogMessageLevel.Completed, $"Game found '{xbeTitle}'.");
                }
                else
                {
                    Log(LogMessageLevel.Warning, $"Game not found with TitleID = {titleId}, Region = '{gameRegion}', Version = {version}.");
                }

                CurrentProgress.Progress2 = 1.0f;
                SendProgress();
            }
            catch (Exception ex)
            {
                Log(LogMessageLevel.Error, $"Scanning '{folder}' caused error '{ex}'.");
            }
        }

        public bool StartScanning(GameData[]? gameData, Config config, Action<ProgressInfo>? progress, Action<LogMessage> logger, Stopwatch stopwatch, CancellationToken cancellationToken)
        {
            try
            {
                Logger = logger;
                Progress = progress;

                GameDataList = gameData;
                if (GameDataList == null)
                {
                    Log(LogMessageLevel.Error, "RepackList.json not found.");
                    return false;
                }

                if (Directory.Exists(config.OutputPath) == false)
                {
                    Log(LogMessageLevel.Error, "Output path is invalid.");
                    return false;
                }

                stopwatch.Restart();

                if (File.Exists("ScanLog.txt"))
                {
                    File.Delete("ScanLog.txt");
                }

                CurrentProgress.Progress1 = 0;
                CurrentProgress.Progress1Text = UserLocale.scandialog_progress_searching_directories;
                CurrentProgress.Progress2 = 0;
                CurrentProgress.Progress2Text = string.Empty;
                SendProgress();

                var pathsScanned = 0;
                var totalPaths = 1;
                var pathsToScan = new List<string>();
                pathsToScan.Add(config.OutputPath);
                while (pathsToScan.Count > 0)
                {
                    var pathToProcess = pathsToScan[0];
                    pathsToScan.RemoveAt(0);

                    CurrentProgress.Progress1 = pathsScanned / (float)totalPaths;
                    SendProgress();

                    ProcessFolder(pathToProcess, config, stopwatch, cancellationToken);

                    try
                    {
                        var pathsToAdd = Directory.GetDirectories(pathToProcess);
                        pathsToScan.AddRange(pathsToAdd);
                        totalPaths += pathsToAdd.Length;
                    }
                    catch (Exception ex)
                    {
                        Log(LogMessageLevel.Error, $"Unable to get folders in '{pathToProcess}' as caused error '{ex}'.");
                    }

                    pathsScanned++;

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }

                CurrentProgress.Progress1 = 1.0f;
                SendProgress();

                stopwatch.Stop();

                Log(LogMessageLevel.Done, $"Completed Scanning List (Time Taken {stopwatch.Elapsed.TotalHours:00}:{stopwatch.Elapsed.Minutes:00}:{stopwatch.Elapsed.Seconds:00}).");
                return true;
            }
            catch (Exception ex)
            {
                Log(LogMessageLevel.Error, $"Exception occurred '{ex}'.");
            }
            return false;
        }
    }
}
