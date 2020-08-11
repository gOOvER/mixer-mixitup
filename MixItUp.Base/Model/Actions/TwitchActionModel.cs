﻿using MixItUp.Base.ViewModel.User;
using StreamingClient.Base.Util;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Twitch.Base.Models.NewAPI.Ads;

namespace MixItUp.Base.Model.Actions
{
    public enum TwitchActionType
    {
        Host,
        [Name("Run Ad")]
        RunAd,
        Raid,
        VIPUser,
        UnVIPUser
    }

    [DataContract]
    public class TwitchActionModel : ActionModelBase
    {
        private static SemaphoreSlim asyncSemaphore = new SemaphoreSlim(1);

        protected override SemaphoreSlim AsyncSemaphore { get { return TwitchActionModel.asyncSemaphore; } }

        [DataMember]
        public TwitchActionType ActionType { get; set; }

        [DataMember]
        public string ChannelName { get; set; }

        [DataMember]
        public int AdLength { get; set; } = 60;

        [DataMember]
        public string Username { get; set; }

        public TwitchActionModel(TwitchActionType type, string channelName = null, int adLength = 0, string username = null)
            : base(ActionTypeEnum.Twitch)
        {
            this.ActionType = type;
            this.ChannelName = channelName;
            this.AdLength = adLength;
            this.Username = username;
        }

        internal TwitchActionModel(MixItUp.Base.Actions.StreamingPlatformAction action)
            : base(ActionTypeEnum.Twitch)
        {
            if (action.ActionType == Base.Actions.StreamingPlatformActionType.Host)
            {
                this.ActionType = TwitchActionType.Host;
                this.ChannelName = action.HostChannelName;
            }
            else if (action.ActionType == Base.Actions.StreamingPlatformActionType.Raid)
            {
                this.ActionType = TwitchActionType.Raid;
                this.ChannelName = action.HostChannelName;
            }
            else if (action.ActionType == Base.Actions.StreamingPlatformActionType.RunAd)
            {
                this.ActionType = TwitchActionType.RunAd;
                this.AdLength = action.AdLength;
            }
        }

        protected override async Task PerformInternal(UserViewModel user, StreamingPlatformTypeEnum platform, IEnumerable<string> arguments, Dictionary<string, string> specialIdentifiers)
        {
            if (this.ActionType == TwitchActionType.Host)
            {
                string channelName = await this.ReplaceStringWithSpecialModifiers(this.ChannelName, user, platform, arguments, specialIdentifiers);
                await ChannelSession.Services.Chat.SendMessage("/host @" + channelName, sendAsStreamer: true, platform: StreamingPlatformTypeEnum.Twitch);
            }
            else if (this.ActionType == TwitchActionType.Raid)
            {
                string channelName = await this.ReplaceStringWithSpecialModifiers(this.ChannelName, user, platform, arguments, specialIdentifiers);
                await ChannelSession.Services.Chat.SendMessage("/raid @" + channelName, sendAsStreamer: true, platform: StreamingPlatformTypeEnum.Twitch);
            }
            else if (this.ActionType == TwitchActionType.RunAd)
            {
                AdResponseModel response = await ChannelSession.TwitchUserConnection.RunAd(ChannelSession.TwitchUserNewAPI, this.AdLength);
                if (response == null)
                {
                    await ChannelSession.Services.Chat.SendMessage("ERROR: We were unable to run an ad, please try again later");
                }
                else if (!string.IsNullOrEmpty(response.message))
                {
                    await ChannelSession.Services.Chat.SendMessage("ERROR: " + response.message);
                }
            }
            else if (this.ActionType == TwitchActionType.VIPUser || this.ActionType == TwitchActionType.UnVIPUser)
            {
                UserViewModel targetUser = null;
                if (!string.IsNullOrEmpty(this.Username))
                {
                    string username = await this.ReplaceStringWithSpecialModifiers(this.Username, user, platform, arguments, specialIdentifiers);
                    targetUser = ChannelSession.Services.User.GetUserByUsername(username, platform);
                }
                else
                {
                    targetUser = user;
                }

                if (targetUser != null)
                {
                    if (this.ActionType == TwitchActionType.VIPUser)
                    {
                        await ChannelSession.Services.Chat.SendMessage("/vip @" + targetUser.TwitchUsername, sendAsStreamer: true, platform: StreamingPlatformTypeEnum.Twitch);
                    }
                    else if (this.ActionType == TwitchActionType.UnVIPUser)
                    {
                        await ChannelSession.Services.Chat.SendMessage("/unvip @" + targetUser.TwitchUsername, sendAsStreamer: true, platform: StreamingPlatformTypeEnum.Twitch);
                    }
                }
            }
        }
    }
}