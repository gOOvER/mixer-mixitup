﻿using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Controls.Actions;
using MixItUp.Base.ViewModel.Requirements;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Window.Commands
{
    public abstract class CommandEditorWindowViewModelBase : WindowViewModelBase
    {
        public const string MixItUpCommandFileExtension = ".miucommand";

        public string Name
        {
            get { return this.name; }
            set
            {
                this.name = value;
                this.NotifyPropertyChanged();
            }
        }
        private string name;

        public RequirementsSetViewModel Requirements { get; set; } = new RequirementsSetViewModel();

        public ObservableCollection<ActionEditorControlViewModelBase> Actions { get; set; } = new ObservableCollection<ActionEditorControlViewModelBase>();

        public ObservableCollection<ActionTypeEnum> ActionTypes { get; set; } = new ObservableCollection<ActionTypeEnum>();
        public ActionTypeEnum SelectedActionType
        {
            get { return this.selectedActionType; }
            set
            {
                this.selectedActionType = value;
                this.NotifyPropertyChanged();
            }
        }
        private ActionTypeEnum selectedActionType;

        public ICommand AddCommand { get; private set; }
        public ICommand SaveCommand { get; private set; }
        public ICommand TestCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand ImportCommand { get; private set; }

        public event EventHandler CloseEditor = delegate { };

        private CommandModelBase existingCommand;

        public CommandEditorWindowViewModelBase(CommandModelBase existingCommand)
            : this()
        {
            this.existingCommand = existingCommand;

            this.Name = this.existingCommand.Name;
            this.Requirements = new RequirementsSetViewModel(this.existingCommand.Requirements);
        }

        public CommandEditorWindowViewModelBase()
        {
            List<ActionTypeEnum> actionTypes = new List<ActionTypeEnum>(EnumHelper.GetEnumList<ActionTypeEnum>());
            actionTypes.Remove(ActionTypeEnum.Custom);
            foreach (ActionTypeEnum hiddenActions in ChannelSession.Settings.ActionsToHide)
            {
                actionTypes.Remove(hiddenActions);
            }

            foreach (ActionTypeEnum actionType in actionTypes.OrderBy(a => a.ToString()))
            {
                this.ActionTypes.Add(actionType);
            }

            this.AddCommand = this.CreateCommand(async (parameter) =>
            {
                if (this.ActionTypes.Contains(this.SelectedActionType))
                {
                    ActionEditorControlViewModelBase editorViewModel = null;
                    switch (this.SelectedActionType)
                    {
                        case ActionTypeEnum.Chat: editorViewModel = new ChatActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Command: editorViewModel = new CommandActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Conditional: editorViewModel = new ConditionalActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Consumables: editorViewModel = new ConsumablesActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Counter: editorViewModel = new CounterActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Discord: editorViewModel = new DiscordActionEditorControlViewModel(); break;
                        case ActionTypeEnum.ExternalProgram: editorViewModel = new ExternalProgramActionEditorControlViewModel(); break;
                        case ActionTypeEnum.File: editorViewModel = new FileActionEditorControlViewModel(); break;
                        case ActionTypeEnum.GameQueue: editorViewModel = new GameQueueActionEditorControlViewModel(); break;
                        case ActionTypeEnum.IFTTT: editorViewModel = new IFTTTActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Input: editorViewModel = new InputActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Moderation: editorViewModel = new ModerationActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Overlay: editorViewModel = new OverlayActionEditorControlViewModel(); break;
                        case ActionTypeEnum.OvrStream: editorViewModel = new OvrStreamActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Serial: editorViewModel = new SerialActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Sound: editorViewModel = new SoundActionEditorControlViewModel(); break;
                        case ActionTypeEnum.SpecialIdentifier: editorViewModel = new SpecialIdentifierActionEditorControlViewModel(); break;
                        case ActionTypeEnum.StreamingSoftware: editorViewModel = new StreamingSoftwareActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Streamlabs: editorViewModel = new StreamlabsActionEditorControlViewModel(); break;
                        case ActionTypeEnum.TextToSpeech: editorViewModel = new TextToSpeechActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Translation: editorViewModel = new TranslationActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Twitch: editorViewModel = new TwitchActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Twitter: editorViewModel = new TwitterActionEditorControlViewModel(); break;
                        case ActionTypeEnum.Wait: editorViewModel = new WaitActionEditorControlViewModel(); break;
                        case ActionTypeEnum.WebRequest: editorViewModel = new WebRequestActionEditorControlViewModel(); break;
                    }
                    await this.AddActionViewModel(editorViewModel);
                }
            });

            this.SaveCommand = this.CreateCommand(async (parameter) =>
            {
                CommandModelBase command = await this.ValidateAndBuildCommand();
                if (command != null)
                {
                    ChannelSession.Settings.SetCommand(command);
                    await ChannelSession.SaveSettings();

                    await this.SaveCommandToSettings(command);

                    this.CloseEditor(this, new EventArgs());
                }
            });

            this.TestCommand = this.CreateCommand(async (parameter) =>
            {
                CommandModelBase command = await this.ValidateAndBuildCommand();
                if (command != null)
                {
                    await command.TestPerform();
                }
            });

            this.ExportCommand = this.CreateCommand(async (parameter) =>
            {
                CommandModelBase command = await this.ValidateAndBuildCommand();
                if (command != null)
                {
                    string fileName = ChannelSession.Services.FileService.ShowSaveFileDialog(this.Name + MixItUpCommandFileExtension);
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        await FileSerializerHelper.SerializeToFile(fileName, command);
                    }
                }
            });

            this.ImportCommand = this.CreateCommand(async (parameter) =>
            {
                string fileName = ChannelSession.Services.FileService.ShowOpenFileDialog(string.Format("Mix It Up Command (*.{0})|*.{0}|All files (*.*)|*.*", MixItUpCommandFileExtension));
                if (!string.IsNullOrEmpty(fileName))
                {
                    // TODO
                }
            });
        }

        public abstract Task<Result> Validate();

        public abstract Task<CommandModelBase> GetCommand();

        public abstract Task SaveCommandToSettings(CommandModelBase command);

        public void MoveActionUp(ActionEditorControlViewModelBase actionViewModel)
        {
            int index = this.Actions.IndexOf(actionViewModel);
            if (index > 0)
            {
                this.Actions.Remove(actionViewModel);
                this.Actions.Insert(index - 1, actionViewModel);
            }
        }

        public void MoveActionDown(ActionEditorControlViewModelBase actionViewModel)
        {
            int index = this.Actions.IndexOf(actionViewModel);
            if (index >= 0 && index < this.Actions.Count - 1)
            {
                this.Actions.Remove(actionViewModel);
                this.Actions.Insert(index + 1, actionViewModel);
            }
        }

        public async Task DuplicateAction(ActionEditorControlViewModelBase actionViewModel)
        {
            ActionModelBase action = await actionViewModel.ValidateAndGetAction();
            if (action != null)
            {
                await this.AddAction(action);
            }
        }

        public void DeleteAction(ActionEditorControlViewModelBase actionViewModel)
        {
            this.Actions.Remove(actionViewModel);
        }

        protected override async Task OnLoadedInternal()
        {
            if (this.existingCommand != null)
            {
                foreach (ActionModelBase action in this.existingCommand.Actions)
                {
                    await this.AddAction(action);
                }
            }
        }

        private async Task AddAction(ActionModelBase action)
        {
            ActionEditorControlViewModelBase editorViewModel = null;
            switch (action.Type)
            {
                case ActionTypeEnum.Chat: editorViewModel = new ChatActionEditorControlViewModel((ChatActionModel)action); break;
                case ActionTypeEnum.Command: editorViewModel = new CommandActionEditorControlViewModel((CommandActionModel)action); break;
                case ActionTypeEnum.Conditional: editorViewModel = new ConditionalActionEditorControlViewModel((ConditionalActionModel)action); break;
                case ActionTypeEnum.Consumables: editorViewModel = new ConsumablesActionEditorControlViewModel((ConsumablesActionModel)action); break;
                case ActionTypeEnum.Counter: editorViewModel = new CounterActionEditorControlViewModel((CounterActionModel)action); break;
                case ActionTypeEnum.Discord: editorViewModel = new DiscordActionEditorControlViewModel((DiscordActionModel)action); break;
                case ActionTypeEnum.ExternalProgram: editorViewModel = new ExternalProgramActionEditorControlViewModel((ExternalProgramActionModel)action); break;
                case ActionTypeEnum.File: editorViewModel = new FileActionEditorControlViewModel((FileActionModel)action); break;
                case ActionTypeEnum.GameQueue: editorViewModel = new GameQueueActionEditorControlViewModel((GameQueueActionModel)action); break;
                case ActionTypeEnum.IFTTT: editorViewModel = new IFTTTActionEditorControlViewModel((IFTTTActionModel)action); break;
                case ActionTypeEnum.Input: editorViewModel = new InputActionEditorControlViewModel((InputActionModel)action); break;
                case ActionTypeEnum.Moderation: editorViewModel = new ModerationActionEditorControlViewModel((ModerationActionModel)action); break;
                case ActionTypeEnum.Overlay: editorViewModel = new OverlayActionEditorControlViewModel((OverlayActionModel)action); break;
                case ActionTypeEnum.OvrStream: editorViewModel = new OvrStreamActionEditorControlViewModel((OvrStreamActionModel)action); break;
                case ActionTypeEnum.Serial: editorViewModel = new SerialActionEditorControlViewModel((SerialActionModel)action); break;
                case ActionTypeEnum.Sound: editorViewModel = new SoundActionEditorControlViewModel((SoundActionModel)action); break;
                case ActionTypeEnum.SpecialIdentifier: editorViewModel = new SpecialIdentifierActionEditorControlViewModel((SpecialIdentifierActionModel)action); break;
                case ActionTypeEnum.StreamingSoftware: editorViewModel = new StreamingSoftwareActionEditorControlViewModel((StreamingSoftwareActionModel)action); break;
                case ActionTypeEnum.Streamlabs: editorViewModel = new StreamlabsActionEditorControlViewModel((StreamlabsActionModel)action); break;
                case ActionTypeEnum.TextToSpeech: editorViewModel = new TextToSpeechActionEditorControlViewModel((TextToSpeechActionModel)action); break;
                case ActionTypeEnum.Translation: editorViewModel = new TranslationActionEditorControlViewModel((TranslationActionModel)action); break;
                case ActionTypeEnum.Twitch: editorViewModel = new TwitchActionEditorControlViewModel((TwitchActionModel)action); break;
                case ActionTypeEnum.Twitter: editorViewModel = new TwitterActionEditorControlViewModel((TwitterActionModel)action); break;
                case ActionTypeEnum.Wait: editorViewModel = new WaitActionEditorControlViewModel((WaitActionModel)action); break;
                case ActionTypeEnum.WebRequest: editorViewModel = new WebRequestActionEditorControlViewModel((WebRequestActionModel)action); break;
            }
            await this.AddActionViewModel(editorViewModel);
        }

        private async Task AddActionViewModel(ActionEditorControlViewModelBase editorViewModel)
        {
            if (editorViewModel != null)
            {
                editorViewModel.Initialize(this);
                await editorViewModel.OnLoaded();
                this.Actions.Add(editorViewModel);
            }
        }

        public async Task<CommandModelBase> ValidateAndBuildCommand()
        {
            List<Result> results = new List<Result>();

            results.Add(await this.Validate());
            results.AddRange(await this.Requirements.Validate());
            foreach (ActionEditorControlViewModelBase actionViewModel in this.Actions)
            {
                results.Add(await actionViewModel.Validate());
            }

            if (results.Any(r => !r.Success))
            {
                StringBuilder error = new StringBuilder();
                error.AppendLine(MixItUp.Base.Resources.TheFollowingErrorsMustBeFixed);
                error.AppendLine();
                foreach (Result result in results)
                {
                    if (!result.Success)
                    {
                        error.AppendLine(" - " + result.Message);
                    }
                }
                await DialogHelper.ShowMessage(error.ToString());
                return null;
            }

            CommandModelBase command = await this.GetCommand();
            if (command == null)
            {
                return null;
            }

            if (this.existingCommand != null)
            {
                command.ID = this.existingCommand.ID;
            }
            command.Requirements = this.Requirements.GetRequirements();

            foreach (ActionEditorControlViewModelBase actionViewModel in this.Actions)
            {
                ActionModelBase action = await actionViewModel.GetAction();
                if (action == null)
                {
                    return null;
                }
                command.Actions.Add(action);
            }

            return command;
        }
    }
}
