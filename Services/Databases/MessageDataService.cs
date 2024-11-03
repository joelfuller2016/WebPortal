using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using WebAI.Models;
using Newtonsoft.Json;

namespace WebAI.Services.Database
{
    public class MessageDataService
    {
        private readonly string _connectionString;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MessageDataService(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _connectionString = connectionString;
        }

        public int SaveMessage(MessageModel message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int messageId = InsertMessage(message, connection);
                            SaveMetadata(messageId, message.Metadata, connection);

                            transaction.Commit();
                            return messageId;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error saving message", ex);
                throw;
            }
        }

        private int InsertMessage(MessageModel message, SQLiteConnection connection)
        {
            string insertQuery = @"
                INSERT INTO Messages 
                (TaskId, MilestoneId, ProjectId, Content, Role, CreatedAt, Type, TokenizedContent) 
                VALUES 
                (@TaskId, @MilestoneId, @ProjectId, @Content, @Role, @CreatedAt, @Type, @TokenizedContent);
                SELECT last_insert_rowid();";

            using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
            {
                command.Parameters.AddWithValue("@TaskId", (object)message.TaskId ?? DBNull.Value);
                command.Parameters.AddWithValue("@MilestoneId", (object)message.MilestoneId ?? DBNull.Value);
                command.Parameters.AddWithValue("@ProjectId", (object)message.ProjectId ?? DBNull.Value);
                command.Parameters.AddWithValue("@Content", message.Content);
                command.Parameters.AddWithValue("@Role", message.Role);
                command.Parameters.AddWithValue("@CreatedAt", message.CreatedAt);
                command.Parameters.AddWithValue("@Type", message.Type.ToString());
                command.Parameters.AddWithValue("@TokenizedContent",
                    (object)message.TokenizedContent ?? DBNull.Value);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void SaveMetadata(int messageId, Dictionary<string, object> metadata, SQLiteConnection connection)
        {
            if (metadata != null && metadata.Count > 0)
            {
                string insertQuery = @"
                    INSERT INTO MessageMetadata 
                    (MessageId, MetadataKey, MetadataValue) 
                    VALUES 
                    (@MessageId, @Key, @Value)";

                using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                {
                    foreach (KeyValuePair<string, object> kvp in metadata)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@MessageId", messageId);
                        command.Parameters.AddWithValue("@Key", kvp.Key);
                        command.Parameters.AddWithValue("@Value",
                            JsonConvert.SerializeObject(kvp.Value));
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public MessageModel GetMessage(int messageId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    MessageModel message = GetMessageBase(messageId, connection);
                    if (message != null)
                    {
                        LoadMessageMetadata(message, connection);
                    }
                    return message;
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting message with ID: {0}", messageId), ex);
                throw;
            }
        }

        private MessageModel GetMessageBase(int messageId, SQLiteConnection connection)
        {
            string selectQuery = @"
                SELECT MessageId, TaskId, MilestoneId, ProjectId, Content, 
                       Role, CreatedAt, Type, TokenizedContent 
                FROM Messages 
                WHERE MessageId = @MessageId";

            using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
            {
                command.Parameters.AddWithValue("@MessageId", messageId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return CreateMessageFromReader(reader);
                    }
                }
            }
            return null;
        }

        private void LoadMessageMetadata(MessageModel message, SQLiteConnection connection)
        {
            string selectQuery = @"
                SELECT MetadataKey, MetadataValue 
                FROM MessageMetadata 
                WHERE MessageId = @MessageId";

            using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
            {
                command.Parameters.AddWithValue("@MessageId", message.MessageId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string key = reader.GetString(0);
                        string serializedValue = reader.GetString(1);
                        object value = JsonConvert.DeserializeObject(serializedValue);
                        message.AddMetadata(key, value);
                    }
                }
            }
        }

        public List<MessageModel> GetMessageThread(int? taskId = null, int? milestoneId = null, int? projectId = null)
        {
            List<MessageModel> messages = new List<MessageModel>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT MessageId, TaskId, MilestoneId, ProjectId, Content, 
                               Role, CreatedAt, Type, TokenizedContent 
                        FROM Messages 
                        WHERE 1=1";

                    if (taskId.HasValue)
                        selectQuery += " AND TaskId = @TaskId";
                    if (milestoneId.HasValue)
                        selectQuery += " AND MilestoneId = @MilestoneId";
                    if (projectId.HasValue)
                        selectQuery += " AND ProjectId = @ProjectId";

                    selectQuery += " ORDER BY CreatedAt";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        if (taskId.HasValue)
                            command.Parameters.AddWithValue("@TaskId", taskId.Value);
                        if (milestoneId.HasValue)
                            command.Parameters.AddWithValue("@MilestoneId", milestoneId.Value);
                        if (projectId.HasValue)
                            command.Parameters.AddWithValue("@ProjectId", projectId.Value);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                MessageModel message = CreateMessageFromReader(reader);
                                LoadMessageMetadata(message, connection);
                                messages.Add(message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error getting message thread", ex);
                throw;
            }

            return messages;
        }

        public List<MessageModel> GetMessagesByRole(string role, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<MessageModel> messages = new List<MessageModel>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT MessageId, TaskId, MilestoneId, ProjectId, Content, 
                               Role, CreatedAt, Type, TokenizedContent 
                        FROM Messages 
                        WHERE Role = @Role";

                    if (startDate.HasValue)
                        selectQuery += " AND CreatedAt >= @StartDate";
                    if (endDate.HasValue)
                        selectQuery += " AND CreatedAt <= @EndDate";

                    selectQuery += " ORDER BY CreatedAt";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@Role", role);
                        if (startDate.HasValue)
                            command.Parameters.AddWithValue("@StartDate", startDate.Value);
                        if (endDate.HasValue)
                            command.Parameters.AddWithValue("@EndDate", endDate.Value);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                MessageModel message = CreateMessageFromReader(reader);
                                LoadMessageMetadata(message, connection);
                                messages.Add(message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting messages for role: {0}", role), ex);
                throw;
            }

            return messages;
        }

        private MessageModel CreateMessageFromReader(SQLiteDataReader reader)
        {
            return new MessageModel
            {
                MessageId = reader.GetInt32(reader.GetOrdinal("MessageId")),
                TaskId = reader.IsDBNull(reader.GetOrdinal("TaskId")) ?
                    (int?)null : reader.GetInt32(reader.GetOrdinal("TaskId")),
                MilestoneId = reader.IsDBNull(reader.GetOrdinal("MilestoneId")) ?
                    (int?)null : reader.GetInt32(reader.GetOrdinal("MilestoneId")),
                ProjectId = reader.IsDBNull(reader.GetOrdinal("ProjectId")) ?
                    (int?)null : reader.GetInt32(reader.GetOrdinal("ProjectId")),
                Content = reader.GetString(reader.GetOrdinal("Content")),
                Role = reader.GetString(reader.GetOrdinal("Role")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                Type = (MessageType)Enum.Parse(typeof(MessageType),
                    reader.GetString(reader.GetOrdinal("Type"))),
                TokenizedContent = reader.IsDBNull(reader.GetOrdinal("TokenizedContent")) ?
                    null : reader.GetString(reader.GetOrdinal("TokenizedContent"))
            };
        }

        public void DeleteMessage(int messageId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            // Delete metadata first
                            string deleteMetadataQuery = @"
                                DELETE FROM MessageMetadata 
                                WHERE MessageId = @MessageId";
                            using (SQLiteCommand command = new SQLiteCommand(deleteMetadataQuery, connection))
                            {
                                command.Parameters.AddWithValue("@MessageId", messageId);
                                command.ExecuteNonQuery();
                            }

                            // Delete message
                            string deleteMessageQuery = @"
                                DELETE FROM Messages 
                                WHERE MessageId = @MessageId";
                            using (SQLiteCommand command = new SQLiteCommand(deleteMessageQuery, connection))
                            {
                                command.Parameters.AddWithValue("@MessageId", messageId);
                                int rowsAffected = command.ExecuteNonQuery();

                                if (rowsAffected == 0)
                                {
                                    throw new DataException(
                                        string.Format("Message with ID {0} not found", messageId));
                                }
                            }

                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error deleting message with ID: {0}", messageId), ex);
                throw;
            }
        }
    }
}