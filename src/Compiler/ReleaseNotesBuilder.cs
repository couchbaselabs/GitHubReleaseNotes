using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Octokit;
using MarkdownSharp;
using System.Diagnostics;

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

        public async Task<Tuple<string,string>> BuildReleaseNotes()
        {
            LoadMilestones();

            GetTargetMilestone();
            var issues = await GetIssues(targetMilestone);
            var markdownBuilder = new StringBuilder();
            var xmlBuilder = new StringBuilder();
            var previousMilestone = GetPreviousMilestone();
            var numberOfCommits = await gitHubClient.GetNumberOfCommitsBetween(previousMilestone, targetMilestone);
            var message = String.Empty;

            if (repository.Equals("sync_gateway"))
            {
                xmlBuilder.AppendFormat("          <article id=\"{0}\">\n", this.milestoneTitle); 
                xmlBuilder.AppendFormat("            <title>{0} {1}</title>\n", this.milestoneTitle, DateTime.UtcNow.ToLongDateString());
                xmlBuilder.AppendFormat("            <description>{0} Release Notes for Sync Gateway</description>\n", this.milestoneTitle);
                xmlBuilder.AppendLine  ("            <introduction>");
            }
            else
            {
                xmlBuilder.AppendFormat("                <topic id=\"{0}\">\n", this.milestoneTitle); 
                xmlBuilder.AppendFormat("                    <title>{0} {1}</title>\n", this.milestoneTitle, DateTime.UtcNow.ToLongDateString());
                xmlBuilder.AppendLine  ("                    <body>");
            }

            var m = new Markdown(new MarkdownOptions { EmptyElementSuffix = " />" });

            if (issues.Count > 0)
            {
                var issuesText = String.Format(issues.Count == 1 ? "{0} issue" : "{0} issues", issues.Count);

                if (numberOfCommits > 0)
                {
                    var commitsLink = GetCommitsLink(previousMilestone);
                    var commitsText = String.Format(numberOfCommits == 1 ? "{0} commit" : "{0} commits", numberOfCommits);

                    message = string.Format(@"As part of this release we had [{0}]({1}) which resulted in [{2}]({3}) being closed.", commitsText, commitsLink, issuesText, targetMilestone.HtmlUrl());
                }
                else
                {
                    message = string.Format(@"As part of this release we had [{0}]({1}) closed.", issuesText, targetMilestone.HtmlUrl());
                }
            }
            else if (numberOfCommits > 0)
            {
                var commitsLink = GetCommitsLink(previousMilestone);
                var commitsText = String.Format(numberOfCommits == 1 ? "{0} commit" : "{0} commits", numberOfCommits);
                message = string.Format(@"As part of this release we had [{0}]({1}).", commitsText, commitsLink);
            }
            
            markdownBuilder.AppendLine(message);
            xmlBuilder.Append("          ");
            xmlBuilder.Append(
                ConvertMarkdownToXml(message, m)
            );
            markdownBuilder.AppendLine(targetMilestone.Description);
            markdownBuilder.AppendLine();

            xmlBuilder.Append("          ");
            xmlBuilder.Append(
                ConvertMarkdownToXml(targetMilestone.Description, m)
            );

            AddIssues(issues, m, markdownBuilder, xmlBuilder, milestoneTitle);

            try
            {
                await AddFooter(markdownBuilder, xmlBuilder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error appending the footer: " + ex.Message);
            }

            if (repository.Equals("sync_gateway"))
            {
                xmlBuilder.AppendLine("            </introduction>");
                xmlBuilder.AppendLine("          </article>");
            }
            else
            {
                xmlBuilder.AppendLine  ("                    </body>");                
                xmlBuilder.AppendLine("                </topic>"); 
            }

            return new Tuple<string,string>(markdownBuilder.ToString(), xmlBuilder.ToString());
        }

        static string ConvertMarkdownToXml(string message, Markdown m)
        {
            return m.Transform(message).Replace("<p>", "<paragraph>").Replace("</p>", "</paragraph>").Replace("<a ", "<external-ref ").Replace("</a>", "</external-ref>").Replace("<ul>", "<unordered-list>").Replace("</ul>", "</unordered-list>").Replace("<li>", "<list-item>").Replace("</li>", "</list-item>").Replace("<b>", "<strong>").Replace("</b>", "</strong>");
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

        static void AddIssues(IList<Issue> issues, Markdown m, StringBuilder stringBuilder, StringBuilder xmlBuilder, String version)
        {
            Append(issues.Where(i => i.State == ItemState.Closed), "performance", "Performance Improvements", stringBuilder, xmlBuilder, m, version);
            Append(issues.Where(i => i.State == ItemState.Closed), "enhancement", "Enhancements", stringBuilder, xmlBuilder, m, version);
            Append(issues.Where(i => i.State == ItemState.Closed), "bug", "Bugs", stringBuilder, xmlBuilder, m, version);
            Append(issues.Where(i => i.State == ItemState.Open), "known-issue", "Known Issues", stringBuilder, xmlBuilder, m, version);
        }

        async Task AddFooter(StringBuilder stringBuilder, StringBuilder xmlBuilder)
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
            }
            else 
            {
                using (var reader = file.OpenText())
                {
                    stringBuilder.Append(await reader.ReadToEndAsync());
                }
            }

            var xmlFile = new FileInfo(String.Concat(repository, "-", "footer.xml"));

            if (!xmlFile.Exists)
            {
                return;
            }

            using (var reader = xmlFile.OpenText())
            {
                var footerTemplate = await reader.ReadToEndAsync();
                var footer = String.Format(footerTemplate, milestoneTitle.Replace(".", String.Empty), this.milestoneTitle);
                xmlBuilder.AppendLine(footer);
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

        static void Append(IEnumerable<Issue> issues, string label, string pluralizedLabel, StringBuilder stringBuilder, StringBuilder xmlBuilder, Markdown m, string milestoneTitle)
        {
            var features = issues
                .Where(x => x.Labels.Any(l => l.Name == label))
                .ToList();
            if (features.Count > 0)
            {
                stringBuilder.AppendFormat("__{0}__\r\n", pluralizedLabel);

                xmlBuilder.AppendFormat("                <section id=\"{0}-{1}\">\n", milestoneTitle.Replace(".", String.Empty), pluralizedLabel);
                xmlBuilder.AppendFormat("                    <title>{0}</title>\n", pluralizedLabel);
                xmlBuilder.AppendLine  ("                    <body>");
                xmlBuilder.AppendLine  ("                      <unordered-list>");

                var issueText = String.Empty;

                foreach (var issue in features)
                {
                    issueText = string.Format("- [__#{0}__]({1}) {2}\r\n", issue.Number, issue.HtmlUrl, issue.Title.Capitalize());
                    stringBuilder.Append(issueText);
                    xmlBuilder.AppendFormat("                          <list-item><external-ref href=\"{0}\"><strong>#{1}</strong></external-ref> {2}</list-item>\n", issue.HtmlUrl, issue.Number, m.Transform(issue.Title).Capitalize().Replace("<p>", String.Empty).Replace("</p>", String.Empty).Replace(Environment.NewLine, String.Empty));
                }

                stringBuilder.AppendLine();

                xmlBuilder.AppendLine  ("                      </unordered-list>");
                xmlBuilder.AppendLine  ("                    </body>");
                xmlBuilder.AppendLine  ("                  </section>");
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
