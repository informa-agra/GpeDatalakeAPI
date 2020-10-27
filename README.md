# DataLake API .net Core

### Whats this?

This is a skeleton `.net Core 3.1` API running from the CLI.

It is written this way so it can be scheduled in Windows Task Manager or something similar.

#### Logging

Logging is provided by Serilog, which logs to the console and FileOutput. This can be modified in
`Program.cs` by changing the 2nd property of the `WriteTo.File` set up.

```c#
        static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.File(
                    formatter: new JsonFormatter(),
                    $"./dataLog-{DateTime.UtcNow:yyMMdd-HHmmss}.json",
                    LogEventLevel.Debug,
                    fileSizeLimitBytes: 10000000,
                    flushToDiskInterval: new TimeSpan(0, 4, 0, 0))
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .CreateLogger();

```

### Usage

> This framework is provided as is, modification only involves loading your own `DataSet` and defining your `DataPoint` format.

#### Adding your own DataSet...

Unless you're comfortable importing a full dataset then iterating Table data to DataPoints, i would suggest using a Table at a time.

> If it's needed - we have an elastic search integration that fits into this codebase and uses scroll queries to return big data.
> 
>Contact us if it's required...


See DatalakeExporter.cs class +- line: 271

Here you need to bring in your own data collection

`DatalakeExporter.cs`
```c#
   private async Task<List<DataPoint>> ProcessData(string index, int days = 1, int startDays = 0, string productIncoming = "")
        {
            // TODO: Load your data here
            // Add your datasource GET in here
            // for now i'll use a list of DataPoint with one entry'
            var data = new List<DataPoint>()
            {
                new DataPoint("id", "category", "concept", "macroRegion", "region", 2020, 1, "unit", 22.0f, "vintage")
            };
```


 For GPE, a single `DataPoint` is defined as follows

#### Definition of DataPoint

`DataPoint.cs`
```c#
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
```


Your datapoint(s) neeto match the spec you passed to the `feed-service` team.

>This needs to be matched (base types) by the Dictionary definition held by the `Feed-Service`

##### Example csv uploadable Dictionary Definition
```csv
name,type,description
activeIngredient,string(256),A custom property
additionalInformation,string(50),A custom property
aggregate,long,A custom property
applicationCategory1,integer,A custom property
```

##### Example property definition
|name|type|description|
|----|----|-----------|
|field name (camelCase)|string(maxWidth), integer, long, decimal, float, type|always a string and considered essential as clients will use this to decide whether to search by this field|

A `type` needs to be defined within the parent level `Dictionary Object`, then this has its 
own dictionary definition. (nested types = nested dictionaries).


## Settings

`gpe-app.json`
```json,title="code"
{
  "Title": "GPE DataLake API",
  "ConnectionStrings": {
    "main1": "connection to db here",
    "main2": "connection to db here"
  },
  "Settings": {
    "dev": {
      "dataLakeAccount": "markit/resellers/ihsmarkit_marketdashboard_inbound/accounts/restapi.dev",
      "dataLakePassword": "i34753$D8t4hg%e7rHGG",
      "dataLakeBase": "https://feed.ihsmarkit.com",
      "env": "dev"
    },
    "qa": {
      "dataLakeAccount": "markit/resellers/ihsmarkit_marketdashboard_inbound/accounts/restapi.qa",
      "dataLakePassword": "p2OSTAJefre$i&$aw!Et",
      "dataLakeBase": "https://feed.ihsmarkit.com",
      "env": "qa"
    },
    "prod": {
      "dataLakeAccount": "markit/resellers/ihsmarkit_marketdashboard_inbound/accounts/restapi.prod",
      "dataLakePassword": "p2OSTAJefre$i&$aw!Et",
      "dataLakeBase": "https://feed.ihsmarkit.com",
      "env": "prod"
    } 
  }
} 
```

Use the settings file `gpe-app.json` to control settings, in this example there are two connection string placeholders.

They are referenced using `configuration.GetSection($"ConnectionStrings:main1").Value;`

Other settings are referenced as `configuration.GetSection($"Settings:{env}:env").Value;`

### Running the app

Running the application in VS will default to use env `dev`, when running outside or within VS you may use the args[0] as `dev | qa | prod`.

```powershell
./[applicationname] [env]

./gpedatalakeapi.exe dev
./gpedatalakeapi.exe qa
./gpedatalakeapi.exe prod
```



### Logging format example

```json
{"Timestamp":"2020-10-27T20:22:08.7409260+00:00","Level":"Information","MessageTemplate":"Init DataLake API"}
{"Timestamp":"2020-10-27T20:22:09.0379213+00:00","Level":"Information","MessageTemplate":"Using env: qa"}
{"Timestamp":"2020-10-27T20:22:09.1453969+00:00","Level":"Debug","MessageTemplate":"env: qa"}
{"Timestamp":"2020-10-27T20:22:09.1492604+00:00","Level":"Debug","MessageTemplate":"baseUrl: https://feed.ihsmarkit.com"}
{"Timestamp":"2020-10-27T20:22:09.1502969+00:00","Level":"Debug","MessageTemplate":"----------------------------"}
{"Timestamp":"2020-10-27T20:22:09.1639272+00:00","Level":"Debug","MessageTemplate":"starting @ Tue, 27 Oct 2020 20:22:09 GMT\r"}
{"Timestamp":"2020-10-27T20:22:09.8479302+00:00","Level":"Debug","MessageTemplate":"{\"errorCode\":\"INVALID_USERNAME_OR_PASSWORD\",\"errorMessage\":\"Invalid username or password.\"}"}
```

## Possible Issues

* Invalid username or password
  * get `feed-service` team to check your account path and password.
* Dictionary error
    * get `feed-serivce` team to check your dictionary matches your datapoint definition types.



> | AK 2020