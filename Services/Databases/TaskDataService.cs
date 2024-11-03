using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using WebAI.Models;

namespace WebAI.Services.Database
{
    public class TaskDataService
    {
        private readonly string _connectionString;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public TaskDataService(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _connectionString = connectionString;
        }

        public int CreateTask(TaskModel task)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            int taskId = InsertTask(task, connection);

                            if (task.Dependencies != null && task.Dependencies.Count > 0)
                            {
                                foreach (TaskDependency dependency in task.Dependencies)
                                {
                                    InsertTaskDependency(taskId, dependency, connection);
                                }
                            }

                            transaction.Commit();
                            return taskId;
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
                log.Error("Error creating task", ex);
                throw;
            }
        }

        private int InsertTask(TaskModel task, SQLiteConnection connection)
        {
            string insertQuery = @"
                INSERT INTO Tasks 
                (MilestoneId, ParentTaskId, Title, Description, Priority, Status, 
                 AssignedAgentId, CreatedAt, StartedAt, CompletedAt, BlockedReason) 
                VALUES 
                (@MilestoneId, @ParentTaskId, @Title, @Description, @Priority, @Status,
                 @AssignedAgentId, @CreatedAt, @StartedAt, @CompletedAt, @BlockedReason);
                SELECT last_insert_rowid();";

            using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
            {
                command.Parameters.AddWithValue("@MilestoneId", task.MilestoneId);
                command.Parameters.AddWithValue("@ParentTaskId", (object)task.ParentTaskId ?? DBNull.Value);
                command.Parameters.AddWithValue("@Title", task.Title);
                command.Parameters.AddWithValue("@Description", (object)task.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("@Priority", task.Priority.ToString());
                command.Parameters.AddWithValue("@Status", task.Status.ToString());
                command.Parameters.AddWithValue("@AssignedAgentId", (object)task.AssignedAgentId ?? DBNull.Value);
                command.Parameters.AddWithValue("@CreatedAt", task.CreatedAt);
                command.Parameters.AddWithValue("@StartedAt", (object)task.StartedAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompletedAt", (object)task.CompletedAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@BlockedReason", (object)task.BlockedReason ?? DBNull.Value);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void InsertTaskDependency(int taskId, TaskDependency dependency, SQLiteConnection connection)
        {
            string insertDependencyQuery = @"
                INSERT INTO TaskDependencies 
                (TaskId, DependsOnTaskId, CreatedAt) 
                VALUES 
                (@TaskId, @DependsOnTaskId, @CreatedAt)";

            using (SQLiteCommand command = new SQLiteCommand(insertDependencyQuery, connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);
                command.Parameters.AddWithValue("@DependsOnTaskId", dependency.DependsOnTaskId);
                command.Parameters.AddWithValue("@CreatedAt", dependency.CreatedAt);

                command.ExecuteNonQuery();
            }
        }

        public TaskModel GetTask(int taskId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    TaskModel task = GetTaskBase(taskId, connection);

                    if (task != null)
                    {
                        task.Dependencies = GetTaskDependencies(taskId, connection);
                        task.SubTasks = GetSubTasks(taskId, connection);
                        task.AgentMetrics = GetTaskAgentMetrics(taskId, connection);
                    }

                    return task;
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting task with ID: {0}", taskId), ex);
                throw;
            }
        }

        private TaskModel GetTaskBase(int taskId, SQLiteConnection connection)
        {
            string selectQuery = @"
                SELECT TaskId, MilestoneId, ParentTaskId, Title, Description, 
                       Priority, Status, AssignedAgentId, CreatedAt, StartedAt, 
                       CompletedAt, BlockedReason 
                FROM Tasks 
                WHERE TaskId = @TaskId";

            using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return CreateTaskFromReader(reader);
                    }
                }
            }
            return null;
        }

        private List<TaskDependency> GetTaskDependencies(int taskId, SQLiteConnection connection)
        {
            List<TaskDependency> dependencies = new List<TaskDependency>();
            string selectQuery = @"
                SELECT d.DependencyId, d.TaskId, d.DependsOnTaskId, d.CreatedAt,
                       t.Title, t.Status
                FROM TaskDependencies d
                JOIN Tasks t ON d.DependsOnTaskId = t.TaskId
                WHERE d.TaskId = @TaskId";

            using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dependencies.Add(CreateDependencyFromReader(reader));
                    }
                }
            }
            return dependencies;
        }

        private List<TaskModel> GetSubTasks(int parentTaskId, SQLiteConnection connection)
        {
            List<TaskModel> subTasks = new List<TaskModel>();
            string selectQuery = @"
                SELECT TaskId, MilestoneId, ParentTaskId, Title, Description, 
                       Priority, Status, AssignedAgentId, CreatedAt, StartedAt, 
                       CompletedAt, BlockedReason 
                FROM Tasks 
                WHERE ParentTaskId = @ParentTaskId";

            using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
            {
                command.Parameters.AddWithValue("@ParentTaskId", parentTaskId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        subTasks.Add(CreateTaskFromReader(reader));
                    }
                }
            }
            return subTasks;
        }

        private List<AgentMetricsModel> GetTaskAgentMetrics(int taskId, SQLiteConnection connection)
        {
            List<AgentMetricsModel> metrics = new List<AgentMetricsModel>();
            string selectQuery = @"
                SELECT MetricId, AgentId, TaskId, Status, StartTime, CompletionTime,
                       SuccessRate, Notes 
                FROM AgentMetrics 
                WHERE TaskId = @TaskId";

            using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        metrics.Add(CreateAgentMetricsFromReader(reader));
                    }
                }
            }
            return metrics;
        }

        public List<TaskModel> GetTasksByMilestone(int milestoneId)
        {
            List<TaskModel> tasks = new List<TaskModel>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT TaskId, MilestoneId, ParentTaskId, Title, Description, 
                               Priority, Status, AssignedAgentId, CreatedAt, StartedAt, 
                               CompletedAt, BlockedReason 
                        FROM Tasks 
                        WHERE MilestoneId = @MilestoneId 
                        AND ParentTaskId IS NULL
                        ORDER BY CreatedAt";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MilestoneId", milestoneId);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                TaskModel task = CreateTaskFromReader(reader);
                                task.Dependencies = GetTaskDependencies(task.TaskId, connection);
                                task.SubTasks = GetSubTasks(task.TaskId, connection);
                                task.AgentMetrics = GetTaskAgentMetrics(task.TaskId, connection);
                                tasks.Add(task);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting tasks for milestone ID: {0}", milestoneId), ex);
                throw;
            }

            return tasks;
        }

        public void UpdateTask(TaskModel task)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            UpdateTaskBase(task, connection);
                            UpdateTaskDependencies(task, connection);

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
                log.Error(string.Format("Error updating task with ID: {0}", task.TaskId), ex);
                throw;
            }
        }

        private void UpdateTaskBase(TaskModel task, SQLiteConnection connection)
        {
            string updateQuery = @"
                UPDATE Tasks 
                SET Title = @Title,
                    Description = @Description,
                    Priority = @Priority,
                    Status = @Status,
                    AssignedAgentId = @AssignedAgentId,
                    StartedAt = @StartedAt,
                    CompletedAt = @CompletedAt,
                    BlockedReason = @BlockedReason
                WHERE TaskId = @TaskId";

            using (SQLiteCommand command = new SQLiteCommand(updateQuery, connection))
            {
                command.Parameters.AddWithValue("@TaskId", task.TaskId);
                command.Parameters.AddWithValue("@Title", task.Title);
                command.Parameters.AddWithValue("@Description", (object)task.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("@Priority", task.Priority.ToString());
                command.Parameters.AddWithValue("@Status", task.Status.ToString());
                command.Parameters.AddWithValue("@AssignedAgentId", (object)task.AssignedAgentId ?? DBNull.Value);
                command.Parameters.AddWithValue("@StartedAt", (object)task.StartedAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompletedAt", (object)task.CompletedAt ?? DBNull.Value);
                command.Parameters.AddWithValue("@BlockedReason", (object)task.BlockedReason ?? DBNull.Value);

                int rowsAffected = command.ExecuteNonQuery();
                if (rowsAffected == 0)
                {
                    throw new DataException(string.Format("Task with ID {0} not found", task.TaskId));
                }
            }
        }

        private void UpdateTaskDependencies(TaskModel task, SQLiteConnection connection)
        {
            // Remove existing dependencies
            string deleteQuery = "DELETE FROM TaskDependencies WHERE TaskId = @TaskId";
            using (SQLiteCommand command = new SQLiteCommand(deleteQuery, connection))
            {
                command.Parameters.AddWithValue("@TaskId", task.TaskId);
                command.ExecuteNonQuery();
            }

            // Add new dependencies
            if (task.Dependencies != null)
            {
                foreach (TaskDependency dependency in task.Dependencies)
                {
                    InsertTaskDependency(task.TaskId, dependency, connection);
                }
            }
        }

        public bool DeleteTask(int taskId)
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
                            DeleteTaskRecursive(taskId, connection);
                            transaction.Commit();
                            return true;
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
                log.Error(string.Format("Error deleting task with ID: {0}", taskId), ex);
                throw;
            }
        }

        private void DeleteTaskRecursive(int taskId, SQLiteConnection connection)
        {
            // Delete subtasks first
            string selectSubtasksQuery = "SELECT TaskId FROM Tasks WHERE ParentTaskId = @TaskId";
            using (SQLiteCommand command = new SQLiteCommand(selectSubtasksQuery, connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DeleteTaskRecursive(reader.GetInt32(0), connection);
                    }
                }
            }

                // Delete dependencies
                string deleteDependenciesQuery = @"
                DELETE FROM TaskDependencies 
                WHERE TaskId = @TaskId OR DependsOnTaskId = @TaskId";
                using (SQLiteCommand command = new SQLiteCommand(deleteDependenciesQuery, connection))
                {
                    command.Parameters.AddWithValue("@TaskId", taskId);
                    command.ExecuteNonQuery();
                }

                // Delete agent metrics
                string deleteMetricsQuery = "DELETE FROM AgentMetrics WHERE TaskId = @TaskId";
                using (SQLiteCommand command = new SQLiteCommand(deleteMetricsQuery, connection))
                {
                    command.Parameters.AddWithValue("@TaskId", taskId);
                    command.ExecuteNonQuery();
                }

                // Delete messages
                string deleteMessagesQuery = "DELETE FROM Messages WHERE TaskId = @TaskId";
                using (SQLiteCommand command = new SQLiteCommand(deleteMessagesQuery, connection))
                {
                    command.Parameters.AddWithValue("@TaskId", taskId);
                    command.ExecuteNonQuery();
                }

                // Delete task
                string deleteTaskQuery = "DELETE FROM Tasks WHERE TaskId = @TaskId";
                using (SQLiteCommand command = new SQLiteCommand(deleteTaskQuery, connection))
                {
                    command.Parameters.AddWithValue("@TaskId", taskId);
                    command.ExecuteNonQuery();
                }
            }

            private TaskModel CreateTaskFromReader(SQLiteDataReader reader)
            {
                return new TaskModel
                {
                    TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                    MilestoneId = reader.GetInt32(reader.GetOrdinal("MilestoneId")),
                    ParentTaskId = reader.IsDBNull(reader.GetOrdinal("ParentTaskId")) ?
                        (int?)null : reader.GetInt32(reader.GetOrdinal("ParentTaskId")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ?
                        null : reader.GetString(reader.GetOrdinal("Description")),
                    Priority = (TaskPriority)Enum.Parse(typeof(TaskPriority),
                        reader.GetString(reader.GetOrdinal("Priority"))),
                    Status = (TaskStatus)Enum.Parse(typeof(TaskStatus),
                        reader.GetString(reader.GetOrdinal("Status"))),
                    AssignedAgentId = reader.IsDBNull(reader.GetOrdinal("AssignedAgentId")) ?
                        null : reader.GetString(reader.GetOrdinal("AssignedAgentId")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    StartedAt = reader.IsDBNull(reader.GetOrdinal("StartedAt")) ?
                        (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("StartedAt")),
                    CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ?
                        (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedAt")),
                    BlockedReason = reader.IsDBNull(reader.GetOrdinal("BlockedReason")) ?
                        null : reader.GetString(reader.GetOrdinal("BlockedReason"))
                };
            }

            private TaskDependency CreateDependencyFromReader(SQLiteDataReader reader)
            {
                return new TaskDependency
                {
                    DependencyId = reader.GetInt32(reader.GetOrdinal("DependencyId")),
                    TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                    DependsOnTaskId = reader.GetInt32(reader.GetOrdinal("DependsOnTaskId")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                    DependsOnTask = new TaskModel
                    {
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        Status = (TaskStatus)Enum.Parse(typeof(TaskStatus),
                            reader.GetString(reader.GetOrdinal("Status")))
                    }
                };
            }

            private AgentMetricsModel CreateAgentMetricsFromReader(SQLiteDataReader reader)
            {
                return new AgentMetricsModel
                {
                    MetricId = reader.GetInt32(reader.GetOrdinal("MetricId")),
                    AgentId = reader.GetString(reader.GetOrdinal("AgentId")),
                    TaskId = reader.GetInt32(reader.GetOrdinal("TaskId")),
                    Status = reader.GetString(reader.GetOrdinal("Status")),
                    StartTime = reader.IsDBNull(reader.GetOrdinal("StartTime")) ?
                        (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("StartTime")),
                    CompletionTime = reader.IsDBNull(reader.GetOrdinal("CompletionTime")) ?
                        (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletionTime")),
                    SuccessRate = reader.IsDBNull(reader.GetOrdinal("SuccessRate")) ?
                        (double?)null : reader.GetDouble(reader.GetOrdinal("SuccessRate")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ?
                        null : reader.GetString(reader.GetOrdinal("Notes"))
                };
            }
        }
    }