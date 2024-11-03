using System;
using System.Collections.Generic;
using System.Text;
using WebAI.Models;

namespace WebAI.Services.OpenAI
{
    public class PromptBuilder
    {
        private readonly StringBuilder _promptBuilder;
        private readonly Dictionary<string, object> _context;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public PromptBuilder()
        {
            _promptBuilder = new StringBuilder();
            _context = new Dictionary<string, object>();
        }

        public PromptBuilder WithSystemPrompt(string systemPrompt)
        {
            if (string.IsNullOrEmpty(systemPrompt))
                throw new ArgumentNullException("systemPrompt");

            _promptBuilder.AppendLine("System:");
            _promptBuilder.AppendLine(systemPrompt);
            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithProjectContext(ProjectModel project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            _context["ProjectId"] = project.ProjectId;
            _context["ProjectName"] = project.ProjectName;

            _promptBuilder.AppendLine("Project Context:");
            _promptBuilder.AppendFormat("Project: {0}\n", project.ProjectName);
            _promptBuilder.AppendFormat("Description: {0}\n", project.Description);
            _promptBuilder.AppendFormat("Status: {0}\n", project.Status);
            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithMilestoneContext(MilestoneModel milestone)
        {
            if (milestone == null)
                throw new ArgumentNullException("milestone");

            _context["MilestoneId"] = milestone.MilestoneId;
            _context["MilestoneTitle"] = milestone.Title;

            _promptBuilder.AppendLine("Milestone Context:");
            _promptBuilder.AppendFormat("Title: {0}\n", milestone.Title);
            _promptBuilder.AppendFormat("Description: {0}\n", milestone.Description);
            _promptBuilder.AppendFormat("Success Criteria: {0}\n", milestone.SuccessCriteria);
            _promptBuilder.AppendFormat("Status: {0}\n", milestone.Status);
            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithTaskContext(TaskModel task)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            _context["TaskId"] = task.TaskId;
            _context["TaskTitle"] = task.Title;

            _promptBuilder.AppendLine("Task Context:");
            _promptBuilder.AppendFormat("Title: {0}\n", task.Title);
            _promptBuilder.AppendFormat("Description: {0}\n", task.Description);
            _promptBuilder.AppendFormat("Priority: {0}\n", task.Priority);
            _promptBuilder.AppendFormat("Status: {0}\n", task.Status);

            if (task.Dependencies != null && task.Dependencies.Count > 0)
            {
                _promptBuilder.AppendLine("Dependencies:");
                foreach (TaskDependency dependency in task.Dependencies)
                {
                    _promptBuilder.AppendFormat("- {0}\n", dependency.DependsOnTask.Title);
                }
            }

            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithConversationHistory(List<MessageModel> messages, int maxMessages = 10)
        {
            if (messages == null)
                throw new ArgumentNullException("messages");

            _promptBuilder.AppendLine("Conversation History:");

            int startIndex = Math.Max(0, messages.Count - maxMessages);
            for (int i = startIndex; i < messages.Count; i++)
            {
                MessageModel message = messages[i];
                _promptBuilder.AppendFormat("{0}: {1}\n",
                    message.Role,
                    message.TokenizedContent ?? message.Content);
            }

            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithAgentContext(string agentId, AgentMetricsModel metrics = null)
        {
            if (string.IsNullOrEmpty(agentId))
                throw new ArgumentNullException("agentId");

            _context["AgentId"] = agentId;

            _promptBuilder.AppendLine("Agent Context:");
            _promptBuilder.AppendFormat("Agent ID: {0}\n", agentId);

            if (metrics != null)
            {
                _promptBuilder.AppendFormat("Success Rate: {0}%\n",
                    metrics.SuccessRate.HasValue ? metrics.SuccessRate.Value.ToString("F2") : "N/A");

                if (metrics.PerformanceMetrics != null && metrics.PerformanceMetrics.Count > 0)
                {
                    _promptBuilder.AppendLine("Performance Metrics:");
                    foreach (KeyValuePair<string, double> metric in metrics.PerformanceMetrics)
                    {
                        _promptBuilder.AppendFormat("- {0}: {1}\n", metric.Key, metric.Value);
                    }
                }
            }

            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithInstructions(string instructions)
        {
            if (string.IsNullOrEmpty(instructions))
                throw new ArgumentNullException("instructions");

            _promptBuilder.AppendLine("Instructions:");
            _promptBuilder.AppendLine(instructions);
            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithConstraints(List<string> constraints)
        {
            if (constraints == null || constraints.Count == 0)
                throw new ArgumentException("Constraints list cannot be null or empty");

            _promptBuilder.AppendLine("Constraints:");
            foreach (string constraint in constraints)
            {
                _promptBuilder.AppendFormat("- {0}\n", constraint);
            }
            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithExpectedOutput(string outputFormat)
        {
            if (string.IsNullOrEmpty(outputFormat))
                throw new ArgumentNullException("outputFormat");

            _promptBuilder.AppendLine("Expected Output Format:");
            _promptBuilder.AppendLine(outputFormat);
            _promptBuilder.AppendLine();
            return this;
        }

        public PromptBuilder WithExamples(List<KeyValuePair<string, string>> examples)
        {
            if (examples == null || examples.Count == 0)
                throw new ArgumentException("Examples list cannot be null or empty");

            _promptBuilder.AppendLine("Examples:");
            foreach (KeyValuePair<string, string> example in examples)
            {
                _promptBuilder.AppendLine("Input:");
                _promptBuilder.AppendLine(example.Key);
                _promptBuilder.AppendLine("Output:");
                _promptBuilder.AppendLine(example.Value);
                _promptBuilder.AppendLine();
            }
            return this;
        }

        public PromptBuilder WithCustomSection(string sectionTitle, string content)
        {
            if (string.IsNullOrEmpty(sectionTitle))
                throw new ArgumentNullException("sectionTitle");
            if (string.IsNullOrEmpty(content))
                throw new ArgumentNullException("content");

            _promptBuilder.AppendLine(sectionTitle + ":");
            _promptBuilder.AppendLine(content);
            _promptBuilder.AppendLine();
            return this;
        }

        public string Build()
        {
            try
            {
                string prompt = _promptBuilder.ToString().Trim();

                // Log the prompt for debugging (excluding sensitive information)
                log.Debug(string.Format("Generated prompt with context: Project={0}, Milestone={1}, Task={2}",
                    _context.ContainsKey("ProjectName") ? _context["ProjectName"] : "None",
                    _context.ContainsKey("MilestoneTitle") ? _context["MilestoneTitle"] : "None",
                    _context.ContainsKey("TaskTitle") ? _context["TaskTitle"] : "None"));

                return prompt;
            }
            catch (Exception ex)
            {
                log.Error("Error building prompt", ex);
                throw;
            }
        }

        public Dictionary<string, object> GetContext()
        {
            return new Dictionary<string, object>(_context);
        }

        public void Clear()
        {
            _promptBuilder.Clear();
            _context.Clear();
        }
    }
}