While the FHIR standard specifies an initial set of search parameters, users often want to be able to search for resources using properties of a resource not yet defined as searchable.  As an example, customers using the US Core (http://hl7.org/fhir/us/core/) profiles may want to be able to search fields such as `race` or `ethnicity`. Another example would be our own internal need to add additional DICOM tags to the `ImagingStudy` resource and make them searchable.

The FHIR standard provides a framework for defining new search parameters and we should provide customers with a mechanism to add new search parameters.

[[_TOC_]]

# Adding a new `SearchParameter` in the FHIR server

To set the stage for the design, we will first have a look at what it takes to add a new `SearchParameter` to the existing FHIR server. Suppose we want to be able to search `race` on `Patient` resources that have the US core extension for race.

Our current behavior is to read the standard set of search parameters from FHIR spec upon server start.  We have those stored in `src\Microsoft.Health.Fhir.Core\Features\Definition\search-parameters.json`.  What is possible to do right now is to edit that file and restart the server.  Below is an example for the race search parameter, which could be added to the end of the search-parameters.json file.

```json
    {
      "fullUrl": "http://hl7.org/fhir/SearchParameter/Patient-race",
      "resource": {
        "resourceType": "SearchParameter",
        "id": "Patient-race",
        "url": "http://hl7.org/fhir/SearchParameter/Patient-race",
        "name": "race",
        "status": "draft",
        "experimental": false,
        "date": "2017-04-19T07:44:43+10:00",
        "code": "race",
        "base": [
          "Patient"
        ],
        "type": "token",
        "description": "Patient race",
        "expression": "Patient.extension.where(url = 'http://hl7.org/fhir/us/core/StructureDefinition/us-core-race').first().extension.where(url = 'ombCategory').value",
        "xpath": "f:Patient/f:extension[@url='http://hl7.org/fhir/us/core/StructureDefinition/us-core-race']/f:extension[@url = 'ombCategory']",
        "xpathUsage": "normal"
      }
```

After adding this search parameter and restarting the server, if we upload a Synthea patient (they have the US core race extension), we see something like this in the database:

```json
{
    "id": "a8a81f34-d235-42a6-9b95-88601ea60089",
    "isSystem": false,
    "version": "1",
    "searchIndices": [
        {
            "p": "_id",
            "c": "a8a81f34-d235-42a6-9b95-88601ea60089"
        },
        {
            "p": "_lastUpdated",
            "st": "2019-03-24T15:16:07.1136876+00:00",
            "et": "2019-03-24T15:16:07.1136876+00:00"
        },
        {
            "p": "active",
            "s": "http://hl7.org/fhir/special-values",
            "c": "false"
        },
        {
            "p": "race",
            "s": "urn:oid:2.16.840.1.113883.6.238",
            "c": "2106-3",
            "n_t": "BLACK"
        },
        {
            "p": "address",
            "s": "Worcester",
            "n_s": "WORCESTER"
        },
        {
            "p": "address",
            "s": "US",
            "n_s": "US"
        },

        // More indexes
    ],
    "partitionKey": "Patient_a8a81f34-d235-42a6-9b95-88601ea60089",
    "lastModified": "2019-03-24T15:16:07.1136876+00:00",
    "rawResource": {
        // The resource
    },
    "request": {
        "url": "https://localhost:44348/Patient",
        "method": "POST"
    },
    "isDeleted": false,
    "resourceId": "a8a81f34-d235-42a6-9b95-88601ea60089",
    "resourceTypeName": "Patient",
    "isHistory": false,
    "lastModifiedClaims": [],
    "compartmentIndices": {},
    "_rid": "SBY1AJIVDN1AAAAAAAAAAA==",
    "_self": "dbs/SBY1AA==/colls/SBY1AJIVDN0=/docs/SBY1AJIVDN1AAAAAAAAAAA==/",
    "_etag": "\"5601fc08-0000-0800-0000-5c979f380000\"",
    "_attachments": "attachments/",
    "_ts": 1553440568
}
```

Notice how `race` has been extracted as a search parameter.

## Uploading new `SearchParameter` definitions
However, requiring users to edit the search-parameters.json and then restart the server is not how we want to add or edit search parameters.  It is not a good experience, causes downtime and the search-parameters.json should only contain the parameters in the spec.

One relatively simple approach to allow users to define their own search parameters is directly through the FHIR API:

```
POST //fhirserver/SearchParameter
PUT/DELETE/GET //fhirserver/SearchParameter/id
```

with a `SearchParameter` payload as described above. It does create a bit of a chicken and egg situation since `SearchParameter` is a resource that is also searchable, but it may not be a big problem. We do need to treat create/update/delete on `SearchParameter` a bit differently from other resources since adding or updating search parameters means that we need to update the logic that extracts search parameters.

An additional problem is what to do if a `SearchParameter` is changed when a Reindex job is running.  As a reindex job runs it will invoke the same extraction logic which runs when a resource is posted to the server, and it uses the current set of search parameters provided by the Search Parameter Definition Manager.  If that is updated while the job is in on going some resources will have that new change applied during extraction and some not.  The most simple solution is reject any POST/PUT of a SearchParameter resource will a reindex job is running.  For the initial version of Custom Search, we will go forward with this simple method of rejecting any changes to Search Parameters while a reindex job is running.

A new filters will be added to the FhirController.  First an `OnActionExecutionAsync` filter will check for the resource type, and if type `SearchParameter` then will perform initial validation on whether or not allow the commit to proceed.  Including:
* Validation of the url to ensure it is unique
* Reject changes to search parameters defined in the FHIR spec, changes are only allowed to custom search parameters
* Reject changes to fully indexed and "live" custom search parameters
* Reject any changes while Reindex jobs are running

Once the resource of `SearchParameter` has been successfully committed to the datastore, then the filter code will resume.  It will add the search parameter information to the `SearchParameterDefinitionManager`, commit the search parameter status to the datastore, and notify the other instances of the update.

### Alternative approaches which would allow `SearchParameter` updates
Discussion during review brought up some options for allowing SearchParameters to be updated while a reindex was running. For the moment we are not planning on implementing these, but they are included for future reference:
* Use an additional state value in the SearchParameterDefinitionManager beyond "supported" and "enabled" which would track the state of a parameter that could become supported once a reindex job completed
* Use the history of the `SearchParameter` as a snapshot of the state of the search parameters when a reindex job was started

## Communicating Search Parameter changes to other instances
As Custom Search and Reindexing are interdependent, we will leverage the polling that the Reindex worker is doing to check for reindex jobs and also poll for changes to the Search Parameters at the same time.  In this manner, all instances of the service will get up to date fairly quickly when a change occurs.

### Alternate messaging worker
Instead of using the reindex worker, we may create a simple messaging worker that is capable enabling communication of the various instances via the data layer.

In addition to the periodic polling, anytime a request occurs which requires up to date search parameters such as:
* Reindexing job request
* Synchronous "test" reindex of a resource
* Special search which includes partially indexed parameters
  
will check the database for any search parameter changes before fulfilling the request.

## Loading new `SearchParameter` definitions
We need to make changes to the way the current list of SearchParameters is loaded. As mentioned currently a static file `search-paraemters.json` is loaded at the beginning.  That will continue to happen, while in addition, any `SearchParaemter`resources currently in the server will be read and added to the `SearchParameterDefinitionManager`.  A more detailed description of how that manager functions is here: [Search Parameter Registry](./SearchParameterRegistry.md).

## Validation for new Search Parameters
* Validation of the `Fhirpath`, not only that it is a valid `Fhirpath`, but that it does not include too much of the resource
* Validation of the data type to determine if we support it, and if not we will reject the SearchParameter
* Validation of the url to ensure it is unique
* Reject changes to search parameters defined in the FHIR spec, changes are only allowed to custom search parameters
* Reject changes to fully indexed and "live" custom search parameters

## Search Parameters defined in extensions
Search parameters will need to be created on properties that are defined in extensions.  We will need to be able to follow the `Fhirpath` that points to data in an extension.


## Re-indexing
Once the search parameter is created it will be reported as supported, but not searchable until the data has been reindexed with the new parameter.  The goal is to support a search with the new parameter as shown below:
```
GET https://fhirserver/Patient?race=2106-3
```
or
```
GET https://fhirserver/Patient?race:text=black
```
That becomes possible once the data is fully indexed.  For more details on how the reindexing works see: [Reindexing design](./Reindexing.md).

Once a parameter is fully reindexed, it should be reported in the capabilities statement.  In addition, once a search parameter is fully indexed and it is "live", 

# Test Strategy

E2E testing is needed where we first load some set of resources, add `SearchParameter`, call `$reindex`, and verify that search works.
Variations:
* Update an existing parameter, reindex and then search
* Remove a parameter, have it immediately unavailable for search and not in the capabilities statement

# Security

POST/PUT/DELETE `/SearchParameter` endpoint will be secured using a role called `searchAdmin` we will secure reindexing with the same role (essentially renaming the reindex role)