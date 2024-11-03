using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WebAI.Models
{
    public class MessageModel
    {
        public int MessageId { get; set; }
        public int? TaskId { get; set; }
        public int? MilestoneId { get; set; }
        public int? ProjectId { get; set; }
        public string Content { get; set; }
        public string Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public MessageType Type { get; set; }
        public Dictionary<string, object> Metadata { get; private set; }
        public string TokenizedContent { get; set; }

        // Navigation properties
        public TaskModel Task { get; set; }
        public MilestoneModel Milestone { get; set; }
        public ProjectModel Project { get; set; }

        public MessageModel()
        {
            CreatedAt = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
            Type = MessageType.Standard;
        }

        public void AddMetadata(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException("key");

            Metadata[key] = value;
        }

        public T GetMetadata<T>(string key, T defaultValue)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException("key");

            object value;
            if (Metadata.TryGetValue(key, out value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public bool IsSystemMessage
        {
            get { return string.Equals(Role, "system", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsUserMessage
        {
            get { return string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsAgentMessage
        {
            get { return string.Equals(Role, "assistant", StringComparison.OrdinalIgnoreCase); }
        }

        public void TokenizeContent()
        {
            if (string.IsNullOrEmpty(Content))
                return;

            // Simple tokenization for context tracking
            TokenizedContent = Content.Replace("\n", " ")
                                    .Replace("\r", " ")
                                    .Replace("  ", " ")
                                    .Trim();
        }
    }

    public enum MessageType
    {
        Standard,
        SystemPrompt,
        UserQuery,
        AgentResponse,
        ErrorMessage,
        StatusUpdate,
        Notification,
        DebugInfo,
        MetricUpdate
    }

    public class MessageDTO
    {
        public int MessageId { get; set; }
        public string Content { get; set; }
        public string Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Type { get; set; }
        public string Context { get; set; }
        public Dictionary<string, string> FormattedMetadata { get; set; }
        public MessageThreadInfo ThreadInfo { get; set; }

        public static MessageDTO FromModel(MessageModel model)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            MessageDTO dto = new MessageDTO();
            dto.MessageId = model.MessageId;
            dto.Content = model.Content;
            dto.Role = model.Role;
            dto.CreatedAt = model.CreatedAt;
            dto.Type = model.Type.ToString();
            dto.Context = GetContextString(model);
            dto.FormattedMetadata = FormatMetadata(model.Metadata);
            dto.ThreadInfo = new MessageThreadInfo
            {
                ProjectName = model.Project != null ? model.Project.ProjectName : null,
                MilestoneTitle = model.Milestone != null ? model.Milestone.Title : null,
                TaskTitle = model.Task != null ? model.Task.Title : null,
                ThreadLevel = DetermineThreadLevel(model)
            };

            return dto;
        }

        private static string GetContextString(MessageModel model)
        {
            if (model.Project != null)
                return string.Format("Project: {0}", model.Project.ProjectName);
            if (model.Milestone != null)
                return string.Format("Milestone: {0}", model.Milestone.Title);
            if (model.Task != null)
                return string.Format("Task: {0}", model.Task.Title);
            return "General";
        }

        private static Dictionary<string, string> FormatMetadata(Dictionary<string, object> metadata)
        {
            Dictionary<string, string> formatted = new Dictionary<string, string>();
            foreach (KeyValuePair<string, object> kvp in metadata)
            {
                formatted[kvp.Key] = FormatMetadataValue(kvp.Value);
            }
            return formatted;
        }

        private static string FormatMetadataValue(object value)
        {
            if (value == null)
                return "null";

            if (value is DateTime)
                return ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss");

            if (value is TimeSpan)
                return ((TimeSpan)value).ToString(@"hh\:mm\:ss");

            if (value is IFormattable)
                return ((IFormattable)value).ToString("G", null);

            return value.ToString();
        }

        private static ThreadLevel DetermineThreadLevel(MessageModel model)
        {
            if (model.TaskId.HasValue)
                return ThreadLevel.Task;
            if (model.MilestoneId.HasValue)
                return ThreadLevel.Milestone;
            if (model.ProjectId.HasValue)
                return ThreadLevel.Project;
            return ThreadLevel.General;
        }
    }

    public class MessageThreadInfo
    {
        public string ProjectName { get; set; }
        public string MilestoneTitle { get; set; }
        public string TaskTitle { get; set; }
        public ThreadLevel ThreadLevel { get; set; }
    }

    public enum ThreadLevel
    {
        General,
        Project,
        Milestone,
        Task
    }

    public class MessageBuilder
    {
        private readonly MessageModel _message;

        public MessageBuilder(string content, string role)
        {
            if (string.IsNullOrEmpty(content))
                throw new ArgumentNullException("content");
            if (string.IsNullOrEmpty(role))
                throw new ArgumentNullException("role");

            _message = new MessageModel
            {
                Content = content,
                Role = role
            };
        }

        public MessageBuilder WithType(MessageType type)
        {
            _message.Type = type;
            return this;
        }

        public MessageBuilder WithProject(int projectId)
        {
            _message.ProjectId = projectId;
            return this;
        }

        public MessageBuilder WithMilestone(int milestoneId)
        {
            _message.MilestoneId = milestoneId;
            return this;
        }

        public MessageBuilder WithTask(int taskId)
        {
            _message.TaskId = taskId;
            return this;
        }

        public MessageBuilder WithMetadata(string key, object value)
        {
            _message.AddMetadata(key, value);
            return this;
        }

        public MessageModel Build()
        {
            _message.TokenizeContent();
            return _message;
        }

        public static MessageBuilder CreateSystemMessage(string content)
        {
            return new MessageBuilder(content, "system")
                .WithType(MessageType.SystemPrompt);
        }

        public static MessageBuilder CreateUserMessage(string content)
        {
            return new MessageBuilder(content, "user")
                .WithType(MessageType.UserQuery);
        }

        public static MessageBuilder CreateAgentMessage(string content)
        {
            return new MessageBuilder(content, "assistant")
                .WithType(MessageType.AgentResponse);
        }
    }
}