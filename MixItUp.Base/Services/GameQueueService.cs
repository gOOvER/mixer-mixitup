﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public class GameQueueService
    {
        private const string QueuePositionSpecialIdentifier = "queueposition";

        private LockedList<UserViewModel> queue = new LockedList<UserViewModel>();

        public GameQueueService() { }

        public IEnumerable<UserViewModel> Queue { get { return this.queue.ToList(); } }

        public bool IsEnabled { get; private set; }

        public async Task Enable()
        {
            this.IsEnabled = true;
            await this.Clear();
        }

        public async Task Disable()
        {
            this.IsEnabled = false;
            await this.Clear();
        }

        public async Task Join(UserViewModel user)
        {
            if (await this.ValidateJoin(user))
            {
                if (ChannelSession.Settings.GameQueueSubPriority)
                {
                    if (user.HasPermissionsTo(UserRoleEnum.Subscriber))
                    {
                        int totalSubs = this.Queue.Count(u => u.HasPermissionsTo(UserRoleEnum.Subscriber));
                        this.queue.Insert(totalSubs, user);
                    }
                    else
                    {
                        this.queue.Add(user);
                    }
                }
                else
                {
                    this.queue.Add(user);
                }

                int position = this.queue.IndexOf(user);
                await ChannelSession.Settings.GetCommand(ChannelSession.Settings.GameQueueUserJoinedCommandID).Perform(new CommandParametersModel(user, new Dictionary<string, string>() { { QueuePositionSpecialIdentifier, this.GetUserPosition(user).ToString() } }));
            }
            GlobalEvents.GameQueueUpdated();
        }

        public async Task JoinFront(UserViewModel user)
        {
            if (await this.ValidateJoin(user))
            {
                this.queue.Insert(0, user);
                await ChannelSession.Settings.GetCommand(ChannelSession.Settings.GameQueueUserJoinedCommandID).Perform(new CommandParametersModel(user, new Dictionary<string, string>() { { QueuePositionSpecialIdentifier, this.GetUserPosition(user).ToString() } }));
            }
            GlobalEvents.GameQueueUpdated();
        }

        public Task Leave(UserViewModel user)
        {
            this.queue.Remove(user);
            GlobalEvents.GameQueueUpdated();
            return Task.FromResult(0);
        }

        public Task MoveUp(UserViewModel user)
        {
            this.queue.MoveUp(user);
            GlobalEvents.GameQueueUpdated();
            return Task.FromResult(0);
        }

        public Task MoveDown(UserViewModel user)
        {
            this.queue.MoveDown(user);
            GlobalEvents.GameQueueUpdated();
            return Task.FromResult(0);
        }

        public async Task SelectFirst()
        {
            if (this.queue.Count > 0)
            {
                UserViewModel user = this.queue.ElementAt(0);
                this.queue.Remove(user);
                await ChannelSession.Settings.GetCommand(ChannelSession.Settings.GameQueueUserSelectedCommandID).Perform(new CommandParametersModel(user));
                GlobalEvents.GameQueueUpdated();
            }
        }

        public async Task SelectFirstType(RoleRequirementModel roleRequirement)
        {
            foreach (UserViewModel user in this.queue.ToList())
            {
                Result result = await roleRequirement.Validate(new CommandParametersModel(user));
                if (result.Success)
                {
                    this.queue.Remove(user);
                    await ChannelSession.Settings.GetCommand(ChannelSession.Settings.GameQueueUserSelectedCommandID).Perform(new CommandParametersModel(user));
                    GlobalEvents.GameQueueUpdated();
                    return;
                }
            }
            await this.SelectFirst();
        }

        public async Task SelectRandom()
        {
            if (this.queue.Count > 0)
            {
                int index = RandomHelper.GenerateRandomNumber(this.queue.Count());
                UserViewModel user = this.queue.ElementAt(index);
                this.queue.Remove(user);
                await ChannelSession.Settings.GetCommand(ChannelSession.Settings.GameQueueUserSelectedCommandID).Perform(new CommandParametersModel(user));
                GlobalEvents.GameQueueUpdated();
            }
        }

        public int GetUserPosition(UserViewModel user)
        {
            int position = this.queue.IndexOf(user);
            return (position != -1) ? position + 1 : position;
        }

        public async Task PrintUserPosition(UserViewModel user)
        {
            int position = this.GetUserPosition(user);
            if (position != -1)
            {
                await ServiceManager.Get<ChatService>().SendMessage(string.Format("You are #{0} in the queue to play", position), user.Platform);
            }
            else
            {
                await ServiceManager.Get<ChatService>().SendMessage("You are not currently in the queue to play", user.Platform);
            }
        }

        public async Task PrintStatus(CommandParametersModel parameters)
        {
            StringBuilder message = new StringBuilder();
            message.Append(string.Format("There are currently {0} waiting to play.", this.queue.Count()));

            if (this.queue.Count() > 0)
            {
                message.Append(" The following users are next up to play: ");

                List<string> users = new List<string>();
                for (int i = 0; i < this.queue.Count() && i < 5; i++)
                {
                    users.Add("@" + this.queue[i].Username);
                }

                message.Append(string.Join(", ", users));
                message.Append(".");
            }

            await ServiceManager.Get<ChatService>().SendMessage(message.ToString(), parameters.Platform);
        }

        public Task Clear()
        {
            this.queue.Clear();
            GlobalEvents.GameQueueUpdated();
            return Task.FromResult(0);
        }

        private async Task<bool> ValidateJoin(UserViewModel user)
        {
            int position = this.GetUserPosition(user);
            if (position != -1)
            {
                await ServiceManager.Get<ChatService>().SendMessage(string.Format("You are already #{0} in the queue", position), user.Platform);
                return false;
            }
            return true;
        }
    }
}
