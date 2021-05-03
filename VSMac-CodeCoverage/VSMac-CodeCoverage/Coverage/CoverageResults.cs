﻿using System.Collections.Generic;
using System.IO;

namespace CodeCoverage.Coverage
{
  public interface ICoverageResults
  {
    Dictionary<string, CoverageSummary> ModuleCoverage { get; }
    Dictionary<int, int> CoverageForFile(string path);
    void SaveTo(Stream stream);
  }

  public struct CoverageSummary
  {
    public double Line { get; }
    public double Branch { get; }

    public CoverageSummary(double line, double branch)
    {
      Line = line;
      Branch = branch;
    }
  }
}
