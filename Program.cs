// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azdo_Insights;

// ReSharper disable once ClassNeverInstantiated.Global
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var patOption = new Option<string>(
            name: "--PAT",
            description: "A Personal Access Token with code read rights.");

        var orgOption = new Option<string>(
            name: "--Organisation",
            description: "The name of the Azure DevOps Organisation");

        var rootCommand = new RootCommand("Command to find Active Azure DevOps committers in the last month.");
        rootCommand.AddOption(patOption);
        rootCommand.AddOption(orgOption);

        rootCommand.SetHandler(async (organisation, pat) => await GetActiveCommitters(organisation, pat),
            orgOption, patOption);

        return await rootCommand.InvokeAsync(args);

    }
    private static async Task GetActiveCommitters(string org, string pat)
    {
        var baseUri = $"https://dev.azure.com/{org}";
        var orgUrl = new Uri(baseUri);
        
        var credentials = new VssBasicCredential("", pat);

        var allProjects = new List<TeamProjectReference>();
        using (var projectHttpClient = new ProjectHttpClient(orgUrl, credentials))
        {
            string? continuationToken = null;
            do
            {
                var projects = await projectHttpClient.GetProjects(continuationToken: continuationToken);
                allProjects.AddRange(projects);
                continuationToken = projects.ContinuationToken;
                
            } while (!string.IsNullOrEmpty(continuationToken));
        }
            
        var connection = new VssConnection(orgUrl, new VssBasicCredential(string.Empty, pat));
        var allCommitters = new List<string>();
        using (GitHttpClient gitClient = connection.GetClient<GitHttpClient>())
        {
            var allRepos = new List<GitRepository>();
            foreach (var project in allProjects)
            {
                var repos = await gitClient.GetRepositoriesAsync(project.Name);
                allRepos.AddRange(repos);
            }

            foreach (var repo in allRepos)
            {
                try
                {
                    var commits = await gitClient.GetCommitsAsync(repo.ProjectReference.Id, repo.Id,
                        new GitQueryCommitsCriteria()
                        {
                            FromDate = DateTime.Now.AddMonths(-1).ToShortDateString()
                        });

                    var committers = commits.Select(c => c.Committer.Email);
                    allCommitters.AddRange(committers);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Caught exception while retrieving committers for repository {repo.Name} in project {repo.ProjectReference.Name}");
                    Console.WriteLine("----------------------------");
                    Console.WriteLine(e.Message);
                    Console.WriteLine("----------------------------");
                    Console.WriteLine("Continuing...");
                }
            }
        }

        var count = allCommitters.Distinct().Count();
        Console.WriteLine($"Found {count} distinct committers in the last month.");
    }
}



