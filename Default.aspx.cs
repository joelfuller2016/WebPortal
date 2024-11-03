using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Services;
using System.Web.UI;

namespace WebAI
{
    public partial class ChatForm : Page
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly SQLiteChatStorage chatStorage = new SQLiteChatStorage();
        private static readonly OpenAIService openAIService = new OpenAIService();

        public string getcurrentSessionName()
        {
            return Guid.NewGuid().ToString() + "_Chat_" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }
        public string currentSessionName;

        protected void Page_Init(object sender, EventArgs e)
        {
            currentSessionName = getcurrentSessionName();
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                LoadChatSessions();
            }
        }

        private void LoadChatSessions()
        {
            try
            {
                var sessions = chatStorage.GetAllSessionNames();

                if (sessions != null && sessions.Count > 0)
                {
                    cmbChatSessions.DataSource = sessions;
                    cmbChatSessions.DataTextField = "SessionName";
                    cmbChatSessions.DataValueField = "SessionId";
                    cmbChatSessions.DataBind();

                    LoadChatHistory(currentSessionName);
                    LoadOutline(currentSessionName);
                }
            }
            catch (Exception ex)
            {
                log.Error("Error loading chat sessions", ex);
                // Display error message to user
                litChatMessages.Text = "An error occurred while loading chat sessions. Please try again later.";
            }
        }

        protected void cmbChatSessions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbChatSessions.SelectedValue != "")
            {
                currentSessionName = cmbChatSessions.SelectedValue;
                LoadChatHistory(currentSessionName);
                LoadOutline(currentSessionName);
            }
        }

        protected void btnNewChat_Click(object sender, EventArgs e)
        {
            try
            {
                currentSessionName = getcurrentSessionName();
                LoadChatSessions();
                cmbChatSessions.SelectedValue = null;
            }
            catch (Exception ex)
            {
                log.Error("Error creating new chat session", ex);
                // Display error message to user
                litChatMessages.Text = "An error occurred while creating a new chat session. Please try again later.";
            }
        }

        protected async void btnSend_Click(object sender, EventArgs e)
        {
            string userMessage = txtUserInput.Text.Trim();
            if (!string.IsNullOrEmpty(userMessage))
            {
                try
                {
                    AddMessageToChat("You", userMessage);
                    chatStorage.SaveMessage(currentSessionName, "user", userMessage, "user");

                    string aiResponse = await openAIService.GetAIResponseAsync(userMessage);
                    AddMessageToChat("AI", aiResponse);
                    chatStorage.SaveMessage(currentSessionName, "ai", aiResponse, "assistant");

                    txtUserInput.Text = string.Empty;

                    await GenerateAndSaveOutlineAsync();
                }
                catch (Exception ex)
                {
                    log.Error("Error processing message", ex);
                    AddMessageToChat("System", "An error occurred while processing your message. Please try again.");
                }
            }
        }

        private void AddMessageToChat(string sender, string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            //string formattedMessage = $"<div class='chat-message {(isUser ? "user-message" : "bot-message")}'>{timestamp} {sender}: {message}</div>";
            string formattedMessage = $"<div class='chat-message'> <strong>{sender} {timestamp} :</strong> {message}</div>";
            litChatMessages.Text += formattedMessage;
        }

        private void LoadChatHistory(string sessionName)
        {
            try
            {
                litChatMessages.Text = string.Empty;
                var messages = chatStorage.GetChatHistory(sessionName);
                foreach (var message in messages)
                {
                    litChatMessages.Text += $"<p>{message}</p>";
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error loading chat history for session {sessionName}", ex);
                litChatMessages.Text = "An error occurred while loading chat history. Please try again later.";
            }
        }

        private async Task GenerateAndSaveOutlineAsync()
        {
            try
            {
                var chatHistory = chatStorage.GetChatHistory(currentSessionName);
                string conversationContext = string.Join("\n", chatHistory); 

                var outlineItems = await openAIService.GenerateOutlineAsync(conversationContext);
                string outlineJson = JsonConvert.SerializeObject(outlineItems);

                chatStorage.SaveOutline(currentSessionName, outlineJson);
                DisplayOutline(outlineItems);
            }
            catch (Exception ex)
            {
                log.Error($"Error generating and saving outline for session {currentSessionName}", ex);
                // Display error message to the user
            }
        }

        private void DisplayOutline(List<OutlineItem> outlineItems)
        {
            rptOutline.DataSource = outlineItems;
            rptOutline.DataBind();
        }

        private void LoadOutline(string sessionName)
        {
            try
            {
                string outlineJson = chatStorage.GetOutline(sessionName);
                if (!string.IsNullOrEmpty(outlineJson))
                {
                    var outlineItems = JsonConvert.DeserializeObject<List<OutlineItem>>(outlineJson);
                    DisplayOutline(outlineItems);
                }
                else
                {
                    rptOutline.DataSource = null;
                    rptOutline.DataBind();
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error loading outline for session {sessionName}", ex);
                // Display error message to the user
            }
        }
        [WebMethod]
        public static void UpdateOutlineItem(string sessionName, int itemId, bool isChecked)
        {
            try
            {
                string outlineJson = chatStorage.GetOutline(sessionName);
                if (!string.IsNullOrEmpty(outlineJson))
                {
                    var outlineItems = JsonConvert.DeserializeObject<List<OutlineItem>>(outlineJson);
                    var itemToUpdate = outlineItems.Find(item => item.Id == itemId);
                    if (itemToUpdate != null)
                    {
                        itemToUpdate.IsChecked = isChecked;
                        string updatedOutlineJson = JsonConvert.SerializeObject(outlineItems);
                        chatStorage.SaveOutline(sessionName, updatedOutlineJson);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error updating outline item for session {sessionName}, item {itemId}", ex);
                // Since this is called via AJAX, we can't directly notify the user of the error
                // You might want to implement a way to show errors to the user in the AJAX success callback
            }
        }
    }
}