using System;
using Avalonia.Controls;

namespace PocketMC.App.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            // Fail-safe check during early initialization stages
            if (RootGrid == null || SidebarBorder == null || MainAreaGrid == null || 
                TopCardGrid == null || TopControlsPanel == null || MiddleGrid == null || 
                CpuCardBorder == null || RamCardBorder == null || BottomGrid == null || 
                NetworkCardBorder == null || PlayersCardBorder == null)
            {
                return;
            }

            var width = e.NewSize.Width;

            // Calculate the actual available width for the server details dashboard area
            // Sidebar is 250px + 16px spacing = 266px width
            double mainWidth = (width >= 750) ? (width - 266) : width;

            // 1. Root Grid sidebar layout reflow
            if (width < 750)
            {
                // Stack sidebar on top of main content
                RootGrid.ColumnDefinitions = ColumnDefinitions.Parse("*");
                RootGrid.RowDefinitions = RowDefinitions.Parse("Auto, 16, *");

                Grid.SetColumn(SidebarBorder, 0);
                Grid.SetRow(SidebarBorder, 0);

                Grid.SetColumn(MainAreaGrid, 0);
                Grid.SetRow(MainAreaGrid, 2);
            }
            else
            {
                // Sidebar on the left, main content on the right
                RootGrid.ColumnDefinitions = ColumnDefinitions.Parse("250, 16, *");
                RootGrid.RowDefinitions = RowDefinitions.Parse("*");

                Grid.SetColumn(SidebarBorder, 0);
                Grid.SetRow(SidebarBorder, 0);

                Grid.SetColumn(MainAreaGrid, 2);
                Grid.SetRow(MainAreaGrid, 0);
            }

            // 2. Top Card Controls reflow (reflow actions underneath title when tight on space)
            if (mainWidth < 780)
            {
                TopCardGrid.ColumnDefinitions = ColumnDefinitions.Parse("*");
                TopCardGrid.RowDefinitions = RowDefinitions.Parse("Auto, 12, Auto");

                Grid.SetColumn(TopCardGrid.Children[0], 0);
                Grid.SetRow(TopCardGrid.Children[0], 0);

                Grid.SetColumn(TopControlsPanel, 0);
                Grid.SetRow(TopControlsPanel, 2);
                TopControlsPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            }
            else
            {
                TopCardGrid.ColumnDefinitions = ColumnDefinitions.Parse("*, Auto");
                TopCardGrid.RowDefinitions = RowDefinitions.Parse("*");

                Grid.SetColumn(TopCardGrid.Children[0], 0);
                Grid.SetRow(TopCardGrid.Children[0], 0);

                Grid.SetColumn(TopControlsPanel, 1);
                Grid.SetRow(TopControlsPanel, 0);
                TopControlsPanel.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            }

            // 3. Middle Cards reflow (CPU and RAM side-by-side or stacked)
            if (mainWidth < 680)
            {
                MiddleGrid.ColumnDefinitions = ColumnDefinitions.Parse("*");
                MiddleGrid.RowDefinitions = RowDefinitions.Parse("Auto, 16, Auto");

                Grid.SetColumn(CpuCardBorder, 0);
                Grid.SetRow(CpuCardBorder, 0);

                Grid.SetColumn(RamCardBorder, 0);
                Grid.SetRow(RamCardBorder, 2);
            }
            else
            {
                MiddleGrid.ColumnDefinitions = ColumnDefinitions.Parse("*, 16, *");
                MiddleGrid.RowDefinitions = RowDefinitions.Parse("*");

                Grid.SetColumn(CpuCardBorder, 0);
                Grid.SetRow(CpuCardBorder, 0);

                Grid.SetColumn(RamCardBorder, 2);
                Grid.SetRow(RamCardBorder, 0);
            }

            // 4. Bottom Cards reflow (Network and Players side-by-side or stacked)
            if (mainWidth < 680)
            {
                BottomGrid.ColumnDefinitions = ColumnDefinitions.Parse("*");
                BottomGrid.RowDefinitions = RowDefinitions.Parse("Auto, 16, Auto");

                Grid.SetColumn(NetworkCardBorder, 0);
                Grid.SetRow(NetworkCardBorder, 0);

                Grid.SetColumn(PlayersCardBorder, 0);
                Grid.SetRow(PlayersCardBorder, 2);
            }
            else
            {
                BottomGrid.ColumnDefinitions = ColumnDefinitions.Parse("*, 16, *");
                BottomGrid.RowDefinitions = RowDefinitions.Parse("*");

                Grid.SetColumn(NetworkCardBorder, 0);
                Grid.SetRow(NetworkCardBorder, 0);

                Grid.SetColumn(PlayersCardBorder, 2);
                Grid.SetRow(PlayersCardBorder, 0);
            }
        }
    }
}
