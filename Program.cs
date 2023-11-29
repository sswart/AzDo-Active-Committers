// See https://aka.ms/new-console-template for more information

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Unicode;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Azdo_Insights;

public class Program
{
    public static async Task Main(string[] args)
    {
        var baseUri = Environment.GetEnvironmentVariable("AzDoOrganisation");
        var pat = Environment.GetEnvironmentVariable("AzDoPAT");
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
                    Console.WriteLine(e.Message);
                }
            }
        }

        var count = allCommitters.Distinct().Count();
        Console.WriteLine($"Found {count} distinct committers in the last month.");
    }
    private record ProjectRepository(string Project, string Repository);
}



