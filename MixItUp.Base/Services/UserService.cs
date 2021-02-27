﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchNewAPI = Twitch.Base.Models.NewAPI;
using GlimeshBase = Glimesh.Base;
using MixItUp.Base.Services.Twitch;

namespace MixItUp.Base.Services
{
    public interface IUserService
    {
        UserViewModel GetUserByID(Guid id);

        UserViewModel GetUserByUsername(string username, StreamingPlatformTypeEnum platform = StreamingPlatformTypeEnum.None);

        UserViewModel GetUserByPlatformID(StreamingPlatformTypeEnum platform, string id);

        Task<UserViewModel> AddOrUpdateUser(TwitchNewAPI.Users.UserModel twitchChatUser);

        Task<UserViewModel> AddOrUpdateUser(GlimeshBase.Models.Users.UserModel glimeshChatUser);

        Task<UserViewModel> RemoveUserByUsername(StreamingPlatformTypeEnum platform, string username);

        void Clear();

        IEnumerable<UserViewModel> GetAllUsers();

        IEnumerable<UserViewModel> GetAllWorkableUsers();

        UserViewModel GetRandomUser(CommandParametersModel parameters);

        UserViewModel GetUserFullSearch(StreamingPlatformTypeEnum platform, string userID, string username);

        int Count();
    }

    public class UserService : IUserService
    {
        public static readonly HashSet<string> SpecialUserAccounts = new HashSet<string>() { "boomtvmod", "streamjar", "pretzelrocks", "scottybot", "streamlabs", "streamelements", "nightbot", "deepbot", "moobot", "coebot", "wizebot", "phantombot", "stay_hydrated_bot", "stayhealthybot", "anotherttvviewer", "commanderroot", "lurxx", "thecommandergroot", "moobot", "thelurxxer", "twitchprimereminder", "communityshowcase", "banmonitor", "wizebot" };

        private LockedDictionary<Guid, UserViewModel> usersByID = new LockedDictionary<Guid, UserViewModel>();

        private LockedDictionary<StreamingPlatformTypeEnum, Dictionary<string, Guid>> platformUserIDLookups { get; set; } = new LockedDictionary<StreamingPlatformTypeEnum, Dictionary<string, Guid>>();
        private LockedDictionary<StreamingPlatformTypeEnum, Dictionary<string, Guid>> platformUsernameLookups { get; set; } = new LockedDictionary<StreamingPlatformTypeEnum, Dictionary<string, Guid>>();

        public UserService()
        {
            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.Platforms)
            {
                this.platformUserIDLookups[platform] = new Dictionary<string, Guid>();
                this.platformUsernameLookups[platform] = new Dictionary<string, Guid>();
            }
        }

        public UserViewModel GetUserByID(Guid id)
        {
            if (this.usersByID.ContainsKey(id))
            {
                return this.usersByID[id];
            }
            return null;
        }

        public UserViewModel GetUserByUsername(string username, StreamingPlatformTypeEnum platform = StreamingPlatformTypeEnum.None)
        {
            if (!string.IsNullOrEmpty(username))
            {
                if (platform == StreamingPlatformTypeEnum.None)
                {
                    foreach (StreamingPlatformTypeEnum p in StreamingPlatforms.Platforms)
                    {
                        UserViewModel user = this.GetUserByUsername(username, p);
                        if (user != null)
                        {
                            return user;
                        }
                    }
                }
                else
                {
                    username = username.ToLower().Replace("@", "").Trim();
                    if (this.platformUsernameLookups[platform].ContainsKey(username))
                    {
                        return this.GetUserByID(this.platformUsernameLookups[platform][username]);
                    }
                }
            }
            return null;
        }

        public UserViewModel GetUserByPlatformID(StreamingPlatformTypeEnum platform, string id)
        {
            if (this.platformUserIDLookups[platform].ContainsKey(id))
            {
                return this.GetUserByID(this.platformUserIDLookups[platform][id]);
            }
            return null;
        }

        public async Task<UserViewModel> AddOrUpdateUser(TwitchNewAPI.Users.UserModel twitchChatUser)
        {
            if (!string.IsNullOrEmpty(twitchChatUser.id) && !string.IsNullOrEmpty(twitchChatUser.login))
            {
                UserViewModel user = this.GetUserByPlatformID(StreamingPlatformTypeEnum.Twitch, twitchChatUser.id);
                if (user != null)
                {
                    user = new UserViewModel(twitchChatUser);
                }
                await this.AddOrUpdateUser(user);
                return user;
            }
            return null;
        }

        public async Task<UserViewModel> AddOrUpdateUser(GlimeshBase.Models.Users.UserModel glimeshChatUser)
        {
            if (!string.IsNullOrEmpty(glimeshChatUser.id) && !string.IsNullOrEmpty(glimeshChatUser.username))
            {
                UserViewModel user = this.GetUserByPlatformID(StreamingPlatformTypeEnum.Glimesh, glimeshChatUser.id);
                if (user != null)
                {
                    user = new UserViewModel(glimeshChatUser);
                }
                await this.AddOrUpdateUser(user);
                return user;
            }
            return null;
        }

        private async Task AddOrUpdateUser(UserViewModel user)
        {
            if (!user.IsAnonymous)
            {
                this.usersByID[user.ID] = user;

                if (!string.IsNullOrEmpty(user.TwitchID) && !string.IsNullOrEmpty(user.TwitchUsername))
                {
                    this.platformUserIDLookups[StreamingPlatformTypeEnum.Twitch][user.TwitchID] = user.ID;
                    this.platformUsernameLookups[StreamingPlatformTypeEnum.Twitch][user.TwitchUsername] = user.ID;
                }

                if (UserService.SpecialUserAccounts.Contains(user.Username.ToLower()))
                {
                    user.IgnoreForQueries = true;
                }
                else if (ChannelSession.GetCurrentUser().ID.Equals(user.ID))
                {
                    user.IgnoreForQueries = true;
                }
                else if (ServiceContainer.Get<TwitchSessionService>().BotNewAPI != null && ServiceContainer.Get<TwitchSessionService>().BotNewAPI.id.Equals(user.TwitchID))
                {
                    user.IgnoreForQueries = true;
                }
                else
                {
                    user.IgnoreForQueries = false;
                    if (user.Data.ViewingMinutes == 0)
                    {
                        await ChannelSession.Services.Events.PerformEvent(new EventTrigger(EventTypeEnum.ChatUserFirstJoin, user));
                    }

                    if (ChannelSession.Services.Events.CanPerformEvent(new EventTrigger(EventTypeEnum.ChatUserJoined, user)))
                    {
                        user.LastSeen = DateTimeOffset.Now;
                        user.Data.TotalStreamsWatched++;
                        await ChannelSession.Services.Events.PerformEvent(new EventTrigger(EventTypeEnum.ChatUserJoined, user));
                    }
                }
            }
        }

        public async Task<UserViewModel> RemoveUserByUsername(StreamingPlatformTypeEnum platform, string username)
        {
            if (!string.IsNullOrEmpty(username))
            {
                username = username.ToLower().Replace("@", "").Trim();
                if (this.platformUsernameLookups[platform].ContainsKey(username))
                {
                    UserViewModel user = this.GetUserByID(this.platformUsernameLookups[platform][username]);
                    await this.RemoveUser(user);
                    return user;
                }
            }
            return null;
        }

        private async Task RemoveUser(UserViewModel user)
        {
            if (user != null)
            {
                this.usersByID.Remove(user.ID);

                if (!string.IsNullOrEmpty(user.TwitchID) && !string.IsNullOrEmpty(user.TwitchUsername))
                {
                    this.platformUserIDLookups[StreamingPlatformTypeEnum.Twitch].Remove(user.TwitchID);
                    this.platformUsernameLookups[StreamingPlatformTypeEnum.Twitch].Remove(user.TwitchUsername);
                }

                await ChannelSession.Services.Events.PerformEvent(new EventTrigger(EventTypeEnum.ChatUserLeft, user));
            }
        }

        public void Clear()
        {
            this.usersByID.Clear();

            foreach (var kvp in this.platformUserIDLookups)
            {
                kvp.Value.Clear();
            }

            foreach (var kvp in this.platformUsernameLookups)
            {
                kvp.Value.Clear();
            }
        }

        public IEnumerable<UserViewModel> GetAllUsers() { return this.usersByID.Values; }

        public IEnumerable<UserViewModel> GetAllWorkableUsers()
        {
            IEnumerable<UserViewModel> results = this.GetAllUsers();
            return results.Where(u => !u.IgnoreForQueries);
        }

        public UserViewModel GetRandomUser(CommandParametersModel parameters)
        {
            List<UserViewModel> results = new List<UserViewModel>(this.GetAllWorkableUsers());
            results.Remove(parameters.User);
            results.RemoveAll(u => !parameters.Platform.HasFlag(u.Platform));
            return results.Random();
        }

        public UserViewModel GetUserFullSearch(StreamingPlatformTypeEnum platform, string userID, string username)
        {
            UserViewModel user = null;
            if (!string.IsNullOrEmpty(userID))
            {
                user = ChannelSession.Services.User.GetUserByPlatformID(platform, userID);
                if (user == null)
                {
                    UserDataModel userData = ChannelSession.Settings.GetUserDataByPlatformID(platform, userID);
                    if (userData != null)
                    {
                        user = new UserViewModel(userData);
                    }
                }
            }

            if (user == null)
            {
                user = ChannelSession.Services.User.GetUserByUsername(username);
                if (user == null)
                {
                    UserDataModel userData = ChannelSession.Settings.GetUserDataByUsername(platform, username);
                    if (userData != null)
                    {
                        user = new UserViewModel(userData);
                    }
                    else
                    {
                        user = new UserViewModel(username);
                    }
                }
            }

            return user;
        }

        public int Count() { return this.usersByID.Count; }
    }
}
