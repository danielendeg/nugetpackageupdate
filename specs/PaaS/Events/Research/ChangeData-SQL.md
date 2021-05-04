[[_TOC_]]

In order to support publishing post DB changes in [event framework](./event-framework.md), we need a change feed table to record the create/update/delete information of fhir resources that happened in fhir database. 

The change feed table will also be managed in same SQL Server/Azure SQL that fhir server deployed. When customers enable the eventing service, the data procedure that handle the data resources need to write the operations information to the change feed table (By adding the statement in transaction, or create a DML trigger for resource table), there will be background workers to capture change feed information, then construct the events and publish them to the Event Grid topic.


# Requirements and Goals
- Save the change feed information for fhir resources in SQL database.
- To enhance the scalability, the change feed information should be labeled whether they need to be published as the notifications.
- Internal data operations should not trigger events.
- The change feed information need to be aligned with fhir resource in real time, it should not become the bottleneck in latency of publish process.
- The change feed information can be separated, to facilitate multiple workers concurrent process the information to handle the high throughput scenarios.
- Support an API from custom side to fetch change feed information? (**To be discussed**)

# Design
We will store the fhir resources **Create/Update/Delete** information, combined with the necessary fields of the resource itself (ResourceType, ResourceID).

## Table schema
#### `Resource(Update)`

- Sometimes we don't expect the fhir resources to be published to the eventing service, E.g. in bulk import or pre-filter. We plan to add a field "IsEvent" to indicate whether the resource need to be published to the Event Grid topic. 

- Fields:
  - **`ResourceSurrogateId`**
    - Bitint: The key that generated based on time to write the data
  - `ResourceTypeId`
    - Smallint: The ID of the resource type (See ResourceType table)
  - `ResourceId`
    - varchar(64): The resource ID (must be the same as the in the resource itself)
  - `Version`
    - Int: The version of the resource being written
  - `RawResource`
    - Varbinary(max): A compressed UTF16-encoded JSON document
  - `IsDeleted`
    - Bit: Whether this resource marks the resource as deleted
  - `IsHistory`
    - Bit: Whether this resource is history data
  - `IsRawResourceMetaSet`
    - Bit: Whether this resource is from meta set
  - **`IsEvent(new)`**
    - Bit: whether the change information from this resource should be published



#### `ChangeFeed`
- Stores change feed information for fhir resources.
- Fields:
  - `ResourceSurrogateId`
    - Bitint: The key that generated based on time to write the data
  - `ResourceTypeId`
    - Smallint: The ID of the resource type (See ResourceType table)
  - `Version`
    - Int: The version of the resource being written
  - `Timestamp`
    - Datetime2(7): When the data change from related resource happens.
  - `RequestMethod`
    - varchar(10): The data change method. "Created", "Updated" or "Deleted".
    - (Option) Int: Created -> 0, Updated -> 1, Deleted -> 2 
  - `IsHistory`
    - Bit: Whether this change feed information is history data.

## Transaction
```sql
CREATE PROCEDURE dbo.UpsertResource
  ...
AS
  BEGIN TRANSACTION

  /* <All Other Statements> */

  DECLARE @currentDateTime datetime2(7) = SYSUTCDATETIME()
   
  INSERT INTO dbo.ResourceChangeFeed
    (ResourceSurrogateId, Timestamp, ResourceId, RequestMethod, ResourceTypeId, ResourceVersion, IsHistory)
  VALUES
    (@resourceSurrogateId, @currentDateTime, @resourceId, @version, @requestMethod, @version, 0)

   /* <All Other Statements> */

   COMMIT TRANSACTION
GO
```

## Event schema
 (**maybe need to be moved to design on workers**)
 - Microsoft.Health.FHIR.ResourceCreated
 ```json
 [
  {
    "topic": "/subscriptions/{subscription-id}/resourceGroups/{resource-group-name}/providers/Microsoft.Health/workspaces/{workspace-name}",
    "subject": "/{fhir-server-domain-name}/Observation/cb875194-1195-4617-b2e9-0966bd6b8a98",
    "eventType": "Microsoft.Health.FHIR.ResourceCreated",
    "eventTime": "2020-12-21T18:41:00.9584103Z",
    "id": "931e1650-001e-001b-66ab-eeb76e069631 ",
    "data": {
      "resourceType ": "Observation",
      "resourceId": "cb875194-1195-4617-b2e9-0966bd6b8a98 ",
      "resourceVersionId": "1",
      "FhirServer": "myfhirserver.contoso.com",
    },
    "dataVersion": "1",
    "metadataVersion": "1"
  }
]
 ```
 - Microsoft.Health.FHIR.ResourceUpdated
 ```json
 [
  {
    "topic": "/subscriptions/{subscription-id}/resourceGroups/{resource-group-name}/providers/Microsoft.Health/workspaces/{workspace-name}",
    "subject": "/{fhir-server-domain-name}/Observation/cb875194-1195-4617-b2e9-0966bd6b8a98",
    "eventType": "Microsoft.Health.FHIR.ResourceUpdated",
    "eventTime": "2020-12-21T18:41:00.9584103Z",
    "id": "931e1650-001e-001b-66ab-eeb76e069631 ",
    "data": {
      "resourceType ": "Observation",
      "resourceFhirId": "cb875194-1195-4617-b2e9-0966bd6b8a98 ",
      "resourceVersionId": "1",
      "FhirServer": "myfhirserver.contoso.com",
    },
    "dataVersion": "1",
    "metadataVersion": "1"
  }
]
 ```
  - Microsoft.Health.FHIR.ResourceDeleted
 ```json
 [
  {
    "topic": "/subscriptions/{subscription-id}/resourceGroups/{resource-group-name}/providers/Microsoft.Health/workspaces/{workspace-name}",
    "subject": "/{fhir-server-domain-name}/Observation/cb875194-1195-4617-b2e9-0966bd6b8a98",
    "eventType": "Microsoft.Health.FHIR.ResourceDeleted",
    "eventTime": "2020-12-21T18:41:00.9584103Z",
    "id": "931e1650-001e-001b-66ab-eeb76e069631 ",
    "data": {
      "resourceType ": "Observation",
      "resourceFhirId": "cb875194-1195-4617-b2e9-0966bd6b8a98 ",
      "resourceVersionId": "1",
      "FhirServer": "myfhirserver.contoso.com",
    },
    "dataVersion": "1",
    "metadataVersion": "1"
  }
]
 ```
## Reindex
The data change from reindex or other resource level operation, that have no effects on payload of resource records but update their records in database, should not be published to the topic.

## Partition
(**To be discussed**) 

To facilitate multiple workers concurrent capture the change feed information, the information can be partitioned by their "ResourceSurrogateId".

## Maintaining / Flush
For the first release, we plan to maintain a incremental change feed table for first version.

We will add retention policy in the future.

## Pre-filter at Publisher Level
For the first release, no pre-filter will be provided. (**To be discussed**)

In some user cases, customer may want to do pre-filter before change feed information be published to the topic, E.g. Filter the information based on detailed value of the associated resource. In the future plan, publish workers can use the "ResourceSurrogateId" and [search](https://www.hl7.org/fhir/search.html#3.1.1) store procedure to filter the information.

Just like the search example in [Chained parameters](https://www.hl7.org/fhir/search.html#chaining), the popential settings when using the pre-filer could be as below, then the "Patient" type events can be published only if they meet the conditions.

```json
// In pre-filter configuration
{
  "ResourceType": "Patient",
  "Rules":[
    "general-practitioner.name=Joe",
    "general-practitioner.address-state=MN"
  ]
}
```

# Test Strategy
(TBD)
# Security
(TBD)


# Appendix

## History option
### Trigger
```sql
CREATE TRIGGER tr_ResourceChangeFeed ON Resource
AFTER INSERT, UPDATE, DELETE
AS
  DECLARE @currentDateTime  datetime2(7) = SYSUTCDATETIME()
  DECLARE @version          INT
  DECLARE @requestMethod    VARCHAR(10)
  DECLARE @resourceTypeId   VARCHAR(64)
  DECLARE @resourceId       VARCHAR(64)
  DECLARE @resourceSurrogateId VARCHAR(64) 

  SELECT @version = Version, @resourceId = ResourceId, @requestMethod = RequestMethod, @resourceTypeId = ResourceTypeId, @resourceSurrogateId = resourceSurrogateId FROM inserted
  INSERT INTO ChangeFeed VALUES (@ResourceSurrogateId, @currentDateTime, @resourceId, @requestMethod, @resourceTypeId, @version, 0)
GO
```