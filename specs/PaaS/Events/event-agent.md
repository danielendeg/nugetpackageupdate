**Event Agent Summary**

In order continuously get the FHIR resources that have changed, there will be an Event Agent which runs under the FhirApplication Service Fabric application.

The Event Agent will get the FHIR resources that have changed by connecting to the FHIR SQL database and will retrieve a set of changed records by calling the [dbo.FetchResourceChanges](https://github.com/microsoft/fhir-server/blob/main/src/Microsoft.Health.Fhir.SqlServer/Features/Schema/Migrations/14.diff.sql#L131) Stored Procedure. 

The Event Agent will then forward the changed resources to the Event Grid.

**Event Agent Checkpoint**

In order to support cases where the Event Agent needs to stop and resume (upgrades), we will be maintaining a checkpoint of what events the Event Agent has successfully processed and sent to Event Grid.

The checkpoint contains an identifier of the last successfully processed event id from the ResourceChangeData table. It also contains a datetime of the last successfully processed event, but this datetime is for informational purposes at this point in time.

The checkpoint will need to be persisted in a location. We considered blob storage, and we considered SQL. We are choosing SQL due to the fact that the SQL database is something that already exists and the Event Agent already has access to it.

**Event Agent Checkpoint SQL table**

This is an example of a checkpoint stored in the newly created `dbo.EventAgentCheckpoint` SQL table:

|CheckpointId|LastProcessedDateTime             |LastProcessedIdentifier|UpdatedOn
|------------|----------------------------------|-----------------------|---------------------------|
|EventAgent0 |2021-07-08 20:04:24.8936110 +00:00|51                     |2021-07-08 20:04:42.2311392

The primary key will be the checkpoint id (e.g. `EventAgent0`).

- CheckpointId - An identifier for the checkpoint. Meant to represent some common way look up the checkpoint for an Event Agent application.
- LastProcessedDateTime - The datetime of the last successfully processed event
- LastProcessedIdentifier - The identifier of the last successfully processed event
- UpdatedOn - A datetime for shows when the checkpoint was last updated.

The `dbo.EventAgentCheckpoint` table is defined as follows:

``` sql
CREATE TABLE dbo.EventAgentCheckpoint
(
 CheckpointId varchar(64) NOT NULL,
 LastProcessedDateTime datetimeoffset(7),
 LastProcessedIdentifier varchar(64),
 UpdatedOn datetime2(7) NOT NULL DEFAULT sysutcdatetime(),
CONSTRAINT PK_EventAgentCheckpoint PRIMARY KEY CLUSTERED (CheckpointId)
)
ON [PRIMARY]
```

**Event Agent - fetch checkpoint on startup**

When the Event Agent starts up, it will first try and get a checkpoint for the Event Agent. If a checkpoint exists for that Event Agent, the Event Agent will resume processing from that given checkpoint. If a checkpoint does not exist, then the Event Agent will start processing from the first available ResourceChangeData event. The Event Agent will attempt to get a checkpoint by calling the `dbo.FetchEventAgentCheckpoint` Stored Procedure.

``` sql
SELECT TOP(1)
  CheckpointId,
  LastProcessedDateTime,
  LastProcessedIdentifier
  FROM dbo.EventAgentCheckpoint
WHERE CheckpointId = @CheckpointId
```

**Event Agent - set checkpoint on successful event processing**

After the Event Agent starts up and begins processing events, we will occasionally want to set checkpoints to indicate what events have been successfully processed so that we can resume from that position if the application were to restart.

To set a checkpoint in the sql database we will call the `dbo.UpdateEventAgentCheckpoint` Stored Procedure. This either creates or updates a row with a given checkpoint id.

The user will need to pass the following parameters:



``` sql
BEGIN
    IF EXISTS (SELECT * FROM dbo.EventAgentCheckpoint WHERE CheckpointId = @CheckpointId)
    UPDATE dbo.EventAgentCheckpoint SET CheckpointId = @CheckpointId, LastProcessedDateTime = @LastProcessedDateTime, LastProcessedIdentifier = @LastProcessedIdentifier, UpdatedOn = sysutcdatetime()
    WHERE CheckpointId = @CheckpointId
    ELSE
    INSERT INTO dbo.EventAgentCheckpoint
        (CheckpointId, LastProcessedDateTime, LastProcessedIdentifier, UpdatedOn)
    VALUES
        (@CheckpointId, @LastProcessedDateTime, @LastProcessedIdentifier, sysutcdatetime())
END
GO
```

**Example checkpoint**

|CheckpointId|LastProcessedDateTime             |LastProcessedIdentifier|UpdatedOn
|------------|----------------------------------|-----------------------|---------------------------|
|Identifier0 |2021-07-08 20:04:24.8936110 +00:00|51                     |2021-07-08 20:04:42.2311392


**Decision on SQL vs. Storage Account for persisting checkpoints**

In theory it would be nice to store checkpoints in an Azure Service that is specific to the Event Agent and can be accessed whether the Event Agent is run on Service Fabric or AKS. Storage seems like the most lightweight solution to store the checkpoints and is relatively solution agnostic. However provisioning a storage account for the workspace platform and granting the FHIR service access to the Storage Account appeared to be a rather large story. In order to meet our timelines for the Event Framework, we decided to store the checkpoints in the FHIR Service SQL database. Some additional complexities around Storage Account are listed below.

- Ideally we would use a managed identity to connect to a storage account.
- Currently adding a managed identity for a FHIR Service adds 5-10 minutes to the provisioning time of a FHIR Service hosted in Service Fabric. This additional amount of time is considered a dealbreaker for customers, so using managed identity is not currently an option.
- Using Connection String requires secret rotation which is a lot additional overhead given our timelines.
- Using Service Principal is also a challenge because we do not currently support a certificate or key vault per FHIR Service.
- We would need to leverage the AccountRouting service to get and set certificates with the Storage Account. This would add quite a bit of traffic to account routing, and is a much more complex solution than adding a new SQL table and two new stored procedures.
- In addition, there are complexities around when and how to coordinate adding the permissions to the storage account when additional FHIR services are spun up.
- We could also create our own identity provider/ permission broker, separate from AccountRouting. This is also a large story and not something we can take on at this time.

Given the assumptions/reality listed above, if wanted to support Storage Account these are the possible approaches ranked in order of likelyhood and complexity:
1) Assuming the FHIR Service is on Service Fabric and everything else remains as-is, service principal + AccountRouting is likely least amount of work.
2) Another option which would require more work but is cleaner would be to create our own identity broker for Service Fabric that resolves these sort of identities for Service Fabric apps.
3) If the managed identity thing with Service Fabric is "fixed" so that it does not add significant amounts of time to the deployment times, then that would be the way to go. However, this is not something that is likely to be addressed in the near term.
4) If FHIR Service is on AKS then the managed identity deployment time issue is no longer a problem and managed identity would be the way to go. My understanding is that AKS is not something on the table for the near term, but may be part of the long term vision.

