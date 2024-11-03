using System;
using System.Collections.Generic;
using System.Linq;
using WebAI.Models;
using WebAI.Services.Database;

namespace WebAI.Services.Managers
{
    public class TaskManager
    {
        private readonly TaskDataService _taskDataService;
        private readonly MilestoneDataService _milestoneDataService;
        private readonly MessageDataService _messageDataService;
        private readonly AgentDataService _agentDataService;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public TaskManager(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _taskDataService = new TaskDataService(connectionString);
            _milestoneDataService = new MilestoneDataService(connectionString);
            _messageDataService = new MessageDataService(connectionString);
            _agentDataService = new AgentDataService(connectionString);
        }

        public TaskDTO CreateTask(int milestoneId, string title, string description, TaskPriority priority, int? parentTaskId = null)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrEmpty(title))
                    throw new ArgumentException("Task title cannot be empty");

                // Create task model
                TaskModel task = new TaskModel
                {
                    MilestoneId = milestoneId,
                    ParentTaskId = parentTaskId,
                    Title = title,
                    Description = description,
                    Priority = priority,
                    Status = TaskStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                // Save task
                int taskId = _taskDataService.CreateTask(task);
                task.TaskId = taskId;

                // Create system message
                MessageModel systemMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Task '{0}' created with {1} priority.", title, priority))
                    .WithTask(taskId)
                    .WithType(MessageType.SystemPrompt)
                    .Build();

                _messageDataService.SaveMessage(systemMessage);

                return TaskDTO.FromModel(task);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error creating task: {0}", title), ex);
                throw;
            }
        }

        public TaskDTO GetTask(int taskId)
        {
            try
            {
                TaskModel task = _taskDataService.GetTask(taskId);
                if (task == null)
                    return null;

                return TaskDTO.FromModel(task);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting task with ID: {0}", taskId), ex);
                throw;
            }
        }

        public TaskDTO AssignTask(int taskId, string agentId)
        {
            try
            {
                TaskModel task = _taskDataService.GetTask(taskId);
                if (task == null)
                    throw new ArgumentException(string.Format("Task with ID {0} not found", taskId));

                string previousAgent = task.AssignedAgentId;
                task.AssignedAgentId = agentId;

                _taskDataService.UpdateTask(task);

                // Log assignment
                MessageModel assignmentMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Task assigned to agent {0}", agentId) +
                    (previousAgent != null ? string.Format(" (previously: {0})", previousAgent) : ""))
                    .WithTask(taskId)
                    .WithType(MessageType.StatusUpdate)
                    .Build();

                _messageDataService.SaveMessage(assignmentMessage);

                return TaskDTO.FromModel(task);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error assigning task {0} to agent {1}", taskId, agentId), ex);
                throw;
            }
        }

        public TaskDTO UpdateTaskStatus(int taskId, TaskStatus newStatus, string reason = null)
        {
            try
            {
                TaskModel task = _taskDataService.GetTask(taskId);
                if (task == null)
                    throw new ArgumentException(string.Format("Task with ID {0} not found", taskId));

                if (!CanTransitionToStatus(task.Status, newStatus))
                    throw new InvalidOperationException(
                        string.Format("Invalid status transition from {0} to {1}", task.Status, newStatus));

                TaskStatus oldStatus = task.Status;
                task.UpdateStatus(newStatus);

                if (newStatus == TaskStatus.Blocked)
                    task.BlockedReason = reason ?? "Task blocked without specific reason";

                _taskDataService.UpdateTask(task);

                // Log status change
                MessageModel statusMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Task status changed from {0} to {1}", oldStatus, newStatus) +
                    (reason != null ? string.Format(" Reason: {0}", reason) : ""))
                    .WithTask(taskId)
                    .WithType(MessageType.StatusUpdate)
                    .Build();

                _messageDataService.SaveMessage(statusMessage);

                return TaskDTO.FromModel(task);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error updating status for task ID: {0}", taskId), ex);
                throw;
            }
        }

        public TaskDTO AddDependency(int taskId, int dependsOnTaskId)
        {
            try
            {
                TaskModel task = _taskDataService.GetTask(taskId);
                if (task == null)
                    throw new ArgumentException(string.Format("Task with ID {0} not found", taskId));

                TaskModel dependsOnTask = _taskDataService.GetTask(dependsOnTaskId);
                if (dependsOnTask == null)
                    throw new ArgumentException(string.Format("Dependency task with ID {0} not found", dependsOnTaskId));

                // Check for circular dependencies
                if (WouldCreateCircularDependency(taskId, dependsOnTaskId))
                    throw new InvalidOperationException("Adding this dependency would create a circular reference");

                TaskDependency dependency = new TaskDependency
                {
                    TaskId = taskId,
                    DependsOnTaskId = dependsOnTaskId,
                    CreatedAt = DateTime.UtcNow
                };

                if (task.Dependencies == null)
                    task.Dependencies = new List<TaskDependency>();

                task.Dependencies.Add(dependency);
                _taskDataService.UpdateTask(task);

                // Log dependency
                MessageModel dependencyMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Added dependency on task '{0}'", dependsOnTask.Title))
                    .WithTask(taskId)
                    .WithType(MessageType.StatusUpdate)
                    .Build();

                _messageDataService.SaveMessage(dependencyMessage);

                return TaskDTO.FromModel(task);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error adding dependency between tasks {0} and {1}", taskId, dependsOnTaskId), ex);
                throw;
            }
        }

        public TaskDTO AddSubTask(int parentTaskId, string title, string description, TaskPriority priority)
        {
            try
            {
                TaskModel parentTask = _taskDataService.GetTask(parentTaskId);
                if (parentTask == null)
                    throw new ArgumentException(string.Format("Parent task with ID {0} not found", parentTaskId));

                TaskDTO subTask = CreateTask(parentTask.MilestoneId, title, description, priority, parentTaskId);

                // Log subtask creation
                MessageModel subtaskMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Added subtask '{0}' to task '{1}'", title, parentTask.Title))
                    .WithTask(parentTaskId)
                    .WithType(MessageType.StatusUpdate)
                    .Build();

                _messageDataService.SaveMessage(subtaskMessage);

                return GetTask(subTask.TaskId);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error adding subtask to parent task ID: {0}", parentTaskId), ex);
                throw;
            }
        }

        public List<MessageModel> GetTaskHistory(int taskId)
        {
            try
            {
                return _messageDataService.GetMessageThread(taskId: taskId);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting history for task ID: {0}", taskId), ex);
                throw;
            }
        }

        public void RecordAgentMetrics(int taskId, AgentMetricsModel metrics)
        {
            try
            {
                TaskModel task = _taskDataService.GetTask(taskId);
                if (task == null)
                    throw new ArgumentException(string.Format("Task with ID {0} not found", taskId));

                _agentDataService.SaveAgentMetrics(metrics);

                // Log metrics summary
                MessageModel metricsMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Agent metrics recorded - Success Rate: {0}%, Status: {1}",
                        metrics.SuccessRate, metrics.Status))
                    .WithTask(taskId)
                    .WithType(MessageType.MetricUpdate)
                    .Build();

                _messageDataService.SaveMessage(metricsMessage);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error recording agent metrics for task ID: {0}", taskId), ex);
                throw;
            }
        }

        private bool CanTransitionToStatus(TaskStatus currentStatus, TaskStatus newStatus)
        {
            switch (currentStatus)
            {
                case TaskStatus.Pending:
                    return newStatus == TaskStatus.InProgress ||
                           newStatus == TaskStatus.Cancelled;

                case TaskStatus.InProgress:
                    return newStatus == TaskStatus.Completed ||
                           newStatus == TaskStatus.Blocked ||
                           newStatus == TaskStatus.Failed;

                case TaskStatus.Blocked:
                    return newStatus == TaskStatus.InProgress ||
                           newStatus == TaskStatus.Cancelled;

                case TaskStatus.Completed:
                    return newStatus == TaskStatus.InProgress; // Allow reopening

                case TaskStatus.Failed:
                    return newStatus == TaskStatus.InProgress ||
                           newStatus == TaskStatus.Cancelled;

                case TaskStatus.Cancelled:
                    return newStatus == TaskStatus.Pending; // Allow restarting

                default:
                    return false;
            }
        }

        private bool WouldCreateCircularDependency(int taskId, int dependsOnTaskId)
        {
            HashSet<int> visited = new HashSet<int>();
            return CheckCircularDependency(taskId, dependsOnTaskId, visited);
        }

        private bool CheckCircularDependency(int taskId, int dependsOnTaskId, HashSet<int> visited)
        {
            if (taskId == dependsOnTaskId)
                return true;

            if (!visited.Add(taskId))
                return false;

            TaskModel task = _taskDataService.GetTask(dependsOnTaskId);
            if (task?.Dependencies == null || !task.Dependencies.Any())
                return false;

            foreach (TaskDependency dependency in task.Dependencies)
            {
                if (CheckCircularDependency(taskId, dependency.DependsOnTaskId, visited))
                    return true;
            }

            return false;
        }
    }
}