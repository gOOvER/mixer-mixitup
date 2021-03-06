﻿using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using System.Linq;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public interface IAlertsService
    {
        ThreadSafeObservableCollection<AlertChatMessageViewModel> Alerts { get; }

        Task AddAlert(AlertChatMessageViewModel alert);
    }

    public class AlertsService : IAlertsService
    {
        public ThreadSafeObservableCollection<AlertChatMessageViewModel> Alerts { get; private set; } = new ThreadSafeObservableCollection<AlertChatMessageViewModel>();
        private LockedDictionary<string, AlertChatMessageViewModel> alertsLookup = new LockedDictionary<string, AlertChatMessageViewModel>();

        public async Task AddAlert(AlertChatMessageViewModel alert)
        {
            if (!string.IsNullOrEmpty(alert.Color))
            {
                this.alertsLookup[alert.ID] = alert;

                if (ChannelSession.Settings.LatestChatAtTop)
                {
                    this.Alerts.Insert(0, alert);
                }
                else
                {
                    this.Alerts.Add(alert);
                }

                if (this.Alerts.Count > ChannelSession.Settings.MaxMessagesInChat)
                {
                    AlertChatMessageViewModel removedAlert = (ChannelSession.Settings.LatestChatAtTop) ? this.Alerts.Last() : this.Alerts.First();
                    this.alertsLookup.Remove(removedAlert.ID);
                    this.Alerts.Remove(removedAlert);
                }

                await ChannelSession.Services.Chat.WriteToChatEventLog(alert);

                if (!ChannelSession.Settings.OnlyShowAlertsInDashboard)
                {
                    await ChannelSession.Services.Chat.AddMessage(alert);
                }
            }
        }
    }
}
