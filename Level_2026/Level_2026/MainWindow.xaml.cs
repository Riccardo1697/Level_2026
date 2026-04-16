using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Level_2026.IO;
using Level_2026.Models;
using Level_2026.Core;
using System;

namespace Level_2026
{
    public partial class MainWindow : Window
    {
        private List<Observation> _obs = new();
        private Dictionary<string, double> _fixed = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoadExcel_Click(object sender, RoutedEventArgs e)
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "REPORT.xlsx");

            _obs = ExcelReader.Read(path);

            ObsGrid.ItemsSource = _obs;

            LogBox.AppendText($"Caricate {_obs.Count} osservazioni\n");
        }

        private void RunWLS_Click(object sender, RoutedEventArgs e)
        {
            if (_obs.Count == 0)
            {
                LogBox.AppendText("Nessun dato caricato\n");
                return;
            }

            _fixed = new Dictionary<string, double>
            {
                { _obs[0].From, 100.0 }
            };

            var res = WLS.Adjust(_obs, _fixed);

            var table = res.Heights
                .Select(h => new ResultRow
                {
                    Point = h.Key,
                    Height = h.Value
                })
                .ToList();

            ResGrid.ItemsSource = table;

            LogBox.AppendText($"Compensazione completata. Sigma0={res.Sigma0:F6}\n");
        }
    }
}