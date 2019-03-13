# Bulk Export

## Description

Customers would like to be able to export their data for various purposes. We would like to implement the bulk export based on the [spec](https://github.com/smart-on-fhir/fhir-bulk-data-docs/blob/master/export.md). However, we believe that the customer would like to use the storage system of their choice rather than having us exporting files to our own storage and have them download individual files at the end of the export, so we are adding the capability to specify the destination in addition to the spec.

## Requirements

### Queueing a bulk data export job

- P1
  - User must be able to call `GET [base]/Patient/$export` to export all patients.
  - User must be able to call `GET [base]/$export` to export all resources.
  - User must specify the `_destinationType` query parameter to specify the destination type and `_destinationConnectionSettings` query parameter to specify the destination connection settings. `_destinationConnectionSettings` would be base64 encoding of a JSON payload that's specific to the `_destinationType`.
    - User must be able to use Azure Blob Storage. `_destinationType` will be `AzureBlockBlob` and `_destinationConnectionSettings` will be base64 encoding of SAS token (or connection string).
    - If unknown `_destinationType` is specified, then `400 Bad Request` should be returned.
    - If valid `_destinationType` is specified but the invalid `_destinationConnectionSettings` is specified, then `400 Bad Request` should be returned.
  - User must have privilege to queue an export job. If user does not have the corresponding privilege, then `403 Forbidden` should be returned.
  - User must supply `Accept` header with `application/fhir+json`. If any other value is specified, then `400 Bad Request` should be returned.
  - User must supply `Prefer` header with `respond-async`. If any other value is specified, then `400 Bad Request` should be returned.
  - API must support optional `_since` query parameter. If the date cannot be parsed, then `400 Bad Request` should be returned.
  - API must support optional `_type` query parameter. If an invalid type is specified, then `400 Bad Request` should be returned.
  - API must respond with `202 Accepted` when an export job is accepted.
  - API must respond with `Content-Location` header so the client can poll the asynchronous processing state.
  - API can return `429 Too Many Requests` if there are too many concurrent export job.
    - Initially, only one concurrent job is supported.
  - API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.

- P2
  - API must support optional `_outputFormat` query parameter with default value `application/fhir+ndjson`.
  - API should only export resources where the initiating user have access to.
  - Initially, only storage account in the same region is supported. If another region is specified, then `400 Bad Request` should be returned.
  - Proof of concept and implement different data providers (preferably from different online services such as Azure Blob Storage and Amazon S3).

- P3
  - User must be able to call `GET [base]/Group/[id]/$export` to export all patients belong to the given FHIR group.
  - User must be able to specify `_typeFilter` query parameter to provide additional filter used to limit the type of resource being returned.

### Checking a bulk data export job status

- P1
  - User must be able to call `GET [polling location]` to get the current status of the job.
  - API must respond with `202 Accepted` if the job is pending or in-progress.
  - API must respond with `200 OK` if the job is completed.
  - API must return `transactionTime` to indicate the time when the job was queued. The response should not include any resources modified after this instance and should include any matching resources modified up to and including this instant.
  - API must return `request` to indicate the URI of the original bulk data job.
  - API must return `requiresAccessToken` to indicate whether the download requires access token or not.
  - API must return `output`, which is an array of `BulkExportFileInfo`. to indicate the list of exported files. If no resources are returned, then output must be an empty array.
    - `BulkExportFileInfo` must return `type` to indicate the resource type contained in the file.
    - Each file represented by the `BulkExportFileInfo` must only have one resource type.
    - `BulkExportFileInfo` must return `url` to indicate the path to the exported file.
    - `BulkExportFileInfo` may optionally return `count` to indicate the number of resources in the file.
  - API must respond with `404 Not Found` in case the job does not exist.
  - API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.

- P2
  - API may return `X-Progress` header with text describing the progress of the export.
  - The server should respond with `Retry-After` header to tell client when to poll next.
  - API must respond with `429 Too Many Requests` if the caller is polling too frequently.
  - API must return `error`, which is an array of `BulkExportFileInfo` with resource type `OperationOutcome` to indicate the list of errors occurred during the export. If no errors are returned, then output must be an empty array.

### Data export worker

- P1
  - Worker must be able to export the data to the destination location.
  - Worker must be able to pickup new job periodically.
  - Worker must be able to resume relatively inexpensively if terminated in the middle.
  - Worker must be able to throttle so that it doesn't consume all available resources.

### Cancelling a bulk data export job

- P1
  - User must be able to call `DELETE [polling location]` to cancel an existing bulk export job.
  - API must respond with `202 Accepted` if the cancellation is successfully requested.
  - API must respond with `404 Not Found` if the job does not exist.
  - API must respond with `409 Conflict` if the job is already completed or cancelled or cancellation has already been requested.
  - API must respond with `500 Internal Server Error` in case of error with `OperationOutcome` in JSON format describing the error.

Notes:

- The spec is not very clear about what happens when cancellation is requested for a job that's already completed or cancelled. We can return `202 Accepted` or `409 Conflict` or `410 Gone`.

### List all of the data export job [DRAFT]

- P1
  - This is not in the bulk export spec but we should have an API to list all of the export job so that administrators can manage jobs.

### A job to clean up KeyVault

- P3
  - We will try to delete the secret from KeyVault when the job completes. However, we might need a job to cleanup stale entry in KeyVault if job fails to delete the entry for whatever reason.

## High-level Design

We are adding support for $export operation to allow the user to queue a bulk data export job. This operation is only supported in subset of endpoints.

### New configurations

We will have the following new section in the configuration file.

```json
{
    "FhirServer": {
        ...,
    },
    "BulkExport": {
        "Enabled": true,
        "ConsecutiveFailuresThreshold": 5, // The number of consecutive failures allowed before the job is marked as failed. Setting this number to 0 means job will not be retired and setting this number of -1 means there is no threshold.
        "CosmosDb": {
            "MaximumRUPerMinuteThreshold": 1000, // The amount of RUs allowed to be consumed per minute. Setting this number to 0 means no threshold is enforced.
        },
        "JobHeartbeatTimeoutThresholdInSeconds": 600, // The number of seconds allowed before the worker declares job to be stale and can be picked up again. This number must be greater than 0.
        "JobPollingFrequencyInMilliseconds": 1, // The frequency of polling new jobs in milliseconds. This number must be greater than 0.
        "MaximumConcurrency": 1, // The number of concurrent job allowed.Setting this number to 0 means there is no limit.
        "MaximumFileSizeInMBytes": 100, // The maximum size allowed for each output file. The actual file size might be slightly bigger than the limit here. This number must be greater than 0.
        "MaximumQueryCountSize": 100, // The maximum number of item to be returned by the query. Settings this number to 0 means let the server decide the number of items returned.
        "QueryDelayIntervalInMilliseconds": 500, // The number of milliseconds to wait between queries. Setting this number to 0 means there will not be any delay between queries.
    }
}
```

Note:

- We could consider copy some of these throttling settings to the job record itself so that these can be tweaked on individual job basis.

### $export operation

The caller can call $export operation by calling the API endpoint such as the following.

`GET [fhir base]/$export?_outputFormat=application%2Ffhir%2Bndjson&_destinationType=AzureBlockBlob&_destinationConnectionSettings=ew0KICAiY29ubmVjdGlvblN0cmluZyI6ICJzZXJ2aWNlJmNvbXA9cHJvcGVydGllcyZzdj0yMDE1LTA0LTA1JnNzPWJmJnNydD1zJnN0PTIwMTUtMDQtMjlUMjIlM0ExOCUzQTI2WiZzZT0yMDE1LTA0LTMwVDAyJTNBMjMlM0EyNlomc3I9YiZzcD1ydyZzaXA9MTY4LjEuNS42MC0xNjguMS41LjcwJnNwcj1odHRwcyZzaWc9RiU2R1JWQVo1Q2RqMlB3NHRnVTdJbFNUa1dnbjdiVWtrQWc4UDZIRVNYd21mJTRCIg0KfQ==`

The $export operation will simply queue a job by creating a new job record in the database. A worker will then pick it up and process it asynchronously.

#### Supporting destination

One change that we are making in additional to the bulk data export spec is to include the ability for the user to specify the destination location, which are specified by `_destinationType` and `_destinationConnectionSettings` query parameters.

- `_destinationType` - initially we will only support `AzureBlockBlob`. For prototyping to show we could support additional service providers, we might also implement `AmazonS3`.
- `_destinationConnectionSettings` - the value is base64 encoding of the type specific connection string. For `AzureBlockBlob`, this will be base64 encoding of SAS token (or connection string). For `AmazonS3`, this will be base64 encoding of the pre-signed query URL.

Because of the nature of the connection string, we need to treat it as a secret. In the OSS implementation, we will create an abstract interface `ISecretStore` with specific implementation using Azure KeyVault. Specifically, for KeyVault, the payload will be encoded as base64 string.

``` json
{
    "destinationType": "AzureBlockBlob",
    "destinationConnectionSettings": "ew0KICAiY29ubmVjdGlvblN0cmluZyI6ICJzZXJ2aWNlJmNvbXA9cHJvcGVydGllcyZzdj0yMDE1LTA0LTA1JnNzPWJmJnNydD1zJnN0PTIwMTUtMDQtMjlUMjIlM0ExOCUzQTI2WiZzZT0yMDE1LTA0LTMwVDAyJTNBMjMlM0EyNlomc3I9YiZzcD1ydyZzaXA9MTY4LjEuNS42MC0xNjguMS41LjcwJnNwcj1odHRwcyZzaWc9RiU2R1JWQVo1Q2RqMlB3NHRnVTdJbFNUa1dnbjdiVWtrQWc4UDZIRVNYd21mJTRCIg0KfQ=="
}
```

One of the require properties to be returned in the response of the polling API is the original `request`. We will not be returning the `_destinationType` and `_destinationConnectionSettings` parameters.

#### De-duping the bulk export job

We need to be able to identify and de-dup the bulk export job. This is needed if the user calls the $export operation to queue a new bulk job but failed to get the response for whatever reason (such as network failure). In this case, the job is already queued in the server but the user never received the location to poll the job status. It's likely that user will try to queue a job again but instead of queuing a new job, we would want to return the existing job.

We will generate a hash of the job based the following parameters:

- The input URL (the full URL).
- The subject of the caller.

If there is a matching job with state `Queued` or `Running`, we return `202 Accepted` with the location of that job; otherwise, the API will queue a new job record and return the location for polling.

Note:

- We will use hash instead of checking the properties directly because the parts of the input will be stored in the database and parts of the input might be stored in the secret store.
- Alternatively, we could return `302 Found` and redirect the caller to the existing job but that's not in the spec and client might not be able to react to it.
- If `_since` parameter is not supplied, then the server defaults the timestamp at the time the request is received. If another request comes in later that matches the existing job, we will return the existing job, even though technically the value for `_since` is different. If `_since` parameter is supplied, then it will be treated as different job and so if the caller always append the current timestamp in the request, then we might have multiple job queued.
- We could expand the evaluation of the de-duping logic to include time range and so forth in the future.

#### Location of the bulk export job

We need to return a location for the caller to check the status of the export job.

`Content-Location: [fhir base]/_operations/export/a0a13edb-ce1c-4347-8dca-8abfc6a7d453`

To avoid being mixed up with the valid resources, we will use name `_operations` to distinguish the endpoint. The [FHIR spec](https://www.hl7.org/fhir/structuredefinition.html#invs) constrains the resource name to start with capitalized letter from A-Z so using a name that starts with underscore should be okay.

#### Queuing a new bulk export job

1. User calls one of the FHIR API endpoint with `$export` operation.
2. Validate the call to make sure `$export` operation is supported.
   - If the endpoint is `[base]/Group/[id]/$export`, return `501 Not Implemented`.
   - If the endpoint is not `[base]/$export` or `[base]/Patient/$export`, return `400 Bad Request`.
3. Validate parameters to make sure required parameters are supplied.
4. Validate the `destinationType` to make sure we know how to handle it and validate `destinationConnectionSettings` to make sure it is valid.
5. Check to make sure the caller has the privilege to call $export operation.
   - Return `403 Forbidden` if the caller does not have privilege.
6. Generate the hash based on the input parameters and check to see if there is an existing job.
   - Return `202 Accepted` with the location of the existing job if there is a matching job with state `Queued` or `Running`.
7. Check to see if job can be executed.
   - If the number of job currently executing is greater than or equal to the threshold (initially 1), return `429 Too Many Requests`.
   - Do we need this step? If we are simply queuing and the client is expected to poll, we could just queue the job and let the worker handle it whenever it becomes available.
8. Create a new secret and store the secret in the secret store.
   - If storing the secret in the secret store fails, return `500 Internal Server Error`.
9. Create a new job record and store the job record in the database.
   - If storing the job record fails, return `500 Internal Server Error`.
10. Return `202 Accepted` with the location of the new job for the caller to poll the status.

Note:

- We could also check the connection to the destination here and fail if we get Unauthorized or other type of non-retryable exception without having to queue a job and fail the job immediately.
- We could optimistically execute the logic and knowing that there could be a possibility that a duplicated job might get inserted. Alternatively, we could use distributed lock but that seems heavy weight.

``` json
{
    "cancellationRequestedTimestamp": "", // The timestamp when the cancellation is requested.
    "endTimestamp": "", // The timestamp of when the job was completed.
    "id": "a0a13edb-ce1c-4347-8dca-8abfc6a7d453",
    "input": { // Information related to input.
        "secretName": "ej-a0a13edb-ce1c-4347-8dca-8abfc6a7d453", // The name of the secret where the connection string is stored.
        "query": "[fhir base]/$export?_outputFormat=application%2Ffhir%2Bndjson", // The original query with destinationType and destinationConnectionSettings removed.
    },
    "jobHash": "82C018B0CC318DF5B3DCD39EE516601E909A604DCD4E790F2D930B08B4835CC7", // The hash of the job.
    "jobSchemaVersion": 1, // The version of the job schema.
    "numberOfConsecutiveFailures": 0, // The number of consecutive failures. If this value exceeds ConsecutiveFailuresThreshold, we will mark the job as completed with failure.
    "lastChangeTimestamp": "", // The timestamp of when the job record was last updated. If the status is Running and this timestamp is older than WorkerTimeoutThresholdInSeconds, we will consider the job no longer active and worker is free to pick this up again.
    "output": {
        "errors": [{
            "type": "OperationOutcome",
            "sequence": 1,
            "count": 1,
            "committedBytes:": 105057600,
        }],
        "results": [{
            "type": "Patient", // The type of the resource the file contains.
            "sequence": 1, // The file sequence number.
            "count": 1500, // The number of resources tis file contains.
            "committedBytes:": 105307600, // The number of committed bytes. If this number is greater than MaximumFileSizeInMBytes, we need to create a new blob.
        }, {
            "type": "Patient",
            "sequence": 2,
            "count": 100,
            "committedBytes:": 1012200,
        }, {
            "type": "Observation",
            "sequence": 1,
            "count": 7,
            "committedBytes:": 103200,
        }],
    },
    "partitionKey": "ej-a0a13edb-ce1c-4347-8dca-8abfc6a7d453",
    "progress": {
        "query": "/?_count=500&ct=%7B%22token%22%3A%22%2BRID%3Af79IAO15VDQEAAAAAAAAAA%3D%3D%23RT%3A1%23TRC%3A1%23FPC%3AAQQAAAAAAAAADAAAAAAAAAA%3D%22,%22range%22%3A%7B%22min%22%3A%22%22,%22max%22%3A%22FF%22%7D%7D", // The actual query that's being used for search. This query will be updated each time we have successfully commit the blob.
        "page": 2, // The page number of the current query. This number will be updated each time we have successfully commit the blob.
    },
    "queuedTimestamp": "2019-02-26T15:59:30.350Z", // The timestamp of when the job was queued.
    "requestor": { // List of claims the user had in the JWT token when the job was requested. This will be used to evaluate what resource to export.
        "claims": {
            "aud": "https://resourceproviderservice.internal.mshapis.com",
            "iss": "https://sts.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47/",
            "iat": 1551213411,
            "nbf": 1551213411,
            "exp": 1551217311,
            "aio": "42JgYEgp9D9gY7gxhjFgloRB6445AA==",
            "appid": "7f13fa53-71ca-4645-8cf0-6f3d93a40181",
            "appidacr": "2",
            "idp": "https://sts.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47/",
            "oid": "174c6b39-ecc9-43fb-939d-98b8ad604cde",
            "roles": [
                "admin"
            ],
            "sub": "174c6b39-ecc9-43fb-939d-98b8ad604cde",
            "tid": "72f988bf-86f1-41af-91ab-2d7cd011db47",
            "uti": "t81Ma_m8rkuP4nEtddkBAA",
            "ver": "1.0"
        }
    },
    "startTimestamp": "", // The timestamp of when the job was picked up for the first time. Even if the job fails and gets picked up again, this value will not be changed.
    "status:" :"Queued", // The job status. Must be one of the following values: Queued, Running, Failed, Cancelled, and Completed.
    "totalNumberOfFailures": 0, // The total number of failures. We could choose to fail the job if this value exceeds certain threshold or we could simply use it for telemetry purposes.
}
```

### Bulk export job worker

We need a worker that runs in the background continuously to pick up the bulk export job.

In the OSS implementation, we will use the `IHostedService` to create a hosted service. The implementation assumes there is one worker that is responsible for dispatch potentially multiple jobs.

1. Retrieve the currently executing jobs. If the number of currently executing jobs exceeding `MaximumConcurrency`, then sleep `PollingIntervalInSeconds`.
2. Retrieve all job records where `status != 'Cancelled' && status != 'Completed'` and order by `queuedTimestamp` using ascending order.
3. For each job record:
   1. If `status == 'Queued' || status == 'Failed' || (status == 'Running' && (UtcNow - lastChangeTimestamp) >= JobHeartbeatTimeoutThresholdInSeconds)`, then dispatch a background task asynchronously to start processing the job.
4. Repeat #3 until `MaximumConcurrency` is reached.
5. Sleep `PollingIntervalInSeconds` and repeat #1.

In the managed environment, we have different options how to implement the worker. We could have one process dedicated for each account. This ensures isolation and availability to each account but if there is no export job to be executed, the process is simply taking up resources for no good reason. Alternatively, We could host a pool of workers that can be shared by list of accounts. To start simple, we can host the background worker in-proc like in OSS implementation.

Note:

- We could also have the bulk job API to signal to the worker to the new job in addition to polling.

#### Fault tolerance

Because we could be potentially moving large amount of data between two different remote locations, numerous type of error could happen. We need to be fault tolerant and be able to resume the export process.

For the initial implementation, we will be using Azure Blob storage, specifically using the [block blob](https://docs.microsoft.com/en-us/rest/api/storageservices/understanding-block-blobs--append-blobs--and-page-blobs).

With block blob, we can create a blob (e.g., a file) and upload blocks within that blob with each blocks identified by a base64 string. Each block can be up to 100MB and each blob can include up to 50,000 blocks, so the maximum size of a block blob is slightly more than 4.75TB. When a block is uploaded, it is associated with the specified block blob but will not be part of that blob until a list of blocks that includes the new block ID is committed.

This would be perfect for our scenario. In our bulk export scenario, we will be executing the query in pages so we can use the page number as the block id. Each resource type needs to be exported to a different file, so we will need to maintain a mapping of resource type to a block stream. Each time we execute a query, we will create a new block using the page number as the key. We will enumerate through each record and write to the appropriate stream. We will also be tracking the amount of bytes committed to each blob as well as the number of bytes written to the stream. When we exceed the `MaximumFileSizeInMBytes`, we can create a new blob and a new block. Once all of the results are written, we will commit all of the block in the blob. Once all of the commits succeeds, we will update the job record with the new query (with new continuation token) and the new page number. Essentially, each page is treated as a transaction such that if anything fails in the middle, we will redo the entire page so the overhead in the worst case is determined by `MaximumQueryCountSize`.

If the `MaximumQueryCountSize` is small and we have various types of resource types within each page, then we might end up with multiple blocks of small number of items. This could cause performance overhead.  Initially, we can start simple but if we find that this process creates too much overhead, we can also batch the pages so that we commit every X number of pages. I think we can start simple and tweak as we go. We will have a `jobSchemaVersion` property to track the version of the job record schema. In case we need to change the schema drastically, we can increment the number and maintain backwards compatibility for the old jobs during migration.

#### Balancing between speed and load

We want to export as fast as we can but we also don't want to ending up consuming all of the available resources and causing significant latencies for the FHIR API operations. Therefore, we need to build some infrastructure in place so that we can throttle as needed.

- `MaximumQueryCountSize` - throttle used to change the number of item returned by the query. Since the load (RU/s in case of Cosmos DB) depends on the number of items being searched, we need to be able to control this.
- `QueryDelayIntervalInMilliseconds` - throttle used to change the delay between each query. This can be set to 0 if we don't want any delays.
- `MaximumRUPerMinuteThreshold` - throttle used to change the maximum RUs allowed to be consumed in a minute. This is a specific setting to Cosmos DB.

In the managed environment, we could be introduce more intelligent features such as automatically scaling up the RU during the duration of the job.

### Bulk export job task

Once the worker dispatches the job, we will need to process the actual export.

1. Update the job record and set `status = 'Running'`, set the `startTimestamp` if it's empty, and set the `lastChangeTimestamp` with the ETag.
   - If the update fails with `409 Conflict`, it means some other task is updating this job. Log and abandon it.
   - If the update fails with any other error, we can build retry logic for transient errors or simply log and abandon it and let the worker picks it up again.
2. Get the secret and retrieve the connection string.
   - If we fail to get the secret for whatever reason, increment `numberOfConsecutiveFailures` and `totalNumberOfFailures`, change the `status = 'Failed'`, update the job record, log, and abandon the job.
   - We can introduce some delay in when the job should get picked up next? Otherwise, the job might get picked up again right away.
3. Creates connection to the destination using the connection string.
4. Execute the query using the `progress.query`.
5. Generate the new block id using the `progress.page`. To make sure the block id is always the same length, we will format the number using `d6`.
   1. Get the `CloudBlockBlob` referencing to the current blob for the resource type.
      - If there is no cached `CloudBlockBlob`, find the current information by looking at the `output.results` array. If the `output.results` does not contain any entry for the resource type, create a new entry with `sequence = 1`; otherwise find the entry with the largest `sequence`. If the `committedBytes < MaximumFileSizeInMBytes * 1048576`, then use this entry; otherwise, create a new entry with `sequence` set to be the largest `sequence + 1`.
      - Create `CloudBlockBlob` by calling `GetBlockBlobReference` and construct the name by using the resource type and `sequence`.
      - Cache the `CloudBlockBlob`.
   2. Get the list of committed blocks `blobBlockList` for this `CloudBlockBlob` by calling `DownloadBlockListAsync` to download list of blocks, if we don't have it cached already.
   3. Get the stream to the block within the blob reference.
      - If there is no cached stream, create a `MemoryStream` and cache it.
   4. Write the raw resource string with a new line character to the stream.
   5. Repeat #1 for all resources.
6. Update each `blobBlockList` and insert the new block id.
7. Increment the `committedBytes` by `stream.Length` for each file.
8. Call `PutBlockListAsync` for each `CloudBlockBlob` to commit the new blocks.
9. Update the job record and set `progress.query`, `progress.page`, `progress.output`, `numberOfConsecutiveFailures = 0` with the ETag.
   - If the update fails with `409 Conflict`, it means some other task is updating this job. Log and abandon it.
10. Repeat #4 until there is no more resources to be retrieved.
11. Update the job record and set `status = 'Completed'`, `lastChangeTimestamp`, `endTimestamp` with the ETag.
12. Delete the secret from the secret store. We should fail silently if we cannot delete the secret.

Example:

Let's say in the job record, we have page 1 of the query, which we will call Q1. Q1 returns resource R100 to R199 when executed and because there are more resources so it also returns the next link or continuation token in the response. Because the next link leads to page 2, we will call it Q2 and Q2 will return resource R200 to R299.

Assuming we don't have any retry logic at all (in reality, we probably will implement some retry to handle transient errors).

1. At #4 above, we will execute Q1 which returns R100 to R199.
   - If we fail here, the job simply restarts here again.
2. We will then create a new block within each blob with batch id "1". Because each resource type will be in a separate blob, we will have multiple blocks, each pointing to a blob but all of the blocks will have block id "1".
   - If we fail here, we have the new empty blocks created, but since it's not committed yet so these blocks will not show up in the blob.
   - When the job is retried, it starts again from Q1 and these new blocks will be created again and overwritten.
3. Loop through each resource from R100 to R199 and write the raw resource with '\n' to the corresponding block.
   - If we fail here, we have the new blocks created with data filled in, but since it's not committed yet so these blocks will not show up in the blob.
   - When the job is retried, it starts again from Q1 and these new blocks will be created again and overwritten.
4. At #8 above, we will commit all blocks against all blobs.
   - If we fail here, we have the new blocks created with data filled in and they are committed so they will show up in the blob.
   - When the job is retried, it starts again from Q1 and these new blocks will be created again and overwritten.
5. Update the job record and set the query to be Q2 and page to be 2.
   - Same as above.
6. Execute Q2 which returns R200 to R299 and repeat the process.

In the worst case, we will have the reprocess the same query, but since it will overwrite the existing block with the same data, it would not result in duplicates. The overhead of processing is determined by `MaximumQueryCountSize`. This of course assumes that the query produces deterministic result. For the most part, this would not be a problem because we don't normally delete data. The FHIR delete API simply updates the existing resource and marks the resource as deleted. However, because we support "hard-delete" for GDPR, if someone hard-delete a record, that could change the result of the query. I need to do some more investigation on how this would affect the result.

Note:

- The file name should include the job name in case multiple users queues export job using the same destination.
- Still need to figure out what error to return to the caller.
- How do we ensure the page will fit within a block.

#### Checking a bulk export job status

1. User calls `GET [polling location]`.
2. Return `404 Not Found` if the job doesn't exist.
3. Return `200 OK` if the job is completed or cancelled. The body should should contain the information below describing the export result.
4. Return `202 Accepted` if the job is pending or in-progress. The body should contain the information below describing the export result.
5. Return `500 Internal Server Error` if fails to get the job record or if the job failed.

If the job is completed, cancelled, or in-progress, the body of the response should be the following:

``` json
{
  "transactionTime": "2019-03-02T12:34:20Z", // The time when the job was queued. The response should not include any resources modified after this instance and should include any matching resources modified up to and including this instant.
  "request" : "[base]/$export", // The URI of the original bulk data job.
  "requiresAccessToken" : true,
  "output" : [{
    "type" : "Patient",
    "url" : "http://serverpath2/patient_file_1.ndjson"
  },{
    "type" : "Patient",
    "url" : "http://serverpath2/patient_file_2.ndjson"
  },{
    "type" : "Observation",
    "url" : "http://serverpath2/observation_file_1.ndjson"
  }],
  "error" : [{
    "type" : "OperationOutcome",
    "url" : "http://serverpath2/err_file_1.ndjson"
  }]
}
```

Notes:

- The spec doesn't call out what status to return when the job is cancelled so we will use `200 OK` here.
- The spec doesn't seem to indicate that body should be returned with `202 Accepted`. However, we will return the body with list of files that are already completed so the client can start processing those files early.
- From the spec, it is unclear what status code to return if some resources could not be processed but overall operation succeeded. It mention that that `Response.error` array must be populated but it seems to indicate the status code should be `5XX`.
- I am not sure if we can check the permission to see if the token is required or not by looking at the `destinationConnectionSettings`.

#### Cancelling a bulk export job

1. User calls `DELETE [polling location]`.
2. Return `400 Not Found` if the job doesn't exist.
3. Return `202 Accepted` if the cancellation is successfully requested.
4. Return `409 Conflict` if the job is already completed or cancelled.
5. Return `500 Internal Server Error` if fails to get or update the job record.

## Test Strategy

We should integrate with the [Azure storage emulator](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-emulator) so the development can be done locally.

All components must have associated unit tests. We must have integration tests to verify the ability to resume export and other destructive tests. We will also need E2E tests.

## Security

Because we are storing connection strings, we need to make sure they are stored in secret store and appropriate permission is setup correctly. We also need to have a security review with the data flow.

## Other [DEAFT]

Describe any impact to localization, globalization, deployment, back-compat, SOPs, ISMS, etc.
