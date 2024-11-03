using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebAI.Models;
using WebAI.Services.Database;
using static WebAI.Models.StateEnums;
using TaskStatus = WebAI.Models.TaskStatus;

namespace WebAI.Services.Managers
{
    public class AgentManager
    {
        private readonly AgentDataService _agentDataService;
        private readonly TaskDataService _taskDataService;
        private readonly MessageDataService _messageDataService;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly Dictionary<string, AgentState> _agentStates;

        public AgentManager(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _agentDataService = new AgentDataService(connectionString);
            _taskDataService = new TaskDataService(connectionString);
            _messageDataService = new MessageDataService(connectionString);
            _agentStates = new Dictionary<string, AgentState>();
        }

        public string InitializeAgent(string agentRole)
        {
            try
            {
                string agentId = GenerateAgentId(agentRole);
                _agentStates[agentId] = AgentState.Available;

                // Log agent initialization
                MessageModel initMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Agent {0} initialized with role: {1}", agentId, agentRole))
                    .WithType(MessageType.SystemPrompt)
                    .Build();

                _messageDataService.SaveMessage(initMessage);

                return agentId;
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error initializing agent with role: {0}", agentRole), ex);
                throw;
            }
        }

        public void AssignTaskToAgent(string agentId, int taskId)
        {
            try
            {
                ValidateAgentAvailability(agentId);

                TaskModel task = _taskDataService.GetTask(taskId);
                if (task == null)
                    throw new ArgumentException(string.Format("Task with ID {0} not found", taskId));

                // Update agent state
                _agentStates[agentId] = AgentState.Processing;

                // Update task assignment
                task.AssignedAgentId = agentId;
                task.Status = TaskStatus.InProgress;
                _taskDataService.UpdateTask(task);

                // Create metrics entry for tracking
                AgentMetricsModel metrics = new AgentMetricsModel
                {
                    AgentId = agentId,
                    TaskId = taskId,
                    StartTime = DateTime.UtcNow
                };
                metrics.StartTask();

                _agentDataService.SaveAgentMetrics(metrics);

                // Log assignment
                MessageModel assignMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Agent {0} assigned to task: {1}", agentId, task.Title))
                    .WithTask(taskId)
                    .WithType(MessageType.StatusUpdate)
                    .Build();

                _messageDataService.SaveMessage(assignMessage);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error assigning task {0} to agent {1}", taskId, agentId), ex);
                throw;
            }
        }

        public void UpdateAgentStatus(string agentId, AgentState newState, string reason = null)
        {
            try
            {
                if (!_agentStates.ContainsKey(agentId))
                    throw new ArgumentException(string.Format("Agent {0} not found", agentId));

                AgentState oldState = _agentStates[agentId];
                _agentStates[agentId] = newState;

                // Log state change
                MessageModel stateMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Agent {0} state changed from {1} to {2}", agentId, oldState, newState) +
                    (reason != null ? string.Format(" Reason: {0}", reason) : ""))
                    .WithType(MessageType.StatusUpdate)
                    .Build();

                _messageDataService.SaveMessage(stateMessage);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error updating status for agent {0}", agentId), ex);
                throw;
            }
        }

        public void CompleteTask(string agentId, int taskId, bool success, string notes = null)
        {
            try
            {
                TaskModel task = _taskDataService.GetTask(taskId);
                if (task == null)
                    throw new ArgumentException(string.Format("Task with ID {0} not found", taskId));

                if (task.AssignedAgentId != agentId)
                    throw new InvalidOperationException(
                        string.Format("Task {0} is not assigned to agent {1}", taskId, agentId));

                // Update task status
                task.Status = success ? TaskStatus.Completed : TaskStatus.Failed;
                task.CompletedAt = DateTime.UtcNow;
                _taskDataService.UpdateTask(task);

                // Update agent state
                _agentStates[agentId] = AgentState.Available;

                // Update metrics
                AgentMetricsModel metrics = new AgentMetricsModel
                {
                    AgentId = agentId,
                    TaskId = taskId,
                    CompletionTime = DateTime.UtcNow,
                    Notes = notes
                };
                metrics.CompleteTask(success, notes);

                _agentDataService.SaveAgentMetrics(metrics);

                // Log completion
                MessageModel completionMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Agent {0} {1} task: {2}",
                        agentId, success ? "completed" : "failed", task.Title) +
                    (notes != null ? string.Format(" Notes: {0}", notes) : ""))
                    .WithTask(taskId)
                    .WithType(MessageType.StatusUpdate)
                    .Build();

                _messageDataService.SaveMessage(completionMessage);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error completing task {0} for agent {1}", taskId, agentId), ex);
                throw;
            }
        }

        public void RecordError(string agentId, int taskId, string error)
        {
            try
            {
                AgentMetricsModel metrics = new AgentMetricsModel
                {
                    AgentId = agentId,
                    TaskId = taskId,
                    Status = "Error"
                };
                metrics.AddError(error);

                _agentDataService.SaveAgentMetrics(metrics);

                // Log error
                MessageModel errorMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Agent {0} encountered error: {1}", agentId, error))
                    .WithTask(taskId)
                    .WithType(MessageType.ErrorMessage)
                    .Build();

                _messageDataService.SaveMessage(errorMessage);

                // Update agent state
                UpdateAgentStatus(agentId, AgentState.Error, error);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error recording error for agent {0}, task {1}", agentId, taskId), ex);
                throw;
            }
        }

        public List<AgentMetricsModel> GetAgentHistory(string agentId, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                return _agentDataService.GetAgentMetrics(agentId, startDate, endDate);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting history for agent {0}", agentId), ex);
                throw;
            }
        }

        public AgentMetricsModel GetAgentCurrentMetrics(string agentId)
        {
            try
            {
                return _agentDataService.GetLatestMetrics(agentId);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting current metrics for agent {0}", agentId), ex);
                throw;
            }
        }

        public AgentState GetAgentState(string agentId)
        {
            if (!_agentStates.ContainsKey(agentId))
                throw new ArgumentException(string.Format("Agent {0} not found", agentId));

            return _agentStates[agentId];
        }

        private string GenerateAgentId(string role)
        {
            return string.Format("{0}_{1}", role, Guid.NewGuid().ToString("N").Substring(0, 8));
        }

        private void ValidateAgentAvailability(string agentId)
        {
            if (!_agentStates.ContainsKey(agentId))
                throw new ArgumentException(string.Format("Agent {0} not found", agentId));

            if (_agentStates[agentId] != AgentState.Available)
                throw new InvalidOperationException(
                    string.Format("Agent {0} is not available. Current state: {1}",
                        agentId, _agentStates[agentId]));
        }

        public void Shutdown(string agentId)
        {
            try
            {
                if (!_agentStates.ContainsKey(agentId))
                    return;

                // Log shutdown
                MessageModel shutdownMessage = MessageBuilder.CreateSystemMessage(
                    string.Format("Agent {0} shutting down", agentId))
                    .WithType(MessageType.SystemPrompt)
                    .Build();

                _messageDataService.SaveMessage(shutdownMessage);

                _agentStates.Remove(agentId);
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error shutting down agent {0}", agentId), ex);
                throw;
            }
        }

        public void RecordAgentMetrics(int taskId, AgentMetricsModel metrics)
        {
            try
            {
                if (metrics == null)
                    throw new ArgumentNullException("metrics");

                // Save the metrics
                _agentDataService.SaveAgentMetrics(metrics);

                // Log metrics update
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
                log.Error(string.Format("Error recording agent metrics for task {0}", taskId), ex);
                throw;
            }
        }

    }
}