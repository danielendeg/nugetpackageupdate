# EventHub Connector

## Description

Users of the Azure API for FHIR **and** the FHIR server for Azure will want to a) be able to do large scale population analytics on the data and b) react when records are created/updated.

One way to achieve both those goals would be to create a connection to Azure Event Hubs. The Event Hub connection could be used to emit a resource every time it is created, updated, or deleted. There could also be an option to pipe bulk FHIR exports to the Event Hub.

From the Event Hub, it could then be piped to Azure Data Lake storage, Data Explorer, BLOB storage, etc. An alternative would be to just emit the resourceType and id and an external process, e.g. logic app could then pull the resource and act on it.

A [simple Azure Data Explorer POC](EvenHubConnectorDataExplorerPOC.md) has already been created.

## High-level Design

As a user, I should be able to specify an Event Hub to connect to. Once connected, I should be able to specify what events to emit. The following options should be available:

1. Emit `resourceType` and `id` whenever a resource is successfully created or updated.
2. Emit the entire resource whenever successfully created, updated, or deleted. If it is a delete event, the resource should be appropriately tagged to allow it to be filtered in subsequent queries.
3. Emit bulk FHIR exports to the Event Hub.

The Event Hub should live in the users subscription to allow them to connect it whatever downstream processing is appropriate. Multiple consumer groups can enable independent processing by multiple downstream systems.

In the Azure API for FHIR, the connection to Event Hub should be managed with ARM and the portal. In the OSS FHIR Server for Azure, the connection could be specified with configuration parameters.

## Test Strategy

1. Test that emitted events can be ingested into Data Explorer. See the [simple Azure Data Explorer POC](EvenHubConnectorDataExplorerPOC).
2. Test that emitted events can be processed by a logic app.

## Security

The user will be in control of the Event Hub and must manage the security boundary. We should consider de-identification.

## Other

1. Managing credentials for the Event Hub will be a consideration.
2. HIPAA compliance for Data Explorer and other components to be investigated.