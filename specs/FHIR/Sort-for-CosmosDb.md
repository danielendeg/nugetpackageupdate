*Summary of the feature.*

[[_TOC_]]

# Business Justification

Customers want to be able to sort their search results using the `_sort` query parameter.

# Scenarios

1. Enable search requests using `_sort` for fhir-servers backed by CosmosDb datastore. Initial support will be for string type search values. But this solution is generic enough that we can expand it to other search value types also.

# Metrics

1. Accounts that have _sort enabled (by using the AccountTelemetry data).
2. Request metrics for requests that include the _sort parameter.

# Design

Due to limitations in CosmosDb for the ORDER BY clause and the way our search indices are structured, we cannot currently sort the search results for a given search in an efficient manner. There are two major challenges (which we will expand upon in the rest of the document):

1. We store all our search-related data in an array format (`searchIndices`) in the resource document. We usually use sub-queries in order to extract information from these values inside the array. Unfortunately, CosmosDb does not support using the `ORDER BY` clause in sub-queries.
2. Some search parameters can have multiple values (eg: `name`). This adds complexity since we will have to sort those multiple values (within the resource) before doing an overall sort across all relevant resources.

To overcome these challenges we will add more information to the resource document (during upsert) to support sorting.

## Extracting search values required to support sort

We will add new search index extraction logic (let's refer to this as `SortSearchIndexer` for now) that will extract values for parameters that need to be sorted and store them in a separate field in the resource docment in the below format.

```json
"sortedIndices" :
    {
        "<paramName>":
        {
            "ascValue" : "<paramValue>",
            "descValue" : "<paramValue>",
        }
    },
```

Sample of a partial Patient resource:
```json
{
    "id": "9d74d2ab-81f9-499b-987d-9dd6582303ed",
    "isSystem": false,
    "version": "1",
    "searchIndices": [
        {
            "p": "_id",
            "c": "9d74d2ab-81f9-499b-987d-9dd6582303ed"
        },
        {
            "p": "family",
            "s": "Huels583",
            "n_s": "HUELS583"
        },
        {
            "p": "given",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "name",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "name",
            "s": "Huels583",
            "n_s": "HUELS583"
        },
        {
            "p": "name",
            "s": "Mr.",
            "n_s": "MR."
        },
    ],
}
```

Sample of a partial Patient resource with the new data:
```json
{
    "id": "9d74d2ab-81f9-499b-987d-9dd6582303ed",
    "isSystem": false,
    "version": "1",
    "sortedIndices" :
    {
        "name":
        {
            "ascValue": "ANTONIA30",
            "descValue": "MR.",
        }
    },
    "searchIndices": [
        {
            "p": "_id",
            "c": "9d74d2ab-81f9-499b-987d-9dd6582303ed"
        },
        {
            "p": "family",
            "s": "Huels583",
            "n_s": "HUELS583"
        },
        {
            "p": "given",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "name",
            "s": "Antonia30",
            "n_s": "ANTONIA30"
        },
        {
            "p": "name",
            "s": "Huels583",
            "n_s": "HUELS583"
        },
        {
            "p": "name",
            "s": "Mr.",
            "n_s": "MR."
        },
    ],
}
```

Since storing these values takes up a non-trivial amount of space, we will only extract values for parameters for which sort is enabled. We will add a new section in the config file to allow users to enable sorting for parameters that are needed for their use case.

```json
"Sort": {
        "Parameters": [
            "param1_asc",
            "param2_asc",
            "param2_desc",
            "<paramName>_<sortingOrder>"
        ]
    }
```

We will extract the appropriate parameter values during resource create/update after reading the config in the `Sort:Parameters` section. The reason we need the `asc` or `desc` description is to support parameters that can have multiple values. The FHIR spec defines the following behaviour for such parameters:

```
A search parameter can refer to an element that repeats, and therefore there can be multiple values for a given search parameter for a single resource. In this case, the sort is based on the item in the set of multiple parameters that comes earliest in the specified sort order when ordering the returned resources
```

We will need to store both the "min" and "max" values for such multi-value parameters if we want to support ordering in both directions. If the user only needs support for ascending sort, then we only need to extract one value for the parameter. Hence, we provide the ability for the user to configure the sorting order for each parameter.

## Enabling search parameters for sort for existing resources

After updating the config, customers will have to send a POST request to the existing `$reindex` endpoint in the following format

```
POST <fhir-server>/$reindex?_sortParameterName=<paramName>
POST <fhir-server>/$reindex?_sortParameterName=<paramName1>,<paramName2>,<paramName3>
```

Current reindex mechanism looks for new SearchParameters that have been added and then reindexes the affected resources (to learn more about how reindex works you can refer to the [documentation here](./CustomSearch/Reindexing.md)). We will use similar logic here except that the parameter name is specified in the request. During the reindexing process we will make use of the `SortSearchIndexer` (which will take care of extracting the appropriate values based on the `Sort:Parameters` section of the config file) to update existing resource with the new data.

## Support for resources with missing values

Let's take an example search query
```
GET <fhir-server>/Patient?_sort=address
```

If there are `Patient` resources that do not have address data, CosmosDb will drop those resources from the final result. To prevent this scenario, we have to explicitly add an index to the CosmosDb container for every parameter for which sort is enabled. Resources with missing fields will be at the beginning of the result list for ascending sort and vice-versa for descending.

So once we complete reindexing the resources we will have to create a new index for the CosmosDb collection (index creation can happen in parallel as well). We can do this using the .NET SDK by getting the container, updating the index paths and the replacing the container ([Microsoft docs reference](https://docs.microsoft.com/en-us/azure/cosmos-db/how-to-manage-indexing-policy?tabs=dotnetv3%2Cpythonv3#dotnet-sdk)). We will also cross reference the value in the configuration file in order to determine whether we need to add `asc` or `desc` to the index path.

## Keeping track of supported search parameters for sort

The search logic needs a way to know which search parameters are supported for `_sort`. Once reindexing resources and creating new indices is complete, we will add a new document in the datastore to mark that the specified search parameter(s) is enabled for sort. This is building on top of the `SearchParameterRegistry` concept ([relevant documentation](./CustomSearch/SearchParameterRegistry.md)) and the `__searchparameterstatus__` documents we currently have. We'll build a similar registry for parameters for which sort is enabled and this will act as the source of truth (let's refer to this as `SortParameterRegistry` for now).

## Execution of search for _sort

When we get a search request with the `_sort` parameter, we will ping the `SortParameterRegistry` to confirm that the parameter has been enabled for `_sort`. If it is not enabled, we will remove the parameter from our search query (which is the existing behavior). If it is supported, we will generate an appropriate SQL query that will use an `ORDER BY <parameterName>` clause to get the desired result. Once a _sort search parameter has been found to be enabled, we can cache that information in-memory to prevent pinging the `SortParameterRegistry` every time a search involving that parameter is executed.

## Removal of search parameters that have been previously "enabled" for sort

There could be a scenario where a user removes a previously enabled search parameter from the `Sort:Parameters` config. The search index extractor will not be affected by this change since it depends on the config file to determine which indices to extract. Subsequent resource upserts will not contain this search parameter value (for sort). Similarly we could configure the search logic to validate with both the `SortParameterRegistry` as well as the config file to determine whether to include or exclude a search parameter for `_sort`. This would make sure that we don't return incorrect results for a request which includes `_sort=<deletedSearchParameter>`. 

But this also leaves the `SortParameterRegistry` in an "incorrect" state. To avoid this scenario, we can add logic to the server startup to look at all sort enabled parameters in the `SortParameterRegistry` and disable the ones that are not present in the `Sort:Parameters` config.

## Sorting by multiple parameters

```
GET <fhir-server>/Patient?_sort=family,address-city
```

To support sorting by multiple parameters in CosmosDb, we will need to explicitly add composite indices for each parameter combination that we would like to support. Adding a new index has a one-time RU cost as CosmosDb has to update its internal indices. Until CosmosDb completes the update process, searching using those parameters will provide incorrect results. Note that there is no reindexing of resources involved here since the expectation is that the individual parameters have already been enabled for sorting (which will be validated). The only work we need to do in this scenario is to add this new index to the CosmosDb container.

### Option 1 - Use the config file

```json
"Sort": {
        "Parameters": [
            "param1:asc",
            "param2:asc",
            "param2:desc",
            "<paramName>:<sortingOrder>"
        ],
        "CompositeParameters": [
            "param1_asc+param2_asc",
            "param1_asc+param2_asc+param3_desc",
            "param2_asc+param1_asc"
            "<paramName>_<sortingOrder>+<paramName>_<sortingOrder>+...+<paramName>_<sortingOrder>"
        ]
    }
```

On server startup we will read the `Sort:CompositeParameters` section of the config for sort and create the appropriate indices. This will be done by a background worker since we don't want to hold up initialization of the server. Once the indices have been created, we will create an entry in the `SortParameterRegistry` for the corresponding composite parameter. 

### Option 2 - Use the $reindex endpoint

The other option would be to use the same mechanism we used for enabling individual search parameters for sort. The user will send a POST request to the $reindex endpoint with the composite parameter they want to enable. We will validate the composite search parameter, create the new indices in CosmosDb and update the `SortParameterRegistry` once the index creation is complete.

```
POST <fhir-server>/$reindex?_sortParameterName=param1_asc+param2_asc
```

Option 2 is preferable since the mechanism is very similar to enabling support for sort for individual search parameters.

## Support on PaaS

Since this change involves allowing customers to add/update configuration of the fhir-server we will have to make changes on the PaaS product to support this.
1. Piping the new config values from ARM through our RP worker to the FHIR Application.
2. Portal changes to support the new config values.
3. Powershell/CLI changes to support the new config values.

These config values will be part of the fhir-server properties. So this will not require any ARM manifest change (except for a version upgrade).

# Test Strategy

1. Unit tests and integration tests.
2. E2E tests for validating that search using `_sort` works as expected.
3. E2E tests for validating that reindexing of existing resources for newly enabled parameter for _sort works as expected. 

# Security

N/A

# Other

### Summary of required changes
1. New configuration values for fhir-server.
2. New search value extraction logic on create/update resource operations.
3. Additional logic in ExpressionParser to parse search requests containing `_sort`.
4. Additional logic in QueryBuilder to generate appropriate SQL query for `_sort`.
5. New mechanism for triggering $reindex - using the `_sortParameterName` query parameter.
6. Additional logic to keep track of parameters for which `_sort` have been enabled.
7. New startup logic to create new composite indices for CosmosDb in order to support sorting by multiple parameters (might not be required).
8. E2E tests
9. PaaS changes.
