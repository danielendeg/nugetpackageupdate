# EventHub Connector

## Description
Users of the Azure API for FHIR and the FHIR server for Azure will want to a) be able to do large scale population analytics on the data and b) react when records are created/updated. One way to achieve both those goals would be to create a connection to Azure EvenHubs. The event hub connection could be used to emit a resource every time it is created or updated. From the event hub, it could then be piped to Azure Data Lake storage, Data Explorer, BLOB storage, etc. An alternative would be to just emit the resourceType and id and an external process, e.g. logic app could then pull the resource and act on it.

A [simply POC](EvenHubConnectorDataExplorerPOC.md) has already been created.

## High-level Design
As a user, I should be able to specify an EventHub to connect to. Once connected, I should be able to specify what events to emit. The following options should be available:
1. Emit `resourceType` and `id` whenever a resource is successfully created or updated.
1. Emit the entire resource whenever successfully created or updated.

The EventHub should live in the users subscription to allow them to connect it whatever downstream processing is appropriate. Multiple consumer groups can enable independent processing by multiple downstream systems.

## Test Strategy

1. Test that emitted events can be ingested into Data Explorer. See [simply POC](EvenHubConnectorDataExplorerPOC).
1. Test that emitted events can be processed by a logic app.

## Security
The user will be in control of the event hub and must manage the security boundary. We should consider de-identification.

## Other
1. Managing credentials for the EventHub will be a consideration.
1. HIPAA compliance for Data Explorer and other components to be investigated. 