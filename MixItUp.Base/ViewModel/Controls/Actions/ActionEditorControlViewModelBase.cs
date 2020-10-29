﻿using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Window.Commands;
using MixItUp.Base.ViewModels;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MixItUp.Base.ViewModel.Controls.Actions
{
    public abstract class ActionEditorControlViewModelBase : UIViewModelBase
    {
        public abstract ActionTypeEnum Type { get; }

        public virtual string HelpLinkIdentifier { get { return this.Type.ToString(); } }

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

        public ICommand PlayCommand { get; private set; }
        public ICommand MoveUpCommand { get; private set; }
        public ICommand MoveDownCommand { get; private set; }
        public ICommand CopyCommand { get; private set; }
        public ICommand HelpCommand { get; private set; }
        public ICommand DeleteCommand { get; private set; }

        public bool Enabled
        {
            get { return this.enabled; }
            set
            {
                this.enabled = value;
                this.NotifyPropertyChanged();
            }
        }
        private bool enabled;

        public bool IsMinimized
        {
            get { return this.isMinimized; }
            set
            {
                this.isMinimized = value;
                this.NotifyPropertyChanged();
                this.NotifyPropertyChanged("ShowNameTextBlock");
                this.NotifyPropertyChanged("ShowNameTextBox");
            }
        }
        private bool isMinimized;

        public bool ShowNameTextBlock { get { return this.IsMinimized; } }

        public bool ShowNameTextBox { get { return !this.IsMinimized; } }

        private CommandEditorWindowViewModelBase commandEditorViewModel;

        public ActionEditorControlViewModelBase(ActionModelBase action)
        {
            this.Name = action.Name;
            this.Enabled = action.Enabled;
            this.IsMinimized = true;
        }

        public ActionEditorControlViewModelBase()
        {
            this.Name = EnumLocalizationHelper.GetLocalizedName(this.Type);
            this.Enabled = true;
        }

        protected override Task OnLoadedInternal()
        {
            this.PlayCommand = this.CreateCommand(async (parameter) =>
            {
                CommandModelBase command = await this.commandEditorViewModel.ValidateAndBuildCommand();
                if (command != null)
                {
                    ActionModelBase action = await this.ValidateAndGetAction();
                    if (action != null)
                    {
                        await action.TestPerform(command.GetUniqueSpecialIdentifiers());
                    }
                }
            });

            this.MoveUpCommand = this.CreateCommand((parameter) =>
            {
                this.commandEditorViewModel.MoveActionUp(this);
                return Task.FromResult(0);
            });

            this.MoveDownCommand = this.CreateCommand((parameter) =>
            {
                this.commandEditorViewModel.MoveActionDown(this);
                return Task.FromResult(0);
            });

            this.CopyCommand = this.CreateCommand(async (parameter) =>
            {
                await this.commandEditorViewModel.DuplicateAction(this);
            });

            this.HelpCommand = this.CreateCommand((parameter) =>
            {
                ProcessHelper.LaunchLink("https://github.com/SaviorXTanren/mixer-mixitup/wiki/Actions#" + this.HelpLinkIdentifier);
                return Task.FromResult(0);
            });

            this.DeleteCommand = this.CreateCommand((parameter) =>
            {
                this.commandEditorViewModel.DeleteAction(this);
                return Task.FromResult(0);
            });

            return Task.FromResult(0);
        }

        public void Initialize(CommandEditorWindowViewModelBase commandEditorViewModel)
        {
            this.commandEditorViewModel = commandEditorViewModel;
        }

        public virtual Task<Result> Validate()
        {
            return Task.FromResult(new Result());
        }

        public async Task<ActionModelBase> GetAction()
        {
            ActionModelBase action = await this.GetActionInternal();
            if (action != null)
            {
                action.Name = this.Name;
                action.Enabled = this.Enabled;
            }
            return action;
        }

        public async Task<ActionModelBase> ValidateAndGetAction()
        {
            Result result = await this.Validate();
            if (!result.Success)
            {
                await DialogHelper.ShowMessage(result.ToString());
                return null;
            }
            return await this.GetAction();
        }

        protected abstract Task<ActionModelBase> GetActionInternal();
    }
}
