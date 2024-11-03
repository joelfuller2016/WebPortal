using System;
using System.Collections.Generic;
using System.Linq;

namespace WebAI.Models
{
    public class ProjectModel
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public ProjectStatus Status { get; set; }

        // Navigation properties
        public List<MilestoneModel> Milestones { get; private set; }
        public List<MessageModel> Messages { get; private set; }

        public ProjectModel()
        {
            Milestones = new List<MilestoneModel>();
            Messages = new List<MessageModel>();
            CreatedAt = DateTime.UtcNow;
            Status = ProjectStatus.Active;
        }

        public void AddMilestone(MilestoneModel milestone)
        {
            if (milestone == null)
                throw new ArgumentNullException("milestone");

            Milestones.Add(milestone);
        }

        public void AddMessage(MessageModel message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            Messages.Add(message);
        }

        public void UpdateStatus(ProjectStatus newStatus)
        {
            Status = newStatus;
        }
    }

    public enum ProjectStatus
    {
        Active,
        OnHold,
        Completed,
        Cancelled
    }

    public class ProjectDTO
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; }
        public int MilestoneCount { get; set; }
        public int CompletedMilestoneCount { get; set; }
        public double ProgressPercentage { get; set; }

        public static ProjectDTO FromModel(ProjectModel model)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            return new ProjectDTO
            {
                ProjectId = model.ProjectId,
                ProjectName = model.ProjectName,
                Description = model.Description,
                CreatedAt = model.CreatedAt,
                Status = model.Status.ToString(),
                MilestoneCount = model.Milestones != null ? model.Milestones.Count : 0,
                CompletedMilestoneCount = model.Milestones != null ?
                    model.Milestones.Count(m => m.Status == MilestoneStatus.Completed) : 0,
                ProgressPercentage = CalculateProgress(model)
            };
        }

        private static double CalculateProgress(ProjectModel model)
        {
            if (model.Milestones == null || model.Milestones.Count == 0)
                return 0;

            int totalMilestones = model.Milestones.Count;
            int completedMilestones = model.Milestones.Count(m => m.Status == MilestoneStatus.Completed);
            return Math.Round((double)completedMilestones / totalMilestones * 100, 2);
        }
    }
}