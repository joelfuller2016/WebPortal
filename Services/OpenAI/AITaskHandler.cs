using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebAI.Models;
using WebAI.Services.Database;
using WebAI.Services.Managers;

namespace WebAI.Services.OpenAI
{
    public class AITaskHandler
    {
        private readonly OpenAIService _openAIService;
        private readonly TaskManager _taskManager;
        private readonly AgentManager _agentManager;
        private readonly MessageDataService _messageDataService;
        private readonly PromptBuilder _promptBuilder;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public AITaskHandler(string connectionString, string openAIApiKey)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");
            if (string.IsNullOrEmpty(openAIApiKey))
                throw new ArgumentNullException("openAIApiKey");

            _openAIService = new OpenAIService(openAIApiKey);
            _taskManager = new TaskManager(connectionString);
            _agentManager = new AgentManager(connectionString);
            _messageDataService = new MessageDataService(connectionString);
            _promptBuilder = new PromptBuilder();
        }

        public async Task<bool> HandleTask(int taskId)
        {
            string agentId = null;
            try
            {
                // Get task details
                TaskDTO task = _taskManager.GetTask(taskId);
                if (task == null)
                    throw new ArgumentException(string.Format("Task with ID {0} not found", taskId));

                // Initialize agent
                agentId = _agentManager.InitializeAgent("TaskExecutor");
                await PrepareAndExecuteTask(task, agentId);

                return true;
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error handling task ID: {0}", taskId), ex);
                if (agentId != null)
                {
                    _agentManager.RecordError(agentId, taskId, ex.Message);
                }
                throw;
            }
        }

        private async Task PrepareAndExecuteTask(TaskDTO task, string agentId)
        {
            try
            {
                // Assign task to agent
                _taskManager.AssignTask(task.TaskId, agentId);

                // Build initial prompt
                string prompt = BuildTaskPrompt(task);

                // Get conversation history
                List<MessageModel> history = _messageDataService.GetMessageThread(taskId: task.TaskId);

                bool taskComplete = false;
                int maxAttempts = 5;
                int currentAttempt = 0;

                while (!taskComplete && currentAttempt < maxAttempts)
                {
                    currentAttempt++;

                    try
                    {
                        // Execute task
                        string response = await ExecuteTaskAttempt(prompt, history);

                        // Process response
                        taskComplete = await ProcessTaskResponse(task, agentId, response);

                        if (!taskComplete)
                        {
                            // Update prompt for next attempt
                            prompt = BuildRetryPrompt(task, history);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(string.Format("Error in attempt {0} for task {1}", currentAttempt, task.TaskId), ex);

                        if (currentAttempt >= maxAttempts)
                            throw;

                        // Log failure and continue to next attempt
                        _agentManager.RecordError(agentId, task.TaskId,
                            string.Format("Attempt {0} failed: {1}", currentAttempt, ex.Message));
                    }
                }

                // Update task status based on completion
                _taskManager.UpdateTaskStatus(task.TaskId,
                    taskComplete ? WebAI.Models.TaskStatus.Completed : WebAI.Models.TaskStatus.Failed,
                    taskComplete ? "Task completed successfully" : "Task failed after maximum attempts");
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error executing task {0}", task.TaskId), ex);
                throw;
            }
        }

        private string BuildTaskPrompt(TaskDTO task)
        {
            return _promptBuilder
                .WithSystemPrompt("You are a task execution agent. Your goal is to complete the assigned task according to the specifications.")
                .WithTaskContext(new TaskModel
                {
                    TaskId = task.TaskId,
                    Title = task.Title,
                    Description = task.Description,
                    Priority = (TaskPriority)Enum.Parse(typeof(TaskPriority), task.Priority),
                    Status = (WebAI.Models.TaskStatus)Enum.Parse(typeof(WebAI.Models.TaskStatus), task.Status)
                })
                .WithInstructions("Please complete this task according to the following guidelines:\n" +
                                "1. Analyze the task requirements\n" +
                                "2. Provide a step-by-step solution\n" +
                                "3. Include any relevant output or results\n" +
                                "4. Indicate clearly if the task is completed or needs further action")
                .WithConstraints(new List<string>
                {
                    "Maintain data consistency",
                    "Follow security guidelines",
                    "Provide clear documentation",
                    "Report any errors or issues"
                })
                .Build();
        }

        private string BuildRetryPrompt(TaskDTO task, List<MessageModel> history)
        {
            return _promptBuilder
                .Clear()
                .WithSystemPrompt("Previous attempts to complete this task were unsuccessful. Please review the history and try a different approach.")
                .WithTaskContext(new TaskModel
                {
                    TaskId = task.TaskId,
                    Title = task.Title,
                    Description = task.Description,
                    Priority = (TaskPriority)Enum.Parse(typeof(TaskPriority), task.Priority),
                    Status = (WebAI.Models.TaskStatus)Enum.Parse(typeof(WebAI.Models.TaskStatus), task.Status)
                })
                .WithConversationHistory(history)
                .WithInstructions("Please review the previous attempts and:\n" +
                                "1. Identify why previous attempts failed\n" +
                                "2. Propose a different approach\n" +
                                "3. Execute the new solution\n" +
                                "4. Verify the results")
                .Build();
        }

        private async Task<string> ExecuteTaskAttempt(string prompt, List<MessageModel> history)
        {
            try
            {
                return await _openAIService.GetAIResponseAsync(prompt);
            }
            catch (Exception ex)
            {
                log.Error("Error executing task attempt with OpenAI", ex);
                throw;
            }
        }

        private async Task<bool> ProcessTaskResponse(TaskDTO task, string agentId, string response)
        {
            try
            {
                // Log the response
                MessageModel responseMessage = MessageBuilder.CreateAgentMessage(response)
                    .WithTask(task.TaskId)
                    .WithType(MessageType.AgentResponse)
                    .Build();

                await Task.Run(() => _messageDataService.SaveMessage(responseMessage));

                // Analyze response for completion indicators
                bool isComplete = await Task.Run(() => AnalyzeResponseForCompletion(response));

                // Record metrics
                AgentMetricsModel metrics = new AgentMetricsModel
                {
                    AgentId = agentId,
                    TaskId = task.TaskId,
                    Status = isComplete ? "Completed" : "Failed",
                    CompletionTime = DateTime.UtcNow,
                    Notes = "Response: " + response.Substring(0, Math.Min(response.Length, 100)) + "..."
                };

                await Task.Run(() => _agentManager.RecordAgentMetrics(task.TaskId, metrics));

                return isComplete;
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error processing response for task {0}", task.TaskId), ex);
                throw;
            }
        }

        private bool AnalyzeResponseForCompletion(string response)
        {
            // Basic completion analysis - can be enhanced with more sophisticated logic
            string lowerResponse = response.ToLower();
            return lowerResponse.Contains("task completed") ||
                   lowerResponse.Contains("successfully completed") ||
                   lowerResponse.Contains("finished successfully");
        }

        private class TaskExecutionResult
        {
            public bool Success { get; set; }
            public string Output { get; set; }
            public List<string> Errors { get; set; }
            public Dictionary<string, object> Metrics { get; set; }

            public TaskExecutionResult()
            {
                Errors = new List<string>();
                Metrics = new Dictionary<string, object>();
            }
        }
    }
}