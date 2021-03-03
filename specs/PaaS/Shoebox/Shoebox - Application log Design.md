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
We will make use of the mediator pattern for processing exceptions and a separate event source logger to emit the customer logs. We will create a mediator in the OSS FHIR instance to send notifications, for all exceptions, containing the properties for the exception object and associated data for the current request. We will then create a handler on the PaaS side to consume these notifications and process accordingly to filter for the logs we want to send to customers and to make sure the content added to the diagnostic log is customer appropriate.

## Guidelines for logs to be viewed by customers
	• Message field must be customer appropriate. A friendly message with no stack trace or internal only information.
	• Log must contain Azure ResourceId in order for the log to be routed for shoebox.
	• Log must contain the name of the operation that it is emitted from.

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
We are not logging any new information that we have not already been logging to both Geneva and AI. These new shoebox logs will contain less data then our current logs. 

# Other
We should write up an SOP for reviewing and testing new logs we surface to the customer to make sure it only contains the data we want.
