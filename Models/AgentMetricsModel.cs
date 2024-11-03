using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace WebAI.Models
{
    public class AgentMetricsModel
    {
        public int MetricId { get; set; }
        public string AgentId { get; set; }
        public int TaskId { get; set; }
        public string Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public double? SuccessRate { get; set; }
        public string Notes { get; set; }
        public List<string> Errors { get; private set; }
        public Dictionary<string, double> PerformanceMetrics { get; private set; }

        // Navigation properties
        public TaskModel Task { get; set; }

        public AgentMetricsModel()
        {
            Errors = new List<string>();
            PerformanceMetrics = new Dictionary<string, double>();
            StartTime = DateTime.UtcNow;
        }

        public void StartTask()
        {
            StartTime = DateTime.UtcNow;
            Status = "InProgress";
            Errors.Clear();
            PerformanceMetrics.Clear();
        }

        public void CompleteTask(bool success, string notes)
        {
            CompletionTime = DateTime.UtcNow;
            Status = success ? "Completed" : "Failed";
            Notes = notes;
            CalculateSuccessRate();
        }

        public void AddError(string error)
        {
            if (string.IsNullOrEmpty(error))
                throw new ArgumentNullException("error");

            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            Errors.Add(string.Format("{0}: {1}", timestamp, error));
        }

        public void AddPerformanceMetric(string metricName, double value)
        {
            if (string.IsNullOrEmpty(metricName))
                throw new ArgumentNullException("metricName");

            PerformanceMetrics[metricName] = value;
        }

        private void CalculateSuccessRate()
        {
            if (string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                // Calculate success rate based on errors and performance metrics
                double errorPenalty = Errors.Count * 0.1; // 10% penalty per error
                double baseRate = 1.0;

                // Adjust base rate based on performance metrics if they exist
                if (PerformanceMetrics.Count > 0)
                {
                    double sum = 0;
                    foreach (double value in PerformanceMetrics.Values)
                    {
                        sum += value;
                    }
                    baseRate = sum / PerformanceMetrics.Count;
                }

                double rate = baseRate - errorPenalty;
                if (rate < 0) rate = 0;
                if (rate > 1) rate = 1;

                SuccessRate = rate * 100;
            }
            else
            {
                SuccessRate = 0;
            }
        }
    }

    public class AgentMetricsDTO
    {
        public int MetricId { get; set; }
        public string AgentId { get; set; }
        public int TaskId { get; set; }
        public string TaskTitle { get; set; }
        public string Status { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public double? SuccessRate { get; set; }
        public string Notes { get; set; }
        public List<string> Errors { get; set; }
        public Dictionary<string, double> PerformanceMetrics { get; set; }
        public AgentPerformanceSummary PerformanceSummary { get; set; }

        public static AgentMetricsDTO FromModel(AgentMetricsModel model)
        {
            if (model == null)
                throw new ArgumentNullException("model");

            AgentMetricsDTO dto = new AgentMetricsDTO();
            dto.MetricId = model.MetricId;
            dto.AgentId = model.AgentId;
            dto.TaskId = model.TaskId;
            dto.TaskTitle = model.Task != null ? model.Task.Title : null;
            dto.Status = model.Status;
            dto.StartTime = model.StartTime;
            dto.CompletionTime = model.CompletionTime;
            dto.Duration = CalculateDuration(model);
            dto.SuccessRate = model.SuccessRate;
            dto.Notes = model.Notes;
            dto.Errors = new List<string>(model.Errors);
            dto.PerformanceMetrics = new Dictionary<string, double>(model.PerformanceMetrics);
            dto.PerformanceSummary = new AgentPerformanceSummary(model);

            return dto;
        }

        private static TimeSpan? CalculateDuration(AgentMetricsModel model)
        {
            if (model.StartTime.HasValue && model.CompletionTime.HasValue)
            {
                return model.CompletionTime.Value - model.StartTime.Value;
            }
            return null;
        }
    }

    public class AgentPerformanceSummary
    {
        public bool IsCompleted { get; set; }
        public string PerformanceLevel { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Recommendations { get; set; }
        public Dictionary<string, string> MetricAnalysis { get; set; }

        public AgentPerformanceSummary(AgentMetricsModel metrics)
        {
            if (metrics == null)
                throw new ArgumentNullException("metrics");

            IsCompleted = string.Equals(metrics.Status, "Completed", StringComparison.OrdinalIgnoreCase);
            ErrorCount = metrics.Errors.Count;
            Recommendations = new List<string>();
            MetricAnalysis = new Dictionary<string, string>();

            AnalyzePerformance(metrics);
        }

        private void AnalyzePerformance(AgentMetricsModel metrics)
        {
            // Determine performance level
            if (metrics.SuccessRate.HasValue)
            {
                double rate = metrics.SuccessRate.Value;
                if (rate >= 90)
                    PerformanceLevel = "Excellent";
                else if (rate >= 75)
                    PerformanceLevel = "Good";
                else if (rate >= 60)
                    PerformanceLevel = "Fair";
                else
                    PerformanceLevel = "Poor";
            }
            else
            {
                PerformanceLevel = "Unknown";
            }

            // Generate recommendations
            if (ErrorCount > 0)
            {
                Recommendations.Add(string.Format("Review {0} errors for improvement opportunities", ErrorCount));
            }

            foreach (KeyValuePair<string, double> metric in metrics.PerformanceMetrics)
            {
                MetricAnalysis[metric.Key] = AnalyzeMetric(metric.Key, metric.Value);
            }

            // Add time-based recommendations
            if (metrics.StartTime.HasValue && metrics.CompletionTime.HasValue)
            {
                TimeSpan duration = metrics.CompletionTime.Value - metrics.StartTime.Value;
                if (duration.TotalMinutes > 30)
                {
                    Recommendations.Add("Consider optimizing for faster completion time");
                }
            }
        }

        private string AnalyzeMetric(string metricName, double value)
        {
            if (value >= 0.9)
                return string.Format("{0} performance is excellent", metricName);
            if (value >= 0.7)
                return string.Format("{0} performance is good", metricName);
            if (value >= 0.5)
                return string.Format("{0} performance needs improvement", metricName);

            return string.Format("{0} performance is below expected levels", metricName);
        }
    }
}