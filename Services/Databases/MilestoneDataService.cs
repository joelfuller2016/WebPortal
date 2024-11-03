using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using WebAI.Models;

namespace WebAI.Services.Database
{
    public class MilestoneDataService
    {
        private readonly string _connectionString;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MilestoneDataService(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _connectionString = connectionString;
        }

        public int CreateMilestone(MilestoneModel milestone)
        {
            if (milestone == null)
                throw new ArgumentNullException("milestone");

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string insertQuery = @"
                        INSERT INTO Milestones 
                        (ProjectId, Title, Description, SuccessCriteria, Status, CreatedAt, CompletedAt) 
                        VALUES 
                        (@ProjectId, @Title, @Description, @SuccessCriteria, @Status, @CreatedAt, @CompletedAt);
                        SELECT last_insert_rowid();";

                    using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ProjectId", milestone.ProjectId);
                        command.Parameters.AddWithValue("@Title", milestone.Title);
                        command.Parameters.AddWithValue("@Description", (object)milestone.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SuccessCriteria", (object)milestone.SuccessCriteria ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Status", milestone.Status.ToString());
                        command.Parameters.AddWithValue("@CreatedAt", milestone.CreatedAt);
                        command.Parameters.AddWithValue("@CompletedAt", (object)milestone.CompletedAt ?? DBNull.Value);

                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error creating milestone", ex);
                throw;
            }
        }

        public MilestoneModel GetMilestone(int milestoneId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT MilestoneId, ProjectId, Title, Description, 
                               SuccessCriteria, Status, CreatedAt, CompletedAt 
                        FROM Milestones 
                        WHERE MilestoneId = @MilestoneId";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MilestoneId", milestoneId);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return CreateMilestoneFromReader(reader);
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting milestone with ID: {0}", milestoneId), ex);
                throw;
            }
        }

        public List<MilestoneModel> GetMilestonesByProject(int projectId)
        {
            List<MilestoneModel> milestones = new List<MilestoneModel>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT MilestoneId, ProjectId, Title, Description, 
                               SuccessCriteria, Status, CreatedAt, CompletedAt 
                        FROM Milestones 
                        WHERE ProjectId = @ProjectId 
                        ORDER BY CreatedAt";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ProjectId", projectId);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                milestones.Add(CreateMilestoneFromReader(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting milestones for project ID: {0}", projectId), ex);
                throw;
            }

            return milestones;
        }

        public void UpdateMilestone(MilestoneModel milestone)
        {
            if (milestone == null)
                throw new ArgumentNullException("milestone");

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string updateQuery = @"
                        UPDATE Milestones 
                        SET Title = @Title, 
                            Description = @Description, 
                            SuccessCriteria = @SuccessCriteria, 
                            Status = @Status, 
                            CompletedAt = @CompletedAt 
                        WHERE MilestoneId = @MilestoneId";

                    using (SQLiteCommand command = new SQLiteCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MilestoneId", milestone.MilestoneId);
                        command.Parameters.AddWithValue("@Title", milestone.Title);
                        command.Parameters.AddWithValue("@Description", (object)milestone.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@SuccessCriteria", (object)milestone.SuccessCriteria ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Status", milestone.Status.ToString());
                        command.Parameters.AddWithValue("@CompletedAt", (object)milestone.CompletedAt ?? DBNull.Value);

                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            throw new DataException(string.Format("Milestone with ID {0} not found", milestone.MilestoneId));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error updating milestone with ID: {0}", milestone.MilestoneId), ex);
                throw;
            }
        }

        public void UpdateMilestoneStatus(int milestoneId, MilestoneStatus status)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string updateQuery = @"
                        UPDATE Milestones 
                        SET Status = @Status,
                            CompletedAt = CASE 
                                WHEN @Status = 'Completed' THEN @CompletedAt 
                                ELSE NULL 
                            END
                        WHERE MilestoneId = @MilestoneId";

                    using (SQLiteCommand command = new SQLiteCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@MilestoneId", milestoneId);
                        command.Parameters.AddWithValue("@Status", status.ToString());
                        command.Parameters.AddWithValue("@CompletedAt",
                            status == MilestoneStatus.Completed ? DateTime.UtcNow : (object)DBNull.Value);

                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            throw new DataException(string.Format("Milestone with ID {0} not found", milestoneId));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error updating status for milestone ID: {0}", milestoneId), ex);
                throw;
            }
        }

        public bool DeleteMilestone(int milestoneId)
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
                            // Delete associated records first
                            DeleteAssociatedRecords(milestoneId, connection);

                            // Delete the milestone
                            string deleteQuery = "DELETE FROM Milestones WHERE MilestoneId = @MilestoneId";
                            using (SQLiteCommand command = new SQLiteCommand(deleteQuery, connection))
                            {
                                command.Parameters.AddWithValue("@MilestoneId", milestoneId);
                                int rowsAffected = command.ExecuteNonQuery();

                                transaction.Commit();
                                return rowsAffected > 0;
                            }
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error deleting milestone with ID: {0}", milestoneId), ex);
                throw;
            }
        }

        private void DeleteAssociatedRecords(int milestoneId, SQLiteConnection connection)
        {
            // Delete Tasks and their dependencies
            using (SQLiteCommand command = new SQLiteCommand(
                "SELECT TaskId FROM Tasks WHERE MilestoneId = @MilestoneId", connection))
            {
                command.Parameters.AddWithValue("@MilestoneId", milestoneId);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int taskId = reader.GetInt32(0);
                        DeleteTaskRecords(taskId, connection);
                    }
                }
            }

            // Delete Messages
            using (SQLiteCommand command = new SQLiteCommand(
                "DELETE FROM Messages WHERE MilestoneId = @MilestoneId", connection))
            {
                command.Parameters.AddWithValue("@MilestoneId", milestoneId);
                command.ExecuteNonQuery();
            }
        }

        private void DeleteTaskRecords(int taskId, SQLiteConnection connection)
        {
            // Delete Task Dependencies
            using (SQLiteCommand command = new SQLiteCommand(
                "DELETE FROM TaskDependencies WHERE TaskId = @TaskId OR DependsOnTaskId = @TaskId", connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);
                command.ExecuteNonQuery();
            }

            // Delete Agent Metrics
            using (SQLiteCommand command = new SQLiteCommand(
                "DELETE FROM AgentMetrics WHERE TaskId = @TaskId", connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);
                command.ExecuteNonQuery();
            }

            // Delete Messages
            using (SQLiteCommand command = new SQLiteCommand(
                "DELETE FROM Messages WHERE TaskId = @TaskId", connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);
                command.ExecuteNonQuery();
            }

            // Delete Task
            using (SQLiteCommand command = new SQLiteCommand(
                "DELETE FROM Tasks WHERE TaskId = @TaskId", connection))
            {
                command.Parameters.AddWithValue("@TaskId", taskId);
                command.ExecuteNonQuery();
            }
        }

        private MilestoneModel CreateMilestoneFromReader(SQLiteDataReader reader)
        {
            return new MilestoneModel
            {
                MilestoneId = reader.GetInt32(reader.GetOrdinal("MilestoneId")),
                ProjectId = reader.GetInt32(reader.GetOrdinal("ProjectId")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ?
                    null : reader.GetString(reader.GetOrdinal("Description")),
                SuccessCriteria = reader.IsDBNull(reader.GetOrdinal("SuccessCriteria")) ?
                    null : reader.GetString(reader.GetOrdinal("SuccessCriteria")),
                Status = (MilestoneStatus)Enum.Parse(typeof(MilestoneStatus),
                    reader.GetString(reader.GetOrdinal("Status"))),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ?
                    (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("CompletedAt"))
            };
        }
    }
}