# Dicom Cast Retry Policy and Failure Resilience

## Problem
 Currently when processing change feed entries, certain errors cause the entire dicom-cast application to shut down and stop processing all change feed entries. The current logging of failures is also incomplete as it is difficult for the user to track down which specific dicom file caused the issue.

 ## Goals
 1. Comprehensive retry policy for errors that can be resolved without reuploading a dicom file
 1. Proper logging of failures to allow users to fix any errors on the Dicom or FHIR side
 1. Configurable levels of validation

 # Types of Errors
 
 ## Transient Errors

 These are errors that we expect to be resolved on their own with the passing of time. 
 
 Examples: Server Too Busy Exception from FHIR, Timeout waiting for response from FHIR

 **Retry Policy for Transient Errors**
 1. Retry the same entry with exponential back off
    1. Log each failure
 1. Do not process additional change feed entries until retry this one x amount of times
 1. After x times, mark it as un-processable, log an error and then move onto the next entry
	1. Essentially reclassifying it as an intransient error
    1. Place it into some storage where we will not retry but user could possibly access

![High-level Architecture](TransientErrorRetry.png)


Future work itmes: Allow user to access a log of failures



## Intransient Errors

These can be broken down further into errors that are caused by the FHIR server or errors that are due to the dicom file containing invalid data. For the FHIR errors we will implement a retry policy, whereas we will not implement a retry policy for errors that are due to invalid data from a dicom file.

### **Fhir side errors**

These errors are errors that we get from the FHIR server when we either try to retrieve an existing resource to modify it or when we make a transaction to update/create resources. Since they are errors that are on the FHIR side, most of them can be resolved by manually fixing something in the FHIR server. Since the dicom file does not need to be changed for this to be fixed, the changes would never get captured in the change log so unless the user deletes and reuploads the dicom file the change feed entry would not be processed again. We would like to create a retry policy for these cases.


Some examples of these errors and how they can be handled are following:

| Exception | Thrown | Reason/Fix | 
| :------- | :----- | :------- | 
| ResourceConflictException | A FHIR resource has been modified or created that we are also trying to change | Could possibly be resolved with automatic retry (currently we do this) |
| MultipleMatchingResourceException | When trying to retrieve an existing FHIR resources and there are multiple | One will most likely need to be manually deleted | 
| FhirResourceValidationException | When the FHIR resource retrieved is invalid | Some of the data in the FHIR resource is invaid, would need to be manually updted |
| TransactionFailedException | By FHIR server in response to a request |  | 
| InvalidFhirResponse | When get a response from FHIR after posting bundle when processing the response we find an error with it. | Most likely due to an error when processing the request in the FHIR server. Uncertain of fix. |


For a few of these errors, automatically retrying could possibly result in a success, where as for the others it would require a manual update on the FHIR side before the change feed entry could be processed successfully. 

**Retry Policy:**
1. Place any failed change feed entrys into the persistant storage for dicom-cast (currently blob storage)
    1. In addition to the change feed entry, we should store the number of times it has failed, time of most recent failure, and possibly the reason for the failure
1. After a certain amount of time has passed, retrieve items from the storage to be retried
1.  Make a request for the updated change feed entry from the dicom server
1.  If the change feed entry is "current" (not replaced or deleted) then add the entry to the current batch of changes being processed and process it
	1. If it is replaced or deleted then remove from the storage and continue processing other events
1. If get an error while processing again 
    1. If reached the limit of number of times to retry, log error, and stop retrying (remove entry from storage and possibly place into a persistant storage of failed to proccess items)
    1. If still below the threshhold, update thhe number of times retried in storage and retry again after time period

![High-level Architecture](FhirSideErrorRetry.png)

**Pros:**
* We have a retry policy for errors that could be resolved just by trying again such as ResourceConflictException.
* If someone is monitoring the logs, they could manually fix any errors that are on the FHIR side before the next retry

**Cons:**
* Ideally for retries that require an action before they can be retried we should not automatically retry
* Possible v2 -  allow for user to notify us via a storage service queue when a change feed entry is ready to be retried and then we process it


**Open Questions:**
1. What should the time period for retry be and how often should we retry?
    1. Possibly retry rapidly a few times and then start waiting longer periods of time
1. At what point do we give up on retrying? (after x amount of time or x amount of retries)
1. What is the form of storage that we should use?
    1. Storage Blob
        1. Currently we use this to store the sync state so the benefit is do not need a different storage locatoin
    1. Queue Stoarge
    1. Table Storage


### **DICOM Side Errors**
These errors  are the errors that are raised due to the change feed entry having a problem in it, particularly if the metadata is missing information or is an invalid format.
We will not have a retry policy for these because they require the dicom file to be reuploaded, but they may be stored in FHIR depending on level of validation configured in dicom-cast

**Validation levels**
1. Full Validation
    1. We will only do the transaction if all the meta data we get is valid
        1. In the case that there is invalid data we will not try to make a FHIR transaction, we will just log the error. The entry will not be stored anywhere because the solution is for the user to delete the instance and reupload which would be captured in the change feed.
1. Partial Validation (best effort store)
    1. As long as the required elements for the transaction (like InstanceID etc) are present we will complete the transaction.
        1. For any data that is invalid we will log a warning saying which field is invalid and that we did not store it into FHIR

The level of validation should be configurabble and the user can set it in the application settings.


## Logging Policy

**General**

Update logging to log more specific information. For change feed entry that fails will log specifics about the dicom instance such as the study, series, and instance ids so the user can fix the particular instance. 

For FHIR side errors add in possible solutions to the errors.

**Change Feed Notification Interface**

To allow oss users more flexibility on how the want to get errors, we will abstract the logging by one level by creating an interface. Our implementation of the interface will use the Logger to continue logging to Application Insights as it does currently. Users could possibly implement  their own version to get alerts and notifications as they want.

## Testing Strategy

Unit tests will be added for individual compnents to test properly storing and retrieving failed items, validating if we should retry or not based on if the change feed entry has been deleted or replaced, etc.

## Metrics
Some metrics we may consider tracking

1. Rate of failure on first attempt
1. Rate of success on 1st retry, 2nd retry etc.
1. Last time changfeed entry properly stored
1. How far behind we are in processing the changefeed
1. Number of items that need to be retried
1. Number of items that we gave up retrying on


## Alerts
Some possible alerts we may consider
1. If the error rate is too high or trending up
1. If we keep getting transient errors
1. Multiple change feed entries failing for same reason





