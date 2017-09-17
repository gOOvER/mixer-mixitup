﻿using Mixer.Base.Model.Chat;
using Mixer.Base.Model.User;
using MixItUp.Base;
using MixItUp.Base.Chat;
using MixItUp.Base.Commands;
using MixItUp.Base.ViewModel.Chat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MixItUp.WPF.Controls.Chat
{
    /// <summary>
    /// Interaction logic for ChatControl.xaml
    /// </summary>
    public partial class ChatControl : MainControlBase
    {
        public bool EnableCommands { get; set; }

        public ObservableCollection<ChatUserControl> UserControls = new ObservableCollection<ChatUserControl>();

        public ObservableCollection<ChatMessageControl> MessageControls = new ObservableCollection<ChatMessageControl>();
        public List<ChatMessageViewModel> Messages = new List<ChatMessageViewModel>();

        private CancellationTokenSource backgroundThreadCancellationTokenSource = new CancellationTokenSource();

        private List<ChatCommand> PreMadeChatCommands = new List<ChatCommand>();

        private bool blockChat = false;

        public ChatControl()
        {
            InitializeComponent();

            this.PreMadeChatCommands.Add(new UptimeChatCommand());
            this.PreMadeChatCommands.Add(new GameChatCommand());
            this.PreMadeChatCommands.Add(new TitleChatCommand());
            this.PreMadeChatCommands.Add(new TimeoutChatCommand());
            this.PreMadeChatCommands.Add(new PurgeChatCommand());
            this.PreMadeChatCommands.Add(new StreamerAgeChatCommand());
            this.PreMadeChatCommands.Add(new MixerAgeChatCommand());
            this.PreMadeChatCommands.Add(new FollowAgeChatCommand());
            this.PreMadeChatCommands.Add(new SparksChatCommand());
            this.PreMadeChatCommands.Add(new QuoteChatCommand());
            this.PreMadeChatCommands.Add(new GiveawayChatCommand());
        }

        protected override async Task InitializeInternal()
        {
            this.Window.Closing += Window_Closing;

            if (await ChannelSession.InitializeChatClient())
            {
                this.ChatList.ItemsSource = this.MessageControls;
                this.UserList.ItemsSource = this.UserControls;

                this.RefreshViewerAndChatCount();

                foreach (ChatUserModel user in await ChannelSession.MixerConnection.Chats.GetUsers(ChannelSession.Channel))
                {
                    this.AddUser(new ChatUserViewModel(user));
                }

                ChannelSession.ChatClient.OnClearMessagesOccurred += ChatClient_OnClearMessagesOccurred;
                ChannelSession.ChatClient.OnDeleteMessageOccurred += ChatClient_OnDeleteMessageOccurred;
                ChannelSession.ChatClient.OnDisconnectOccurred += ChatClient_OnDisconnectOccurred;
                ChannelSession.ChatClient.OnMessageOccurred += ChatClient_OnMessageOccurred;
                ChannelSession.ChatClient.OnPollEndOccurred += ChatClient_OnPollEndOccurred;
                ChannelSession.ChatClient.OnPollStartOccurred += ChatClient_OnPollStartOccurred;
                ChannelSession.ChatClient.OnPurgeMessageOccurred += ChatClient_OnPurgeMessageOccurred;
                ChannelSession.ChatClient.OnUserJoinOccurred += ChatClient_OnUserJoinOccurred;
                ChannelSession.ChatClient.OnUserLeaveOccurred += ChatClient_OnUserLeaveOccurred;
                ChannelSession.ChatClient.OnUserTimeoutOccurred += ChatClient_OnUserTimeoutOccurred;
                ChannelSession.ChatClient.OnUserUpdateOccurred += ChatClient_OnUserUpdateOccurred;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(async () => { await this.ChannelRefreshBackground(); }, this.backgroundThreadCancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(async () => { await this.TimerCommandsBackground(); }, this.backgroundThreadCancellationTokenSource.Token);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
        }

        private async Task ChannelRefreshBackground()
        {
            while (!this.backgroundThreadCancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    await ChannelSession.RefreshChannel();
                    this.RefreshViewerAndChatCount();

                    this.backgroundThreadCancellationTokenSource.Token.ThrowIfCancellationRequested();

                    await Task.Delay(1000 * 30);
                }
                catch (Exception) { }
            }

            this.backgroundThreadCancellationTokenSource.Token.ThrowIfCancellationRequested();
        }

        private async Task TimerCommandsBackground()
        {
            int timerCommandIndex = 0;
            while (!this.backgroundThreadCancellationTokenSource.Token.IsCancellationRequested)
            {               
                int startMessageCount = this.Messages.Count;
                try
                {
                    DateTimeOffset startTime = DateTimeOffset.Now;

                    Thread.Sleep(1000 * 60 * ChannelSession.Settings.TimerCommandsInterval);
                    if (ChannelSession.Settings.TimerCommands.Count > 0)
                    {
                        TimerCommand command = ChannelSession.Settings.TimerCommands[timerCommandIndex];

                        while ((this.Messages.Count - startMessageCount) <= ChannelSession.Settings.TimerCommandsMinimumMessages)
                        {
                            Thread.Sleep(1000 * 10);
                        }

                        await command.Perform();

                        timerCommandIndex++;
                        timerCommandIndex = timerCommandIndex % ChannelSession.Settings.TimerCommands.Count;
                    }
                }
                catch (ThreadAbortException) { return; }
                catch (Exception) { }
            }

            this.backgroundThreadCancellationTokenSource.Token.ThrowIfCancellationRequested();
        }

        private async void ChatClearMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            await ChannelSession.ChatClient.ClearMessages();
        }

        private void BlockChatButton_Click(object sender, RoutedEventArgs e)
        {
            this.blockChat = true;
            this.BlockChatButton.Visibility = Visibility.Collapsed;
            this.EnableChatButton.Visibility = Visibility.Visible;
        }

        private void EnableChatButton_Click(object sender, RoutedEventArgs e)
        {
            this.blockChat = false;
            this.BlockChatButton.Visibility = Visibility.Visible;
            this.EnableChatButton.Visibility = Visibility.Collapsed;
        }

        private void ChatMessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.SendChatMessageButton_Click(this, new RoutedEventArgs());
                this.ChatMessageTextBox.Focus();
            }
        }

        private async void SendChatMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(this.ChatMessageTextBox.Text))
            {
                string message = this.ChatMessageTextBox.Text;
                this.ChatMessageTextBox.Text = string.Empty;
                await this.Window.RunAsyncOperation(async () =>
                {
                    await ChannelSession.ChatClient.SendMessage(message);
                });
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.backgroundThreadCancellationTokenSource.Cancel();
        }

        #region Chat Update Methods

        private void AddUser(ChatUserViewModel user)
        {
            if (!ChannelSession.ChatUsers.ContainsKey(user.ID))
            {
                ChannelSession.ChatUsers.Add(user.ID, user);
                var orderedUsers = ChannelSession.ChatUsers.Values.OrderByDescending(u => u.PrimaryRole).ThenBy(u => u.UserName).ToList();
                this.UserControls.Insert(orderedUsers.IndexOf(user), new ChatUserControl(user));

                this.RefreshViewerAndChatCount();
            }
        }

        private void RemoveUser(ChatUserViewModel user)
        {
            ChatUserControl userControl = this.UserControls.FirstOrDefault(u => u.User.Equals(user));
            if (userControl != null)
            {
                this.UserControls.Remove(userControl);
                ChannelSession.ChatUsers.Remove(userControl.User.ID);

                this.RefreshViewerAndChatCount();
            }
        }

        private void RefreshViewerAndChatCount()
        {
            this.ViewersCountTextBlock.Text = ChannelSession.Channel.viewersCurrent.ToString();
            this.ChatCountTextBlock.Text = ChannelSession.ChatUsers.Count.ToString();
        }

        private async void AddMessage(ChatMessageViewModel message)
        {
            ChatMessageControl messageControl = new ChatMessageControl(message);

            this.Messages.Add(message);
            this.MessageControls.Add(messageControl);

            if (this.blockChat && !message.ID.Equals(Guid.Empty))
            {
                messageControl.DeleteMessage();
                await ChannelSession.ChatClient.DeleteMessage(message.ID);
            }
            else if (this.EnableCommands && ChatMessageCommand.IsCommand(message) && !message.User.Roles.Contains(UserRole.Banned))
            {
                ChatMessageCommand messageCommand = new ChatMessageCommand(message);

                ChatCommand command = this.PreMadeChatCommands.FirstOrDefault(c => c.ContainsCommand(messageCommand.CommandName));
                if (command == null)
                {
                    command = ChannelSession.Settings.ChatCommands.FirstOrDefault(c => c.ContainsCommand(messageCommand.CommandName));
                }

                if (command != null)
                {
                    if (message.User.Roles.Any(r => r >= command.LowestAllowedRole))
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        command.Perform(message.User, messageCommand.CommandArguments);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    else
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        ChannelSession.BotChatClient.Whisper(message.User.UserName, "You do not permission to run this command");
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                }
            }
        }

        private async Task PurgeUser(ChatUserViewModel user)
        {
            await ChannelSession.ChatClient.PurgeUser(user.UserName);
            foreach (ChatMessageControl messageControl in this.MessageControls)
            {
                if (messageControl.Message.User.Equals(user) && !messageControl.Message.IsWhisper)
                {
                    messageControl.DeleteMessage();
                }
            }
        }

        #endregion Chat Update Methods

        #region Context Menu Events

        private async void MessageDeleteMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.ChatList.SelectedItem != null)
            {
                ChatMessageControl control = (ChatMessageControl)this.ChatList.SelectedItem;
                if (!control.Message.IsWhisper)
                {
                    await ChannelSession.ChatClient.DeleteMessage(control.Message.ID);
                    control.DeleteMessage();
                }
            }
        }

        private async void MessageUserPurgeMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.ChatList.SelectedItem != null)
            {
                ChatMessageControl control = (ChatMessageControl)this.ChatList.SelectedItem;
                if (control.Message.User != null)
                {
                    await this.PurgeUser(control.Message.User);
                }
            }
        }

        private async void MessageUserTimeout1MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.ChatList.SelectedItem != null)
            {
                ChatMessageControl control = (ChatMessageControl)this.ChatList.SelectedItem;
                if (control.Message.User != null)
                {
                    await ChannelSession.ChatClient.TimeoutUser(control.Message.User.UserName, 60);
                }
            }
        }

        private async void MessageUserTimeout5MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.ChatList.SelectedItem != null)
            {
                ChatMessageControl control = (ChatMessageControl)this.ChatList.SelectedItem;
                if (control.Message.User != null)
                {
                    await ChannelSession.ChatClient.TimeoutUser(control.Message.User.UserName, 300);
                }
            }
        }

        private async void UserPurgeMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.UserList.SelectedItem != null)
            {
                ChatUserControl control = (ChatUserControl)this.UserList.SelectedItem;
                await this.PurgeUser(control.User);
            }
        }

        private async void UserTimeout1MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.UserList.SelectedItem != null)
            {
                ChatUserControl control = (ChatUserControl)this.UserList.SelectedItem;
                await ChannelSession.ChatClient.TimeoutUser(control.User.UserName, 60);
            }
        }

        private async void UserTimeout5MenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.UserList.SelectedItem != null)
            {
                ChatUserControl control = (ChatUserControl)this.UserList.SelectedItem;
                await ChannelSession.ChatClient.TimeoutUser(control.User.UserName, 300);
            }
        }

        #endregion Context Menu Events

        #region Chat Event Handlers

        private void ChatClient_OnDisconnectOccurred(object sender, WebSocketCloseStatus e)
        {
            // Show Re-Connecting...
        }

        private void ChatClient_OnClearMessagesOccurred(object sender, ChatClearMessagesEventModel e)
        {
        }

        private void ChatClient_OnDeleteMessageOccurred(object sender, ChatDeleteMessageEventModel e)
        {
            ChatMessageControl message = this.MessageControls.FirstOrDefault(msg => msg.Message.ID.Equals(e.id));
            if (message != null)
            {
                message.DeleteMessage();
            }
        }

        private void ChatClient_OnMessageOccurred(object sender, ChatMessageEventModel e)
        {
            this.AddMessage(new ChatMessageViewModel(e));
        }

        private void ChatClient_OnPollEndOccurred(object sender, ChatPollEventModel e)
        {
            
        }

        private void ChatClient_OnPollStartOccurred(object sender, ChatPollEventModel e)
        {
            
        }

        private void ChatClient_OnPurgeMessageOccurred(object sender, ChatPurgeMessageEventModel e)
        {
            IEnumerable<ChatMessageControl> userMessages = this.MessageControls.Where(msg => msg.Message.User != null && msg.Message.User.ID.Equals(e.user_id));
            if (userMessages != null)
            {
                foreach (ChatMessageControl message in userMessages)
                {
                    message.DeleteMessage();
                }
            }
        }

        private void ChatClient_OnUserJoinOccurred(object sender, ChatUserEventModel e)
        {
            ChatUserViewModel user = new ChatUserViewModel(e);
            this.AddUser(user);
        }

        private void ChatClient_OnUserLeaveOccurred(object sender, ChatUserEventModel e)
        {
            ChatUserViewModel user = new ChatUserViewModel(e);
            this.RemoveUser(user);
        }

        private void ChatClient_OnUserTimeoutOccurred(object sender, ChatUserEventModel e)
        {
            
        }

        private void ChatClient_OnUserUpdateOccurred(object sender, ChatUserEventModel e)
        {
            ChatUserViewModel user = new ChatUserViewModel(e);
            this.RemoveUser(user);
            this.AddUser(user);
        }

        #endregion Chat Event Handlers
    }
}
