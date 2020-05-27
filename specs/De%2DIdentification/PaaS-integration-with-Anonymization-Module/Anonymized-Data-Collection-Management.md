# Anonymized Data Collection Management
## Goals
Here we designed to integrate anonymized views to persistence layer of FHIR Service. The design is based on Azure Cosmos DB that is currently supported in PaaS server.
- Data owners are able to manage (provision, delete) anonymized data in Azure Cosmos DB.
- Customers are able to access anonymized data in specific collection with standard FHIR API supported by FHIR Server.

## Data Collections for Anonymized Views
For each anonymized view, we allocate a new data collection (cosmos container) to persist anonymized data.
1. Each anonymized collection are named with anonymized view name for simlicity, format: ```anonymized{viewName}{version}```, like "anonymizedexampleR4".
2. New anonymized collection creation will fail if the collection already exists in database.
![container.jpg](/.attachments/container-b0692ead-196a-4d2f-91b5-e25e51356f3b.jpg)


## Anonymized data collection creation
Here we describe the process to create an anonymized data collection:
1. When FHIR server application starts, a background service called anonymization worker will also be started. It polls anonymization jobs from FHIR Operation Store.
2. When data owner submit a creation request, FHIR server will presist a anonymization job record and return status code 202.
3. Anonymization worker schedules an anonymization task which reads the submitted job, creates a new data collection, perform search to query all resources, do anonymization, import the anonymized data to the new data collection and update the job progress.
4. Anonymization worker updates the job record to <span style="color:green">Completed</span> status if all data has been transformed or ```Failed``` status if encountered an error.

![worker.jpg](/.attachments/worker-7ed8a018-9c10-4e88-8078-005cc4ab3b68.jpg)
We designed several goals that anonymized worker needs to meet
- Worker must be able to anonymize all data to the destination data collection.
- Worker must be able to pickup new job periodically.
- Worker must be able to resume relatively inexpensively if terminated in the middle.
- Worker must be able to throttle so that it doesn't consume all available resources.

## Anonymized View with FHIR CRUD, Search and Export
With anonymized data collections in the same cosmos db, we can naturally have all standard FHIR APIs for anonymized view without add extra controllers/actions. \
A possible change would be that we need to inject anonymized collectin Ids as a scoped service. Each FHIR request has a unique collection Id, and data store will get the scoped collection Id for each read/write operation. \
For export scenario, we need to add collection id information to export job record and inject the scoped id when picking up a job record.

## Impact components
* PaaS Repo
1. New Anonymizer Worker Background Service, including JobWorker/JobRecord/JobConfiguration/JobTask/JobProgress.
2. Update export module to support export job of anonymized views.

* OSS Repo
1. Update persistence module for multiple collection Id injection.

## Open Questions
### 1. How to manage throughput (RU/s) for anonymized containers? 
Currently there is only one container (data collection) in PaaS server. When provisioning a PaaS server, users are allowed to set the throughput (RU/s) on database level. \
However, we will have mulpitile containers with anonymized views and we should be aware that anonymized view provisioning process will consume a lot of RU due to heavy data read/write operations. Since users cannot set the throughput (RU/s) at container level, there might be two strategy to manage throughput (RU/s) for anonymized containers:
1. Set dedicated throughput (like 400 RU/s) for each new containers.
    - Pro: Simple to understand and implement.
    - Con: The initial throughput may not meet the cumstomer's demand.
2. Let all containers share the database throughput.
    - Pro: Customer has some flexibility to set overall throughput for all containers.
    - Con: Customer can not get predictable performance on any specific container. 
    - Con: Customer can have a maximum of 25 containers in a shared throughput database. After the first 25 containers, you can add more containers to the database only if they are provisioned with dedicated throughput, which is separate from the shared throughput of the database.

### 2. When do we validate the anonymized configuration?
When a provision request comes, we should validate the anonymization configuration before saving it into anonymized view store.
- If it's valid, `202 Accepted` should be returned.
- If it's invalid, `400 Bad Request` should be returned.

### 3. Bulk Import support for migrating anonymized data to new collections 
We can only import resource to cosmos db one by one for now. A bulk import function would be a big improvement for anonymized view provisioning process. Should we implement a bulk import function by ourselves or leverage any possible work from FHIR  OSS?

## Performance
As anonymization view provision is a long-running job and more data collections have been introduced, we want to confirm the performance impact of anonymization integration and conduct some performance tests with a FHIR Server which stores 64,571 Synthea sample resources.

### The speed of bulk import
We run both one-by-one import and bulk import strategy against 528090 resources with 1000 RU/s. The one-by-one import with anonymize run hangs unexpectedly (probably due to exceeding RU limitation)

| Run   |      One-by-one import      |  bulk import (batch size : 100)
|----------|:-------------:|------:|------:|
| Migrate data only |    3h16min   |   1h56min |
| Extract search indices and migrate data | 5h37min |    2h51min |
| Anonymize, Extract search indices and migrate data |  - | 3h6min |

Since bulk import largely reduces provision time cost, our following experiemnts are all conducted with bulk import.

### The speed of provision
We run export and provision for 4 rounds.
The average time for exporting and provisioning 10,000 resources are 31 and 182 seconds, respectively.

Detailed results specified in seconds are as shown.

||Round1|Round2|Round3|Round4|Average|
|:-:|:-:|:-:|:-:|:-:|:-:|
|Export|191|208|204|205|202|
|Provision|1,198|1,171|1,169|1,170|1,177|

### The impact of provision on other APIs
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
