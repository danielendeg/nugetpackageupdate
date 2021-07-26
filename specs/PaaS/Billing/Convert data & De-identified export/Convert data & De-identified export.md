[[_TOC_]]

# 1. **Business Justification**

Azure API for FHIR currently charges users in three aspects: service lifetime, network egress and Cosmos DB usage. Among all the fantastic functionalities in Azure API for FHIR, there are two features that are attracting growing attention: [*De-identified export*][azure-fhir-deid] and [*Convert data*][azure-fhir-converter]. Charge for those two features will drive the team to provide better solutions and services as well as optimize customers' experience.

# 2. **Scenarios**

When customers use the [*De-identified export*][azure-fhir-deid] and [*Convert data*][azure-fhir-converter] features to manage their data, they will be charged according to the specific usage volumes.

# 3. **Design**

## 3.1 Overview

The billing module mainly contains three parts now, separately are metrics collection, usage handling and usage report. [Billing agent][billing-agent] firstly collects metrics, then processes those metrics to standard usage format, finally reports usage data to PAV2 from where commerce team will handle usage data further. The following picture shows the current billing modules and their billing mechanisms:

![Current Billing Modules][current-billing-modules]

### 3.1.1 Metrics configurations

Now the PaaS has been charging for network egress for a while, and related billing metrics account and namespace are already settled, the *Convert data* feature and *De-identified export* feature can reuse them instead of applying new metrics account credentials.

The same as egress feature, we should add related feature configuration sections in [environment groups](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdeployment%2FenvironmentGroups), the configuration section is in `BillingFeature` format and is used to judge whether to execute usage retrieving process or not. The billing feature configuration are as follows:

> *Convert data*

| Property | Content | Comment |
| :- | :- | :- |
| name | convertData | |
| enabled | true | |
| generalAvailabilityDateTimeUtc | | |
| allowlisted | true | |

> *De-identified export*

| Property | Content | Comment |
| :- | :- | :- |
| name | deIdExport | |
| enabled | true | |
| generalAvailabilityDateTimeUtc | | |
| allowlisted | true | |

> Example

```json
"features": [
    {
        "name": "convertData",
        "enabled": true,
        "generalAvailabilityDateTimeUtc": "",
        "allowlisted": true
    },
    {
        "name": "deIdExport",
        "enabled": true,
        "generalAvailabilityDateTimeUtc": "",
        "allowlisted": true
    },
],
```

### 3.1.2 Meter configurations

Meter ID is a GUID that is used to mark the billing module while communicating with commerce team. The newly added *Convert-Data* and *De-identified export* should also have their meter IDs. To use the meter ID correctly, we should also update the [environment groups](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdeployment%2FenvironmentGroups) configuration section. In each location item under `billing.locations`, we can add the following configurations:

```json
{
    "convertDataMeter": "$GUID",
    "deIdExportMeter": "$GUID"
}
```

To use these meter IDs programmatically, we should add the following properties in [ServiceEnvironment](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FCommon%2FServicePlatform%2FServiceEnvironment.cs&version=GBmaster&line=299&lineEnd=300&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents):

```C#
public string BillingConvertDataMeter { get; set; }

public string BillingDeIdExportMeter { get; set; }
```

To inject configurations in environment groups to ServiceEnvironment, we should also add the following sentences in [Get-ServiceFabricDeploymentParameters.ps1](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdeployment%2Fscripts%2FGet-ServiceFabricDeploymentParameters.ps1&version=GBmaster&line=130&lineEnd=131&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents):

```powershell
"BillingConvertDataMeter" {
    $environmentGroupArgs.billing.locations."$regionShortId".convertDataMeter
}

"BillingDeIdExportMeter" {
    $environmentGroupArgs.billing.locations."$regionShortId".deIdExportMeter
}
```

## 3.2 Metrics Collection

### 3.2.1 Metrics object

The mainly chargeable metrics is data volume, and we concentrate on different data flow directions for *Convert data* and *De-identified export*(see Appendix: Metrics definitions for detailed metrics structure description), i.e. we charge *Convert data* feature based on request data volume, and charge *De-identified export* feature based on response data volume, the reasons are as follows:

> *Convert data*

We charge customers based on request data volume, this strategy is related with the internal process details. The engine of *Convert data* feature is [FHIR-Converter][fhir-converter], it contains three processing phases, separately are pre-processing, rendering and post-processing.

The rendering and post-processing phases are all tightly associated with conversion templates that are provided by users and are unpredictable. The pre-processing phase is about input data handling and the workload is forseeable. If malicious users create tons of response data based on a small input, just ignore them.

As for the billing unit here, we write down the request data volume in byte size to metrics system, then we count **1 quantity per gibibyte** in usage records.

> *De-identified export*

We charge customers based on response data volume(asynchronously), all the process workload is linearly dependent on the data response size, so directly charging for it is a straightforward strategy. Meanwhile, the cross-region network overhead will be charged in egress billing module, which has been implemented yet.

As for the billing unit here, we write down the response data volume in byte size to metrics system, then we count **1 quantity per gibibyte** in usage records.

> Summarize

*The billing quantity*

| Billing module | Billing object | Billing unit in metrics | Billing unit in usage records |
| :- | :- | :- | :- |
| *Convert data* | Request data volume | byte size | 1 quantity per gibibyte |
| *De-identified export* | Response data volume | byte size | 1 quantity per gibibyte |

*The malicious scenarios(that we just ignore)*

| No. | Feature | Scenario |
| :- | :- | :- |
| 1 | *Convert data* | User send small input data, align with complex templates that can produce large output |
| 2 | *De-identified export* | User send export request, align with complex configuration that can consume a lot of CPU resources and produce small output |

### 3.2.2 Register metrics in Geneva

The production phase and consumption phase of metrics should negotiate a consistent metrics name and create metrics writer and reader based on that name. The following table describes the metrics names that are going to be used by the billing features.

| Feature | Metrics Name | Comment |
| :- | :- | :- |
| *Convert data* | "BillingConvertData" | Based on the rule: `concat('Billing', $featureName)` |
| *De-identified export* | "BillingDeIdExport" | Based on the rule: `concat('Billing', $featureName)` |

We have to [register these metrics in Geneva](https://jarvis-west.dc.ad.msft.net/settings/mdm?account=ResoluteProd-Billing&namespace=ResoluteProd-Billing&metric=BillingEgress&tab=metrics) before we can use them. To use the metrics in different scenarios, we should register them with different accounts and namespaces. The following tables describes all the accounts and namespaces we should take care of:

| Account | Namespace | Metric Name | Usage |
| :- | :- | :- | :- |
| ResoluteNonProd-Billing | ResoluteNonProd-Billing | BillingConvertData | Non-production usage of *Convert data* feature |
| ResoluteNonProd-Billing | ResoluteNonProd-Billing | BillingDeIdExport | Non-production usage of *De-identified export* feature |
| ResoluteProd-Billing | ResoluteProd-Billing | BillingConvertData | Production usage of *Convert data* feature |
| ResoluteProd-Billing | ResoluteProd-Billing | BillingDeIdExport | Production usage of *De-identified export* feature |

Above accounts and namespaces should be consistent with related [environment group configurations](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fdeployment%2FenvironmentGroups%2Ftest.json&version=GBmaster&line=473&lineEnd=474&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents), like the following snippet:

```json
"genevaMetricsConfig": {
    "metricsAccountName": "ResoluteNonProd-Billing",
    "metricsNamespace": "ResoluteNonProd-Billing",
    "metricsAccountEnvironment": "Int"
},
```

### 3.2.3 Write metrics in upstream

The whole metrics handling process is a data flow, the Geneva is in the middle of the flow, the collection of original usage information(the request data volume in *Convert data* and the response data volume in *De-identified export*) is the upstream, to where we should write metrics based on original usage information.

In order to intercept all the data request/response usage information, a straightforward way is to insert a middleware in [frontend service][paas-frontend-service], just like what the network egress billing module is doing. The middleware can determine which feature is being used for the current request(by parsing keywords from request path) and write down metrics.

The network egress billing module uses `ByteCountingStreamMiddleware` to count response data volume and uses `BillingResponseLogMiddleware` to write down the data volume to Geneva. The `ByteCountingStreamMiddleware` is reusable but it only counts the response data volume, we can add an extra process to count request data volume and store it in context.

> *Convert data*

The *Convert data* feature need to count request data volume, the following table describes all the middlewares needed for writing metrics of *Convert data*.

| Middleware | Functionality | Operation | Execution Order |
| :- | :- | :- | :- |
| ByteCountingStreamMiddleware | Count request body size as well as response body size, store in context | Update | 1 |
| ConvertDataLogMiddleware | Determine requests using *Convert data* feature and write request data volume to Geneva | Create New | 2 |

All the middlewares should be registered in `Startup` of [frontend service][paas-frontend-service] through `IApplicationBuilder`, the order of execution should be treated with caution and should be ensured by invoking `RequestDelegate` in appropriate phase.

> *De-identified export*

The metrics collection phase of *De-identified export* is a little different from *Convert data*, since the *De-identified export* operation is asynchronous and the results will be directly written to blob storage. Based on these characteristics, we can't use middleware to intercept usage metrics in frontend service.

Let's try to decompose the *De-identified export* operation: [the OSS](https://github.com/microsoft/fhir-server/blob/1438cae4d2c6888a7e80b59591cc268f3a1fe4ff/src/Microsoft.Health.Fhir.Core/Features/Operations/Export/ExportJobTask.cs#L245) firstly publishes a notification with `ExportTaskMetricsNotification` type, and the PaaS receives the notification in `ExportTaskMetricsNotificationHandler`. When the handler is called, it means a *De-identified export* operation has finished, we can write metrics inside the handler. Before updating the handler, we should also record the relations between resources and operation job IDs, then we can smoothly collect the metrics.

The following snippets are the handling logic of the [ExportTaskMetricsNotificationHandler](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FFhirApplication%2FFhirService.Core%2FFeatures%2FMetrics%2FExportTaskMetricsNotificationHandler.cs&version=GBmaster&line=32&lineEnd=33&lineStartColumn=1&lineEndColumn=1&lineStyle=plain&_a=contents) and the detailed structure of `ExportTaskMetricsNotification`. The `Handle` method already contains the `_resourceId`, which points to the resource from which we are collecting metrics. Then we can judge whether the current operation is *De-identified export* or not by `IsAnonymizedExport`, and we can also know the exact anonymized data size(i.e. the response data volume) by `DataSize`. We should pay extra attention to the `Status` property since it tells us whether this export job is successfully finished or not.

```C#
public Task Handle(ExportTaskMetricsNotification notification, CancellationToken cancellationToken)
{
    EnsureArg.IsNotNull(notification, nameof(notification));

    _logger.LogInformation(GenerateMetricsMessage(_resourceId, notification));

    return Task.CompletedTask;
}
```

```C#
public class ExportTaskMetricsNotification : IMetricsNotification, INotification
{
    public ExportTaskMetricsNotification(ExportJobRecord exportJobRecord);

    public string FhirOperation { get; }
    public string ResourceType { get; }
    public string Id { get; }
    public string Status { get; }
    public DateTimeOffset QueuedTime { get; }
    public DateTimeOffset? EndTime { get; }
    public long DataSize { get; }
    public bool IsAnonymizedExport { get; }
}
```

### 3.2.4 Read metrics in downstream

There are ready-made [MdmMetricReader](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FResourceProviderApplication%2FBillingAgent%2FMetrics%2FMdmMetricReader.cs) that can read metrics between a given time range and [BillingMetricReaderFactory](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FResourceProviderApplication%2FBillingAgent%2FMetrics%2FBillingMetricReaderFactory.cs) that can provide necessary arguments to create a MdmMetricReader. We can directly use them and specially notice the consistency of metrics names between upstream and downstream.

### 3.2.5 Holistic view of metrics collection flow

The following picture is a holistic view of metrics collection flow, the main drivers in upstream is middlewares inserted in frontend service and the export job handler, and the main drivers in downstream is usage getters and usage reporters.

![Metrics Collection Flow Chart][metrics-collection-flow-chart]

## 3.3 Usage Information Handling

Standard data format of usage information is `BillingUsageRecordSet`, which could be directly sent to usage report phase. The ideal scenario is that all the upstream of usage information straightway generate standard usage records, however it's a tough approach given the complexities of usage information and upstream types. So we should use different usage record handlers to deal with different usage information.

The handler is called usage getter, its functionality is to request metrics and wrap them to `BillingUsageRecordSet` format. The network egress billing module uses [EgressUsageGetter](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FResourceProviderApplication%2FBillingAgent%2FBillingReporter%2FEgressUsageGetter.cs) to handle egress metrics, analogously, we can add `ConvertDataUsageGetter` and `DeIdExportUsageGetter` to handle related metrics.

| Handler | Functionality | Comment |
| :- | :- | :- |
| ConvertDataUsageGetter | Retrieve metrics of *Convert data* feature and wrap them to `BillingUsageRecordSet` | |
| DeIdExportUsageGetter | Retrieve metrics of *De-identified export* feature and wrap them to `BillingUsageRecordSet` | |

The [billing agent][billing-agent] uses getters to retrieve usage records periodically and report them to usage reporters.

## 3.4 Usage Record Reporting

Usage records will be pushed to the PAV2 usage table, in the meantime the partition key of the usage records set will be reported to the PAV2 queue. The commerce team will listen on the queue, dequeue the partition key and retrieve usage records from the PAV2 usage table by the partition key.

There are ready-made [UsageReporter](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FResourceProviderApplication%2FBillingAgent%2FBillingReporter%2FUsageReporter.cs), [BillingStorage](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FResourceProviderApplication%2FBillingAgent%2FBillingStorage%2FBillingStorage.cs) and [UsageReportingClientFacade](https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FResourceProviderApplication%2FBillingAgent%2FBillingStorage%2FUsageReportingClientFacade.cs) to handle usage records reporting works, we can directly reuse them after retrieving usage records from usage getters.

## 3.5 Exception Handling

### 3.5.1 Billing agent exception

There might be exceptions occur while the billing agent is running, the basic strategy of handling such exceptions is retrying, anything breaking the billing loop will cause the loop run from beginning again.

### 3.5.2 Metrics collections exception

In some cases, we shouldn't collect metrics and just let the requests go, all the scenarios are separately listed as follows:

| No. | Feature | Scenario | Strategy |
| :- | :- | :- | :- |
| 1 | *Convert data* | Operation responses with 4xx errors | Don't collect metrics |
| 2 | *Convert data* | Operation responses with 5xx errors | Don't collect metrics |
| 3 | *De-identified export* | Operation responses with 4xx errors | Don't collect metrics |
| 4 | *De-identified export* | Operation responses with 5xx errors | Don't collect metrics |
| 5 | *De-identified export* | Invoker cancels the operation | Don't collect metrics |
| 6 | / | All other cases when the operation doesn't work normally, and doesn't respond meaningful results | Don't collect metrics |

# 4. **Test Strategy**

## 4.1 Unit tests

Use mocked context to test fine-grained components.

## 4.2 Integration tests

To test whether metrics system and meter system are working as expected, we should deploy the updated project to test subscription, and register this test resource provider in test Azure environment(Dogfood), test according to pre-designed test scenarios. The following table describes such scenarios:

| No. | Category | Scenario | Operation |
| :- | :- | :- | :- |
| 1 | / | Do nothing related with *Convert data* and *De-identified export*, see whether these two features will be billed | |
| 2 | / | Send several request, see whether the usage records will be stored correctly | |
| 3 | / | Send several normal request, see whether the meter system(usage queue & table) stores usage records correctly | |
| 4 | / | Send request with small input data and large templates, see whether the correct usage records will be stored | |

# 5. **Security**

The billing agent is a standalone service and will not interact with other system modules in runtime. The crash of billing agent will cause it to run again without breaking any other normal processes.

All the input of billing agent are internally generated and the output will be used internally as well. The malicious data from attackers will not influence the functionality of billing agent.

# 6. **Other**

## 6.1 Billing loop time

Since the billing agent will loop all the accounts of Azure API for FHIR, and a single loop period is 1 hour, the total execution time of a loop shouldn't exceed 60 minutes. The average execution time of Gen 1 billing agent is 10 minutes per loop [according to Eric Trumble](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/15364?_a=files&path=%2Fspecs%2FPaaS%2FBilling%2FBillingAgentV2%2FBillingAgentV2.md&anchor=lengthy-execution-time), and most of the execution time is occupied by commerce API invoking.

After the two new billing features are added to PaaS, the total execution time of Gen 1 will increase by at most 10 minutes(assume one type of metrics operation will take 5 minutes and the two features are frequently used, the actual execution time will be less than the estimation) and the total execution time will be less than 20 minutes. The time could be decreased more deeply if we do the statistics in parallel and the total execution time will not influence the availability of the billing agent.

If we assume there are 2 parallel slots(a maximum of two billing features can be operated at the same time) while billing agent is running, the following picture is the time usage comparison before and after the new billing feature are added:

![Billing Loop Gantt Chart][billing-loop-gantt-chart]

## 6.2 Effective billing window

The billing process might be delayed because of exception circumstances, each time the billing loop fails, it will restart and bill from the failure point until current datetime. However the retrospection could only be executed in [a finite windows of 48 hours](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs/pullrequest/15364?_a=files&path=%2Fspecs%2FPaaS%2FBilling%2FBillingAgentV2%2FBillingAgentV2.md&anchor=exception-handling-%26-retrying) and the missed billing details will not be found any more.

## 6.3 Quantity hint in response

We can also add the quantity in response header to let customers know how many quantities they are charged for.

## 6.4 Open questions

### What if resources are moved to another subscription between billing loops?

Say we have a FHIR resource which has created a metrics set *M*, and it's moved to another subscription before the next billing loop coming, then the metrics set *M* can't be correctly charged since the real URI of the resource has changed and are different from that in *M*.

# 7. **Appendix**

## 7.1 Metrics definitions

*Convert data* and *De-identified export* share the same metrics definition, the following table describes the metrics definition.

| Property | Description | Example |
| :- | :- | :- |
| $value | The byte size of target object | 65536 |
| MeterId | The billing resource guid configured with Commerce team to define billing category | b315ced8-f684-47d7-b42b-e84517aac135 |
| ResourceId | The resource ID of the resource fo which the metrics is emitted | /subscriptions/cc148bf2-42fb-4913-a3fb-2f284a69eb89/resourceGroups/xiatia/providers/Microsoft.HealthcareApis/services/ferris-fhir-extension |

[azure-fhir-deid]:https://docs.microsoft.com/en-us/azure/healthcare-apis/fhir/de-identified-export
[azure-fhir-converter]:https://docs.microsoft.com/en-us/azure/healthcare-apis/fhir/convert-data
[fhir-converter]:https://github.com/microsoft/FHIR-Converter
[paas-frontend-service]:https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FFrontendApplication%2FFrontendService
[billing-agent]:https://microsofthealth.visualstudio.com/Health/_git/health-paas?path=%2Fsrc%2FResourceProviderApplication%2FBillingAgent%2FBiller.cs
[current-billing-modules]:assets/Current%20Billing%20Modules.jpg
[billing-loop-gantt-chart]:assets/Gantt%20Chart%20of%20Billing%20Loop%20Time.png
[metrics-collection-flow-chart]:assets/Metrics%20Collection%20Flow.png