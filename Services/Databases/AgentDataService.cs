using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Data;
using WebAI.Models;

namespace WebAI.Services.Database
{
    public class AgentDataService
    {
        private readonly string _connectionString;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public AgentDataService(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");

            _connectionString = connectionString;
        }

        public void SaveAgentMetrics(AgentMetricsModel metrics)
        {
            if (metrics == null)
                throw new ArgumentNullException("metrics");

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    using (SQLiteTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            SaveMetricsBase(metrics, connection);
                            SavePerformanceMetrics(metrics, connection);
                            SaveErrorLogs(metrics, connection);

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
                log.Error("Error saving agent metrics", ex);
                throw;
            }
        }

        private void SaveMetricsBase(AgentMetricsModel metrics, SQLiteConnection connection)
        {
            string insertQuery = @"
                INSERT INTO AgentMetrics 
                (AgentId, TaskId, Status, StartTime, CompletionTime, SuccessRate, Notes)
                VALUES 
                (@AgentId, @TaskId, @Status, @StartTime, @CompletionTime, @SuccessRate, @Notes)";

            using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
            {
                command.Parameters.AddWithValue("@AgentId", metrics.AgentId);
                command.Parameters.AddWithValue("@TaskId", metrics.TaskId);
                command.Parameters.AddWithValue("@Status", metrics.Status);
                command.Parameters.AddWithValue("@StartTime", (object)metrics.StartTime ?? DBNull.Value);
                command.Parameters.AddWithValue("@CompletionTime", (object)metrics.CompletionTime ?? DBNull.Value);
                command.Parameters.AddWithValue("@SuccessRate", (object)metrics.SuccessRate ?? DBNull.Value);
                command.Parameters.AddWithValue("@Notes", (object)metrics.Notes ?? DBNull.Value);

                command.ExecuteNonQuery();
            }
        }

        private void SavePerformanceMetrics(AgentMetricsModel metrics, SQLiteConnection connection)
        {
            if (metrics.PerformanceMetrics != null && metrics.PerformanceMetrics.Count > 0)
            {
                string insertQuery = @"
                    INSERT INTO AgentPerformanceMetrics 
                    (MetricId, MetricName, MetricValue) 
                    VALUES 
                    (@MetricId, @MetricName, @MetricValue)";

                using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                {
                    foreach (KeyValuePair<string, double> metric in metrics.PerformanceMetrics)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@MetricId", metrics.MetricId);
                        command.Parameters.AddWithValue("@MetricName", metric.Key);
                        command.Parameters.AddWithValue("@MetricValue", metric.Value);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private void SaveErrorLogs(AgentMetricsModel metrics, SQLiteConnection connection)
        {
            if (metrics.Errors != null && metrics.Errors.Count > 0)
            {
                string insertQuery = @"
                    INSERT INTO AgentErrorLogs 
                    (MetricId, ErrorMessage, Timestamp) 
                    VALUES 
                    (@MetricId, @ErrorMessage, @Timestamp)";

                using (SQLiteCommand command = new SQLiteCommand(insertQuery, connection))
                {
                    foreach (string error in metrics.Errors)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@MetricId", metrics.MetricId);
                        command.Parameters.AddWithValue("@ErrorMessage", error);
                        command.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        public List<AgentMetricsModel> GetAgentMetrics(string agentId, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<AgentMetricsModel> metrics = new List<AgentMetricsModel>();

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT MetricId, AgentId, TaskId, Status, StartTime, 
                               CompletionTime, SuccessRate, Notes 
                        FROM AgentMetrics 
                        WHERE AgentId = @AgentId";

                    if (startDate.HasValue)
                        selectQuery += " AND StartTime >= @StartDate";
                    if (endDate.HasValue)
                        selectQuery += " AND StartTime <= @EndDate";

                    selectQuery += " ORDER BY StartTime DESC";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@AgentId", agentId);
                        if (startDate.HasValue)
                            command.Parameters.AddWithValue("@StartDate", startDate.Value);
                        if (endDate.HasValue)
                            command.Parameters.AddWithValue("@EndDate", endDate.Value);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                AgentMetricsModel metric = CreateMetricsFromReader(reader);
                                LoadMetricDetails(metric, connection);
                                metrics.Add(metric);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting metrics for agent: {0}", agentId), ex);
                throw;
            }

            return metrics;
        }

        private void LoadMetricDetails(AgentMetricsModel metric, SQLiteConnection connection)
        {
            LoadPerformanceMetrics(metric, connection);
            LoadErrorLogs(metric, connection);
        }

        private void LoadPerformanceMetrics(AgentMetricsModel metric, SQLiteConnection connection)
        {
            string selectQuery = @"
                SELECT MetricName, MetricValue 
                FROM AgentPerformanceMetrics 
                WHERE MetricId = @MetricId";

            using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
            {
                command.Parameters.AddWithValue("@MetricId", metric.MetricId);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader.GetString(0);
                        double value = reader.GetDouble(1);
                        metric.AddPerformanceMetric(name, value);
                    }
                }
            }
        }

        private void LoadErrorLogs(AgentMetricsModel metric, SQLiteConnection connection)
        {
            string selectQuery = @"
                SELECT ErrorMessage 
                FROM AgentErrorLogs 
                WHERE MetricId = @MetricId 
                ORDER BY Timestamp";

            using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
            {
                command.Parameters.AddWithValue("@MetricId", metric.MetricId);
                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        metric.AddError(reader.GetString(0));
                    }
                }
            }
        }

        private AgentMetricsModel CreateMetricsFromReader(SQLiteDataReader reader)
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

        public AgentMetricsModel GetLatestMetrics(string agentId)
        {
            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string selectQuery = @"
                        SELECT MetricId, AgentId, TaskId, Status, StartTime, 
                               CompletionTime, SuccessRate, Notes 
                        FROM AgentMetrics 
                        WHERE AgentId = @AgentId 
                        ORDER BY StartTime DESC 
                        LIMIT 1";

                    using (SQLiteCommand command = new SQLiteCommand(selectQuery, connection))
                    {
                        command.Parameters.AddWithValue("@AgentId", agentId);

                        using (SQLiteDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                AgentMetricsModel metric = CreateMetricsFromReader(reader);
                                LoadMetricDetails(metric, connection);
                                return metric;
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                log.Error(string.Format("Error getting latest metrics for agent: {0}", agentId), ex);
                throw;
            }
        }
    }
}