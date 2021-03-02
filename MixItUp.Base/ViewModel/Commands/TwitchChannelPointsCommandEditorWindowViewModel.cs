﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Util;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Twitch.Base.Models.NewAPI.ChannelPoints;

namespace MixItUp.Base.ViewModel.Commands
{
    public class TwitchChannelPointsCommandEditorWindowViewModel : CommandEditorWindowViewModelBase
    {
        public ObservableCollection<string> ChannelPointRewards { get; set; } = new ObservableCollection<string>();

        public TwitchChannelPointsCommandEditorWindowViewModel(TwitchChannelPointsCommandModel existingCommand) : base(existingCommand) { }

        public TwitchChannelPointsCommandEditorWindowViewModel() : base(CommandTypeEnum.TwitchChannelPoints) { }

        public override Task<Result> Validate()
        {
            if (string.IsNullOrEmpty(this.Name))
            {
                return Task.FromResult(new Result(MixItUp.Base.Resources.ACommandNameMustBeSpecified));
            }
            return Task.FromResult(new Result());
        }

        public override Task<CommandModelBase> CreateNewCommand() { return Task.FromResult<CommandModelBase>(new TwitchChannelPointsCommandModel(this.Name)); }

        public override Task SaveCommandToSettings(CommandModelBase command)
        {
            ChannelSession.TwitchChannelPointsCommands.Remove((TwitchChannelPointsCommandModel)this.existingCommand);
            ChannelSession.TwitchChannelPointsCommands.Add((TwitchChannelPointsCommandModel)command);
            return Task.FromResult(0);
        }

        protected override async Task OnLoadedInternal()
        {
            if (ServiceManager.Get<TwitchSessionService>().IsConnected)
            {
                IEnumerable<CustomChannelPointRewardModel> customChannelPointRewards = await ServiceManager.Get<TwitchSessionService>().UserConnection.GetCustomChannelPointRewards(ServiceManager.Get<TwitchSessionService>().UserNewAPI);
                if (customChannelPointRewards != null)
                {
                    foreach (CustomChannelPointRewardModel customChannelPointReward in customChannelPointRewards)
                    {
                        this.ChannelPointRewards.Add(customChannelPointReward.title);
                    }
                }
            }
            await base.OnLoadedInternal();
        }
    }
}
