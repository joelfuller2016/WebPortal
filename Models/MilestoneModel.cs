using System;
using System.Collections.Generic;
using System.Linq;

namespace WebAI.Models
{
    public class MilestoneModel
    {
        public int MilestoneId { get; set; }
        public int ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string SuccessCriteria { get; set; }
        public MilestoneStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public ProjectModel Project { get; set; }
        public List<TaskModel> Tasks { get; private set; }
        public List<MessageModel> Messages { get; private set; }

        public MilestoneModel()
        {
            Tasks = new List<TaskModel>();
            Messages = new List<MessageModel>();
            CreatedAt = DateTime.UtcNow;
            Status = MilestoneStatus.Pending;
        }

        public void AddTask(TaskModel task)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            Tasks.Add(task);
            UpdateStatus();
        }

        public void AddMessage(MessageModel message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            Messages.Add(message);
        }

        public void UpdateStatus()
        {
            if (Tasks == null || Tasks.Count == 0)
            {
                Status = MilestoneStatus.Pending;
                return;
            }

            bool allTasksComplete = Tasks.All(t => t.Status == TaskStatus.Completed);
            bool anyTasksInProgress = Tasks.Any(t => t.Status == TaskStatus.InProgress);
            bool anyTasksBlocked = Tasks.Any(t => t.Status == TaskStatus.Blocked);

            if (allTasksComplete)
            {
                Status = MilestoneStatus.Completed;
                CompletedAt = DateTime.UtcNow;
            }
            else if (anyTasksBlocked)
            {
                Status = MilestoneStatus.Blocked;
            }
            else if (anyTasksInProgress)
            {
                Status = MilestoneStatus.InProgress;
            }
            else
            {
                Status = MilestoneStatus.Pending;
            }
        }
    }

    public enum MilestoneStatus
    {
        Pending,
        InProgress,
        Blocked,
        Completed,
        Cancelled
    }

    public class MilestoneDTO
    {
        public int MilestoneId { get; set; }
        public int ProjectId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string SuccessCriteria { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public double ProgressPercentage { get; set; }
        public List<string> BlockingIssues { get; set; }

        public static MilestoneDTO FromModel(MilestoneModel model)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            return new MilestoneDTO
            {
                MilestoneId = model.MilestoneId,
                ProjectId = model.ProjectId,
                Title = model.Title,
                Description = model.Description,
                SuccessCriteria = model.SuccessCriteria,
                Status = model.Status.ToString(),
                CreatedAt = model.CreatedAt,
                CompletedAt = model.CompletedAt,
                TaskCount = model.Tasks != null ? model.Tasks.Count : 0,
                CompletedTaskCount = model.Tasks != null ?
                    model.Tasks.Count(t => t.Status == TaskStatus.Completed) : 0,
                ProgressPercentage = CalculateProgress(model),
                BlockingIssues = GetBlockingIssues(model)
            };
        }

        private static double CalculateProgress(MilestoneModel model)
        {
            if (model.Tasks == null || model.Tasks.Count == 0)
                return 0;

            int totalTasks = model.Tasks.Count;
            int completedTasks = model.Tasks.Count(t => t.Status == TaskStatus.Completed);
            return Math.Round((double)completedTasks / totalTasks * 100, 2);
        }

        private static List<string> GetBlockingIssues(MilestoneModel model)
        {
            List<string> issues = new List<string>();

            if (model.Tasks != null)
            {
                foreach (TaskModel task in model.Tasks)
                {
                    if (task.Status == TaskStatus.Blocked)
                    {
                        issues.Add(string.Format("Task '{0}' is blocked: {1}",
                            task.Title,
                            task.BlockedReason ?? "No reason specified"));
                    }
                }
            }

            return issues;
        }
    }
}