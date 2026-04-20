namespace Level_2026.Models
{
    public class ResultRow
    {
        public string Linea { get; set; }
        public string Correzione { get; set; }
        public string Peso { get; set; }
        public string Nodo { get; set; }
        public double? Quota { get; set; }

        public string NodoTo { get; set; }
        public double? Distanza { get; set; }
        public double? QuotaComp { get; set; }
        public double? DeltaQ { get; set; }
        public double? Dh { get; set; }

        public string RowType { get; set; } // header / data / separator
    }
}