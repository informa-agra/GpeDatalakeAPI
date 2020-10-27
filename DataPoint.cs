namespace GpeDatalakeAPI
{
    public class DataPoint
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Concept { get; set; }
        public string MacroRegion { get; set; }
        public string Region { get; set; }
        public long ReportYear { get; set; }
        public long RowId { get; set; }
        public string Unit { get; set; }
        public float Value { get; set; }
        public string Vintage { get; set; }

        public DataPoint(string id, string category, string concept, string macroRegion, string region, long reportYear, long rowId, string unit, float value, string vintage)
        {
            Id = id;
            Category = category;
            Concept = concept;
            MacroRegion = macroRegion;
            Region = region;
            ReportYear = reportYear;
            RowId = rowId;
            Unit = unit;
            Value = value;
            Vintage = vintage;
        }
    }
}