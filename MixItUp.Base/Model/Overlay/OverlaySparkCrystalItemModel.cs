﻿using Mixer.Base.Model.Patronage;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.User;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Overlay
{
    [DataContract]
    public class OverlaySparkCrystalItemModel : OverlayHTMLTemplateItemModelBase
    {
        public const string HTMLTemplate = @"
            <p style=""font-family: '{TEXT_FONT}'; font-size: {REWARD_TEXT_SIZE}px; color: {TEXT_COLOR}; white-space: nowrap; font-weight: bold; margin: auto; text-align: center;"">{REWARD}</p>
            <div style=""position: absolute; background-image: url('{CRYSTAL_EMPTY_IMAGE}'); background-repeat: no-repeat; background-position: top; background-size: cover; height: {CRYSTAL_HEIGHT}px; width: {CRYSTAL_WIDTH}px;"" />
            <div style=""position: absolute; background-image: url('{CRYSTAL_FULL_IMAGE}'); background-repeat: no-repeat; background-position: top; background-size: cover; background-position: bottom; margin-top: {CRYSTAL_EMPTY_HEIGHT}px; height: {CRYSTAL_FILLED_HEIGHT}px; width: {CRYSTAL_WIDTH}px;"" />
            <div style=""position: absolute; background-color: {TEXT_COLOR}; width: {CRYSTAL_WIDTH}px; height: 2px;"">
                <p style=""font-family: '{TEXT_FONT}'; font-size: {AMOUNT_TEXT_SIZE}px; color: {TEXT_COLOR}; white-space: nowrap; font-weight: bold; margin: auto; text-align: center;"">{AMOUNT}</p>
            </div>";

        [DataMember]
        public string TextColor { get; set; }
        [DataMember]
        public string TextFont { get; set; }

        [DataMember]
        public int CrystalWidth { get; set; }
        [DataMember]
        public int CrystalHeight { get; set; }

        [DataMember]
        public string CustomImageFilePath { get; set; }

        [DataMember]
        public OverlayItemEffectVisibleAnimationTypeEnum ProgressAnimation { get; set; }
        [DataMember]
        public string ProgressAnimationName { get { return OverlayItemEffectsModel.GetAnimationClassName(this.ProgressAnimation); } set { } }
        [DataMember]
        public OverlayItemEffectVisibleAnimationTypeEnum MilestoneReachedAnimation { get; set; }
        [DataMember]
        public string MilestoneReachedAnimationName { get { return OverlayItemEffectsModel.GetAnimationClassName(this.MilestoneReachedAnimation); } set { } }

        [DataMember]
        public uint Start { get; set; }
        [DataMember]
        public uint Amount { get; set; }
        [DataMember]
        public uint Goal { get; set; }
        [DataMember]
        public string Reward { get; set; }

        [DataMember]
        public string EmptyImageFilePath { get; set; }
        [DataMember]
        public string FullImageFilePath { get; set; }
        [DataMember]
        public bool IsCustomImage { get { return !string.IsNullOrEmpty(this.CustomImageFilePath); } set { } }

        [DataMember]
        public bool ProgressMade { get; set; }
        [DataMember]
        public bool MilestoneReached { get; set; }

        private PatronagePeriodModel period;
        private PatronageStatusModel status;
        private PatronageMilestoneModel milestone;
        private PatronageMilestoneGroupModel milestoneGroup;

        public OverlaySparkCrystalItemModel() : base() { }

        public OverlaySparkCrystalItemModel(string htmlText, string textColor, string textFont, int crystalWidth, int crystalHeight, string customImageFilePath,
            OverlayItemEffectVisibleAnimationTypeEnum progressAnimation, OverlayItemEffectVisibleAnimationTypeEnum milestoneReachedAnimation)
            : base(OverlayItemModelTypeEnum.SparkCrystal, htmlText)
        {
            this.TextColor = textColor;
            this.TextFont = textFont;
            this.CrystalWidth = crystalWidth;
            this.CrystalHeight = crystalHeight;
            this.CustomImageFilePath = customImageFilePath;
            this.ProgressAnimation = progressAnimation;
            this.MilestoneReachedAnimation = milestoneReachedAnimation;
        }

        public override async Task Initialize()
        {
            await this.RefreshPatronageData();

            GlobalEvents.OnPatronageUpdateOccurred += GlobalEvents_OnPatronageUpdateOccurred;
            GlobalEvents.OnPatronageMilestoneReachedOccurred += GlobalEvents_OnPatronageMilestoneReachedOccurred;

            await base.Initialize();
        }

        public override async Task Disable()
        {
            GlobalEvents.OnPatronageUpdateOccurred -= GlobalEvents_OnPatronageUpdateOccurred;
            GlobalEvents.OnPatronageMilestoneReachedOccurred -= GlobalEvents_OnPatronageMilestoneReachedOccurred;

            await base.Disable();
        }

        protected override Task<Dictionary<string, string>> GetTemplateReplacements(UserViewModel user, IEnumerable<string> arguments, Dictionary<string, string> extraSpecialIdentifiers)
        {
            Dictionary<string, string> replacementSets = new Dictionary<string, string>();

            replacementSets["TEXT_COLOR"] = this.TextColor;
            replacementSets["TEXT_FONT"] = this.TextFont;

            replacementSets["CRYSTAL_WIDTH"] = this.CrystalWidth.ToString();
            replacementSets["CRYSTAL_HEIGHT"] = this.CrystalHeight.ToString();

            if (!string.IsNullOrEmpty(this.CustomImageFilePath))
            {
                replacementSets["CRYSTAL_EMPTY_IMAGE"] = this.CustomImageFilePath.ToString();
                replacementSets["CRYSTAL_FULL_IMAGE"] = this.CustomImageFilePath.ToString();
            }
            else
            {
                replacementSets["CRYSTAL_EMPTY_IMAGE"] = this.EmptyImageFilePath.ToString();
                replacementSets["CRYSTAL_FULL_IMAGE"] = this.FullImageFilePath.ToString();
            }

            replacementSets["AMOUNT"] = this.Amount.ToString();
            replacementSets["REWARD"] = this.Reward.ToString();

            double percentage = (((double)(this.Amount - this.Start)) / ((double)(this.Goal - this.Start)));
            int progressHeight = (int)(((double)this.CrystalHeight) * percentage);
            progressHeight = MathHelper.Clamp(progressHeight, 0, this.CrystalHeight);

            replacementSets["CRYSTAL_FILLED_HEIGHT"] = progressHeight.ToString();
            replacementSets["CRYSTAL_EMPTY_HEIGHT"] = (this.CrystalHeight - progressHeight).ToString();

            replacementSets["REWARD_TEXT_SIZE"] = ((int)(((double)this.CrystalWidth) * 0.30)).ToString(); 
            replacementSets["AMOUNT_TEXT_SIZE"] = ((int)(((double)this.CrystalWidth) * 0.18)).ToString();

            return Task.FromResult(replacementSets);
        }

        private void GlobalEvents_OnPatronageUpdateOccurred(object sender, PatronageStatusModel status)
        {
            this.ProgressMade = true;
            this.MilestoneReached = false;

            this.status = status;
            this.Amount = this.status.patronageEarned;
            this.SendUpdateRequired();
        }

        private async void GlobalEvents_OnPatronageMilestoneReachedOccurred(object sender, PatronageMilestoneModel milestone)
        {
            this.ProgressMade = false;
            this.MilestoneReached = true;

            await this.RefreshPatronageData();
            this.SendUpdateRequired();
        }

        private async Task RefreshPatronageData()
        {
            this.status = await ChannelSession.Connection.GetPatronageStatus(ChannelSession.Channel);
            if (status != null)
            {
                this.Amount = status.patronageEarned;

                this.period = await ChannelSession.Connection.GetPatronagePeriod(status);
                if (this.period != null)
                {
                    this.milestoneGroup = this.period.milestoneGroups.FirstOrDefault(mg => mg.id == this.status.currentMilestoneGroupId);
                    if (this.milestoneGroup != null)
                    {
                        this.EmptyImageFilePath = this.milestoneGroup.uiComponents["vesselImageEmptyPath"].ToString();
                        this.FullImageFilePath = this.milestoneGroup.uiComponents["vesselImageFullPath"].ToString();

                        this.milestone = this.milestoneGroup.milestones.FirstOrDefault(m => m.id == this.status.currentMilestoneId);
                        if (this.milestone != null)
                        {
                            this.Goal = milestone.target;
                            this.Reward = milestone.DollarAmountText();
                        }

                        this.Start = 0;
                        if (this.status.currentMilestoneId > 0)
                        {
                            PatronageMilestoneModel previousMilestone = this.period.milestoneGroups.SelectMany(mg => mg.milestones).FirstOrDefault(m => m.id == (this.status.currentMilestoneId - 1));
                            if (previousMilestone != null)
                            {
                                this.Start = milestone.target;
                            }
                        }
                    }
                }
            }
        }
    }
}
