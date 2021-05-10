﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Projects;
using MonoDevelop.UnitTesting;

namespace CodeCoverage.Core
{
  public interface ICoverageService
  {
    Task CollectCoverageForTestProject(Project testProject);
  }

  public interface ICoverageProvider
  {
    string RunSettingsDataCollectorFriendlyName { get; }
    void Prepare(Project testProject, ConfigurationSelector configuration, DataCollectorSettings coverageSettings);
    ICoverageResults GetCoverage(Project testProject, ConfigurationSelector configuration);
  }

  public class CoverageService : IDisposable
  {
    readonly ICoverageProvider provider;
    readonly ICoverageResultsRepository repository;

    TaskCompletionSource<bool> coverageCollectionCompletion;

    public CoverageService(ICoverageProvider provider, ICoverageResultsRepository repository)
    {
      this.provider = provider;
      this.repository = repository;
      UnitTestService.TestSessionStarting += UnitTestService_TestSessionStarting;
    }

    public async Task CollectCoverageForTestProject(Project testProject)
    {
      await RunTests(testProject);
      if (coverageCollectionCompletion == null) return;
      await coverageCollectionCompletion.Task;
      coverageCollectionCompletion = null;
    }

    protected virtual async Task RunTests(Project testProject)
    {
      IExecutionHandler mode = null;
      ExecutionContext context = new ExecutionContext(mode, IdeApp.Workbench.ProgressMonitors.ConsoleFactory, null);
      var firstRootTest = UnitTestService.FindRootTest(testProject);
      if (coverageCollectionCompletion != null || firstRootTest == null || !firstRootTest.CanRun(mode)) return;
      coverageCollectionCompletion = new TaskCompletionSource<bool>();
      await UnitTestService.RunTest(firstRootTest, context, true).Task;
    }

    private async void UnitTestService_TestSessionStarting(object sender, TestSessionEventArgs e)
    {
      if (coverageCollectionCompletion == null || e.Test.OwnerObject is not Project testProject) return;

      var configuration = IdeApp.Workspace.ActiveConfiguration;
      DataCollectorSettings coverageSettings = GetRunSettings(testProject);
      provider.Prepare(testProject, configuration, coverageSettings);
      await e.Session.Task;
      var results = provider.GetCoverage(testProject, configuration);
      if (results != null) SaveResults(results, testProject, configuration);
      coverageCollectionCompletion.SetResult(true);
    }

    protected DataCollectorSettings GetRunSettings(Project testProject)
    {
      string solutionDirectoryPath = testProject.ParentSolution.BaseDirectory.ToString();
      string[] runSettingsFiles = Directory.GetFiles(solutionDirectoryPath, "*.runsettings");
      string runSettingsFile = runSettingsFiles.FirstOrDefault();
      if (runSettingsFile == null) return null;

      return ParseRunSettings(runSettingsFile);
    }

    protected virtual DataCollectorSettings ParseRunSettings(string runSettingsFile)
    {
      try
      {
        using FileStream settingsFileStream = new FileStream(runSettingsFile, FileMode.Open);
        using StreamReader reader = new StreamReader(settingsFileStream);
        string xml = reader.ReadToEnd();
        DataCollectionRunSettings runSettings = XmlRunSettingsUtilities.GetDataCollectionRunSettings(xml);
        return runSettings.DataCollectorSettingsList.FirstOrDefault(
          s => s.FriendlyName == provider.RunSettingsDataCollectorFriendlyName && s.IsEnabled);
      } catch
      {
        return null;
      }
    }

    protected virtual void SaveResults(ICoverageResults results, Project testProject, ConfigurationSelector configuration)
    {
      repository.SaveResults(results, testProject, configuration);
    }

    public void Dispose()
    {
      UnitTestService.TestSessionStarting -= UnitTestService_TestSessionStarting;
    }
  }
}