﻿using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Commands.Games
{
    public enum RouletteGameCommandBetType
    {
        [Obsolete]
        Traditional,
        NumberRange,
        Custom
    }

    [DataContract]
    public class RouletteGameCommandModel : GameCommandModelBase
    {
        public const string GameRouletteBetTypeSpecialIdentifier = "gamebettype";
        public const string GameRouletteValidBetTypesSpecialIdentifier = "gamevalidbettypes";
        public const string GameRouletteWinningBetTypeSpecialIdentifier = "gamewinningbettype";

        [DataMember]
        public int MinimumParticipants { get; set; }
        [DataMember]
        public int TimeLimit { get; set; }
        [DataMember]
        public RouletteGameCommandBetType BetType { get; set; }
        [DataMember]
        public HashSet<string> BetOptions { get; set; }

        [DataMember]
        public CustomCommandModel StartedCommand { get; set; }
        [DataMember]
        public CustomCommandModel UserJoinCommand { get; set; }
        [DataMember]
        public CustomCommandModel NotEnoughPlayersCommand { get; set; }

        [DataMember]
        public GameOutcomeModel UserSuccessOutcome { get; set; }
        [DataMember]
        public CustomCommandModel UserFailureCommand { get; set; }
        [DataMember]
        public CustomCommandModel GameCompleteCommand { get; set; }

        [JsonIgnore]
        private CommandParametersModel runParameters;
        [JsonIgnore]
        private Dictionary<UserViewModel, CommandParametersModel> runUsers = new Dictionary<UserViewModel, CommandParametersModel>();

        public RouletteGameCommandModel(string name, HashSet<string> triggers, int minimumParticipants, int timeLimit, RouletteGameCommandBetType betType, HashSet<string> betOptions, CustomCommandModel startedCommand,
            CustomCommandModel userJoinCommand, CustomCommandModel notEnoughPlayersCommand, GameOutcomeModel userSuccessOutcome, CustomCommandModel userFailureCommand, CustomCommandModel gameCompleteCommand)
            : base(name, triggers, GameCommandTypeEnum.Roulette)
        {
            this.MinimumParticipants = minimumParticipants;
            this.TimeLimit = timeLimit;
            this.BetType = betType;
            this.BetOptions = betOptions;
            this.StartedCommand = startedCommand;
            this.UserJoinCommand = userJoinCommand;
            this.NotEnoughPlayersCommand = notEnoughPlayersCommand;
            this.UserSuccessOutcome = userSuccessOutcome;
            this.UserFailureCommand = userFailureCommand;
            this.GameCompleteCommand = gameCompleteCommand;
        }

        internal RouletteGameCommandModel(Base.Commands.RouletteGameCommand command)
            : base(command, GameCommandTypeEnum.Roulette)
        {
            this.MinimumParticipants = command.MinimumParticipants;
            this.TimeLimit = command.TimeLimit;
            this.BetType = command.IsNumberRange ? RouletteGameCommandBetType.NumberRange : RouletteGameCommandBetType.Custom;
            this.BetOptions = new HashSet<string>(command.ValidBetTypes);
            this.StartedCommand = new CustomCommandModel(command.StartedCommand) { IsEmbedded = true };
            this.UserJoinCommand = new CustomCommandModel(command.UserJoinCommand) { IsEmbedded = true };
            this.NotEnoughPlayersCommand = new CustomCommandModel(command.NotEnoughPlayersCommand) { IsEmbedded = true };
            this.UserSuccessOutcome = new GameOutcomeModel(command.UserSuccessOutcome);
            this.UserFailureCommand = new CustomCommandModel(command.UserFailOutcome.Command) { IsEmbedded = true };
            this.GameCompleteCommand = new CustomCommandModel(command.GameCompleteCommand) { IsEmbedded = true };
        }

        private RouletteGameCommandModel() { }

        public override IEnumerable<CommandModelBase> GetInnerCommands()
        {
            List<CommandModelBase> commands = new List<CommandModelBase>();
            commands.Add(this.StartedCommand);
            commands.Add(this.UserJoinCommand);
            commands.Add(this.NotEnoughPlayersCommand);
            commands.Add(this.UserSuccessOutcome.Command);
            commands.Add(this.UserFailureCommand);
            commands.Add(this.GameCompleteCommand);
            return commands;
        }

        protected override async Task<bool> ValidateRequirements(CommandParametersModel parameters)
        {
            if (parameters.Arguments.Count > 0 && this.BetOptions.Contains(parameters.Arguments[0].ToLower()))
            {
                return await base.ValidateRequirements(parameters);
            }

            string validBetTypes = string.Empty;
            if (this.BetType == RouletteGameCommandBetType.NumberRange)
            {
                IEnumerable<int> numbers = this.BetOptions.Select(s => int.Parse(s));
                validBetTypes = numbers.Min() + "-" + numbers.Max();
            }
            else if (this.BetType == RouletteGameCommandBetType.Custom)
            {
                validBetTypes = string.Join(", ", this.BetOptions);
            }
            await ChannelSession.Services.Chat.SendMessage(string.Format(MixItUp.Base.Resources.GameCommandRouletteValidBetTypes, validBetTypes));
            return false;
        }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (!this.runUsers.ContainsKey(parameters.User))
            {
                string betType = parameters.Arguments[0].ToLower();
                this.runUsers[parameters.User] = parameters;

                if (this.runParameters == null)
                {
                    this.runParameters = parameters;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
                    {
                        await Task.Delay(this.TimeLimit * 1000);

                        if (this.runUsers.Count < this.MinimumParticipants)
                        {
                            await this.NotEnoughPlayersCommand.Perform(this.runParameters);
                            foreach (var kvp in this.runUsers.ToList())
                            {
                                await this.Requirements.Refund(kvp.Value);
                            }
                            await this.CooldownRequirement.Perform(this.runParameters);
                            this.ClearData();
                            return;
                        }

                        string winningBetType = this.BetOptions.Random();

                        List<CommandParametersModel> winners = new List<CommandParametersModel>();
                        foreach (CommandParametersModel participant in this.runUsers.Values.ToList())
                        {
                            if (string.Equals(winningBetType, parameters.Arguments[0], StringComparison.CurrentCultureIgnoreCase))
                            {
                                winners.Add(parameters);
                                this.PerformOutcome(parameters, this.UserSuccessOutcome, this.GetBetAmount(parameters));
                            }
                            else
                            {
                                await this.UserFailureCommand.Perform(participant);
                            }
                        }

                        this.runParameters.SpecialIdentifiers[GameCommandModelBase.GameWinnersSpecialIdentifier] = string.Join(", ", winners.Select(u => "@" + u.User.Username));
                        await this.GameCompleteCommand.Perform(this.runParameters);

                        await this.CooldownRequirement.Perform(this.runParameters);
                        this.ClearData();
                    }, new CancellationToken());
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                    await this.StartedCommand.Perform(this.runParameters);
                }

                await this.UserJoinCommand.Perform(this.runParameters);
                this.ResetCooldown();
                return;
            }
            else
            {
                await ChannelSession.Services.Chat.SendMessage(MixItUp.Base.Resources.GameCommandAlreadyUnderway);
            }
            await this.Requirements.Refund(parameters);
        }

        private void ClearData()
        {
            this.runParameters = null;
            this.runUsers.Clear();
        }
    }
}