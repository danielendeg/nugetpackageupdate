# Implementation details for export work

## Description

This spec deals with implementation details with respect to retrieving the data and exporting it to the given destination. Based on the [BulkExportDesignSpec](https://microsofthealth.visualstudio.com/Health/_git/health-paas-docs?path=%2Fspecs%2FBulkExport%2FBulkExportDesignSpec.md)

## Starting point

ExportJobTask has acquired the export job to be processed. Currently it has access to the `ExportJobRecord` and the `FhirOperationDataStore`.

## Components

### ExportJobTask

1. Keeps track of all the bookkeeping with respect to the progress of the export task. Will be the only one modifying the `ExportJobRecord` during the entire export process.
2. Will contain the logic to determine
    - Whether a job is new or must be resumed
    - When to mark the job as failed/succeeded/retriable
    - Commit cadence of the exported data (for supporting batching of commits)
    - When a new file should be created (based on max size per file)
3. Will use the `IExportDestinationClient` to get the work done

### IExportDestinationClient

1. Will act as the abstraction layer between the `ExportJobTask` and the different destinations we will support.
2. Its role is to know how to connect to the destination, create new files, upload/commit and return the corresponding confirmation.

## Approaches

1. Use the existing Search infrastructure to query for the data to export.
    - Pro: Infrastructure is already in place and it supports additional query parameters (this will be needed when we support export by resource type or id)
    - Con: Export process will be tightly coupled to search implementation
2. Directly use the `FhirOperationDataStore` to query and get the required data.
    - Pro: More control over export flow which might allow for potential future optimizations
    - Con: Have to build the pipeline from sratch
    - Con: Will have to modify existing code in order to support new data stores that we might add in the future.

For now, we are going to use the existing `SearchService` for retrieving data to export.

## Processing steps

1. Use secret name from the `ExportJobRecord` and get secret from `ISecretStore`.
    - If this fails, we update the failure count in the `ExportJobRecord`. If failureCount is above threshold, we will mark the `JobStatus` as Failed. If it is below threshold, we will mark the `JobStatus` as Retriable.
    - We will modify the stored procedure to return such Retriable jobs also when we call `AcquireExportJobsAsync()`. We can add a further RetryAfter timestamp to the `ExportJobRecord` in case we want to introduce a gap between retries.
2. Get `destinationType` from the above secret and instantiate an appropriate `ExportDestinationClient`. This will be done by using an `ExportDestinationClientFactory` that will know what kind of client to return based on the `destinationType`.
3. Establish connection to destination using the `ExportDestinationClient`. If connection fails, we will determine whether job will be marked as Failed or Retriable and update `ExportJobRecord` accordingly.
    - The job we are picking up could either be a new one or one that had failed previously (for a retriable error). In the latter case, we will initialize the destination client as well as our local cache with relevant data read from the `ExportJobRecord`.
4. Execute search query with appropriate params (such as `maxItemCountPerQuery`, `JobProgress` data (if available from `ExportJobRecord`), continuation token, etc). This will return the corresponding data and a continuation token to continue the search (if there is more data).
5. Determine whether we need to create new files for the corresponding resource type(s) that we are going to export to the client
    - If the resource does not have an existing file, we will create a new one.
    - If the current file for a resource has exceeded the max size per file limit, we will create a new one.
6. The data is then passed to the `ExportDestinationClient`.
7. If we want to batch data before committing, we can repeat steps 4-6. Following this we will commit the data to the destination via the `ExportDestinationClient`. Based on commit result, we will update the `ExportJobRecord` accordingly
    - We will need to reset the `numberOfConsecutiveFailures` count if commit was successful.
    - We will update the `Output` field in the job record with data regarding file name/resources/sequence number/etc. We will also have an in-memory cache that will be holding all this information.
    - If commit fails, we will either retry or fail the job (marking the `JobStatus` appropriately)
    - Update `ExportJobRecord` using the `FhirOperationDataStore`
8. Repeat 4 – 7 until we either hit failure or we have exported all the data.
9. Update `ExportJobRecord` with relevant data, set `JobStatus` to Completed and call `ISecretStore.DeleteSecreteAsync()` to complete the process.

## Testing

1. We will use either Azure Storage Emulator or Azurite for our local E2E tests.
2. We will have to modify the build process to create an Azure Storage account to have a place to export the data.
3. We will need to build a DataValidator component that will compare and validate the exported data.
