using System;
using System.Collections.Generic;
using System.Linq;
using WebAI.Models;
using WebAI.Services.Database;

namespace WebAI.Services.Managers
{
    public class ProgressTracker
    {
        private readonly ProjectDataService _projectDataService;
        private readonly MilestoneDataService _milestoneDataService;
        private readonly TaskDataService _taskDataService;
        private readonly AgentDataService _agentDataService;
        private readonly MessageDataService _messageDataService;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ProgressTracker(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _projectDataService = new ProjectDataService(connectionString);
            _milestoneDataService = new MilestoneDataService(connectionString);
            _taskDataService = new TaskDataService(connectionString);
            _agentDataService = new AgentDataService(connectionString);
            _messageDataService = new MessageDataService(connectionString);
        }

        public ProjectProgressReport GetProjectProgress(int projectId)
        {
            try
            {
                ProjectModel project = _projectDataService.GetProject(projectId);
                if (project == null)
                    throw new ArgumentException(string.Format("Project with ID {0} not found", projectId));

                List<MilestoneModel> milestones = _milestoneDataService.GetMilestonesByProject(projectId);
                Dictionary<int, List<TaskModel>> tasksByMilestone = new Dictionary<int, List<TaskModel>>();

                foreach (MilestoneModel milestone in milestones)
                {
                    tasksByMilestone[milestone.MilestoneId] = _taskDataService.GetTasksByMilestone(milestone.MilestoneId);
                }

                return GenerateProjectProgressReport(project, milestones, tasksByMilestone);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error generating progress report for project ID: {0}", projectId), ex);
                throw;
            }
        }

        private ProjectProgressReport GenerateProjectProgressReport(
            ProjectModel project,
            List<MilestoneModel> milestones,
            Dictionary<int, List<TaskModel>> tasksByMilestone)
        {
            ProjectProgressReport report = new ProjectProgressReport
            {
                ProjectId = project.ProjectId,
                ProjectName = project.ProjectName,
                Status = project.Status,
                StartDate = project.CreatedAt,
                TotalMilestones = milestones.Count,
                CompletedMilestones = milestones.Count(m => m.Status == MilestoneStatus.Completed),
                MilestoneReports = new List<MilestoneProgressReport>()
            };

            foreach (MilestoneModel milestone in milestones)
            {
                List<TaskModel> milestoneTasks = tasksByMilestone[milestone.MilestoneId];
                MilestoneProgressReport milestoneReport = GenerateMilestoneProgressReport(milestone, milestoneTasks);
                report.MilestoneReports.Add(milestoneReport);

                // Update overall statistics
                report.TotalTasks += milestoneReport.TotalTasks;
                report.CompletedTasks += milestoneReport.CompletedTasks;
                report.BlockedTasks += milestoneReport.BlockedTasks;
                report.InProgressTasks += milestoneReport.InProgressTasks;
            }

            // Calculate overall progress percentages
            report.CalculateProgress();
            return report;
        }

        private MilestoneProgressReport GenerateMilestoneProgressReport(MilestoneModel milestone, List<TaskModel> tasks)
        {
            MilestoneProgressReport report = new MilestoneProgressReport
            {
                MilestoneId = milestone.MilestoneId,
                Title = milestone.Title,
                Status = milestone.Status,
                StartDate = milestone.CreatedAt,
                CompletionDate = milestone.CompletedAt,
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == WebAI.Models.TaskStatus.Completed),
                BlockedTasks = tasks.Count(t => t.Status == WebAI.Models.TaskStatus.Blocked),
                InProgressTasks = tasks.Count(t => t.Status == WebAI.Models.TaskStatus.InProgress),
                TaskReports = new List<TaskProgressReport>()
            };

            foreach (TaskModel task in tasks)
            {
                TaskProgressReport taskReport = GenerateTaskProgressReport(task);
                report.TaskReports.Add(taskReport);
            }

            report.CalculateProgress();
            return report;
        }

        private TaskProgressReport GenerateTaskProgressReport(TaskModel task)
        {
            TaskProgressReport report = new TaskProgressReport
            {
                TaskId = task.TaskId,
                Title = task.Title,
                Status = task.Status,
                Priority = task.Priority,
                StartDate = task.StartedAt,
                CompletionDate = task.CompletedAt,
                AssignedAgentId = task.AssignedAgentId,
                BlockedReason = task.BlockedReason,
                SubTasks = new List<TaskProgressReport>()
            };

            if (task.SubTasks != null && task.SubTasks.Any())
            {
                foreach (TaskModel subTask in task.SubTasks)
                {
                    TaskProgressReport subTaskReport = GenerateTaskProgressReport(subTask);
                    report.SubTasks.Add(subTaskReport);
                }
            }

            if (task.AgentMetrics != null && task.AgentMetrics.Any())
            {
                report.AgentPerformance = task.AgentMetrics
                    .OrderByDescending(m => m.CompletionTime)
                    .FirstOrDefault();
            }

            report.CalculateProgress();
            return report;
        }

        public class ProjectProgressReport
        {
            public int ProjectId { get; set; }
            public string ProjectName { get; set; }
            public ProjectStatus Status { get; set; }
            public DateTime StartDate { get; set; }
            public int TotalMilestones { get; set; }
            public int CompletedMilestones { get; set; }
            public int TotalTasks { get; set; }
            public int CompletedTasks { get; set; }
            public int BlockedTasks { get; set; }
            public int InProgressTasks { get; set; }
            public double OverallProgress { get; set; }
            public List<MilestoneProgressReport> MilestoneReports { get; set; }

            public void CalculateProgress()
            {
                if (TotalTasks == 0)
                {
                    OverallProgress = 0;
                    return;
                }

                double taskProgress = ((double)CompletedTasks / TotalTasks) * 100;
                double milestoneProgress = ((double)CompletedMilestones / TotalMilestones) * 100;
                OverallProgress = Math.Round((taskProgress + milestoneProgress) / 2, 2);
            }
        }

        public class MilestoneProgressReport
        {
            public int MilestoneId { get; set; }
            public string Title { get; set; }
            public MilestoneStatus Status { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime? CompletionDate { get; set; }
            public int TotalTasks { get; set; }
            public int CompletedTasks { get; set; }
            public int BlockedTasks { get; set; }
            public int InProgressTasks { get; set; }
            public double Progress { get; set; }
            public List<TaskProgressReport> TaskReports { get; set; }

            public void CalculateProgress()
            {
                if (TotalTasks == 0)
                {
                    Progress = 0;
                    return;
                }

                Progress = Math.Round(((double)CompletedTasks / TotalTasks) * 100, 2);
            }
        }

        public class TaskProgressReport
        {
            public int TaskId { get; set; }
            public string Title { get; set; }
            public WebAI.Models.TaskStatus Status { get; set; }
            public TaskPriority Priority { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? CompletionDate { get; set; }
            public string AssignedAgentId { get; set; }
            public string BlockedReason { get; set; }
            public double Progress { get; set; }
            public List<TaskProgressReport> SubTasks { get; set; }
            public AgentMetricsModel AgentPerformance { get; set; }

            public void CalculateProgress()
            {
                if (Status == WebAI.Models.TaskStatus.Completed)
                {
                    Progress = 100;
                    return;
                }

                if (!SubTasks.Any())
                {
                    Progress = Status == WebAI.Models.TaskStatus.InProgress ? 50 : 0;
                    return;
                }

                double completedSubTasks = SubTasks.Count(st => st.Status == WebAI.Models.TaskStatus.Completed);
                double inProgressSubTasks = SubTasks.Count(st => st.Status == WebAI.Models.TaskStatus.InProgress);

                Progress = Math.Round(
                    ((completedSubTasks * 100) + (inProgressSubTasks * 50)) / SubTasks.Count,
                    2
                );
            }
        }
    }
}