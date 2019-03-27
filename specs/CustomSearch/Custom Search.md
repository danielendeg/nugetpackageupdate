FHIR is very extensible and customers that make use of extensions in FHIR resources will want to make some of those extensions searchable. As an example, customers using the US Core (http://hl7.org/fhir/us/core/) profiles may want to be able to search fields such as `race` or `ethnicity`. Another example would be our own internal need to add additional DICOM tags to the `ImagingStudy` resource and make them searchable.

The FHIR standard provides a framework for defining new search parameters and we should provide customers with a mechanism add new search parameters. Note that adding a new search parameter is relatively trivial, but re-indexing will be needed and we need to provide customers with a way to start re-indexing and monitor if it is done.

[[_TOC_]]

# Adding a new `SearchParameter` in the FHIR server

To set the stage for the design, we will first have a look at what it takes to add a new `SearchParameter` to the existing FHIR server. Suppose we want to be able to search `race` on `Patient` resources that have the US core extension for race.

The search parameters are defined in `src\Microsoft.Health.Fhir.Core\Features\Definition\search-parameters.json`. We can add a section with something like:

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

Additionally, we need to add something like:

```json
    {
        "name": "race",
        "definition": "http://hl7.org/fhir/SearchParameter/Patient-race",
        "type": "token",
        "documentation": "Patient race"
    },

```

To both `src\Microsoft.Health.Fhir.Core\Features\Conformance\AllCapabilities.json` and `src\Microsoft.Health.Fhir.Core\Features\Conformance\DefaultCapabilities.json` to make the search parameter show up in the capability statement.

After adding this search parameter, if we upload a Synthea patients (they have the US core race extension), we see something like this in the database:

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

Notice how `race` has been extracted as a search parameter and we can now do searches like:

```
GET https://fhirserver/Patient?race=2106-3
```

or

```
GET https://fhirserver/Patient?race:text=black
```

So we have all the core capabilities for providing this for customers. The work is mostly related to making this operational.

# High-Level Design

To allow customers to define SearchParameters, we need:

## Uploading new `SearchParameter` definitions

One relatively simple approach would be directly through the FHIR API:

```
POST //fhirserver/SearchParameter
```

with a `SearchParameter` payload as described above. It does create a bit of a chicken and egg situation, since `SearchParameter` is a resource that is also searchable, but it may not be a big problem. We do need to treat create/update/delete on `SearchParameter` a bit differently from other resources since adding or updating search parameters means that we need to change update the logic that extracts search parameters.

We also need to make changes to the way the current list of SearchParameters is loaded. It is currently a static file added to the assembly, which means that and update requires a rebuild. This needs to be dynamic.

## Re-indexing

We actually have a general needs for a re-indexing mechanism, but with custom SearchParameters, we need to provide this capability to the customer as well. The most consistent way (following on the logic from the `$export` operation), would be to provide an `$reindex` operation:

```
POST //fhirserver/$reindex
```

With an optional payload:

```json
{
    "resourceTypes": [
        "Patient",
        "Observation"
    ],
    "maximumConcurrency": 1 //0 means unlimited, reindex at the expense of server performance
}
```

The `resourceTypes` parameter makes it possible to narrow the scope of the indexing since a given `SearchParameter` would only affect one resource. On success we will return `201 Created` with a response payload of:

```json
{
    "startTime": "TIME-STAMP",
    "progress": "0%"
}
```

Repeated calls (while indexing is ongoing) will return `200 OK` with the same payload (but updated progress). Unless some calling frequency threshold is exceeded, in which case we will return `429`.

An alternative would be the pattern used by `$export` with a `GET` with header `Prefer` set to `respond-async`.

The motivation for structuring the `$export` call like that seems a bit unclear and `$import` will likely have to use a `POST` and consequently, it would make sense to do that hear too.

## Resuming interrupted re-indexing

Re-indexing could be interrupted by upgrades, outages, etc. Consequently, the re-index job must be persisted so that it can be picked up again. Something like the logic around `$export` would make sense. 

# Test Strategy

E2E testing is needed where we first load some set of resources, add `SearchParameter`, call `$reindex`, and verify that search works.

# Security

The `/SearchParameter` endpoint may need special RBAC rules once we have RBAC built out. Similarly, `$reindex` should be a privileged operation because it could change the search behavior and it consumes resources that could change the performance of the service during re-indexing.

# Other

Re-indexing could be computationally expensive. From a cost perspective, it would make sense to give the customer the opportunity to scale up while re-indexing. We should consider if the frontend or the backend would need to scale more and provide guidance to the customer.