using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using WebAI.Models;

namespace WebAI.Services.Database
{
    public class ProjectDataService
    {
        private readonly string _connectionString;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ProjectDataService(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _connectionString = connectionString;
        }

        public int CreateProject(ProjectModel project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string insertQuery = @"
                        INSERT INTO Projects 
                        (ProjectName, Description, CreatedAt, Status) 
                        VALUES 
                        (@ProjectName, @Description, @CreatedAt, @Status);
                        SELECT last_insert_rowid();";

                    using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ProjectName", project.ProjectName);
                        command.Parameters.AddWithValue("@Description", (object)project.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@CreatedAt", project.CreatedAt);
                        command.Parameters.AddWithValue("@Status", project.Status.ToString());

                        return Convert.ToInt32(command.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error creating project", ex);
                throw;
            }
        }

        public ProjectModel GetProject(int projectId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT ProjectId, ProjectName, Description, CreatedAt, Status 
                        FROM Projects 
                        WHERE ProjectId = @ProjectId";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ProjectId", projectId);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return CreateProjectFromReader(reader);
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting project with ID: {0}", projectId), ex);
                throw;
            }
        }

        public List<ProjectModel> GetAllProjects()
        {
            List<ProjectModel> projects = new List<ProjectModel>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT ProjectId, ProjectName, Description, CreatedAt, Status 
                        FROM Projects 
                        ORDER BY CreatedAt DESC";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                projects.Add(CreateProjectFromReader(reader));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Error getting all projects", ex);
                throw;
            }

            return projects;
        }

        public void UpdateProject(ProjectModel project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string updateQuery = @"
                        UPDATE Projects 
                        SET ProjectName = @ProjectName, 
                            Description = @Description, 
                            Status = @Status 
                        WHERE ProjectId = @ProjectId";

                    using (SQLiteCommand command = new SQLiteCommand(updateQuery, connection))
                    {
                        command.Parameters.AddWithValue("@ProjectId", project.ProjectId);
                        command.Parameters.AddWithValue("@ProjectName", project.ProjectName);
                        command.Parameters.AddWithValue("@Description", (object)project.Description ?? DBNull.Value);
                        command.Parameters.AddWithValue("@Status", project.Status.ToString());

                        int rowsAffected = command.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            throw new DataException(string.Format("Project with ID {0} not found", project.ProjectId));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error updating project with ID: {0}", project.ProjectId), ex);
                throw;
            }
        }

        public bool DeleteProject(int projectId)
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
                            DeleteAssociatedRecords(projectId, connection);

                            // Delete the project
                            string deleteQuery = "DELETE FROM Projects WHERE ProjectId = @ProjectId";
                            using (SQLiteCommand command = new SQLiteCommand(deleteQuery, connection))
                            {
                                command.Parameters.AddWithValue("@ProjectId", projectId);
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
                log.Error(string.Format("Error deleting project with ID: {0}", projectId), ex);
                throw;
            }
        }

        private void DeleteAssociatedRecords(int projectId, SQLiteConnection connection)
        {
            // Delete Messages
            using (SQLiteCommand command = new SQLiteCommand(
                "DELETE FROM Messages WHERE ProjectId = @ProjectId", connection))
            {
                command.Parameters.AddWithValue("@ProjectId", projectId);
                command.ExecuteNonQuery();
            }

            // Delete Milestones and associated Tasks
            using (SQLiteCommand command = new SQLiteCommand(
                "SELECT MilestoneId FROM Milestones WHERE ProjectId = @ProjectId", connection))
            {
                command.Parameters.AddWithValue("@ProjectId", projectId);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int milestoneId = reader.GetInt32(0);
                        DeleteMilestoneRecords(milestoneId, connection);
                    }
                }
            }

            // Delete Milestones
            using (SQLiteCommand command = new SQLiteCommand(
                "DELETE FROM Milestones WHERE ProjectId = @ProjectId", connection))
            {
                command.Parameters.AddWithValue("@ProjectId", projectId);
                command.ExecuteNonQuery();
            }
        }

        private void DeleteMilestoneRecords(int milestoneId, SQLiteConnection connection)
        {
            // Delete Tasks and Dependencies
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

        private ProjectModel CreateProjectFromReader(SQLiteDataReader reader)
        {
            return new ProjectModel
            {
                ProjectId = reader.GetInt32(reader.GetOrdinal("ProjectId")),
                ProjectName = reader.GetString(reader.GetOrdinal("ProjectName")),
                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ?
                    null : reader.GetString(reader.GetOrdinal("Description")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                Status = (ProjectStatus)Enum.Parse(typeof(ProjectStatus),
                    reader.GetString(reader.GetOrdinal("Status")))
            };
        }
    }
}