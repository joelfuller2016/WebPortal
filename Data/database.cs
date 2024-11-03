using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

public class SQLiteChatStorage
{
    private readonly string _connectionString;
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    public SQLiteChatStorage()
    {
        try
        {
            string dbFile = Path.Combine(AppDomain.CurrentDomain.GetData("DataDirectory").ToString(), "database_file.db");
            if (!File.Exists(dbFile))
            {
                SQLiteConnection.CreateFile(dbFile);
            }

            _connectionString = $"Data Source={dbFile};Version=3;";
            InitializeDatabase();
        }
        catch (Exception ex)
        {
            log.Error("Error initializing SQLiteChatStorage", ex);
            throw;
        }
    }

    private void InitializeDatabase()
    {
        try
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                var createSessionsTableQuery = @"
                    CREATE TABLE IF NOT EXISTS ChatSessions (
                        SessionId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SessionName TEXT UNIQUE,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";

                var createMessagesTableQuery = @"
                    CREATE TABLE IF NOT EXISTS ChatMessages (
                        MessageId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SessionName TEXT,
                        UserId TEXT,
                        Message TEXT,
                        Sender TEXT,
                        Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (SessionName) REFERENCES ChatSessions(SessionName)
                    )";

                var createOutlinesTableQuery = @"
                    CREATE TABLE IF NOT EXISTS ChatOutlines (
                        OutlineId INTEGER PRIMARY KEY AUTOINCREMENT,
                        SessionName TEXT,
                        OutlineJson TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (SessionName) REFERENCES ChatSessions(SessionName)
                    )";

                using (var command = new SQLiteCommand(createSessionsTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
                using (var command = new SQLiteCommand(createMessagesTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
                using (var command = new SQLiteCommand(createOutlinesTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            log.Error("Error initializing database", ex);
            throw;
        }
    }

    public int StartNewSession(string sessionName)
    {
        try
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string insertQuery = "INSERT INTO ChatSessions (SessionName) VALUES (@SessionName); SELECT last_insert_rowid()";
                using (var command = new SQLiteCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@SessionName", sessionName);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error starting new session: {sessionName}", ex);
            throw;
        }
    }

    public void SaveMessage(string SessionName, string userId, string message, string sender)
    {
        try
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string insertQuery = "INSERT INTO ChatMessages (SessionName, UserId, Message, Sender) VALUES (@SessionName, @UserId, @Message, @Sender)";
                using (var command = new SQLiteCommand(insertQuery, connection))
                {
                    command.Parameters.AddWithValue("@SessionName", SessionName);
                    command.Parameters.AddWithValue("@UserId", userId);
                    command.Parameters.AddWithValue("@Message", message);
                    command.Parameters.AddWithValue("@Sender", sender);
                    command.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error saving message for session: {SessionName}", ex);
            throw;
        }
    }

    public int GetSessionIdByName(string sessionName)
    {
        try
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT SessionId FROM ChatSessions WHERE SessionName = @SessionName";
                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@SessionName", sessionName);
                    var result = command.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : -1;
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error getting session ID for session name: {sessionName}", ex);
            throw;
        }
    }

    public List<string> GetChatHistory(string SessionName)
    {
        var messages = new List<string>();
        try
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string selectQuery = "SELECT UserId, Message, Sender, Timestamp FROM ChatMessages WHERE SessionName = @SessionName ORDER BY Timestamp";
                using (var command = new SQLiteCommand(selectQuery, connection))
                {
                    command.Parameters.AddWithValue("@SessionName", SessionName);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string timestamp = reader.GetDateTime(3).ToString("yyyy-MM-dd HH:mm:ss");
                            string message = $"{timestamp} {reader.GetString(2)} ({reader.GetString(0)}): {reader.GetString(1)}";
                            messages.Add(message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error getting chat history for session: {SessionName}", ex);
            throw;
        }
        return messages;
    }

    public List<SessionData> GetAllSessionNames()
    {
        var sessions = new List<SessionData>();
        try
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT SessionId, SessionName FROM ChatSessions";
                using (var command = new SQLiteCommand(query, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            sessions.Add(new SessionData { SessionId = Convert.ToInt32(reader["SessionId"]), SessionName = reader["SessionName"].ToString() });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.Error("Error getting all session names", ex);
            throw;
        }
        return sessions;
    }

    public void SaveOutline(string SessionName, string outlineJson)
    {
        try
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string upsertQuery = @"
                    INSERT OR REPLACE INTO ChatOutlines (SessionName, OutlineJson)
                    VALUES (@SessionName, @OutlineJson)";
                using (var command = new SQLiteCommand(upsertQuery, connection))
                {
                    command.Parameters.AddWithValue("@SessionName", SessionName);
                    command.Parameters.AddWithValue("@OutlineJson", outlineJson);
                    command.ExecuteNonQuery();
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error saving outline for session: {SessionName}", ex);
            throw;
        }
    }

    public string GetOutline(string SessionName)
    {
        try
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string selectQuery = "SELECT OutlineJson FROM ChatOutlines WHERE SessionName = @SessionName";
                using (var command = new SQLiteCommand(selectQuery, connection))
                {
                    command.Parameters.AddWithValue("@SessionName", SessionName);
                    var result = command.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            log.Error($"Error getting outline for session: {SessionName}", ex);
            throw;
        }
    }
}

public class SessionData
{
    public int SessionId { get; set; }
    public string SessionName { get; set; }
}
