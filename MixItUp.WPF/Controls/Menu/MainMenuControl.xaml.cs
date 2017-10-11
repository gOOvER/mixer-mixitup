﻿using MixItUp.WPF.Util;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace MixItUp.WPF.Controls.Menu
{
    public class MainMenuItem : NotifyPropertyChangedBase
    {
        public string Name { get; private set; }
        public MainControlBase Control { get; private set; }

        public MainMenuItem(string name, MainControlBase control)
        {
            this.Name = name;
            this.Control = control;
        }
    }

    /// <summary>
    /// Interaction logic for MainMenuControl.xaml
    /// </summary>
    public partial class MainMenuControl : MainControlBase
    {
        private ObservableCollection<MainMenuItem> menuItems = new ObservableCollection<MainMenuItem>();

        public MainMenuControl()
        {
            InitializeComponent();
        }

        public async Task AddMenuItem(string name, MainControlBase control)
        {
            await control.Initialize(this.Window);
            this.menuItems.Add(new MainMenuItem(name, control));
            if (this.menuItems.Count == 1)
            {
                this.MenuItemSelected(this.menuItems.First());
            }
        }

        protected override Task InitializeInternal()
        {
            this.MenuItemsListBox.ItemsSource = this.menuItems;
            return base.InitializeInternal();
        }

        private void UIElement_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //until we had a StaysOpen glag to Drawer, this will help with scroll bars
            var dependencyObject = Mouse.Captured as DependencyObject;
            while (dependencyObject != null)
            {
                if (dependencyObject is ScrollBar)
                {
                    return;
                }
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }

            this.MenuToggleButton.IsChecked = false;
        }

        private void MenuItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.MenuItemsListBox.SelectedIndex >= 0)
            {
                MainMenuItem item = (MainMenuItem)this.MenuItemsListBox.SelectedItem;
                this.MenuItemSelected(item);
            }
        }

        private void MenuItemSelected(MainMenuItem item)
        {
            this.DataContext = item;
            this.ActiveControlContentControl.Content = item.Control;
            this.MenuToggleButton.IsChecked = false;
        }

        private void SubmitABugButton_Click(object sender, RoutedEventArgs e) { Process.Start("https://github.com/SaviorXTanren/mixer-mixitup/issues/new"); }

        private void DocumentationButton_Click(object sender, RoutedEventArgs e) { Process.Start("https://github.com/SaviorXTanren/mixer-mixitup/wiki"); }
    }
}
