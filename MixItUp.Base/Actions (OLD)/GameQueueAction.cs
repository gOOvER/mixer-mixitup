﻿using MixItUp.Base.Model.Requirements;
using MixItUp.Base.ViewModel.User;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Actions
{
    public enum GameQueueActionType
    {
        JoinQueue,
        QueuePosition,
        QueueStatus,
        LeaveQueue,
        SelectFirst,
        SelectRandom,
        SelectFirstType,
        EnableDisableQueue,
        ClearQueue,
        JoinFrontOfQueue,
        EnableQueue,
        DisableQueue,
    }

    [DataContract]
    public class GameQueueAction : ActionBase
    {
        private static SemaphoreSlim asyncSemaphore = new SemaphoreSlim(1);

        protected override SemaphoreSlim AsyncSemaphore { get { return GameQueueAction.asyncSemaphore; } }

        [DataMember]
        public GameQueueActionType GameQueueType { get; set; }

        [DataMember]
        public RoleRequirementModel RoleRequirement { get; set; }

        [DataMember]
        public string TargetUsername { get; set; }

        public GameQueueAction() : base(ActionTypeEnum.GameQueue) { }

        public GameQueueAction(GameQueueActionType gameQueueType, RoleRequirementModel roleRequirement = null, string targetUsername = null)
            : this()
        {
            this.GameQueueType = gameQueueType;
            this.RoleRequirement = roleRequirement;
            this.TargetUsername = targetUsername;
        }

        protected override Task PerformInternal(UserViewModel user, IEnumerable<string> arguments)
        {
            return Task.FromResult(0);
        }
    }
}
