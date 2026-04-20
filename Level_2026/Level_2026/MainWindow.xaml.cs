using Level_2026.Core;
using Level_2026.IO;
using Level_2026.Models;
using Level_2026.Services;
using Microsoft.Win32;
using System.Collections.Generic;
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
        // CARICA FILE EXCEL
        // ----------------------------
        private void Carica_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx"
            };

            if (dialog.ShowDialog() != true) return;

            _observations = ExcelReader.Read(dialog.FileName);

            // Estrazione nodi
            var nodes = _observations
                .SelectMany(o => new[] { o.From, o.To })
                .Distinct()
                .ToList();

            // UI update
            ListNodes.ItemsSource = nodes;
           
            GridFixed.ItemsSource = _fixed.ToList();

            GridRete.ItemsSource = _observations;

            StatusText.Text = $"Caricato: {nodes.Count} nodi, {_observations.Count} osservazioni";

            // LOG
            Log("=== INPUT CARICATO ===");
            Log($"File: {dialog.FileName}");
            Log($"Osservazioni: {_observations.Count}");
            Log($"Nodi: {nodes.Count}");
        }

        private void RemoveFixed_Click(object sender, RoutedEventArgs e)
        {
            if (GridFixed.SelectedItem == null)
                return;

            var item = GridFixed.SelectedItem;

            // recupera il nodo dalla riga selezionata
            var node = item.GetType().GetProperty("Nodo")?.GetValue(item)?.ToString();

            if (node == null)
                return;

            if (_fixed.ContainsKey(node))
                _fixed.Remove(node);

            // refresh tabella
            GridFixed.ItemsSource = null;
            GridFixed.ItemsSource = _fixed.Select(x => new { Nodo = x.Key, Quota = x.Value });

            Log($"Caposaldo rimosso: {node}");
        }

        // ----------------------------
        // AGGIUNGI / AGGIORNA CAPOSALDO
        // ----------------------------
        private void AddFixed_Click(object sender, RoutedEventArgs e)
        {
            if (ListNodes.SelectedItem == null)
            {
                MessageBox.Show("Seleziona un nodo");
                return;
            }

            string node = ListNodes.SelectedItem.ToString()!;

            if (!double.TryParse(TxtQuota.Text.Replace(",", "."), out double quota))
            {
                MessageBox.Show("Quota non valida");
                return;
            }

            _fixed[node] = quota;

            // refresh tabella
            GridFixed.ItemsSource = null;
            GridFixed.ItemsSource = _fixed
                .Select(x => new { Nodo = x.Key, Quota = x.Value });

            TxtQuota.Clear();
        }

        // ----------------------------
        // COMPENSAZIONE
        // ----------------------------
        private void Compensa_Click(object sender, RoutedEventArgs e)
        {
            if (_observations.Count == 0) return;
            if (_fixed.Count == 0)
            {
                MessageBox.Show("Inserire almeno un caposaldo");
                return;
            }

            WeightType weight = rbInvD2.IsChecked == true
                ? WeightType.InverseDistanceSquared
                : WeightType.InverseDistance;

            var result = WLS.Adjust(_observations, _fixed, weight);

            var rows = new List<ResultRow>();

            var grouped = _observations.GroupBy(o => o.Line);

            foreach (var group in grouped)
            {
                var obsList = group.ToList();

                string nodoStart = obsList[0].From;

                double? quotaNodo = result.Heights.ContainsKey(nodoStart)
                    ? result.Heights[nodoStart]
                    : null;

                // PESO MEDIO (come Python)
                double peso = obsList.Average(o =>
                {
                    double d = Math.Max(o.Dist, 1e-12);
                    return weight == WeightType.InverseDistanceSquared ? 1 / (d * d) : 1 / d;
                });

                // CORREZIONE (mock stile Python)
                double correzione = obsList.Sum(o => o.Dh) - (quotaNodo ?? 0);

                // HEADER LINEA
                rows.Add(new ResultRow
                {
                    Linea = group.Key,
                    Correzione = $"{correzione * 1000:+0.00;-0.00} mm",
                    Peso = $"{peso:F2}",
                    Nodo = nodoStart,
                    Quota = quotaNodo,
                    RowType = "header"
                });

                string prev = nodoStart;

                foreach (var o in obsList)
                {
                    string to = o.To;

                    double? qPrev = result.Heights.ContainsKey(prev) ? result.Heights[prev] : null;
                    double? qTo = result.Heights.ContainsKey(to) ? result.Heights[to] : null;

                    double? dQ = (qPrev.HasValue && qTo.HasValue)
                        ? qTo - qPrev
                        : null;

                    rows.Add(new ResultRow
                    {
                        NodoTo = to,
                        Distanza = o.Dist,
                        QuotaComp = qTo,
                        DeltaQ = dQ,
                        Dh = o.Dh,
                        RowType = "data"
                    });

                    prev = to;
                }

                // separatore
                rows.Add(new ResultRow
                {
                    Nodo = "—",
                    RowType = "separator"
                });
            }

            GridResult.ItemsSource = rows;

            StatusText.Text = $"Compensazione completata - Sigma0 = {result.Sigma0:F6}";
            Log($"Sigma0: {result.Sigma0:F6}");

            ShowComp_Click(null, null);
        }
    }
}