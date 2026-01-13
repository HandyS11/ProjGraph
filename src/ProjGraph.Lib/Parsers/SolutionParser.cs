using Microsoft.Build.Construction;
using ProjGraph.Core.Models;

namespace ProjGraph.Lib.Parsers;

public class SolutionParser
{
    public SolutionGraph Parse(string solutionPath)
    {
        var solution = SolutionFile.Parse(solutionPath);
        var projects = new List<Project>();
        var dependencies = new List<Dependency>();

        var projectMap = new Dictionary<string, Project>();

        foreach (var projectInSolution in solution.ProjectsInOrder)
        {
            if (projectInSolution.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            {
                var p = new Project(
                    Guid.NewGuid(),
                    projectInSolution.ProjectName,
                    projectInSolution.AbsolutePath,
                    projectInSolution.RelativePath,
                    "unknown", // Will be refined by ProjectParser if needed
                    ProjectType.Library
                );
                projects.Add(p);
                projectMap[projectInSolution.RelativePath] = p;
            }
        }

        // Logic to extract dependencies between these projects would usually require
        // parsing individual .csproj files with ProjectParser.

        return new SolutionGraph(
            Path.GetFileName(solutionPath),
            solutionPath,
            projects,
            dependencies
        );
    }
}
