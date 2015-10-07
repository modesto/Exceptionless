using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Jobs;
using Foundatio.Logging;

namespace EventPostsJob {
    public class Program {
        public static int Main(string[] args) {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Api\App_Data");
            if (Directory.Exists(path))
                AppDomain.CurrentDomain.SetData("DataDirectory", path);

            return JobRunner.RunInConsole(new JobRunOptions {
                JobType = typeof(ReproduceQueryOverheadJob),
                ServiceProviderTypeName = "Exceptionless.Insulation.Jobs.FoundatioBootstrapper,Exceptionless.Insulation",
                InstanceCount = 1,
                Interval = TimeSpan.Zero,
                RunContinuous = false
            });
        }
    }

    public class ReproduceQueryOverheadJob : JobBase {
        private readonly bool _changeValueToRunGrowingQuery = false;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly EventStats _stats;

        public ReproduceQueryOverheadJob(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, EventStats stats) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _stats = stats;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken cancellationToken = new CancellationToken()) {
            var options = new PagingOptions { Page = 1, Limit = 10 };
            for (int index = 0; index < 10; index++) {
                var results = await _projectRepository.GetAllAsync(paging: options);
                Logger.Trace().Message($"Total Projects: {results.Total}").Write();

                if (_changeValueToRunGrowingQuery) {
                    var projects = results.Documents.ToList();
                    var organizations = await _organizationRepository.GetByIdsAsync(projects.Select(p => p.Id).ToArray(), useCache: true);
                    StringBuilder builder = new StringBuilder();
                    for (int index2 = 0; index2 < projects.Count; index2++) {
                        if (index2 > 0)
                            builder.Append(" OR ");

                        var project = projects[index2];
                        var organization = organizations.Documents.FirstOrDefault(o => o.Id == project.Id);
                        if (organization != null && organization.RetentionDays > 0)
                            builder.AppendFormat("(project:{0} AND (date:[now/d-{1}d TO now/d+1d}} OR last:[now/d-{1}d TO now/d+1d}}))", project.Id, organization.RetentionDays);
                        else
                            builder.AppendFormat("project:{0}", project.Id);
                    }

                    var result = await _stats.GetTermsStatsAsync(DateTime.MinValue, DateTime.MaxValue, "project_id", builder.ToString());
                }
            }
            return JobResult.Success;
        }
    }
}
