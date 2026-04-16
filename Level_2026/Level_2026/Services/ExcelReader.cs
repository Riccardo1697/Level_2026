using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using Level_2026.Models;

namespace Level_2026.IO
{
    public static class ExcelReader
    {
        public static List<Observation> Read(string path)
        {
            var list = new List<Observation>();

            ExcelPackage.License.SetNonCommercialOrganization("Level2026");

            using var package = new ExcelPackage(new FileInfo(path));

            // 🔥 FOGLIO CORRETTO
            var ws = package.Workbook.Worksheets["GSI.16"];

            if (ws == null)
                throw new Exception("Foglio 'GSI.16' non trovato nel file Excel");

            if (ws.Dimension == null)
                return list;

            int rows = ws.Dimension.End.Row;

            for (int r = 1; r <= rows; r++)
            {
                var fromObj = ws.Cells[$"AI{r}"].Value;
                var toObj = ws.Cells[$"AJ{r}"].Value;
                var dObj = ws.Cells[$"AK{r}"].Value;
                var dhObj = ws.Cells[$"AL{r}"].Value;

                if (fromObj == null || toObj == null || dObj == null || dhObj == null)
                    continue;

                string from = fromObj.ToString().Trim();
                string to = toObj.ToString().Trim();

                if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                    continue;

                if (!double.TryParse(dObj.ToString(), out double d))
                    continue;

                if (!double.TryParse(dhObj.ToString(), out double dh))
                    continue;

                list.Add(new Observation
                {
                    From = from,
                    To = to,
                    Dist = d,
                    Dh = dh,
                    Line = ""
                });
            }

            return list;
        }
    }
}