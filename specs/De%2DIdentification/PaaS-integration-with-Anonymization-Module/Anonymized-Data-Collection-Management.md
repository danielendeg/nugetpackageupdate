# Anonymized Data Collection Management
## Goals
Here we designed to integrate anonymized views to the persistence layer of FHIR Service. The design is based on Azure Cosmos DB that is currently supported in the PaaS server.
- Data owners are able to manage (provision, delete) anonymized data in Azure Cosmos DB.
- Customers are able to access anonymized data in specific collections with standard FHIR API supported by FHIR Server.

## Data Collections for Anonymized Views
For each anonymized view, we allocate a new data collection (cosmos container) to persist anonymized data.
1. Each anonymized collection are named with anonymized view name for simplicity, format: ```anonymized{viewName}{version}```, like "anonymizedexampleR4".
2. New anonymized collection creation will fail if the collection already exists in the database.

![container (1).jpg](/.attachments/container%20(1)-2e8890a3-82a0-4a65-aec5-73be97271b66.jpg)
## Creating anonymized data collection
Here we describe the process to create an anonymized data collection:
1. When the FHIR server application starts, a background service called anonymization worker will also be started. It polls anonymization jobs from FHIR Operation Store.
2. When the data owner submits a creation request, the FHIR server will persist an anonymization job record and return status code 202.
3. Anonymization worker schedules an anonymization task which reads the submitted job, creates a new data collection, performs a search to query all resources, do anonymization, import the anonymized data to the new data collection and update the job progress.
4. Updates the job record to ```Completed``` status if all data has been transformed or ```Failed``` status if encountered an error.

![worker.jpg](/.attachments/worker-7ed8a018-9c10-4e88-8078-005cc4ab3b68.jpg)

We designed several goals that anonymized worker needs to meet
- Worker must be able to anonymize all data to the destination data collection.
- Worker must be able to pick up new job periodically.
- Worker must be able to resume relatively inexpensively if terminated in the middle.
- Worker must be able to throttle so that it doesn't consume all available resources.

## Deleting anonymized data collection
When customers want to cancel an anonymized view provision request or delete an existing anonymized view, we need to implement a deleting logic to remove the corresponding data collection.

## Anonymized View with FHIR CRUD, Search and Export
With anonymized data collections in the same Cosmos DB, we can naturally have all standard FHIR APIs for anonymized view without add extra controllers/actions. \
A possible change would be that we need to inject anonymized collection Ids as a scoped service. Each FHIR request has a unique collection Id, and the Data Store will get the scoped collection Id for each read/write operation. \
For export scenario, we need to add collection id information to export job record and inject the scoped id when picking up a job record.

## Impact components
* PaaS Repo
1. New Anonymizer Worker Background Service, including JobWorker/JobRecord/JobConfiguration/JobTask/JobProgress.
2. Update the export module to support export jobs of anonymized views.

* OSS Repo
1. Update persistence module for multiple collection Id injection.

## Open Questions
### 1. How to manage throughput (RU/s) for anonymized containers? 
Currently there is only one container (data collection) in the PaaS server. When provisioning a PaaS server, users are allowed to set the throughput (RU/s) on the database level. \
However, we will have multiple containers with anonymized views and we should be aware that the anonymized view provisioning process will consume a lot of RU due to heavy data read/write operations. Since users cannot set the throughput (RU/s) at container level, there might be two strategies to manage throughput (RU/s) for anonymized containers:
1. Set dedicated throughput (like 400 RU/s) for each new container.
    - Pro: Simple to understand and implement.
    - Con: The initial throughput may not meet the customer's demand.
2. Let all containers share the database throughput.
    - Pro: The customer has some flexibility to set overall throughput for all containers.
    - Con: Customer can not get predictable performance on any specific container. 
    - Con: Customer can have a maximum of 25 containers in a shared throughput database. After the first 25 containers, you can add more containers to the database only if they are provisioned with dedicated throughput, which is separate from the shared throughput of the database.

### 2. Bulk Import support for migrating anonymized data to new collections 
We can only import resources to cosmos DB one by one for now. A bulk import function would be a big improvement for the anonymized view provisioning process. Should we implement a bulk import function by ourselves or leverage any possible work from FHIR  OSS?

## Performance
As anonymization view provision is a long-running job and more data collections have been introduced, we want to confirm the performance impact of anonymization integration and conduct some performance tests with an FHIR Server which stores 64,571 Synthea sample resources.

### The speed of bulk import
We run both one-by-one import and bulk import strategy against 528090 resources with 1000 RU/s. The one-by-one import with anonymize run hangs unexpectedly (probably due to exceeding RU limitation)

| Run   |      One-by-one import      |  bulk import (batch size: 100)
|----------|:-------------:|------:|------:|
| Migrate data only |    3h16min   |   1h56min |
| Extract search indices and migrate data | 5h37min |    2h51min |
| Anonymize, Extract search indices and migrate data |  - | 3h6min |

Since bulk import largely reduces provision time cost, our following experiments are all conducted with bulk import.

### The speed of provision
We run export and provision for 4 rounds.
The average time for exporting and provisioning 10,000 resources is 31 and 182 seconds, respectively.

Detailed results specified in seconds are as shown.

||Round1|Round2|Round3|Round4|Average|
|:-:|:-:|:-:|:-:|:-:|:-:|
|Export|191|208|204|205|202|
|Provision|1,198|1,171|1,169|1,170|1,177|

### The impact of the provision on other APIs
We run a stress test with 50 users sending search requests.
In every request, we randomly sample a resource type from 19 resource types that Synthea sample data cover and use it to search the database.
```
GET [base]\{resourceType}
```
- If there's no background task running, the QPS is around 11~15.
- If there's one export task running, the QPS is around 5~8.
- If there's one provision task running, the QPS is around 9~12.
- If there's one provision task and one export task running, the QPS is around 4~7.

## Work items and cost estimation
1. Implementation of Anonymizer Worker background service and testing. 2 weeks
2. Implementation of Anonymized Export and testing. 1 week
3. Implementation of Anonymized Search and testing. 1 week
4. Performance enhancement and stress testing. 2 weeks
5. (Optional) Bulk Import implementation and testing. 2 weeks
6. (Optional) Flexible RU management implementation and testing. 1 week
7. Design review and code review. 1 week
