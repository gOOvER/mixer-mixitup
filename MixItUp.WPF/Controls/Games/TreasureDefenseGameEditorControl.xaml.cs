﻿using MixItUp.Base.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.ViewModel.Games;
using MixItUp.Base.ViewModel.Requirement;
using System.Threading.Tasks;

namespace MixItUp.WPF.Controls.Games
{
    /// <summary>
    /// Interaction logic for TreasureDefenseGameEditorControl.xaml
    /// </summary>
    public partial class TreasureDefenseGameEditorControl : GameEditorControlBase
    {
        private TreasureDefenseGameCommandEditorWindowViewModel viewModel;
        private TreasureDefenseGameCommand existingCommand;

        public TreasureDefenseGameEditorControl(CurrencyModel currency)
        {
            InitializeComponent();

            this.viewModel = new TreasureDefenseGameCommandEditorWindowViewModel(currency);
        }

        public TreasureDefenseGameEditorControl(TreasureDefenseGameCommand command)
        {
            InitializeComponent();

            this.existingCommand = command;
            this.viewModel = new TreasureDefenseGameCommandEditorWindowViewModel(command);
        }

        public override async Task<bool> Validate()
        {
            if (!await this.CommandDetailsControl.Validate())
            {
                return false;
            }
            return await this.viewModel.Validate();
        }

        public override void SaveGameCommand()
        {
            this.viewModel.SaveGameCommand(this.CommandDetailsControl.GameName, this.CommandDetailsControl.ChatTriggers, this.CommandDetailsControl.GetRequirements());
        }

        protected override async Task OnLoaded()
        {
            this.DataContext = this.viewModel;
            await this.viewModel.OnLoaded();

            if (this.existingCommand != null)
            {
                this.CommandDetailsControl.SetDefaultValues(this.existingCommand);
            }
            else
            {
                this.CommandDetailsControl.SetDefaultValues("Treasure Defense", "treasure", CurrencyRequirementTypeEnum.MinimumAndMaximum, 10, 1000);
            }
            await base.OnLoaded();
        }
    }
}
