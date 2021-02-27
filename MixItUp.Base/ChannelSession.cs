﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Commands.Games;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Services.Glimesh;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using StreamingClient.Base.Model.OAuth;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base
{
    public static class ChannelSession
    {
        public static GlimeshPlatformService GlimeshUserConnection { get; private set; }
        public static GlimeshPlatformService GlimeshBotConnection { get; private set; }
        public static Glimesh.Base.Models.Users.UserModel GlimeshUser { get; private set; }
        public static Glimesh.Base.Models.Channels.ChannelModel GlimeshChannel { get; private set; }
        public static Glimesh.Base.Models.Users.UserModel GlimeshBot { get; private set; }

        public static ApplicationSettingsV2Model AppSettings { get; private set; }
        public static SettingsV3Model Settings { get; private set; }

        public static ServicesManagerBase Services { get; private set; }

        private static CancellationTokenSource sessionBackgroundCancellationTokenSource = new CancellationTokenSource();
        private static int sessionBackgroundTimer = 0;

        public static bool IsDebug()
        {
            #if DEBUG
                return true;
            #else
                return false;
            #endif
        }

        public static bool IsElevated { get; set; }

        public static List<PreMadeChatCommandModelBase> PreMadeChatCommands { get; private set; } = new List<PreMadeChatCommandModelBase>();

        public static List<ChatCommandModel> ChatCommands { get; set; } = new List<ChatCommandModel>();

        public static List<EventCommandModel> EventCommands { get; set; } = new List<EventCommandModel>();

        public static List<TimerCommandModel> TimerCommands { get; set; } = new List<TimerCommandModel>();

        public static List<ActionGroupCommandModel> ActionGroupCommands { get; set; } = new List<ActionGroupCommandModel>();

        public static List<GameCommandModelBase> GameCommands { get; set; } = new List<GameCommandModelBase>();

        public static List<TwitchChannelPointsCommandModel> TwitchChannelPointsCommands { get; set; } = new List<TwitchChannelPointsCommandModel>();

        public static IEnumerable<CommandModelBase> AllEnabledChatAccessibleCommands
        {
            get
            {
                List<CommandModelBase> commands = new List<CommandModelBase>();
                commands.AddRange(ChannelSession.PreMadeChatCommands.Where(c => c.IsEnabled));
                commands.AddRange(ChannelSession.ChatCommands.Where(c => c.IsEnabled));
                commands.AddRange(ChannelSession.GameCommands.Where(c => c.IsEnabled));
                return commands;
            }
        }

        public static IEnumerable<CommandModelBase> AllCommands
        {
            get
            {
                List<CommandModelBase> commands = new List<CommandModelBase>();
                commands.AddRange(ChannelSession.PreMadeChatCommands);
                commands.AddRange(ChannelSession.ChatCommands);
                commands.AddRange(ChannelSession.GameCommands);
                commands.AddRange(ChannelSession.EventCommands);
                commands.AddRange(ChannelSession.TimerCommands);
                commands.AddRange(ChannelSession.ActionGroupCommands);
                commands.AddRange(ChannelSession.TwitchChannelPointsCommands);
                return commands;
            }
        }

        public static async Task Initialize(ServicesManagerBase serviceHandler)
        {
            ChannelSession.Services = serviceHandler;

            try
            {
                Type mixItUpSecretsType = Type.GetType("MixItUp.Base.MixItUpSecrets");
                if (mixItUpSecretsType != null)
                {
                    ChannelSession.Services.SetSecrets((SecretsService)Activator.CreateInstance(mixItUpSecretsType));
                }
            }
            catch (Exception ex) { Logger.Log(ex); }

            ChannelSession.AppSettings = await ApplicationSettingsV2Model.Load();
        }

        public static async Task Close()
        {
            await ChannelSession.Services.Close();

            await ServiceContainer.Get<TwitchSessionService>().Disconnect();
        }

        public static async Task SaveSettings()
        {
            await ChannelSession.Services.Settings.Save(ChannelSession.Settings);
        }

        public static UserViewModel GetCurrentUser()
        {
            // TO-DO: Update UserViewModel so that all platform accounts are combined into the same UserViewModel

            UserViewModel user = null;

            if (ServiceContainer.Get<TwitchSessionService>().UserNewAPI != null)
            {
                user = ChannelSession.Services.User.GetUserByPlatformID(StreamingPlatformTypeEnum.Twitch, ServiceContainer.Get<TwitchSessionService>().UserNewAPI.id);
                if (user == null)
                {
                    user = new UserViewModel(ServiceContainer.Get<TwitchSessionService>().UserNewAPI);
                }
            }

            return user;
        }

        public static void DisconnectionOccurred(string serviceName)
        {
            Logger.Log(serviceName + " Service disconnection occurred");
            GlobalEvents.ServiceDisconnect(serviceName);
        }

        public static void ReconnectionOccurred(string serviceName)
        {
            Logger.Log(serviceName + " Service reconnection successful");
            GlobalEvents.ServiceReconnect(serviceName);
        }

        public static async Task<bool> InitializeSession()
        {
            try
            {
                if (ChannelSession.Settings == null)
                {
                    IEnumerable<SettingsV3Model> currentSettings = await ChannelSession.Services.Settings.GetAllSettings();

                    if (currentSettings.Any(s => !string.IsNullOrEmpty(s.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].ChannelID) && string.Equals(s.StreamingPlatformAuthentications[StreamingPlatformTypeEnum.Twitch].ChannelID, ServiceContainer.Get<TwitchSessionService>().ChannelV5.id)))
                    {
                        GlobalEvents.ShowMessageBox($"There already exists settings for the account {ServiceContainer.Get<TwitchSessionService>().UserNewAPI.login}. Please sign in with a different account or re-launch Mix It Up to select those settings from the drop-down.");
                        return false;
                    }

                    ChannelSession.Settings = await ChannelSession.Services.Settings.Create(ServiceContainer.Get<TwitchSessionService>().UserNewAPI.login);
                }
                await ChannelSession.Services.Settings.Initialize(ChannelSession.Settings);

                ChannelSession.Settings.Name = ServiceContainer.Get<TwitchSessionService>().UserNewAPI.login;

                // Connect External Services
                Dictionary<IExternalService, OAuthTokenModel> externalServiceToConnect = new Dictionary<IExternalService, OAuthTokenModel>();
                if (ChannelSession.Settings.StreamlabsOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.Streamlabs] = ChannelSession.Settings.StreamlabsOAuthToken; }
                if (ChannelSession.Settings.StreamElementsOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.StreamElements] = ChannelSession.Settings.StreamElementsOAuthToken; }
                if (ChannelSession.Settings.StreamJarOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.StreamJar] = ChannelSession.Settings.StreamJarOAuthToken; }
                if (ChannelSession.Settings.TipeeeStreamOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.TipeeeStream] = ChannelSession.Settings.TipeeeStreamOAuthToken; }
                if (ChannelSession.Settings.TreatStreamOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.TreatStream] = ChannelSession.Settings.TreatStreamOAuthToken; }
                if (ChannelSession.Settings.StreamlootsOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.Streamloots] = ChannelSession.Settings.StreamlootsOAuthToken; }
                if (ChannelSession.Settings.TiltifyOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.Tiltify] = ChannelSession.Settings.TiltifyOAuthToken; }
                if (ChannelSession.Settings.JustGivingOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.JustGiving] = ChannelSession.Settings.JustGivingOAuthToken; }
                if (ChannelSession.Settings.IFTTTOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.IFTTT] = ChannelSession.Settings.IFTTTOAuthToken; }
                if (ChannelSession.Settings.ExtraLifeTeamID > 0) { externalServiceToConnect[ChannelSession.Services.ExtraLife] = new OAuthTokenModel(); }
                if (ChannelSession.Settings.PatreonOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.Patreon] = ChannelSession.Settings.PatreonOAuthToken; }
                if (ChannelSession.Settings.DiscordOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.Discord] = ChannelSession.Settings.DiscordOAuthToken; }
                if (ChannelSession.Settings.TwitterOAuthToken != null) { externalServiceToConnect[ChannelSession.Services.Twitter] = ChannelSession.Settings.TwitterOAuthToken; }
                if (ChannelSession.Services.OBSStudio.IsEnabled) { externalServiceToConnect[ChannelSession.Services.OBSStudio] = null; }
                if (ChannelSession.Services.StreamlabsOBS.IsEnabled) { externalServiceToConnect[ChannelSession.Services.StreamlabsOBS] = null; }
                if (ChannelSession.Services.XSplit.IsEnabled) { externalServiceToConnect[ChannelSession.Services.XSplit] = null; }
                if (!string.IsNullOrEmpty(ChannelSession.Settings.OvrStreamServerIP)) { externalServiceToConnect[ChannelSession.Services.OvrStream] = null; }
                if (ChannelSession.Settings.EnableOverlay) { externalServiceToConnect[ChannelSession.Services.Overlay] = null; }
                if (ChannelSession.Settings.EnableDeveloperAPI) { externalServiceToConnect[ChannelSession.Services.DeveloperAPI] = null; }

                if (externalServiceToConnect.Count > 0)
                {
                    Dictionary<IExternalService, Task<Result>> externalServiceTasks = new Dictionary<IExternalService, Task<Result>>();
                    foreach (var kvp in externalServiceToConnect)
                    {
                        Logger.Log(LogLevel.Debug, "Trying automatic OAuth service connection: " + kvp.Key.Name);

                        try
                        {
                            if (kvp.Key is IOAuthExternalService && kvp.Value != null)
                            {
                                externalServiceTasks[kvp.Key] = ((IOAuthExternalService)kvp.Key).Connect(kvp.Value);
                            }
                            else
                            {
                                externalServiceTasks[kvp.Key] = kvp.Key.Connect();
                            }
                        }
                        catch (Exception sex)
                        {
                            Logger.Log(LogLevel.Error, "Error in external service initial connection: " + kvp.Key.Name);
                            Logger.Log(sex);
                        }
                    }

                    try
                    {
                        await Task.WhenAll(externalServiceTasks.Values);
                    }
                    catch (Exception sex)
                    {
                        Logger.Log(LogLevel.Error, "Error in batch external service connection");
                        Logger.Log(sex);
                    }

                    List<IExternalService> failedServices = new List<IExternalService>();
                    foreach (var kvp in externalServiceTasks)
                    {
                        try
                        {
                            if (kvp.Value.Result != null && !kvp.Value.Result.Success && kvp.Key is IOAuthExternalService)
                            {
                                Logger.Log(LogLevel.Debug, "Automatic OAuth token connection failed, trying manual connection: " + kvp.Key.Name);
                                Result result = await kvp.Key.Connect();
                                if (!result.Success)
                                {
                                    failedServices.Add(kvp.Key);
                                }
                            }
                        }
                        catch (Exception sex)
                        {
                            Logger.Log(LogLevel.Error, "Error in external service failed re-connection: " + kvp.Key.Name);
                            Logger.Log(sex);
                            failedServices.Add(kvp.Key);
                        }
                    }

                    if (failedServices.Count > 0)
                    {
                        Logger.Log(LogLevel.Debug, "Connection failed for services: " + string.Join(", ", failedServices.Select(s => s.Name)));

                        StringBuilder message = new StringBuilder();
                        message.AppendLine("The following services could not be connected:");
                        message.AppendLine();
                        foreach (IExternalService service in failedServices)
                        {
                            message.AppendLine(" - " + service.Name);
                        }
                        message.AppendLine();
                        message.Append("Please go to the Services page to reconnect them manually.");
                        await DialogHelper.ShowMessage(message.ToString());
                    }
                }

                foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                {
                    if (currency.ShouldBeReset())
                    {
                        await currency.Reset();
                    }
                }

                if (ChannelSession.Settings.ModerationResetStrikesOnLaunch)
                {
                    foreach (UserDataModel userData in ChannelSession.Settings.UserData.Values)
                    {
                        if (userData.ModerationStrikes > 0)
                        {
                            userData.ModerationStrikes = 0;
                            ChannelSession.Settings.UserData.ManualValueChanged(userData.ID);
                        }
                    }
                }

                ChannelSession.PreMadeChatCommands.Clear();
                foreach (PreMadeChatCommandModelBase command in ReflectionHelper.CreateInstancesOfImplementingType<PreMadeChatCommandModelBase>())
                {
                    ChannelSession.PreMadeChatCommands.Add(command);
                }

                foreach (PreMadeChatCommandSettingsModel commandSetting in ChannelSession.Settings.PreMadeChatCommandSettings)
                {
                    PreMadeChatCommandModelBase command = ChannelSession.PreMadeChatCommands.FirstOrDefault(c => c.Name.Equals(commandSetting.Name));
                    if (command != null)
                    {
                        command.UpdateFromSettings(commandSetting);
                    }
                }
                ChannelSession.Services.Chat.RebuildCommandTriggers();

                await ChannelSession.Services.Timers.Initialize();
                await ChannelSession.Services.Moderation.Initialize();

                try
                {

                }
                catch (Exception ex)
                {
                    Logger.Log(ex);
                    Logger.Log(LogLevel.Error, "External Services - " + JSONSerializerHelper.SerializeToString(ex));
                    await DialogHelper.ShowMessage("Failed to initialize external services. If this continues, please visit the Mix It Up Discord for assistance." +
                        Environment.NewLine + Environment.NewLine + "Error Details: " + ex.Message);
                    return false;
                }

                ChannelSession.Services.Statistics.Initialize();

                ChannelSession.Services.InputService.HotKeyPressed += InputService_HotKeyPressed;

                foreach (RedemptionStoreProductModel product in ChannelSession.Settings.RedemptionStoreProducts.Values)
                {
                    product.ReplenishAmount();
                }

                foreach (RedemptionStorePurchaseModel purchase in ChannelSession.Settings.RedemptionStorePurchases.ToList())
                {
                    if (purchase.State != RedemptionStorePurchaseRedemptionState.ManualRedeemNeeded)
                    {
                        ChannelSession.Settings.RedemptionStorePurchases.Remove(purchase);
                    }
                }

                ChannelSession.Services.Telemetry.TrackLogin(ChannelSession.Settings.TelemetryUserID, ServiceContainer.Get<TwitchSessionService>().UserNewAPI?.broadcaster_type);

                await ChannelSession.SaveSettings();
                await ChannelSession.Services.Settings.SaveLocalBackup(ChannelSession.Settings);
                await ChannelSession.Services.Settings.PerformAutomaticBackupIfApplicable(ChannelSession.Settings);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                AsyncRunner.RunAsyncBackground(SessionBackgroundTask, sessionBackgroundCancellationTokenSource.Token, 60000);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                await ChannelSession.Services.Telemetry.Connect();
                ChannelSession.Services.Telemetry.SetUserID(ChannelSession.Settings.TelemetryUserID);
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                Logger.Log(LogLevel.Error, "Session Initialization - " + JSONSerializerHelper.SerializeToString(ex));
                await DialogHelper.ShowMessage("Failed to get channel information. If this continues, please visit the Mix It Up Discord for assistance." +
                    Environment.NewLine + Environment.NewLine + "Error Details: " + ex.Message);
            }
            return false;
        }



        private static async Task SessionBackgroundTask(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                sessionBackgroundTimer++;

                await ServiceContainer.Get<TwitchSessionService>().RefreshUser();

                await ServiceContainer.Get<TwitchSessionService>().RefreshChannel();

                if (sessionBackgroundTimer >= 5)
                {
                    await ChannelSession.SaveSettings();
                    sessionBackgroundTimer = 0;

                    if (ServiceContainer.Get<TwitchSessionService>().StreamIsLive)
                    {
                        try
                        {
                            string type = null;
                            if (ServiceContainer.Get<TwitchSessionService>().UserNewAPI.IsPartner())
                            {
                                type = "Partner";
                            }
                            else if (ServiceContainer.Get<TwitchSessionService>().UserNewAPI.IsAffiliate())
                            {
                                type = "Affiliate";
                            }
                            ChannelSession.Services.Telemetry.TrackChannelMetrics(type, ServiceContainer.Get<TwitchSessionService>().StreamV5.viewers, ChannelSession.Services.Chat.AllUsers.Count,
                                ServiceContainer.Get<TwitchSessionService>().StreamV5.game, ServiceContainer.Get<TwitchSessionService>().ChannelV5.views, ServiceContainer.Get<TwitchSessionService>().ChannelV5.followers);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(ex);
                        }
                    }
                }
            }
        }

        private static async void InputService_HotKeyPressed(object sender, HotKey hotKey)
        {
            if (ChannelSession.Settings.HotKeys.ContainsKey(hotKey.ToString()))
            {
                HotKeyConfiguration hotKeyConfiguration = ChannelSession.Settings.HotKeys[hotKey.ToString()];
                CommandModelBase command = ChannelSession.Settings.GetCommand(hotKeyConfiguration.CommandID);
                if (command != null)
                {
                    await command.Perform();
                }
            }
        }
    }
}