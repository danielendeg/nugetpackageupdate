# Shoebox Application logging Design

[[_TOC_]]

# Scenarios
Below are the diagnostics logs from the PM spec that we need to support.

* Diagnostics logs:
    + Application Logs:
        - Critical, Error- P1
       - Warning, Information  - P2
        - As we cannot expose stack traces for our internal code we can use friendly messages for exceptions for customers can figure out what went wrong and file a support ticket if necessary. They will need enough information to have an idea of what went wrong.

# Metrics

The number of customer's that have opted into diagnostic logging. This being tracked on the Azure side as they use this information if we enable a bloom filter in our Derived Event queries. There is an Azure Portal Telemetry Kusto Cluster that we can request access too for this information.

# Design
We will make use of the same pattern we use for Azure Monitor Audit logs. Which is to use a singleton logger that uses an event source. This will give us the greatest granularity to determine exactly what logs should be logged to customers.

For logging exceptions to customers we will take the approach below. The goal is to log exceptions that have actionable outcomes for customers. Example, CMK related errors caused by Key issues. The Customer is in control of the Key in question and can resolve the issue based on the error message presented in the log and the public documentation we have published for CMK. These errors are already surfaced for operations returned by the FHIR service. However, from an administrator point of view the logs need to be accessible at the resource level in Diagnostic Settings to satisfy the Azure Security requirment.

For this design we will make use of the mediator pattern for processing exceptions using notifications and a handler. We will create a mediator, within a middleware, in the OSS FHIR instance to send notifications, for all exceptions, containing the properties for the exception object and associated data for the current request. We will then create a handler on the PaaS side to consume these notifications and process using business logic to filter for the logs we want to send to customers and to make sure the content added to the diagnostic log is customer appropriate. There may be some logs that are thrown before the middleware processing is started. In these cases I would argue the exception happened at a level where it would not be usful to the customer to know about it and this would be the type of exception we would catch from our internal monitoring and fix the issue with the fhir server app. 

There were other considerations for tagging logs using an eventId or using a logging extension to add a tag in a different way. The issues with these approaches is that we add code that is only needed for PaaS into OSS. Managing eventIds and when a logging extension is used for new logs will create extra overhead for OSS code reviews to prevent logs filtering up through our system to customers logs. Using a singleton logger and the mediatr pattern allow us to decouple this internal concept of customer logs from OSS and allows us to be explicit about what we want to surface.

## Guidelines for logs to be viewed by customers
	• Message field must be customer appropriate. A friendly message with no stack trace or internal only information.
	• Log must contain Azure ResourceId in order for the log to be routed for shoebox.
	• Log must contain the name of the operation that it is emitted from.
        In a nutshell we should use the rbac names for opertaion types or if that isn't compatible with our scenario then a very limited set of operations as granular as the ARM child resource level. So FHIR and DICOM would have their own operation names. Ex. format <providerName>/<resourceType>/<subtype>/<Write/Read/Delete/Action>
        For more details on the properties including operationName conventions see the shoebox onboarding documentation. https://microsoft.sharepoint.com/:w:/t/azureteams/docs/ERgWAWHE3E9IhXtNKNcMhZABUP56WkcLwOxtgZFp6hzmfA?rtime=zkHWB5Il10g

### Example MDS Config Change
We will only make events for TraceTelemetry, RequestTelemetry and ExceptionTelemetry. The other *Telemetry tables would not contain useful information for the customer.

``` XML
<EtwProvider name="AzureMonitorDiagnosticLoggingProvider" format="EventSource" storeType="CentralBond" duration="PT1M">
        <Event name="Diagnostic" eventName="AzureMonitorDiagnostic" />
</EtwProvider>

…

<DerivedEvent eventName="AzureMonitorDiagnosticShoeboxExceptions" storeType="CentralBond" duration="PT1M" source="ExceptionTelemetry" account="AuditStore">
        <Query>
          where logToCustomer == "true" && ApplyBloomFilter("SHOEBOX_BLOOM_FILTER", resourceId)
          select time, resourceId, operationName, category, resultType, resultSignature, message, durationMs, correlationId, level, location, uri, properties
        </Query>
</DerivedEvent>

…

<EventStreamingAnnotation name="^AzureMonitorDiagnosticShoeboxExceptions.*">
      <OnBehalf>
        <Content>
          <![CDATA[<Config
                     onBehalfFields="resourceId,category"
                     primaryPartitionField="resourceId"
                     containerSuffix="$category"
                     validJsonColumns="identity,properties"
                     excludeFields="TIMESTAMP"/>]]>
        </Content>
      </OnBehalf>
    </EventStreamingAnnotation>

```

# Test Strategy
We will test by verifying the logs in a new table that gets created in Geneva. We can leverage our TestResoluteNonProd namespace to prevent polluting ResoluteNonProd. We will be testing using the Derived Event without the bloomfilter. Shoebox will pick up these logs just like the audit logs that are tagged. So we will see these in blob storage for Diagnotics Settings when we attach a blob. There is no extra configuration we need to do after they are tagged at the monitoring agent level in order for them to get picked up by shoebox. We will want to verify the new Log Analytics schema after it is deployed.

# Security
We are not logging any new information that we have not already been logging to Geneva. These new shoebox logs will contain less data then our current logs. We will also not be exposing any stack traces as part of exception based application logs.

# Other
We should write up an SOP for reviewing and testing new logs we surface to the customer to make sure it only contains the data we want.
