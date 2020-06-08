The change feed offers the customers the ability to go through the history of the DICOM server and act upon the creates and deletes in the service.

[[_TOC_]]

## High level considerations
* The change feed should be able to be processed from the beginning of the server
* The change feed will be presented in order
* The change feed can be read from any point
* The change feed will consist of action metadata and DICOM metadata
* The change feed will include only creates and deletes

## API Design

Verb | Route | Description
--- | --- | ---
GET | /changefeed | [Read the change feed](#Read_change_feed)
GET | /changefeed/latest | [Read the latest entry in the change feed](#Get_latest_change_feed_item)

### Object model
Field               | Type      | Description
------------------- | --------- | ---
Sequence            | int       | The order which the operation was executed
StudyInstanceUid    | string    | The study instance UID
SeriesInstanceUid   | string    | The series instance UID
SopInstanceUid      | string    | The sop instance uid
Action              | string    | The action that was performed - either "create" or "delete"
Timestamp           | datetime  | The date and time the action was performed in UTC
State               | string    | The current state of the metadata compared to the operation - either "current", "replaced", or "deleted"
Metadata            | object    | Optionally, the current DICOM metadata if the instance exists, or null for a currently deleted instance

### Read change feed
**Route**: /changefeed?offset={int}&limit={int}&includeMetadata={true|false}
```
[
    {
        "Sequence": 1,
        "StudyInstanceUid": "{uid}",
        "SeriesInstanceUid": "{uid}",
        "SopInstanceUid": "{uid}",
        "Action": "create|delete",
        "Timestamp": "2020-03-04T01:03:08.4834Z",
        "State": "current|replaced|deleted",
        "Metadata": {
            "actual": "metadata"
        }
    },
    {
        "Sequence": 2,
        "StudyInstanceUid": "{uid}",
        "SeriesInstanceUid": "{uid}",
        "SopInstanceUid": "{uid}",
        "Action": "create|delete",
        "Timestamp": "2020-03-05T07:13:16.4834Z",
        "State": "current|replaced|deleted",
        "Metadata": {
            "actual": "metadata"
        }
    }
    ...
]
```

#### Parameters
Name            | Type | Description
--------------- | ---- | ---
offset          | int  | The number of records to skip before the values to return
limit           | int  | The number of records to return (default: 10, min: 1, max: 100)
includeMetadata | bool | Whether or not to include the metadata (default: true)

### Get latest change feed item
**Route**: /changefeed/latest
```
{
    "Sequence": 2,
    "StudyInstanceUid": "{uid}",
    "SeriesInstanceUid": "{uid}",
    "SopInstanceUid": "{uid}",
    "Action": "create|delete",
    "Timestamp": "2020-03-05T07:13:16.4834Z",
    "State": "current|replaced|deleted",
    "Metadata": {
        "actual": "metadata"
    }
}
```
## Example usage flow
1. Process starts that wants to monitor the change feed
2. It determines if there's a current offset that it should start with
   * If it has one stored, it uses it.
   * If it has never started and wants to start from beginning it uses offset=0  
   * If it only wants to process from now, it queries `/changefeed/latest` to obtain the last sequence
3. It queries the changefeed with the given offset `/changefeed?offset={offset}`
4. If there are entries  
  4.1 It performs additional processing  
  4.2 It updates it's current offset  
  4.3 It starts again at 2 above  
5. If there are no entries it sleeps for a configured amount of time and starts back at 2.


## Datastore design
### Table
Column              | Type          | Description
------------------- | ------------- | ---
Sequence            | sequence(int) | An incrementing integer for the actions in the db
StudyInstanceUid    | varchar(64)   |
SeriesInstanceUid   | varchar(64)   |
SopInstanceUid      | varchar(64)   |
Action              | smallint      | An enum representing the action that was taken
Timestamp           | datetime2(7)  | The timestamp for when the action was taken
OriginalWatermark   | int           | The watermark of the instance in the original action
CurrentWatermark    | int           | The watermark of the current version of the instance

### Flows
#### Create Action
1. Insert row into `Instance` table
2. Insert row into `ChangeFeed` table
3. Update any existing rows `CurrentWatermark` column with the new watermark

#### Delete Action
1. Remove row from `InstanceTable`
2. Insert row into `DeletedInstance` table
3. Insert row into `ChangeFeed` table
3. Update any existing rows `CurrentWatermark` column with null

## Proposed connectors
### Azure API for FHIR
This would be an option in the hosted Azure API for FHIR where you specify the DICOM change feed and it would poll and create FHIR ImagingStudy resources within the FHIR service.

### Azure Functions Trigger
This would be an available Azure Function trigger much like the CosmosDB trigger that can be setup to act on new actions being taken on the DICOM server.

### Sample connector
This would be a sample in the DICOM repo that would poll the change feed and log items that are created.

## Testing Scenarios
* Empty change feed
* Single instance creation
* Instance deletion
* Instance recreation
* Query string variations
    * invalid types
    * negative `offset`
    * out of bound `limit`
    * out of bound `offset` (past last sequence)
    * with and without `includeMetadata`