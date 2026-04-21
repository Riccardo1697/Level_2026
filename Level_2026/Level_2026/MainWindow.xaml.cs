using Level_2026.Core;
using Level_2026.IO;
using Level_2026.Models;
using Level_2026.Services;
using Microsoft.Win32;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;

namespace Level_2026
{
    public partial class MainWindow : Window
    {
        private List<Observation> _observations = new();
        private Dictionary<string, double> _fixed = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        // ----------------------------
        // LOG
        // ----------------------------
        private void Log(string msg)
        {
            LogBox.AppendText(msg + "\n");
            LogBox.ScrollToEnd();
        }

        // ----------------------------
        // NAVIGAZIONE
        // ----------------------------
        private void ShowInput_Click(object sender, RoutedEventArgs e)
        {
            GridInput.Visibility = Visibility.Visible;
            GridComp.Visibility = Visibility.Collapsed;
        }

        private void ShowComp_Click(object sender, RoutedEventArgs e)
        {
            GridInput.Visibility = Visibility.Collapsed;
            GridComp.Visibility = Visibility.Visible;
        }

        // ----------------------------
        // PARSER SICURO
        // ----------------------------
        private static double ParseDouble(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return 0;

            return double.Parse(
                s.Trim().Replace(",", "."),
                CultureInfo.InvariantCulture
            );
        }

        // ----------------------------
        // CARICA EXCEL
        // ----------------------------
        public static List<Observation> LoadFromInputExcel()
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "INPUT.xlsx"
            );

            if (!File.Exists(path))
                throw new Exception("File INPUT.xlsx non trovato sul Desktop");

            ExcelPackage.License.SetNonCommercialOrganization("Level2026");

            var list = new List<Observation>();

            using var package = new ExcelPackage(new FileInfo(path));
            var ws = package.Workbook.Worksheets.FirstOrDefault();

            if (ws == null)
                throw new Exception("Nessun foglio Excel trovato");

            int row = 2;
            string currentLine = "";

            while (true)
            {
                var da = ws.Cells[row, 2].Text;
                var a = ws.Cells[row, 3].Text;

                if (string.IsNullOrWhiteSpace(da) && string.IsNullOrWhiteSpace(a))
                    break;

                var dhText = ws.Cells[row, 4].Text;
                var distText = ws.Cells[row, 5].Text;
                var linea = ws.Cells[row, 1].Text;

                if (!string.IsNullOrWhiteSpace(linea))
                    currentLine = linea;

                list.Add(new Observation
                {
                    Line = currentLine,
                    From = da,
                    To = a,
                    Dh = ParseDouble(dhText),
                    Dist = ParseDouble(distText)
                });

                row++;
            }

            return list;
        }

        // ----------------------------
        // CARICA INPUT
        // ----------------------------
        private void Carica_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _observations = LoadFromInputExcel();

                var nodes = _observations
                    .SelectMany(o => new[] { o.From, o.To })
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                ListNodes.ItemsSource = nodes;

                GridFixed.ItemsSource = _fixed
                    .Select(x => new { Nodo = x.Key, Quota = x.Value })
                    .ToList();

                GridRete.ItemsSource = _observations;

                StatusText.Text = "Caricato INPUT.xlsx";

                Log("=== INPUT CARICATO ===");
                Log($"Osservazioni: {_observations.Count}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        // ----------------------------
        // FIXED POINTS
        // ----------------------------
        private void AddFixed_Click(object sender, RoutedEventArgs e)
        {
            if (ListNodes.SelectedItem is not string node)
            {
                MessageBox.Show("Seleziona un nodo");
                return;
            }

            if (!double.TryParse(TxtQuota.Text.Replace(",", "."), out double quota))
            {
                MessageBox.Show("Quota non valida");
                return;
            }

            _fixed[node] = quota;

            GridFixed.ItemsSource = _fixed
                .Select(x => new { Nodo = x.Key, Quota = x.Value })
                .ToList();

            TxtQuota.Clear();
        }

        private void RemoveFixed_Click(object sender, RoutedEventArgs e)
        {
            if (GridFixed.SelectedItem == null) return;

            var nodo = GridFixed.SelectedItem
                .GetType()
                .GetProperty("Nodo")?
                .GetValue(GridFixed.SelectedItem)?
                .ToString();

            if (nodo == null) return;

            _fixed.Remove(nodo);

            GridFixed.ItemsSource = _fixed
                .Select(x => new { Nodo = x.Key, Quota = x.Value })
                .ToList();
        }

        // ----------------------------
        // COMPENSAZIONE
        // ----------------------------
        private void Compensa_Click(object sender, RoutedEventArgs e)
        {
            if (_observations.Count == 0 || _fixed.Count == 0)
            {
                MessageBox.Show("Carica dati e capisaldi");
                return;
            }

            var weight = rbInvD2.IsChecked == true
                ? WeightType.InverseDistanceSquared
                : WeightType.InverseDistance;

            var result = WLS.Adjust(_observations, _fixed, weight);

            var rows = new List<ResultRow>();

            var grouped = _observations.GroupBy(o => o.Line);

            string? lastLine = null;

            foreach (var g in grouped)
            {
                var list = g.ToList();
                if (list.Count == 0) continue;

                string line = g.Key;

                double sumDh = list.Sum(x => x.Dh);
                double distTot = list.Sum(x => x.Dist);
                double peso = distTot > 0 ? 1000.0 / distTot : 0;

                string start = list.First().From;

                double? quotaStart = result.Heights.ContainsKey(start)
                    ? result.Heights[start]
                    : null;

                double? correzione = sumDh;

                // HEADER LINEA (solo cambio linea)
                rows.Add(new ResultRow
                {
                    Linea = (line != lastLine) ? line : null,
                    Correzione = $"{correzione * 1000:+0.00;-0.00}",
                    Peso = peso.ToString("F2"),
                    Nod = start,
                    Quota = quotaStart
                });

                lastLine = line;

                string prev = start;

                foreach (var o in list)
                {
                    string to = o.To;

                    double? qPrev = result.Heights.ContainsKey(prev) ? result.Heights[prev] : null;
                    double? qTo = result.Heights.ContainsKey(to) ? result.Heights[to] : null;

                    rows.Add(new ResultRow
                    {
                        Linea = null,
                        Contrassegno = to,
                        D = o.Dist,
                        Dh = o.Dh,
                        QCompensata = qTo,
                        dQCalcolo = (qPrev.HasValue && qTo.HasValue) ? qTo - qPrev : null
                    });

                    prev = to;
                }
            }

            GridResult.ItemsSource = rows;

            StatusText.Text = $"Sigma0 = {result.Sigma0:F6}";
            Log($"Sigma0 = {result.Sigma0:F6}");

            ShowComp_Click(null, null);
        }
    }
}