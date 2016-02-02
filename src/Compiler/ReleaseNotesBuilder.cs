using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octokit;

namespace ReleaseNotesCompiler
{
    using System.IO;

    public class ReleaseNotesBuilder
    {
        IGitHubClient gitHubClient;
        string user;
        string repository;
        string milestoneTitle;
        List<Milestone> milestones;
        Milestone targetMilestone;

        public ReleaseNotesBuilder(IGitHubClient gitHubClient, string user, string repository, string milestoneTitle)
        {
            this.gitHubClient = gitHubClient;
            this.user = user;
            this.repository = repository;
            this.milestoneTitle = milestoneTitle;
        }

        public async Task<string> BuildReleaseNotes()
        {
            LoadMilestones();

            GetTargetMilestone();
            var issues = await GetIssues(targetMilestone);
            var stringBuilder = new StringBuilder();
            var previousMilestone = GetPreviousMilestone();
            var numberOfCommits = await gitHubClient.GetNumberOfCommitsBetween(previousMilestone, targetMilestone);

            if (issues.Count > 0)
            {
                var issuesText = String.Format(issues.Count == 1 ? "{0} issue" : "{0} issues", issues.Count);

                if (numberOfCommits > 0)
                {
                    var commitsLink = GetCommitsLink(previousMilestone);
                    var commitsText = String.Format(numberOfCommits == 1 ? "{0} commit" : "{0} commits", numberOfCommits);

                    stringBuilder.AppendFormat(@"As part of this release we had [{0}]({1}) which resulted in [{2}]({3}) being closed.", commitsText, commitsLink, issuesText, targetMilestone.HtmlUrl());
                }
                else
                {
                    stringBuilder.AppendFormat(@"As part of this release we had [{0}]({1}) closed.", issuesText, targetMilestone.HtmlUrl());
                }
            }
            else if (numberOfCommits > 0)
            {
                var commitsLink = GetCommitsLink(previousMilestone);
                var commitsText = String.Format(numberOfCommits == 1 ? "{0} commit" : "{0} commits", numberOfCommits);
                stringBuilder.AppendFormat(@"As part of this release we had [{0}]({1}).", commitsText, commitsLink);
            }
            stringBuilder.AppendLine();

            stringBuilder.AppendLine(targetMilestone.Description);
            stringBuilder.AppendLine();

            AddIssues(stringBuilder, issues);

            await AddFooter(stringBuilder);

            return stringBuilder.ToString();
        }

        Milestone GetPreviousMilestone()
        {
            var currentVersion = targetMilestone.Version();
            return milestones
                .OrderByDescending(m => m.Version())
                .Distinct().ToList()
                .SkipWhile(x => x.Version() >= currentVersion)
                .FirstOrDefault();
        }

        string GetCommitsLink(Milestone previousMilestone)
        {
            if (previousMilestone == null)
            {
                return string.Format("https://github.com/{0}/{1}/commits/{2}", user, repository, targetMilestone.Title);
            }
            return string.Format("https://github.com/{0}/{1}/compare/{2}...{3}", user, repository, previousMilestone.Title, targetMilestone.Title);
        }

        void AddIssues(StringBuilder stringBuilder, List<Issue> issues)
        {
            Append(issues.Where(i => i.State == ItemState.Closed), "performance", "Performance Improvements", stringBuilder);
            Append(issues.Where(i => i.State == ItemState.Closed), "enhancement", "Enhancements", stringBuilder);
            Append(issues.Where(i => i.State == ItemState.Closed), "bug", "Bugs", stringBuilder);
            Append(issues.Where(i => i.State == ItemState.Open), "known-issue", "Known Issues", stringBuilder);
        }

        static async Task AddFooter(StringBuilder stringBuilder)
        {
            var file = new FileInfo("footer.md");

            if (!file.Exists)
            {
                file = new FileInfo("footer.txt");
            }

            if (!file.Exists)
            {
                stringBuilder.Append(@"## Where to get it
You can download this release from [Couchbase.com](http://www.couchbase.com/nosql-databases/downloads#Couchbase_Mobile)");
                return;
            }

            using (var reader = file.OpenText())
            {
                stringBuilder.Append(await reader.ReadToEndAsync());
            }
        }

        void LoadMilestones()
        {
            milestones = gitHubClient.GetMilestones();
        }

        async Task<List<Issue>> GetIssues(Milestone milestone)
        {
            var issues = await gitHubClient.GetIssues(milestone);
            return issues.Where(CheckForValidLabels).ToList();
        }

        static bool CheckForValidLabels(Issue issue)
        {
            var count = 0;
            foreach(var l in issue.Labels)
            {
                if (l.Name == "chore")
                {
                    return false;
                }
               else if (l.Name == "bug" ||
                    l.Name == "enhancement" ||
                    l.Name == "known-issue" ||
                    l.Name == "performance")
                {
                    count++;
                }
            }
            return count > 0 && !issue.IsPullRequest();
        }

        static void Append(IEnumerable<Issue> issues, string label, string pluralizedLabel, StringBuilder stringBuilder)
        {
            var features = issues
                .Where(x => x.Labels.Any(l => l.Name == label))
                .ToList();
            if (features.Count > 0)
            {
                stringBuilder.AppendFormat("__{0}__\r\n", pluralizedLabel);

                foreach (var issue in features)
                {
                    stringBuilder.AppendFormat("- [__#{0}__]({1}) {2}\r\n", issue.Number, issue.HtmlUrl, issue.Title.Capitalize());
                }
                stringBuilder.AppendLine();
            }
        }

        void GetTargetMilestone()
        {
            targetMilestone = milestones.FirstOrDefault(x => x.Title == milestoneTitle);
            if (targetMilestone == null)
            {
                throw new Exception(string.Format("Could not find milestone for '{0}'.", milestoneTitle));
            }
        }
    }
}
