<%@ Page Async="true" Title="Chat Application" Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="WebAI.ChatForm" %>

<!DOCTYPE html>

<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />

    <link href="~/favicon.ico" rel="shortcut icon" type="image/x-icon" />

    <%--    <link href="css/Styles.css" rel="stylesheet" />--%>
    <link rel="stylesheet" href="https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.3/css/all.min.css">
    <style>
    body, html {
        height: 100%;
        margin: 0;
        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        background-color: #f0f2f5;
    }

    .container {
        display: flex;
        height: 100vh;
        padding: 20px;
        gap: 20px;
    }

    .sidebar {
        flex: 0 0 20%;
        background-color: #ffffff;
        padding: 20px;
        border-radius: 10px;
        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
    }

    .main-content {
        flex: 1;
        display: flex;
        flex-direction: column;
        gap: 20px;
        margin:20px;
    }

    .chat-header, .outline-header {
        padding: 20px;
        background-color: #007bff;
        color: white;
        border-radius: 10px;
        font-weight: bold;
        display: flex;
        justify-content: space-between;
        align-items: center;
    }

    .chat-messages {
        flex: 1;
        background-color: #ffffff;
        padding: 20px;
        border-radius: 10px;
        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        overflow-y: auto;
        height: 600px;
    }

    .chat-input {
        display: flex;
        gap: 10px;
        margin-top: 10px;
    }

    .chat-input textarea {
        flex: 1;
        resize: none;
        border: 1px solid #dee2e6;
        border-radius: 10px;
        padding: 10px;
        font-size: 1rem;
    }

    .chat-input button {
        padding: 10px 20px;
        border: none;
        border-radius: 10px;
        background-color: #007bff;
        color: white;
        font-weight: bold;
        cursor: pointer;
    }

    .chat-input button:hover {
        background-color: #0056b3;
    }

    .outline-panel {
        background-color: #f8f9fa;
        border-radius: 10px;
        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        padding: 15px;
    }

    .outline-header button {
        border: none;
        background-color: transparent;
        color: white;
        font-size: 1.2rem;
        cursor: pointer;
    }

    .chatSessions-panel{
        margin-bottom: 20px;
    }

    /* General Styles for Repeater */
    .outline-item {
        display: flex;
        align-items: center;
        padding: 12px;
        margin: 8px 0;
        background-color: #ffffff;
        border: 1px solid #e0e0e0;
        border-radius: 8px;
        box-shadow: 0 2px 5px rgba(0, 0, 0, 0.1);
        transition: box-shadow 0.3s ease, background-color 0.3s ease;
    }

    .outline-item:hover {
        box-shadow: 0 4px 10px rgba(0, 0, 0, 0.15);
        background-color: #f9f9f9;
    }

    /* Checkbox Styles */
    .outline-checkbox {
        font-size: 16px;
        color: #333;
        cursor: pointer;
        transition: color 0.3s ease;
        display: flex;
        align-items: center;
    }

    .outline-checkbox input[type="checkbox"] {
        accent-color: #007bff;
        width: 24px;
        height: 24px;
        margin-right: 10px;
        border-radius: 4px;
        border: 2px solid #007bff;
        transition: background-color 0.3s ease, border-color 0.3s ease;
    }

    .outline-checkbox input[type="checkbox"]:checked {
        background-color: #007bff;
        border-color: #0056b3;
    }

    .outline-checkbox:hover {
        color: #007bff;
    }

    /* Optional: Adding Animation to Checkbox */
    .outline-checkbox input[type="checkbox"]:checked {
        transform: scale(1.1);
        transition: transform 0.2s ease;
    }

    </style>

</head>
<body>
    <form runat="server">
        <div class="chat-application row">
            <div class="sidebar col-4">
                <div class="chatSessions-panel col-12">
                    <h5>Chat Sessions</h5>
                    <asp:DropDownList ID="cmbChatSessions" runat="server" CssClass="form-control mb-2" AutoPostBack="true" OnSelectedIndexChanged="cmbChatSessions_SelectedIndexChanged">
                    </asp:DropDownList>
                    <asp:Button ID="btnNewChat" runat="server" Text="New Chat" CssClass="btn btn-success btn-block" OnClick="btnNewChat_Click" />
                </div>
                <div class="outline-panel col-12" id="outlinePanel">
                    <div class="outline-header">
                        <h5>Project Outline</h5>
                        <button id="closeOutline" class="btn btn-sm btn-outline-secondary">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                    <div class="outline-content">
                        <asp:Repeater ID="rptOutline" runat="server">
                            <ItemTemplate>
                                <div class="outline-item">
                                    <asp:CheckBox ID="chkItem" runat="server" Text='<%# Eval("ItemText") %>' Checked='<%# Eval("IsChecked") %>'
                                        data-item-id='<%# Eval("Id") %>' CssClass="outline-checkbox" />
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </div>
                </div>
            </div>

            <div class="main-content col-8">
                <div class="chat-container">
                    <div class="chat-header">
                        <h2>Chat Application</h2>
                    </div>
                    <div class="chat-messages" id="chatMessages">
                        <asp:Literal ID="litChatMessages" runat="server"></asp:Literal>
                    </div>
                    <div class="chat-input">
                        <asp:TextBox ID="txtUserInput" TextMode="MultiLine" runat="server" Rows="3" CssClass="form-control" placeholder="Type your message..."></asp:TextBox>
                        <asp:Button ID="btnSend" runat="server" Text="Send" CssClass="btn btn-primary" OnClick="btnSend_Click" />
                    </div>
                </div>
            </div>
        </div>

        <script src="https://code.jquery.com/jquery-3.3.1.min.js"></script>
        <script src="https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/js/bootstrap.bundle.min.js"></script>
        <script>
            $(document).ready(function () {
                function updateOutlineVisibility() {
                    var sessionSelected = $('#<%= cmbChatSessions.ClientID %>').val() !== '';
                    if (sessionSelected) {
                        $('#outlinePanel').addClass('active');
                        $('.main-content').addClass('with-outline');
                    } else {
                        $('#outlinePanel').removeClass('active');
                        $('.main-content').removeClass('with-outline');
                    }
                }

                $('#<%= cmbChatSessions.ClientID %>').change(updateOutlineVisibility);

                $('#closeOutline').click(function () {
                    $('#outlinePanel').removeClass('active');
                    $('.main-content').removeClass('with-outline');
                });

                function checkOffOutlineItems() {
                    $('.outline-checkbox').each(function () {
                        var $checkbox = $(this);
                        var itemId = $checkbox.data('item-id');
                        var sessionId = $('#<%= cmbChatSessions.ClientID %>').val();

                        // Simulate AI checking off the item
                        $checkbox.prop('checked', true);

                        // Send update to server
                        $.ajax({
                            url: '<%= ResolveUrl("~/Default.aspx/UpdateOutlineItem") %>',
                            type: 'POST',
                            data: JSON.stringify({ sessionId: sessionId, itemId: itemId, isChecked: true }),
                            contentType: 'application/json; charset=utf-8',
                            dataType: 'json',
                            success: function (response) {
                                console.log('Outline item ' + itemId + ' checked off successfully');
                            },
                            error: function (error) {
                                console.error('Error updating outline item ' + itemId + ':', error);
                            }
                        });
                    });
                }

                // Call the function to simulate AI checking off items
                checkOffOutlineItems();

                updateOutlineVisibility();
            });
        </script>
    </form>
</body>
</html>
