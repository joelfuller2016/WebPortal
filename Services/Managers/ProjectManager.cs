using System;
using System.Collections.Generic;
using System.Linq;
using WebAI.Models;
using WebAI.Services.Database;

namespace WebAI.Services.Managers
{
    public class ProjectManager
    {
        private readonly ProjectDataService _projectDataService;
        private readonly MilestoneDataService _milestoneDataService;
        private readonly MessageDataService _messageDataService;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ProjectManager(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _projectDataService = new ProjectDataService(connectionString);
            _milestoneDataService = new MilestoneDataService(connectionString);
            _messageDataService = new MessageDataService(connectionString);
        }

        public ProjectDTO CreateProject(string projectName, string description)
        {
            try
            {
                // Validate project name
                if (string.IsNullOrEmpty(projectName))
                    throw new ArgumentException("Project name cannot be empty");

                // Create project model
                ProjectModel project = new ProjectModel
                {
                    ProjectName = projectName,
                    Description = description,
                    CreatedAt = DateTime.UtcNow,
                    Status = ProjectStatus.Active
                };

                // Save project
                int projectId = _projectDataService.CreateProject(project);
                project.ProjectId = projectId;

                // Create initial system message
                MessageModel systemMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Project '{0}' created.", projectName))
                    .WithProject(projectId)
                    .WithType(MessageType.SystemPrompt)
                    .Build();

                _messageDataService.SaveMessage(systemMessage);

                // Return DTO
                return ProjectDTO.FromModel(project);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error creating project: {0}", projectName), ex);
                throw;
            }
        }

        public ProjectDTO GetProject(int projectId)
        {
            try
            {
                ProjectModel project = _projectDataService.GetProject(projectId);
                if (project == null)
                    return null;

                // Load milestones
                List<MilestoneModel> milestones = _milestoneDataService.GetMilestonesByProject(projectId);
                project.Milestones.AddRange(milestones);

                // Load messages
                List<MessageModel> messages = _messageDataService.GetMessageThread(projectId: projectId);
                project.Messages.AddRange(messages);

                return ProjectDTO.FromModel(project);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting project with ID: {0}", projectId), ex);
                throw;
            }
        }

        public List<ProjectDTO> GetAllProjects()
        {
            try
            {
                List<ProjectModel> projects = _projectDataService.GetAllProjects();
                return projects.Select(p => ProjectDTO.FromModel(p)).ToList();
            }
            catch (Exception ex)
            {
                log.Error("Error getting all projects", ex);
                throw;
            }
        }

        public ProjectDTO UpdateProjectStatus(int projectId, ProjectStatus newStatus)
        {
            try
            {
                ProjectModel project = _projectDataService.GetProject(projectId);
                if (project == null)
                    throw new ArgumentException(string.Format("Project with ID {0} not found", projectId));

                ProjectStatus oldStatus = project.Status;
                project.Status = newStatus;

                _projectDataService.UpdateProject(project);

                // Log status change
                MessageModel statusMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Project status changed from {0} to {1}", oldStatus, newStatus))
                    .WithProject(projectId)
                    .WithType(MessageType.StatusUpdate)
                    .Build();

                _messageDataService.SaveMessage(statusMessage);

                return ProjectDTO.FromModel(project);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error updating status for project ID: {0}", projectId), ex);
                throw;
            }
        }

        public ProjectDTO UpdateProject(int projectId, string projectName, string description)
        {
            try
            {
                ProjectModel project = _projectDataService.GetProject(projectId);
                if (project == null)
                    throw new ArgumentException(string.Format("Project with ID {0} not found", projectId));

                string oldName = project.ProjectName;
                string oldDescription = project.Description;

                // Update properties
                if (!string.IsNullOrEmpty(projectName))
                    project.ProjectName = projectName;
                if (description != null) // Allow empty description
                    project.Description = description;

                _projectDataService.UpdateProject(project);

                // Log changes
                if (project.ProjectName != oldName || project.Description != oldDescription)
                {
                    MessageModel updateMessage = MessageBuilder.CreateSystemMessage(
                        "Project details updated: " +
                        (project.ProjectName != oldName ?
                            string.Format("Name changed from '{0}' to '{1}'. ", oldName, project.ProjectName) : "") +
                        (project.Description != oldDescription ?
                            "Description updated." : ""))
                        .WithProject(projectId)
                        .WithType(MessageType.StatusUpdate)
                        .Build();

                    _messageDataService.SaveMessage(updateMessage);
                }

                return ProjectDTO.FromModel(project);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error updating project ID: {0}", projectId), ex);
                throw;
            }
        }

        public bool DeleteProject(int projectId)
        {
            try
            {
                ProjectModel project = _projectDataService.GetProject(projectId);
                if (project == null)
                    return false;

                return _projectDataService.DeleteProject(projectId);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error deleting project ID: {0}", projectId), ex);
                throw;
            }
        }

        public ProjectDTO ArchiveProject(int projectId)
        {
            try
            {
                return UpdateProjectStatus(projectId, ProjectStatus.Completed);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error archiving project ID: {0}", projectId), ex);
                throw;
            }
        }

        public List<MessageModel> GetProjectHistory(int projectId)
        {
            try
            {
                return _messageDataService.GetMessageThread(projectId: projectId);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting history for project ID: {0}", projectId), ex);
                throw;
            }
        }

        public double GetProjectProgress(int projectId)
        {
            try
            {
                ProjectModel project = _projectDataService.GetProject(projectId);
                if (project == null)
                    throw new ArgumentException(string.Format("Project with ID {0} not found", projectId));

                List<MilestoneModel> milestones = _milestoneDataService.GetMilestonesByProject(projectId);
                if (!milestones.Any())
                    return 0;

                int completedMilestones = milestones.Count(m => m.Status == MilestoneStatus.Completed);
                return Math.Round((double)completedMilestones / milestones.Count * 100, 2);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error calculating progress for project ID: {0}", projectId), ex);
                throw;
            }
        }
    }
}