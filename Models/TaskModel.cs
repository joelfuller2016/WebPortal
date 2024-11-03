using System;
using System.Collections.Generic;
using System.Linq;

namespace WebAI.Models
{
    public class TaskModel
    {
        public int TaskId { get; set; }
        public int MilestoneId { get; set; }
        public int? ParentTaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public TaskPriority Priority { get; set; }
        public TaskStatus Status { get; set; }
        public string AssignedAgentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string BlockedReason { get; set; }

        // Navigation properties
        public MilestoneModel Milestone { get; set; }
        public TaskModel ParentTask { get; set; }
        public List<TaskModel> SubTasks { get;  set; }
        public List<TaskDependency> Dependencies { get; set; }
        public List<MessageModel> Messages { get;  set; }
        public List<AgentMetricsModel> AgentMetrics { get;  set; }

        public TaskModel()
        {
            SubTasks = new List<TaskModel>();
            Dependencies = new List<TaskDependency>();
            Messages = new List<MessageModel>();
            AgentMetrics = new List<AgentMetricsModel>();
            CreatedAt = DateTime.UtcNow;
            Status = TaskStatus.Pending;
            Priority = TaskPriority.Medium;
        }

        public bool CanStart()
        {
            if (Status != TaskStatus.Pending)
                return false;

            if (Dependencies == null || Dependencies.Count == 0)
                return true;

            return !Dependencies.Any(d => d.DependsOnTask.Status != TaskStatus.Completed);
        }

        public void AddSubTask(TaskModel task)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            SubTasks.Add(task);
            UpdateParentStatus();
        }

        public void AddDependency(TaskDependency dependency)
        {
            if (dependency == null)
                throw new ArgumentNullException("dependency");

            Dependencies.Add(dependency);
        }

        public void UpdateStatus(TaskStatus newStatus)
        {
            TaskStatus oldStatus = Status;
            Status = newStatus;

            switch (newStatus)
            {
                case TaskStatus.InProgress:
                    if (!StartedAt.HasValue)
                        StartedAt = DateTime.UtcNow;
                    BlockedReason = null;
                    break;

                case TaskStatus.Completed:
                    CompletedAt = DateTime.UtcNow;
                    BlockedReason = null;
                    break;

                case TaskStatus.Blocked:
                    if (string.IsNullOrEmpty(BlockedReason))
                        BlockedReason = "Task marked as blocked";
                    break;
            }

            if (ParentTask != null)
            {
                ParentTask.UpdateParentStatus();
            }
        }

        private void UpdateParentStatus()
        {
            if (SubTasks == null || SubTasks.Count == 0)
                return;

            bool allComplete = SubTasks.All(t => t.Status == TaskStatus.Completed);
            bool anyBlocked = SubTasks.Any(t => t.Status == TaskStatus.Blocked);
            bool anyInProgress = SubTasks.Any(t => t.Status == TaskStatus.InProgress);

            if (allComplete)
                UpdateStatus(TaskStatus.Completed);
            else if (anyBlocked)
                UpdateStatus(TaskStatus.Blocked);
            else if (anyInProgress)
                UpdateStatus(TaskStatus.InProgress);
        }
    }

    public enum TaskStatus
    {
        Pending,
        InProgress,
        Blocked,
        Completed,
        Cancelled,
        Failed
    }

    public enum TaskPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class TaskDependency
    {
        public int DependencyId { get; set; }
        public int TaskId { get; set; }
        public int DependsOnTaskId { get; set; }
        public DateTime CreatedAt { get; set; }

        public TaskModel Task { get; set; }
        public TaskModel DependsOnTask { get; set; }

        public TaskDependency()
        {
            CreatedAt = DateTime.UtcNow;
        }
    }

    public class TaskDTO
    {
        public int TaskId { get; set; }
        public int MilestoneId { get; set; }
        public int? ParentTaskId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string AssignedAgentId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string BlockedReason { get; set; }
        public List<TaskDTO> SubTasks { get; set; }
        public List<string> Dependencies { get; set; }
        public double ProgressPercentage { get; set; }
        public AgentMetricsSummary AgentMetrics { get; set; }

        public static TaskDTO FromModel(TaskModel model)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            TaskDTO dto = new TaskDTO();
            dto.TaskId = model.TaskId;
            dto.MilestoneId = model.MilestoneId;
            dto.ParentTaskId = model.ParentTaskId;
            dto.Title = model.Title;
            dto.Description = model.Description;
            dto.Priority = model.Priority.ToString();
            dto.Status = model.Status.ToString();
            dto.AssignedAgentId = model.AssignedAgentId;
            dto.CreatedAt = model.CreatedAt;
            dto.StartedAt = model.StartedAt;
            dto.CompletedAt = model.CompletedAt;
            dto.BlockedReason = model.BlockedReason;
            dto.ProgressPercentage = CalculateProgress(model);

            // Handle SubTasks
            if (model.SubTasks != null)
            {
                dto.SubTasks = new List<TaskDTO>();
                foreach (TaskModel subTask in model.SubTasks)
                {
                    dto.SubTasks.Add(FromModel(subTask));
                }
            }

            // Handle Dependencies
            if (model.Dependencies != null)
            {
                dto.Dependencies = new List<string>();
                foreach (TaskDependency dep in model.Dependencies)
                {
                    if (dep.DependsOnTask != null && dep.DependsOnTask.Title != null)
                    {
                        dto.Dependencies.Add(dep.DependsOnTask.Title);
                    }
                }
            }

            // Handle Agent Metrics
            if (model.AgentMetrics != null)
            {
                dto.AgentMetrics = AgentMetricsSummary.FromMetrics(model.AgentMetrics);
            }

            return dto;
        }

        private static double CalculateProgress(TaskModel model)
        {
            if (model.Status == TaskStatus.Completed)
                return 100;

            if (model.SubTasks == null || !model.SubTasks.Any())
                return model.Status == TaskStatus.InProgress ? 50 : 0;

            int completedTasks = model.SubTasks.Count(t => t.Status == TaskStatus.Completed);
            int inProgressTasks = model.SubTasks.Count(t => t.Status == TaskStatus.InProgress);

            double progress = ((completedTasks * 100) + (inProgressTasks * 50)) /
                            (double)model.SubTasks.Count;

            return Math.Round(progress, 2);
        }
    }

    public class AgentMetricsSummary
    {
        public int TotalAttempts { get; set; }
        public int SuccessfulAttempts { get; set; }
        public double SuccessRate { get; set; }
        public TimeSpan AverageCompletionTime { get; set; }

        public static AgentMetricsSummary FromMetrics(List<AgentMetricsModel> metrics)
        {
            if (metrics == null || metrics.Count == 0)
                return new AgentMetricsSummary();

            int successful = metrics.Count(m => string.Equals(m.Status, "Completed", StringComparison.OrdinalIgnoreCase));

            List<TimeSpan> completionTimes = new List<TimeSpan>();
            foreach (AgentMetricsModel metric in metrics)
            {
                if (metric.StartTime.HasValue && metric.CompletionTime.HasValue)
                {
                    completionTimes.Add(metric.CompletionTime.Value - metric.StartTime.Value);
                }
            }

            AgentMetricsSummary summary = new AgentMetricsSummary();
            summary.TotalAttempts = metrics.Count;
            summary.SuccessfulAttempts = successful;
            summary.SuccessRate = Math.Round((double)successful / metrics.Count * 100, 2);

            if (completionTimes.Count > 0)
            {
                long averageTicks = (long)completionTimes.Average(t => t.Ticks);
                summary.AverageCompletionTime = TimeSpan.FromTicks(averageTicks);
            }
            else
            {
                summary.AverageCompletionTime = TimeSpan.Zero;
            }

            return summary;
        }
    }
}