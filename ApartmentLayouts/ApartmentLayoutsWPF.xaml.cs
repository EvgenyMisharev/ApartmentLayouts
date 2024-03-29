﻿using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApartmentLayouts
{
    public partial class ApartmentLayoutsWPF : Window
    {
        public string ApartmentLayoutsSettingsSelectionValue;
        public bool ConsiderAreaCoefficient;
        ApartmentLayoutsSettings ApartmentLayoutsSettingsParam = null;
        public ApartmentLayoutsWPF()
        {
            InitializeComponent();
            ApartmentLayoutsSettingsParam = ApartmentLayoutsSettings.GetSettings();
            if (ApartmentLayoutsSettingsParam.ApartmentLayoutsSettingsValue != null)
            {
                if (ApartmentLayoutsSettingsParam.ApartmentLayoutsSettingsValue == "rbt_SeparatedByLevels")
                {
                    (groupBox_ApartmentLayoutsOption.Content as System.Windows.Controls.Grid)
                    .Children.OfType<RadioButton>()
                    .FirstOrDefault(rb => rb.Name == "rbt_SeparatedByLevels").IsChecked = true;
                }
                else
                {
                    (groupBox_ApartmentLayoutsOption.Content as System.Windows.Controls.Grid)
                        .Children.OfType<RadioButton>()
                        .FirstOrDefault(rb => rb.Name == "rbt_NoSeparationByLevels").IsChecked = true;
                }

            }
            if (ApartmentLayoutsSettingsParam.ConsiderAreaCoefficient != null)
            {
                checkBox_Coefficient.IsChecked = ApartmentLayoutsSettingsParam.ConsiderAreaCoefficient;
            }
        }
        private void btn_Ok_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            DialogResult = true;
            Close();
        }
        private void btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        private void ApartmentLayoutsWPF_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                SaveSettings();
                DialogResult = true;
                Close();
            }

            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }
        private void SaveSettings()
        {
            ApartmentLayoutsSettingsSelectionValue = (groupBox_ApartmentLayoutsOption.Content as System.Windows.Controls.Grid)
                .Children.OfType<RadioButton>()
                .FirstOrDefault(rb => rb.IsChecked.Value == true)
                .Name;
            ConsiderAreaCoefficient = (bool)checkBox_Coefficient.IsChecked;
            ApartmentLayoutsSettingsParam.ApartmentLayoutsSettingsValue = ApartmentLayoutsSettingsSelectionValue;
            ApartmentLayoutsSettingsParam.ConsiderAreaCoefficient = ConsiderAreaCoefficient;
            ApartmentLayoutsSettingsParam.SaveSettings();
        }
    }
}
