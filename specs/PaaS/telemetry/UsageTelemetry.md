Design for a system to collect the usage data of our service and persist the data so that the historical data can be queried.

[[_TOC_]]

# Business Justification

We want to know how our service is being used and how it's growing over time.

# Requirements

More detail about the requirements can be found [here](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/11466?_a=files&path=%2Fspecs%2FBusinessOpsPerformanceTracking.md).

Some of the requirements are:

- Both current and historical data can be queried.
- Data can be refreshed at least once a day.
- Data can be analyzed with minimum time granularity of one day.

Based on the requirements, we will collect the following metrics:

- Requests by account, operation, resource type, status code
- Request errors by account, operation, resource type, status code
- Request latency (average, min, max, percentiles) by account, operation, resource type, status code
- Database storage size
- Database index storage size (Cosmos DB only)

# Scenarios

- Find out who are using our service (who is issuing requests and how much).
- Find out what our customers are using our service for (what operations and resources).
- Find out how we are handling these requests (how fast and how many requests we are serving).

# Metrics

- \# of Job activations: alert if the number of job activation is lower than expected
- \# of Job failures
- Processing latency: alert if the latency (current time - next start time) is greater than threshold

# Design

We will build an ETL pipeline to extract the data from our telemetry system, transform it, and load it to the analytical service.

The overall flow is the following. Each step is described more detail below.

1. Read the startTimestamp stored in the table storage for the metric to be processed.
    - The timestamp should be at the start of the day.
    - If the timestamp does not exist, then this is the first time this metrics is being processed so update the startTimestamp in the table storage to be the start of the current day.
1. Calculate the endTimestamp = startTimestamp + 1 day. If endTimestamp >= current time, skip since the current day hasn't completed yet.
1. Load the metric values from MDM. The type of sampling used will differ based on the type of metrics (e.g., count vs average).
1. Transform the data and write to CSV file in a blob storage.
1. Trigger the ADX to ingest the CSV file and wait for ingestion to complete.
1. Update the startTimestamp in the table storage with the endTimestamp.

## New resources to be provisioned

- 1 Service Fabric application that runs in the background to aggregate the metrics.
- 1 storage account in each region to store the aggregated metrics.
- 1 ADX account in home location (or perhaps West US instead of North Central US for prod since we will be running the query so the closer the better) where all the aggregated metrics will be ingested to.

## Extract

We will soon write the requests and database metrics to MDM so that they can be exposed to customers. We will use this as the source for our data.

The data in MDM is pre-aggregated at one minute interval and is available for 90 days. Each metrics can have multiple dimensions attached to it, which allows the data to be sliced further.

Once the data is extracted, we will persist it as a CSV file in the blob storage, and then trigger the Azure Data Explorer to ingest the blob. The reason we are choosing CSV here is because Azure Data Explorer has built-in mechanism to ingest data from CSV file (among other file formats) but CSV file being the most efficient one.

### Extraction process

Due to the nature of the work, we could use something like serverless architecture such as Azure Function but that would be yet another system to maintain and monitor so for now, we will go with the route of implementing it as a ServiceFabric application similar to the BillingAgent. The downside of using ServiceFabric is that the process will likely be idle for the most part. Although ServiceFabric supports timer and reminder through Reliable Actors, I don't think the code is complicated enough to require that.

A storage account will be provisioned in each region. The blob storage will be used to store the aggregated CSV file and the table storage will be used to track the next start timestamp for each metric.

Considering that the storage is cheap, we could choose to persist the CSV file indefinitely. This can act as a backup and would allow us to recreate data in case we need it past 90 days or if we want to ingest the data somewhere else using other technologies down the road. Or, we could choose to delete the file after it's been ingested. ADX has an automatic option to delete the file once the ingestion is completed.

Both the background process and storage account will be in the same region so there should not be any egress charges.

### Extracting the data

First we need to setup the connection to MDM using the certificate. The certificate will be installed by ServiceFabric, and the thumbprint and the monitoring account will be passed in as parameters.

``` csharp
// Create a connection to MDM using the certificate.
var connectionInfo = new ConnectionInfo(GenevaCertThumbprint, StoreLocation.LocalMachine);

// Create a metric reader 
var metricReader = new MetricReader(connectionInfo);

var monitoringAccount = "MicrosoftHealthcareApisShoeboxWestUs2";
var metricNamespace = "Shoebox2";
var metricName = "TotalApiLatency";

var metricIdentifier = new MetricIdentifier(monitoringAccount, metricNamespace, metricName);
```

We can now query MDM for the time series. The data will be queried one day at time.

``` csharp
var dimensionFilters = new DimensionFilter[]
{
    DimensionFilter.CreateIncludeFilter("ResourceId"),
    DimensionFilter.CreateIncludeFilter("Operation"),
    DimensionFilter.CreateIncludeFilter("ResourceType"),
    DimensionFilter.CreateIncludeFilter("StatusCode"),
};

TimeSeries<MetricIdentifier, double?> results = await metricReader.GetTimeSeriesAsync(
    metricIdentifier,
    dimensionFilters,
    startTimeUtc,
    endTimeUtc,
    new SamplingType[]
    {
        SamplingType.Count,
        SamplingType.Average,
        SamplingType.Min,
        SamplingType.Max
    },
    seriesResolutionInMinutes: 1440);

if (results.Results != null && results.Results.Any())
{
    // Each result represent aggregated values of each sampling type over number of minutes specified by seriesResolutionInMinutes
    // for a given metric with combination of dimension values specified by the dimensionFilters.
    // Conceptually, it looks something like the following:
    // ResourceA|Create|Patient|201|[1, NaN, 5, 2, 4]
    // ResourceA|Search|Bundle|200|[5, NaN, NaN, 3, 4]
    // ResourceB|Delete|Observation|200|[NaN, 3, 5, NaN]
    foreach (FilteredTimeSeries result in results.Results)
    {
        // The DimensionList of the result contains the value for each dimension specified.
        string accountName = null;
        string operation = null;
        string resourceType = null;
        string statusCodeInString = null;

        foreach (KeyValuePair<string, string> dimension in result.DimensionList)
        {
            switch (dimension.Key)
            {
                case "ResourceId":
                    accountName = dimension.Value.Split('/').Last();
                    break;

                case "Operation":
                    operation = dimension.Value;
                    break;

                case "ResourceType":
                    resourceType = dimension.Value;
                    break;

                case "StatusCode":
                    statusCodeInString = dimension.Value;
                    break;
            }
        }
        
        // The time series value is an array for values from startTimeUtc to endTimeUtc (seems like both inclusive)
        // with each index representing the aggregated value at interval startTimeUtc.AddMinutes(index * seriesResolutionInMinutes).
        double[] counts = result.GetTimeSeriesValues(SamplingType.Count);
        double[] averages = result.GetTimeSeriesValues(SamplingType.Average);
        double[] maxes = result.GetTimeSeriesValues(SamplingType.Max);
        double[] mins = result.GetTimeSeriesValues(SamplingType.Min);

        for (int i = 0; i < averages.Length; i++)
        {
            double count = counts[i];
            double average = averages[i];
            double max = maxes[i];
            double min = mins[i];

            // For time interval where there was no data, MDM returns NaN for the value.
            if ((double.IsNaN(count) && !(double.IsNaN(average) && double.IsNaN(max) && double.IsNaN(min)) && average != 0) ||
                    (!double.IsNaN(count) && (double.IsNaN(average) && double.IsNaN(max) && double.IsNaN(min))))
            {
                // Sanity check. This should not happen.
            }
            else if (!double.IsNaN(count))
            {
                DateTime timestamp = startTimeUtc.AddMinutes(i * results.TimeResolutionInMinutes);

                if (timestamp < endTimeUtc)
                {
                    // We have data for the current time interval.
                }
            }
        }
    }
}
```

### Persisting the metric data in CSV format

We will persist the metric data in CSV format in the blob storage. MDM only retain data for 90 days so it would be good to have historical data archived in case we want to run different aggregation job in the future.

For requests, it will look like the following:

|Timestamp|UsageKey|SubscriptionId|AccountName|MetricName|Properties|Value|AdditionalValues|
|---------|--------|--------------|-----------|----------|----------|-----|----------------|
|2019-10-21T10:24:00.0000000Z|AvNWEU4LZ95XcAnIIdHyw0oZDwfjK+axW0RAQTJOjlY=|34B00B7C-DBF3-4108-9C4B-90F56345F712|JACKLIU-TEST|Total requests|{"Operation": "create", "ResourceType": "Patient", "StatusCode": 200, "Location": "WestUS2"}|2||
|2019-10-21T10:24:00.0000000Z|AvNWEU4LZ95XcAnIIdHyw0oZDwfjK+axW0RAQTJOjlY=|34B00B7C-DBF3-4108-9C4B-90F56345F712|JACKLIU-TEST|Latency|{"Operation": "create", "ResourceType": "Patient", "StatusCode": 200, "Location": "WestUS2"}|10.5|{"Max": 11, "Min": 10}|
|2019-10-21T10:24:00.0000000Z|AvNWEU4LZ95XcAnIIdHyw0oZDwfjK+axW0RAQTJOjlY=|9CD7E6DF-DD0A-4B20-9CB8-C364C39C8D33|JACKLIU-TEST-R4|Total requests|{"Operation": "read", "ResourceType": "Patient", "StatusCode": 201, "Location": "WestUS2"}|22||
|2019-10-21T10:24:00.0000000Z|AvNWEU4LZ95XcAnIIdHyw0oZDwfjK+axW0RAQTJOjlY=|9CD7E6DF-DD0A-4B20-9CB8-C364C39C8D33|JACKLIU-TEST-R4|Latency|{"Operation": "read", "ResourceType": "Patient", "StatusCode": 201, "Location": "WestUS2"}|9.18181818181818|{"Max": 14, "Min": 6}|
|2019-10-21T10:25:00.0000000Z|GUL4y66vBeF+eo0sSPsrjEKKCUDqoq2ZoXSMdmwbCZs=|9CD7E6DF-DD0A-4B20-9CB8-C364C39C8D33|JACKLIU-TEST-R4|Total requests|{"Operation": "search", "ResourceType": "", "StatusCode": 200, "Location": "EastUS"}|4||
|2019-10-21T10:25:00.0000000Z|GUL4y66vBeF+eo0sSPsrjEKKCUDqoq2ZoXSMdmwbCZs=|9CD7E6DF-DD0A-4B20-9CB8-C364C39C8D33|JACKLIU-TEST-R4|Latency|{"Operation": "search", "ResourceType": "", "StatusCode": 200, "Location": "EastUS"}|23.5|{"Max": 24, "Min": 23}|

For database, it will look like the following:

|Timestamp|UsageKey|SubscriptionId|AccountName|MetricName|Properties|Value|AdditionalValues|
|---------|--------|--------------|-----------|----------|----------|-----|----------------|
|2019-10-21T10:00:00.0000000Z|i33xQ9kccW7PpfwXMAIva0IbBc7e6P1SsfxlqWAwrVI=|34B00B7C-DBF3-4108-9C4B-90F56345F712|JACKLIU-TEST|TotalDataSize|{"Location": "WestUS2"}|150000||
|2019-10-21T11:00:00.0000000Z|QkKC+YJJeRDR/4K0tG0iX0gAQ+BY8h/Fq2BsPtBiH/I=|9CD7E6DF-DD0A-4B20-9CB8-C364C39C8D33|JACKLIU-TEST-R4|TotalDataSize|{"Location": "EastUS"}|238519||

The UsageKey will be used for de-dup entries, which is described in [De-duping data](#De-duping-data) section below. UsageKey will be generated deterministically per ingestion session (most likely will be hash of the monitoringAccount + metricNamespace + metricName + startTimestamp so that if the process repeats itself for the same day, it will generate the same UsageKey).

The AdditionalValues can contain additional values related to the metric. For example, in the case of latency, we can also record minimum and maximum value, as well as percentile values. Alternatively, we can also create new entry instead of putting these values in a dynamic column.

The CSV file will be saved in the blob storage with the path "metrics/d={date}/{monitoringAccount}\_{metricNamespace}\_{metricName}.csv" (e.g., metrics/d=2019-11-10/MicrosoftHealthcareApisShoeboxWestUs2_Shoebox2_TotalApiLatency.csv).

This process will only be enabled in the test and production environment. It will be disabled in the single node template.

### Load data into ADX

Before we can ingest the data into ADX, we need to create appropriate tables:

```
.create tables
    DailyUsage (Timestamp: datetime, UsageKey: string, AccountName: string, MetricName: string, Properties: dynamic, Value: real),
    DailyUsage_Staging (Timestamp: datetime, UsageKey: string, AccountName: string, MetricName: string, Properties: dynamic, Value: real)
```

The staging table is used to de-dup entries, which is described in [De-duping data](#De-duping-data) section below.

To ingest the data, we first need to authenticate against the ADX. The example below uses the AzureServiceTokenProvider but we will need to use certificate since ServiceFabric does not support per-application identity yet.

``` csharp
var azureServiceTokenProvider = new AzureServiceTokenProvider();

var kustoConnectionStringBuilder = new KustoConnectionStringBuilder(kustoEndpoint, databaseName)
        .WithAadUserTokenAuthentication(await azureServiceTokenProvider.GetAccessTokenAsync(kustoEndpoint));
```

Once we are authenticated, we need to check if we have CSV mapping. Ideally, this will already be done during deployment time but we could also choose to do it on the fly.

``` csharp
try
{
    kustoClient.ExecuteControlCommand(command);
}
catch (EntityNotFoundException)
{
    command = CslCommandGenerator.GenerateTableCsvMappingCreateCommand(
        tableName,
        mappingName,
        new[]
        {
            new CsvColumnMapping { ColumnName = "Timestamp", Ordinal = 0 },
            new CsvColumnMapping { ColumnName = "UsageKey", Ordinal = 1 },
            new CsvColumnMapping { ColumnName = "AccountName", Ordinal = 2 },
            new CsvColumnMapping { ColumnName = "MetricName", Ordinal = 3 },
            new CsvColumnMapping { ColumnName = "Properties", Ordinal = 4 },
            new CsvColumnMapping { ColumnName = "Value", Ordinal = 5 },
            new CsvColumnMapping { ColumnName = "AdditionalValues", Ordinal = 6 },
        });

    kustoClient.ExecuteControlCommand(command);
}
```

We are now ready to ingest the blob.

``` csharp
var properties = new KustoQueuedIngestionProperties(KustoDatabaseName, KustoTableName)
{
    Format = DataSourceFormat.csv,
    CSVMappingReference = KustoTableMappingName,
    IgnoreFirstRecord = true,
    ReportLevel = IngestionReportLevel.FailuresAndSuccesses,
    ReportMethod = IngestionReportMethod.Table
};

string sas = blob.GetSharedAccessSignature(
    new SharedAccessBlobPolicy()
    {
        Permissions = SharedAccessBlobPermissions.Read,
        SharedAccessStartTime = DateTime.UtcNow.AddMinutes(-5),
        SharedAccessExpiryTime = DateTime.UtcNow.AddHours(1),
    });

try
{
    IKustoIngestionResult result = await _ingestClient.IngestFromSingleBlobAsync(
        blob.Uri.ToString() + sas,
        deleteSourceOnSuccess: false,
        properties);

    IngestionStatus[] statuses = null;

    bool pending = true;

    while (pending)
    {
        statuses = result.GetIngestionStatusCollection().ToArray();

        // We will only have one status since we are ingesting the data to the same table.
        switch (statuses[0].Status)
        {
            case Status.Succeeded:
                pending = false;
                break;

            case Status.Queued:
            case Status.Pending:
                await Task.Delay(TimeSpan.FromSeconds(10));
                continue;

            default:
                // The ingestion has failed.               
        }
    }
}
catch (Exception ex)
{
    // Failed to queue the ingestion.
}
```

Once the ingestion is queued, it can take up to 5 minutes for the data to be ingested. This is explained in the [IngestionBatching policy](https://docs.microsoft.com/bs-latn-ba/azure/kusto/concepts/batchingpolicy).

Because we are ingested data from the blob storage in various regions, there will be egress charge for the ingestion.

### Persisting the last processed time for each account

After the data is ingested successfully by ADX, we will persist the timestamp of the next start time to the table storage using the {monitoringAccount} as partition key and {metricNamespace}\_{metricName} as the row key.

If the process fails at any given point before the timestamp is written, the process resumes from the previous start time. If the previous process already created the CSV file, it will be overwritten. If the previous process already sent the data to ADX and is already ingested by ADX, the de-duping data process will ensure that no duplicate will show up in the final table.

### De-duping data

We will try to avoid writing duplicated data but sometimes it happens. For example, the process could fail after triggering the ingestion in ADX but before updating the last processed timestamp. When the process resumes, it starts from the previous checkpoint and now duplicated data is being ingested again.

To avoid this problem, we can first ingest the data in a staging table then only merge the data that's not in the actual table with transaction.

First, we will create a function that joins the two table together using anti kind based on the usage key. This will select all entries in DailyUsage_Staging table but not in HourlyUsage table.

```
.create function RemoveDuplicateDailyUsage()
{
    DailyUsage_Staging
        | join hint.strategy = broadcast kind = anti (DailyUsage | where Timestamp >= ago(5d) | distinct UsageKey) on UsageKey
}
```

We will then setup update policy such that when data is ingested into DailyUsage_Staging, it will automatically trigger RemoveDuplicateDailyUsage function and then merge the results from that function into the DailyUsage table.

```
.alter table DailyUsage policy update
@'[{"IsEnabled": true, "Source": "DailyUsage_Staging", "Query": "RemoveDuplicateDailyUsage()", "IsTransactional": true, "PropagateIngestionProperties": true}]'
```

Because we are using update policy to trigger the function, ADX automatically "scope" the data so that only newly ingested records are passed in.

Finally, we will setup the retention policy on the staging table to be shorter. The number of days for the retention policy depends on how long we think the job could potentially been interrupted due to maintenance, upgrade, or any other situation.

```
.alter-merge table DailyUsage_Staging policy retention softdelete = 5d
```

The downside of this approach is that the ingestion is more expensive but it will prevent duplicated data and give us accurate result.

# Test Strategy

Unit tests will be written to test the functionality of each component.

E2E tests will be written to make sure the data is being summarized and exported correctly. The test can programmatically access both data from MDM and from ADX and compare the results. Writing a test that triggers action and wait for the data to be exported might be difficult (since the background task runs on a schedule). We can do a few things here:

1. In the test environment, we can change the frequency of the background job. Instead of running it daily, we could change it to run at much shorter interval so that the test could in theory run a few scenarios that generates different type of events and wait for the data to be ingested. The test can be set to run only during periodic test instead of running on every PR to reduce the time that PR takes.
1. The E2E tests could get data from MDM and from ADX and compare them for the previous day.

Manual tests will also be used to verify that the exported data matches what we expect. We can look at the data from multiple sources (MDM, raw logs, and etc.,) to make sure the numbers are correct.

# Security

The source data is already summarized and doesn't contain PII. None of the dimensions contains PII data.

# Other

We will need provision an instance of ADX prior to enable this pipeline. The ADX instance will most likely be in our non-production subscription because we want to be able to access these report using our Redmond credentials.

This work also depends on the following user stories:

- [User Story 70321: Emit metrics for Collection Storage Size](https://microsofthealth.visualstudio.com/Health/_workitems/edit/70321)
- [User Story 70213: Emit more accurate FHIR API metrics](https://microsofthealth.visualstudio.com/Health/_workitems/edit/70213)

# Additional considerations

We could also choose to persist hourly usage for some time. This could give us some operational insights such as looking at traffic throughout day over time. The daily usage can be stored for long period of time (or indefinitely) whereas hourly usage can be shorter such as 6 months.

Another thing we could do is to persist over minute aggregated data from MDM to CSV file so that we can still access the raw data after 90 days. This is useful if we want to run different job to summarize the data even after 90 days. Some values can be aggregated easily (e.g., count, average, min, and mix) but some values might be more difficult (e.g., percentile).

