The purpose of this document is to detail current requirements and architecture of the Azure IoT Connector for FHIR and cover potential alternative architectures and their relative merits.

[[_TOC_]]

# Business Justification

Now that the Azure IoT Connector for FHIR is available in Public Preview the team has several requirements to meet as we work towards GA.  Those requirements include supporting Azure Security Benchmarks, billing meters, and general service improvements.

# Scenarios

## Functional Requirements

1. **Late arriving data**: In remote scenarios it is common to have infrequent connection to a network.  The system should support resolving data that arrives hours, days, or even weeks after it was initially recorded.  Such data needs to be appropriately record in the patient record for the proper time period based on the user's configuration.

1. **Out of order data**: It is common for data not come in chronological sequence.  Our architecture needs to account for this and map and order data according to time appropriately.

1. **Duplicate data**:  It is common data to be duplicated at many different points into the process.  The client or gateway  data can send data for processing but not get the acknowledgement that it was received.  In this case the service will have the data but the client will retransmit because it .  Because of this we have an at least once processing requirement and need to ensure the times data is transmitted or processed multiple times the result is consistent and there is no duplication.

1. **Link data to patient**: Data ingested by the service needs to resolve identifiers from the FHIR service (example device identifier) and properly link the resources created (Observations) to the patient.

1. **Support high frequency data ingestion**: There is a need to support data frequencies from devices at per second and faster latency.  This unlocks scenarios to process and evaluate the data as soon as possible.  The service needs to be able to scale to meet these scenarios and manage the data volume in a way that doesn't overwhelm the FHIR server.

1. **Manage FHIR server load**: The service should manage the data volume devices and ensure the load on the FHIR is predictable and manageable.

1. **Support device data projection**: It is common for devices to send multiple data points to the cloud in one message.  The current architecture allows the customer project that message into smaller messages that can be represented independently in FHIR.

1. **Support multiple device formats**: The are multiple device data schemas in the industry today.  It is important to be able to support any eligible device. The current architecture allows for any serialized UTF-8 JSON payload to be used provided required data points can be mapped (device id and observed time).  We also have the option of supporting non-JSON formats in the future.

1. **FHIR resource management**: The IoT solution should be able to ingest high frequency data sets (devices sending ) while maintaining a manageable number of FHIR resources.  At such high ingestion ranges it isn't appropriate to create an observation for each value.  Instead the current implementation allows the user to configure grouping by time periods (according to the hour or day) or correlation id.

## Other Requirements

1. **Low cost**:  In order to increase adoption we want to provide the service at the lowest cost point possible.  The current architecture has a cost of roughly $100 a month at the lowest ingestion rates (1 MB/sec). One challenge with the current services to compose the Azure IoT Connector for FHIR is the cost is based on reserved capacity and not necessarily tied to utilization.  An important goal of any new architecture is to reduce both the overall cost of run the service and cost of the service when there is low to no utilization.

1. **Support Azure security benchmarks**: As an Azure service we have serval requirements we need to meet.  They include support for RBAC, Private Link, and BYOK (bring your own key) encryption.  The current architecture has several blockers as the support varies across the services we are built on.

1. **Support privacy requirements**:  A crucial requirement is support for the various health and privacy regulations (HIPAA, HITRUST, GDPR).  One of the strength of the current design is no data is stored permanently in the connector.  It either has a short life time (7 days for event hub) or only exists in memory (Compute and Stream Analytics).

# Design

## Current Architecture

### Event Hub

Each instance of the Azure IoT Connector for FHIR deploys a standard SKU Event Hub namespace.  That namespace has two event hubs.  The first event hub is a write only endpoint for the customer to send their device data to.  An Azure function reads from device Event Hub and performs the normalization logic applying the user supplied device mapping template.  The output from this process is send to the normalized data Event Hub.  Data output to the normalized Event Hub is partition according to device id ensuring all data for a given device has an affinity to a specific partition.

### Stream Analytics

Stream Analytics is used to group data according to a configurable time period (15 minutes in PaaS), device, semantic type, and optionally patient and encounter.  This grouping processing allows us to control how often data is egressed to the FHIR server.  It also simplifies the processing requirements downstream.  Grouping by device and semantic type provides an easy unit of work for the FHIR conversion process to consume.  By using an Azure Function as the output to the query we able to avoid any additional at rest storage of the data.  Both Stream Analytics and the output Azure Function.  Stream Analytics supports other query outputs (Event Hub & Storage) but they either can't support the size of the output (Event Hub, max 1 MB message) or would result in additional storage of health care data (Storage Blob or Tables).

Another advantage of Stream Analytics is we are guaranteed delivery of each time period to the output (Azure Function).  If there is an unhandled exception on the job output it is retried until successful.

In addition, Stream Analytics handles several time based problems that can occur when dealing with data streams.  More information can be found [here](
https://docs.microsoft.com/en-us/azure/stream-analytics/stream-analytics-time-handling).

### Compute

There are two processes hosted on Azure Functions today.  The first, covered in the Event Hub section, is responsible for the device data normalization.  The second is invoked as a job out from the Stream Analytics job.  The function receives batches of normalized data grouped according to device and type.  When processing the batches the FHIR conversion function performs several tasks:

* Sort and align data according to the time of occurrence.
* Resolve any needed identifier from the FHIR server.
* Create a deterministic identifier for the device data.
* Create a new observation or merge newly arrived data with existing observation.

Azure Functions are used for a few reasons:

* Existing support for a scalable & reliable Event Hub processor.
* Supported as an output from Stream Analytics.

### Other Components

1. **Azure Storage**:  Azure Storage is used to support the Event Hub processing.  It has two roles.  The first is the storage of the each event hub partition's stream position.  As data is ingested and normalized, each partition's position (also known as the *watermark*) is saved to blob.  The processor also uses the Azure Storage to maintain lease locks for coordinating access to partitions.  Azure Storage is also used to store the mapping templates necessary to normalize device data and convert the normalized and grouped data into FHIR.  Templates are loaded from blob during each function execution ensuring the latest version is used during processing.

1. **Azure Key Vault**: Secrets for accessing the Event Hub and Azure Storage and stored in the key vault and accessed by the Azure Function.  The function's managed identity is granted access to the key vault so secrets can be retrieved.

1. **Application Insights**: The compute for normalization and FHIR conversion hosted on Azure Functions is configured to use Application Insights for logging.  In the managed version of the service logs and metrics are also sent to Geneva.

## Proposed Architectures

### Keep Current Architecture

#### Advantages

1. Stream Analytics and Event Hub provide a proven reliable and scalable backbone for processing messages.

1. Data exists transiently through the pipeline till it reaches the destination FHIR server.

1. Customer has a ready to use event endpoint.

1. Infrastructure is dedicated to the customer.

1. Mirror's OSS architecture.

#### Disadvantages

1. Event Hub endpoint not visible on customer subscription.  Because we host the event hub endpoint the customer doesn't have the visibility which limit's their ability to connect.  This also posses problem for securing the Event Hub with RBAC.  The identities that need access to the Event Hub endpoint are on the customer's tenant.  At this time, it doesn't appear possible to grant access to customer tenant accounts on a Microsoft tenant hosted Event Hub.

1. Stream Analytics adds additional costs.

1. Stream Analytics is limited to calling Azure Functions hosted on consumption or App service plans using the default host name pattern.  This restricts us to hosting our compute on App Services using App Service Plans to be compliant with Geneva logging requirements.

1. Stream Analytics today has major limitations supporting Azure benchmark requirements like BYOK and Private Link.

1. If a single partition is overloaded Stream Analytics can run out of memory preventing progress.

1. Compute is restricted to Azure Functions running on App Service Plans for Geneva support.  Not the most cost effective or scalable compute solution.

### Shared Event Hub Namespace

The current architecture creates a new Event Hub namespace for each IoT Connector instance.  Instead the proposal is to have one Event Hub namespace shared for all connectors on a given FHIR service.  Billing for the Event Hub is at the namespace level for the number TUs (throughput units) reserved for the namespace.  Sharing the namespace will reduce the total cost of operation for a customer using multiple IoT Connectors at low capacities.  For example, it would be possible to run several connectors receiving data for from thousands of devices if the measurement frequency is a few readings per day.

The namespace is also where Private Link configuration is done. One namespace will simplify the setup required for private link.

A standard Event Hub namespace is limited to 10 event hubs which would limit us to 5 or 10 connectors for a given FHIR service.  The number depends if we host the normalized data event hub on the namespace in addition to the required device data event hub.

#### Advantages

1. Allows multiple IoT Connectors with out additional cost until the throughput limits are increased.
1. Less Event Hub quota consumed on the subscription.
1. Simplifies Private Link setup.

#### Disadvantages

1. Capacity isn't reserved for each connector.
1. Hard limit of 5 or 10 connectors.
1. Not backwards compatible with current Public Preview deployments.
1. Provisioning & Deprovisioning is more complicated, some resources are shared.

### Remove Stream Analytics

Another option is replacing Stream Analytics with another method for grouping and buffering data.  The two main goals with this change are reducing cost and increasing our flexibility for compute.

The goal would be to keep the grouping logic memory based without any additional storage if possible.  One way to achieve this is create a custom Event Hub processor connected to the normalized data Event Hub.  Rather than operating solely on a time based window, data would be sent to the FHIR when X number of records were received or after Y period of time, which ever occurs first.  This evaluation would be done per partition.  An example would be data is sent to the FHIR server when we reach 1000 events or when 5 minutes have passed, which ever occurs first.

Since we are now handling the grouping logic care needs to be taken on our end to ensure we are accounting for the various time considerations we currently get automatically with [Stream Analytics](
https://docs.microsoft.com/en-us/azure/stream-analytics/stream-analytics-time-handling).

Some compute options include:

* [AKS](https://docs.microsoft.com/en-us/azure/aks/)
* [KEDA](https://keda.sh/)
* [DAPR](https://cloudblogs.microsoft.com/opensource/2020/07/01/announcing-azure-functions-extension-for-dapr/)
* [Service Fabric](https://github.com/Microsoft/service-fabric#service-fabric-release-schedule)
* [Service Fabric Mesh](https://docs.microsoft.com/en-us/azure/service-fabric-mesh/service-fabric-mesh-overview)

Optionally we can build in additional processing stages in to the pipeline. This could allow better performance and error recovery characteristics are the cost of additional complexity and intermediate storage.  One potentially technology we can use if we go this route would be using [durable Azure Functions](https://docs.microsoft.com/en-us/azure/azure-functions/durable/).

#### Advantages

1. Potentially large cost reduction, particularly when the usage is low.
1. More options for hosting compute. We have more options to homogenize how compute is hosted across Azure API FHIR and the IoT Connector.
1. Reduces blockers for Azure Security benchmarks.
1. More control over the grouping process.
1. More opportunity for logging and telemetry.
1. Better execution characteristics when the pipeline is behind (should always make forward progress in high load scenarios).

#### Disadvantages

1. Additional development needed.
1. Additional CI testing needed.
1. Scale and performance testing needed.
1. If data is staged, need to make sure it properly cleaned up.
1. Rate of egress to FHIR is no longer guaranteed to occur at most every X unit of time.  Traffic on FHIR server is theoretically higher data volume is higher.
1. We are responsible for implementing our own scale out logic.

### Customer Hosted Event Source

In this option, the IoT Connector no longer hosts the Event Hub responsible for ingesting device data.  The customer, out of band, sets up one of the supported event endpoints (Event Hub, IoT Hub, etc.).  Our service would have a managed identity and the customer can grant access to the event endpoint using that IoT Connector managed identity.

To reduce the burden of the extra setup needed our create service wizard can include the creation or configuration of the event source.

We would still host the normalized data event hub for our internal processing.

Another option is we can create the Event Hub on the customer's subscription and manage it and restrict access as needed.  This is called [Service to Sevice, i.e. S2S](https://microsoft.sharepoint.com/:w:/t/msh/EfkVBF-BAVRFuuC6sBGvHr0Bjw3dhcmbzKOsKYS9tG6G6g?e=5BctNO).

#### Advantages

1. Event endpoint is visible on customer subscription and be easily connected to other Azure Services.
1. Customer can scale event hub on their own.
1. Customer can view telemetry and logs of Event Hub directly.
1. Private Link configuration of endpoint can be directly managed by the customer.
1. Customer can configure RBAC to control access to event endpoint.
1. Customer has the option accessing data on the device endpoint.
1. Customer can use other event sources like IoT Hub with out needing an intermediate hop to a device data event hub.
1. Event Hub no longer counts on our subscription quota.

#### Disadvantages

1. Additional customer setup required.
1. Device data pipeline line is no longer completely closed for compliance.
1. Additional surface area for errors to occur (customer can break the event source).
1. If we support connection string access we have to manage those secrets in our services.

### Additional Options

1. **Separate IoT Service**: The IoT Connector is currently a child service of the Azure API FHIR. Future iterations of the service could be modified so the IoT Connector is it's own service and outputs to the configured FHIR service (or other data sink).

1. **Dedicated Event Hubs**:  Dedicated Event Hubs can be used in place of the Standard Tier.  Dedicated Event Hubs offer additional options like BYOK and potentially allow for sharing resources and lowering costs but require a larger up front cost to deploy to a data center (~5,000 per month).

1. **Normalize in Stream Analytics**: Stream Analytics has support for [custom deserializers](https://docs.microsoft.com/en-us/azure/stream-analytics/custom-deserializer-examples).  We could move the normalization logic here.  Previously when we investigated this option we ran into issues.  Providing the mapping files and the ability to diagnosis issues was problematic.

## Azure Security Benchmarks

### Private Link

Private Link allows customers to map private endpoints to Azure PaaS services.  Today Azure API for FHIR is finalizing their support for Private Link.  As a feature of the Azure API for FHIR, the IoT Connector also needs to support Private Link.  

The first challenge is enabling the Azure Function that interacts with the FHIR service the ability to access the service across the private link.  Some potential options to investigate include [VNet Integration](https://docs.microsoft.com/en-us/azure/app-service/web-sites-integrate-with-vnet) and [Hybrid Connections](https://docs.microsoft.com/en-us/azure/app-service/app-service-hybrid-connections).

The other requirement is configuring the endpoint (Event Hub) to have a private endpoint. Event Hub does support [Private Link](https://docs.microsoft.com/en-us/azure/event-hubs/private-link-service) as part of their standard tier.  It is uncertain if we continue to host the Event Hub endpoint if we can configure it as part of the customer's private link.

### BYOK (Bring your own key)

BYOK (Bring your own key) is an option for the customer to supply the encryption key for any data at rest.  Currently, the only service used in the IoT Connector that has data at rest is the Event Hub.  Event Hub does support BYOK but only for [dedicated clusters](https://docs.microsoft.com/en-us/azure/event-hubs/configure-customer-managed-key).  According to documentation, BYOK can be configured per namespace.  A dedicated cluster can support [50 namespaces per CU](https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-dedicated-overview#event-hubs-dedicated-quotas-and-limits).

Dedicated clusters are expensive, costing just under $5,000 per month per CU.  Self-service for dedicated event hub clusters is currently in [beta](https://docs.microsoft.com/en-us/azure/event-hubs/event-hubs-dedicated-cluster-create-portal) which presents challenges with automating their creation as part of our deployment scripts.

If we modify our service to no longer host the device data Event Hub and require the customer to bring their own we would presumably still need to provide an option for BYOK on the normalized data for the customer.

### AAD Data Plane Authorization

At this time the IoT Connector data plane is a write only Evet Hub connection. The customer sends device data to this endpoint.  The service currently allows the customer to create one or more connections that are secured with [SAS key](https://docs.microsoft.com/en-us/azure/event-hubs/authorize-access-shared-access-signature) that the customer can retrieve and rotate using our service APIs.

One of the requirements for the Azure Security benchmarks is to support Azure Active Directory as an option for securing the data plane.  Event Hub does support [AAD](https://docs.microsoft.com/en-us/azure/event-hubs/authorize-access-azure-active-directory) for access. Presumably we could pass through authorization requests via our API and grant access to the underlying Event Hub.  The issue is the Event Hub we provision is on our subscription.  The identities on the customer tenant can't be granted access to Event Hub that exists on our infrastructure tenant.  This presents a major obstacle to implement this benchmark with the current architecture.

Some options:

1. We still host the device data Event Hub but offer an AMQP proxy that handles the initial request and implements [claimed based authorization](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-amqp-protocol-guide#claims-based-authorization).  This has the advantage of allowing us to control the FQDN we expose to customers.  On the downside, there aren't any ready made AMQP proxies today so it is a significant developer investment.  It is also unclear how easily a custom AMQP endpoint will integrate with other services that can use Event Hub like IoT Central and IoT Hub.

1. We no longer host the Event Hub.  The customer is responsible deploying the eventing end point.  Our service would have a managed identity that the customer can use to grant access read access to the Event Hub (or another endpoint).  Our service would read device messages from a customer configured endpoint and process them.  An alternative to this option is deploying the Event Hub on the customer's subscription for them by leveraging [S2S](https://armwiki.azurewebsites.net/authorization/ServicetoServiceAuthorization.html).

## Recommendations

Remove the service provided event endpoint and connect to a customer supplied endpoint.

* Explore merits of removing event endpoint from our service and going to a model we we can connect to a set of supported event sources.
* Verify Stream Analytics can be removed with minimal developer effort.
* Verify Azure Functions on AKS with KEDA for scaling.
* Verify IoT solutions can support the pull model directly from their service (IoT Hub & IoT Central)